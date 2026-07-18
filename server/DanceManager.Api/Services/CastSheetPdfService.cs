using DanceManager.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DanceManager.Api.Services;

/// <summary>Data used to render the musical cast-sheet PDFs.</summary>
public class CastSheetData
{
    public string StudioName { get; set; } = string.Empty;
    public List<ShowSection> Sections { get; set; } = new();
    public List<ShowProgram> Program { get; set; } = new();
    public List<Routine> Routines { get; set; } = new();
    public List<DanceClass> Classes { get; set; } = new();
    /// <summary>Explicit per-number casts (override class participation when present).</summary>
    public List<RoutineCast> RoutineCasts { get; set; } = new();
    public List<RecitalParticipation> Participations { get; set; } = new();
    /// <summary>Class rosters — used to group dancers on the per-dancer sheet.</summary>
    public List<Enrollment> Enrollments { get; set; } = new();
    public List<Student> Students { get; set; } = new();
}

/// <summary>
/// Builds two printable cast sheets for a musical production:
/// a per-number cast list (numbers in running order, each with its dancers) and
/// a per-dancer schedule (each dancer grouped by cast group, listing the numbers
/// they're in). A number's dancers are its explicit <see cref="RoutineCast"/> when
/// present, otherwise its class's recital participation.
/// </summary>
public class CastSheetPdfService
{
    private record NumberRow(Routine Routine, int? Position, string GroupName, List<int> DancerIds);

    private sealed class Ctx
    {
        public Dictionary<int, string> StudentName = new();
        public List<NumberRow> Numbers = new();
    }

    public byte[] BuildByNumber(CastSheetData d)
    {
        var ctx = Prepare(d);
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                BasePage(page);
                HeaderBlock(page, "Cast List", d.StudioName);
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(9);
                    if (ctx.Numbers.Count == 0)
                    {
                        col.Item().Text("No numbers yet.").Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }
                    foreach (var n in ctx.Numbers)
                        col.Item().Element(c => ComposeNumber(c, n, ctx));
                });
                FooterBlock(page);
            });
        });
        return doc.GeneratePdf();
    }

    public byte[] BuildByDancer(CastSheetData d)
    {
        var ctx = Prepare(d);

        // Group dancers by their (first) enrolled class, then an Ungrouped bucket.
        var enrollGroup = new Dictionary<int, int>();
        foreach (var e in d.Enrollments)
            enrollGroup.TryAdd(e.StudentId, e.ClassId);
        var groupName = d.Classes.ToDictionary(c => c.Id, c => c.Name);

        // studentId -> the numbers they're in, preserving running order.
        var byDancer = new Dictionary<int, List<NumberRow>>();
        foreach (var n in ctx.Numbers)
            foreach (var sid in n.DancerIds)
            {
                if (!byDancer.TryGetValue(sid, out var list)) byDancer[sid] = list = new();
                list.Add(n);
            }

        var sections = new List<(string Header, List<Student> Dancers)>();
        foreach (var cls in d.Classes.OrderBy(c => c.Name))
        {
            var dancers = d.Students
                .Where(s => enrollGroup.TryGetValue(s.Id, out var cid) && cid == cls.Id)
                .OrderBy(s => ctx.StudentName.GetValueOrDefault(s.Id, string.Empty))
                .ToList();
            if (dancers.Count > 0) sections.Add((cls.Name, dancers));
        }
        var ungrouped = d.Students
            .Where(s => !enrollGroup.ContainsKey(s.Id))
            .OrderBy(s => ctx.StudentName.GetValueOrDefault(s.Id, string.Empty))
            .ToList();
        if (ungrouped.Count > 0) sections.Add(("Ungrouped", ungrouped));

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                BasePage(page);
                HeaderBlock(page, "Dancer Schedules", d.StudioName);
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);
                    if (sections.Count == 0)
                    {
                        col.Item().Text("No dancers yet.").Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }
                    foreach (var (header, dancers) in sections)
                        col.Item().Element(c => ComposeDancerGroup(c, header, dancers, byDancer, ctx));
                });
                FooterBlock(page);
            });
        });
        return doc.GeneratePdf();
    }

    // --- Shared preparation ---------------------------------------------------
    private Ctx Prepare(CastSheetData d)
    {
        var studentName = d.Students.ToDictionary(
            s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim());
        var groupName = d.Classes.ToDictionary(c => c.Id, c => c.Name);

        var castByRoutine = new Dictionary<int, HashSet<int>>();
        foreach (var c in d.RoutineCasts)
        {
            if (!castByRoutine.TryGetValue(c.RoutineId, out var set))
                castByRoutine[c.RoutineId] = set = new();
            set.Add(c.StudentId);
        }
        var participatingByClass = new Dictionary<int, HashSet<int>>();
        foreach (var p in d.Participations)
        {
            if (!p.IsParticipating) continue;
            if (!participatingByClass.TryGetValue(p.ClassId, out var set))
                participatingByClass[p.ClassId] = set = new();
            set.Add(p.StudentId);
        }

        List<int> Dancers(Routine r)
        {
            if (castByRoutine.TryGetValue(r.Id, out var cast) && cast.Count > 0) return cast.ToList();
            if (participatingByClass.TryGetValue(r.ClassId, out var part)) return part.ToList();
            return new List<int>();
        }
        string GroupOf(int classId) => groupName.GetValueOrDefault(classId, "Unknown group");

        // Numbers in running order (sections by index, entries by position), then
        // any numbers not yet placed in the show order, grouped/sorted for stability.
        var routineById = d.Routines.ToDictionary(r => r.Id);
        var used = new HashSet<int>();
        var numbers = new List<NumberRow>();
        var pos = 0;
        void AddEntry(ShowProgram e)
        {
            if (e.RoutineId is null || !routineById.TryGetValue(e.RoutineId.Value, out var r)) return;
            if (!used.Add(r.Id)) return;
            pos++;
            numbers.Add(new NumberRow(r, pos, GroupOf(r.ClassId), Dancers(r)));
        }
        foreach (var sec in d.Sections.OrderBy(s => s.OrderIndex).ThenBy(s => s.Id))
            foreach (var e in d.Program.Where(p => p.SectionId == sec.Id).OrderBy(p => p.OrderPosition))
                AddEntry(e);
        foreach (var e in d.Program.Where(p => p.SectionId == null).OrderBy(p => p.OrderPosition))
            AddEntry(e);
        foreach (var r in d.Routines
                     .Where(r => !used.Contains(r.Id))
                     .OrderBy(r => GroupOf(r.ClassId)).ThenBy(r => r.SongTitle))
            numbers.Add(new NumberRow(r, null, GroupOf(r.ClassId), Dancers(r)));

        return new Ctx { StudentName = studentName, Numbers = numbers };
    }

    private static string TitleOf(Routine r) =>
        string.IsNullOrWhiteSpace(r.SongTitle) ? "Untitled number" : r.SongTitle;

    // --- Composition ----------------------------------------------------------
    private void ComposeNumber(IContainer container, NumberRow n, Ctx ctx)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(34).Text(n.Position is null ? "—" : $"{n.Position}.")
                    .SemiBold().FontColor(Colors.Grey.Darken1);
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text(TitleOf(n.Routine)).SemiBold().FontColor(Colors.Black);
                    var meta = n.Position is null ? $"{n.GroupName} · not in show order" : n.GroupName;
                    meta += $"  ·  {n.DancerIds.Count} dancer{(n.DancerIds.Count == 1 ? "" : "s")}";
                    var costume = n.Routine.CostumeLabel?.Trim();
                    if (!string.IsNullOrEmpty(costume)) meta += $"  ·  Costume: {costume}";
                    info.Item().Text(meta).FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
            var names = n.DancerIds
                .Select(id => ctx.StudentName.GetValueOrDefault(id, $"#{id}"))
                .OrderBy(x => x)
                .ToList();
            col.Item().PaddingLeft(34).PaddingTop(1).Text(names.Count == 0 ? "— no cast —" : string.Join(", ", names))
                .FontSize(9.5f).FontColor(names.Count == 0 ? Colors.Grey.Medium : Colors.Grey.Darken2);
        });
    }

    private void ComposeDancerGroup(
        IContainer container, string header, List<Student> dancers,
        Dictionary<int, List<NumberRow>> byDancer, Ctx ctx)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(3).Text(header).FontSize(13).SemiBold().FontColor(Colors.Black);
            foreach (var s in dancers)
            {
                var nums = byDancer.GetValueOrDefault(s.Id) ?? new List<NumberRow>();
                col.Item().PaddingBottom(3).Row(row =>
                {
                    row.ConstantItem(150).Text(ctx.StudentName.GetValueOrDefault(s.Id, $"#{s.Id}"))
                        .SemiBold().FontColor(Colors.Grey.Darken3);
                    if (nums.Count == 0)
                    {
                        row.RelativeItem().Text("— not cast —").FontSize(9.5f).Italic().FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        // Include the costume per number so the sheet doubles as the
                        // dancer's changing plan for the night.
                        var parts = nums.Select(n =>
                        {
                            var t = n.Position is null
                                ? $"{TitleOf(n.Routine)} (unscheduled)"
                                : $"#{n.Position} {TitleOf(n.Routine)}";
                            var costume = n.Routine.CostumeLabel?.Trim();
                            return string.IsNullOrEmpty(costume) ? t : $"{t} — {costume}";
                        });
                        row.RelativeItem().Text($"{nums.Count}:  " + string.Join("  ·  ", parts))
                            .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                    }
                });
            }
        });
    }

    // --- Page chrome ----------------------------------------------------------
    private static void BasePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));
    }

    private static void HeaderBlock(PageDescriptor page, string title, string studioName)
    {
        page.Header().Column(col =>
        {
            col.Item().Text(title).FontSize(20).SemiBold().FontColor(Colors.Black);
            if (!string.IsNullOrWhiteSpace(studioName))
                col.Item().Text(studioName).FontColor(Colors.Grey.Medium);
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void FooterBlock(PageDescriptor page)
    {
        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Generated ");
            t.Span(DateTime.Now.ToString("MMM d, yyyy"));
            t.Span("   •   Page ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }
}
