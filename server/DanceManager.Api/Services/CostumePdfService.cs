using DanceManager.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DanceManager.Api.Services;

/// <summary>Data used to render a printable costume sheet for one routine.</summary>
public class CostumeSheetData
{
    public string StudioName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string RoutineTitle { get; set; } = string.Empty;
    public List<CostumeOption> BoysOptions { get; set; } = new();
    public List<CostumeOption> GirlsOptions { get; set; } = new();
    /// <summary>optionId → fetched, validated image bytes (embedded when present).</summary>
    public Dictionary<int, byte[]> PhotoBytesByOptionId { get; set; } = new();
}

/// <summary>
/// Builds a clean, printable costume sheet PDF (QuestPDF): Boys and Girls
/// sections, each listing the costume description, accessories, shoes, the
/// embedded photo (when the photo link resolved to a real image), and any
/// vendor link for a routine.
/// </summary>
public class CostumePdfService
{
    public byte[] Build(CostumeSheetData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("Costume Sheet").FontSize(18).SemiBold().FontColor(Colors.Black);
                    col.Item().Text(data.RoutineTitle).FontSize(12).SemiBold();
                    var subtitle = string.Join("  ·  ",
                        new[] { data.StudioName, data.ClassName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(subtitle))
                        col.Item().Text(subtitle).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Element(c => ComposeGroup(c, "Boys", data.BoysOptions, data.PhotoBytesByOptionId));
                    col.Item().Element(c => ComposeGroup(c, "Girls", data.GirlsOptions, data.PhotoBytesByOptionId));
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Generated ").FontColor(Colors.Grey.Medium);
                    x.Span(DateTime.Now.ToString("MMM d, yyyy")).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeGroup(
        IContainer container, string title, List<CostumeOption> options,
        Dictionary<int, byte[]> photos)
    {
        container.Column(col =>
        {
            col.Item().Text($"{title} Costumes").FontSize(13).SemiBold().FontColor(Colors.Black);

            if (options.Count == 0)
            {
                col.Item().PaddingTop(4).Text("No options recorded.").Italic().FontColor(Colors.Grey.Medium);
                return;
            }

            foreach (var opt in options)
            {
                var hasPhoto = photos.TryGetValue(opt.Id, out var photoBytes);
                col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
                {
                    if (hasPhoto)
                    {
                        row.ConstantItem(90).AlignTop().Image(photoBytes!).FitWidth();
                        row.ConstantItem(10);
                    }
                    row.RelativeItem().Column(card =>
                    {
                        card.Spacing(3);
                        if (!string.IsNullOrWhiteSpace(opt.Description))
                            card.Item().Text(opt.Description).SemiBold();
                        Field(card, "Accessories", opt.Accessories);
                        Field(card, "Shoes", opt.Shoes);
                        // Only print the photo URL as text when we couldn't embed it.
                        if (!hasPhoto) Field(card, "Photo", opt.PhotoLink);
                        Field(card, "Vendor", opt.OptionLink);
                    });
                });
            }
        });
    }

    private static void Field(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Text(x =>
        {
            x.Span($"{label}: ").SemiBold().FontColor(Colors.Grey.Darken1);
            x.Span(value);
        });
    }
}
