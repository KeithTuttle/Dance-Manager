using System.Text.Json;
using DanceManager.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DanceManager.Api.Services;

/// <summary>
/// Bundle of data used to render a substitute-teacher handoff sheet.
/// </summary>
public class SubHandoffData
{
    public string StudioName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public List<Student> Roster { get; set; } = new();
    public ClassSession? LatestSession { get; set; }
    public List<Routine> Routines { get; set; } = new();
    /// <summary>Formations grouped per routine id, ordered by OrderIndex.</summary>
    public Dictionary<int, List<Formation>> FormationsByRoutine { get; set; } = new();
}

/// <summary>
/// Builds a clean, printable Sub Handoff PDF using QuestPDF: class roster,
/// most-recent class-session notes, routine music links, and a snapshot of each
/// current formation (rendered as a labeled coordinate diagram).
/// </summary>
public class SubHandoffPdfService
{
    public byte[] Build(SubHandoffData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().PaddingVertical(12).Element(c => ComposeContent(c, data));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Sub Handoff — generated ");
                    t.Span(DateTime.Now.ToString("MMM d, yyyy h:mm tt"));
                    t.Span("   •   Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, SubHandoffData data)
    {
        container.Column(col =>
        {
            col.Item().Text("Substitute Teacher Handoff")
                .FontSize(20).SemiBold().FontColor(Colors.Black);
            col.Item().PaddingTop(2).Text(text =>
            {
                text.Span("Class: ").SemiBold();
                text.Span(string.IsNullOrWhiteSpace(data.ClassName) ? "—" : data.ClassName);
                if (!string.IsNullOrWhiteSpace(data.StudioName))
                {
                    text.Span("     Studio: ").SemiBold();
                    text.Span(data.StudioName);
                }
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, SubHandoffData data)
    {
        container.Column(col =>
        {
            col.Spacing(16);

            // Class notes section
            col.Item().Element(c => Section(c, "Latest Class Notes", inner =>
            {
                if (data.LatestSession is null)
                {
                    inner.Text("No class-session notes available.").Italic().FontColor(Colors.Grey.Medium);
                    return;
                }

                inner.Column(notesCol =>
                {
                    notesCol.Item().Text(t =>
                    {
                        t.Span("Session date: ").SemiBold();
                        t.Span(data.LatestSession.Date.ToString("MMM d, yyyy"));
                    });
                    notesCol.Item().PaddingTop(4).Text(
                        string.IsNullOrWhiteSpace(data.LatestSession.Notes)
                            ? "(no notes recorded)"
                            : data.LatestSession.Notes);
                });
            }));

            // Roster section
            col.Item().Element(c => Section(c, $"Class Roster ({data.Roster.Count})", inner =>
            {
                if (data.Roster.Count == 0)
                {
                    inner.Text("No students on the roster.").Italic().FontColor(Colors.Grey.Medium);
                    return;
                }

                inner.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Student");
                        HeaderCell(header, "Alerts");
                        HeaderCell(header, "Movement Modifications");
                    });

                    foreach (var s in data.Roster)
                    {
                        BodyCell(table, $"{s.FirstName} {s.LastName}".Trim());
                        BodyCell(table, s.InjuryAlert ? "Injury alert" : "—");
                        BodyCell(table, string.IsNullOrWhiteSpace(s.MovementModifications) ? "—" : s.MovementModifications!);
                    }
                });
            }));

            // Music / routines section
            col.Item().Element(c => Section(c, "Routine Music", inner =>
            {
                if (data.Routines.Count == 0)
                {
                    inner.Text("No routines for this class.").Italic().FontColor(Colors.Grey.Medium);
                    return;
                }

                inner.Column(musicCol =>
                {
                    musicCol.Spacing(6);
                    foreach (var r in data.Routines)
                    {
                        musicCol.Item().Text(t =>
                        {
                            t.Span(string.IsNullOrWhiteSpace(r.SongTitle) ? "(untitled)" : r.SongTitle).SemiBold();
                            if (!string.IsNullOrWhiteSpace(r.Artist))
                                t.Span($" — {r.Artist}");
                        });
                        if (!string.IsNullOrWhiteSpace(r.VideoUrl))
                            musicCol.Item().Text(r.VideoUrl).FontColor(Colors.Blue.Medium).FontSize(9);
                    }
                });
            }));

            // Formations section
            col.Item().Element(c => Section(c, "Stage Formations", inner =>
            {
                var hasAny = data.Routines.Any(r =>
                    data.FormationsByRoutine.TryGetValue(r.Id, out var fs) && fs.Count > 0);

                if (!hasAny)
                {
                    inner.Text("No formations mapped yet.").Italic().FontColor(Colors.Grey.Medium);
                    return;
                }

                inner.Column(formCol =>
                {
                    formCol.Spacing(12);
                    foreach (var r in data.Routines)
                    {
                        if (!data.FormationsByRoutine.TryGetValue(r.Id, out var formations) || formations.Count == 0)
                            continue;

                        formCol.Item().Text(string.IsNullOrWhiteSpace(r.SongTitle) ? "(untitled routine)" : r.SongTitle)
                            .SemiBold().FontSize(11);

                        foreach (var f in formations)
                            formCol.Item().Element(c => ComposeFormation(c, f, data.Roster));
                    }
                });
            }));
        });
    }

    private static void ComposeFormation(IContainer container, Formation formation, List<Student> roster)
    {
        var coords = ParseCoordinates(formation.StudentCoordinates);

        container.Column(col =>
        {
            col.Item().PaddingTop(4).Text(t =>
            {
                t.Span($"#{formation.OrderIndex + 1}  ").FontColor(Colors.Grey.Medium);
                t.Span(string.IsNullOrWhiteSpace(formation.FormationName) ? "Formation" : formation.FormationName).SemiBold();
            });

            if (coords.Count == 0)
            {
                col.Item().Text("No positions set.").Italic().FontSize(9).FontColor(Colors.Grey.Medium);
                return;
            }

            // Stage diagram: fixed-size canvas with initials placed by percentage.
            const float canvasWidth = 460f;
            const float canvasHeight = 150f;
            col.Item().PaddingTop(4).Width(canvasWidth).Height(canvasHeight)
                .Border(1).BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.Grey.Lighten4)
                .Layers(layers =>
                {
                    layers.PrimaryLayer(); // background
                    foreach (var (studentId, pos) in coords)
                    {
                        var initials = Initials(studentId, roster);
                        var x = Math.Clamp(pos.X, 0, 100) / 100.0f;
                        var y = Math.Clamp(pos.Y, 0, 100) / 100.0f;
                        layers.Layer().AlignLeft().AlignTop()
                            .TranslateX(x * canvasWidth).TranslateY(y * canvasHeight)
                            .Text(initials).FontSize(8).SemiBold().FontColor(Colors.Blue.Darken2);
                    }
                });

            // Coordinate list fallback / detail.
            col.Item().PaddingTop(4).Text(t =>
            {
                t.Span("Positions: ").SemiBold().FontSize(9);
                var parts = coords.Select(kv =>
                    $"{Initials(kv.Key, roster)} ({kv.Value.X:0}%, {kv.Value.Y:0}%)");
                t.Span(string.Join("   ", parts)).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static Dictionary<int, Point> ParseCoordinates(string json)
    {
        var result = new Dictionary<int, Point>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var studentId)) continue;
                float x = 0, y = 0;
                if (prop.Value.TryGetProperty("x", out var xe)) x = (float)xe.GetDouble();
                if (prop.Value.TryGetProperty("y", out var ye)) y = (float)ye.GetDouble();
                result[studentId] = new Point(x, y);
            }
        }
        catch (JsonException)
        {
            // Malformed JSON -> empty positions rather than crashing the PDF.
        }
        return result;
    }

    private static string Initials(int studentId, List<Student> roster)
    {
        var s = roster.FirstOrDefault(r => r.Id == studentId);
        if (s is null) return $"#{studentId}";
        var f = string.IsNullOrEmpty(s.FirstName) ? "" : s.FirstName[..1];
        var l = string.IsNullOrEmpty(s.LastName) ? "" : s.LastName[..1];
        var initials = (f + l).ToUpperInvariant();
        return string.IsNullOrEmpty(initials) ? $"#{studentId}" : initials;
    }

    private static void Section(IContainer container, string title, Action<IContainer> content)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(13).SemiBold().FontColor(Colors.Black);
            col.Item().PaddingTop(4).PaddingBottom(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().Element(content);
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).SemiBold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(9);

    private readonly record struct Point(float X, float Y);
}
