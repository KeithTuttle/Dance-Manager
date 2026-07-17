using System.Text.Json;
using DanceManager.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DanceManager.Api.Services;

/// <summary>Data used to render the recital show-order PDF.</summary>
public class ShowOrderData
{
    public string StudioName { get; set; } = string.Empty;
    public List<ShowSection> Sections { get; set; } = new();
    /// <summary>Show numbers, each with Routine.Class included where routine-linked.</summary>
    public List<ShowProgram> Program { get; set; } = new();
    /// <summary>Participating (IsParticipating) recital participation rows.</summary>
    public List<RecitalParticipation> Participations { get; set; } = new();
    /// <summary>Explicit per-number casts (override participation for a routine when present).</summary>
    public List<RoutineCast> RoutineCasts { get; set; } = new();
    public List<Student> Students { get; set; } = new();
}

/// <summary>
/// Builds a printable recital running-order PDF: numbers grouped by section, each
/// with its running number, title, class name, and (for the teacher's own numbers)
/// the participating dancers. Consecutive numbers within a section that share a
/// dancer get a "Quick change" flag.
/// </summary>
public class ShowOrderPdfService
{
    public byte[] Build(ShowOrderData data)
    {
        // classId -> set of participating studentIds
        var participatingByClass = new Dictionary<int, HashSet<int>>();
        foreach (var p in data.Participations)
        {
            if (!participatingByClass.TryGetValue(p.ClassId, out var set))
                participatingByClass[p.ClassId] = set = new HashSet<int>();
            set.Add(p.StudentId);
        }
        // routineId -> explicit cast studentIds (overrides class participation when present).
        var castByRoutine = new Dictionary<int, HashSet<int>>();
        foreach (var c in data.RoutineCasts)
        {
            if (!castByRoutine.TryGetValue(c.RoutineId, out var set))
                castByRoutine[c.RoutineId] = set = new HashSet<int>();
            set.Add(c.StudentId);
        }

        var studentName = data.Students.ToDictionary(
            s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim());

        // Display groups: named sections in order, then an "Unassigned" bucket if
        // used. Running numbers are precomputed per group (StartNumber) so the
        // count isn't threaded through QuestPDF's deferred composition lambdas.
        var groups = new List<(string? Header, List<ShowProgram> Entries, int StartNumber)>();
        var running = 0;
        foreach (var section in data.Sections.OrderBy(s => s.OrderIndex).ThenBy(s => s.Id))
        {
            var entries = data.Program.Where(p => p.SectionId == section.Id)
                .OrderBy(p => p.OrderPosition).ToList();
            groups.Add((section.Name, entries, running));
            running += entries.Count;
        }
        var unassigned = data.Program.Where(p => p.SectionId == null)
            .OrderBy(p => p.OrderPosition).ToList();
        if (unassigned.Count > 0)
            groups.Add((data.Sections.Count > 0 ? "Unassigned" : null, unassigned, running));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("Recital Show Order").FontSize(20).SemiBold().FontColor(Colors.Black);
                    if (!string.IsNullOrWhiteSpace(data.StudioName))
                        col.Item().Text(data.StudioName).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    if (data.Program.Count == 0)
                    {
                        col.Item().Text("No numbers in the show order yet.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    foreach (var (header, entries, startNumber) in groups)
                    {
                        if (entries.Count == 0) continue;
                        col.Item().Element(c => ComposeGroup(
                            c, header, entries, startNumber, participatingByClass, castByRoutine, studentName));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Show order — generated ");
                    t.Span(DateTime.Now.ToString("MMM d, yyyy"));
                    t.Span("   •   Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeGroup(
        IContainer container,
        string? header,
        List<ShowProgram> entries,
        int startNumber,
        Dictionary<int, HashSet<int>> participatingByClass,
        Dictionary<int, HashSet<int>> castByRoutine,
        Dictionary<int, string> studentName)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(header))
                col.Item().PaddingBottom(2).Text(header!).FontSize(13).SemiBold().FontColor(Colors.Black);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var number = startNumber + i + 1;
                var routine = entry.Routine;
                var title = routine is not null
                    ? (string.IsNullOrWhiteSpace(routine.SongTitle) ? "Untitled routine" : routine.SongTitle)
                    : (string.IsNullOrWhiteSpace(entry.Title) ? "Untitled number" : entry.Title!);
                var className = routine?.Class?.Name;
                var dancers = StudentSet(entry, participatingByClass, castByRoutine);

                col.Item().PaddingTop(i == 0 ? 0 : 4).Row(row =>
                {
                    row.ConstantItem(28).Text($"{number}.").SemiBold().FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().Column(info =>
                    {
                        info.Item().Text(title).SemiBold().FontColor(Colors.Black);
                        if (!string.IsNullOrWhiteSpace(className))
                            info.Item().Text(className!).FontSize(9).FontColor(Colors.Grey.Medium);

                        // Dancers in this number: class participation (routine-linked)
                        // or the students attached to a standalone number.
                        if (dancers.Count > 0)
                        {
                            var names = dancers.Select(id => studentName.TryGetValue(id, out var n) ? n : $"#{id}")
                                .OrderBy(n => n);
                            info.Item().PaddingTop(1).Text(string.Join(", ", names))
                                .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                        }
                    });
                });

                // Quick-change flag: consecutive numbers in THIS section sharing a dancer.
                if (i < entries.Count - 1)
                {
                    var shared = SharedDancers(entry, entries[i + 1], participatingByClass, castByRoutine, studentName);
                    if (shared.Count > 0)
                    {
                        col.Item().PaddingTop(2).PaddingLeft(28).Text(t =>
                        {
                            t.Span("⚡ Quick change: ").SemiBold().FontColor(Colors.Orange.Darken2);
                            t.Span(string.Join(", ", shared)).FontColor(Colors.Orange.Darken2);
                        });
                    }
                }
            }
        });
    }

    private static List<string> SharedDancers(
        ShowProgram a, ShowProgram b,
        Dictionary<int, HashSet<int>> participatingByClass,
        Dictionary<int, HashSet<int>> castByRoutine,
        Dictionary<int, string> studentName)
    {
        var setA = StudentSet(a, participatingByClass, castByRoutine);
        var setB = StudentSet(b, participatingByClass, castByRoutine);
        if (setA.Count == 0 || setB.Count == 0) return new();
        return setA.Where(setB.Contains)
            .Select(id => studentName.TryGetValue(id, out var n) ? n : $"#{id}")
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>Dancers in a number: the routine's explicit cast if any, else the class's
    /// recital participation (routine-linked), or the students attached to a standalone number.</summary>
    private static HashSet<int> StudentSet(
        ShowProgram entry,
        Dictionary<int, HashSet<int>> participatingByClass,
        Dictionary<int, HashSet<int>> castByRoutine)
    {
        if (entry.RoutineId is not null)
        {
            // Explicit per-number cast wins over class participation.
            if (castByRoutine.TryGetValue(entry.RoutineId.Value, out var cast) && cast.Count > 0)
                return cast;
            var classId = entry.Routine?.Class?.Id;
            if (classId is not null && participatingByClass.TryGetValue(classId.Value, out var set))
                return set;
            return new HashSet<int>();
        }
        if (string.IsNullOrWhiteSpace(entry.StudentIds)) return new HashSet<int>();
        try { return JsonSerializer.Deserialize<List<int>>(entry.StudentIds)?.ToHashSet() ?? new(); }
        catch { return new HashSet<int>(); }
    }
}
