using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LabelDocument = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class OrderLabelDocumentComposer
{
    internal static byte[] Render(LabelDocument label, byte[] logo, DateTime packedAt) => Document.Create(document =>
        document.Page(page =>
        {
            // The immutable legacy label opens as a portrait page with clockwise-rotated content.
            page.Size(new PageSize(216.03f, 288.04f));
            page.Margin(0);
            page.DefaultTextStyle(style => style
                .FontFamily(DocumentStyle.Latin, DocumentStyle.Thai)
                .FontSize(9)
                .FontColor(DocumentStyle.Ink));
            page.Content().RotateLayoutClockwise().Layers(layers =>
            {
                layers.PrimaryLayer().PaddingTop(30).PaddingHorizontal(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(25);
                        columns.RelativeColumn(75);
                    });

                    foreach (var (key, value) in Rows(label))
                    {
                        table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingLeft(2).Text(key)
                            .FontFamily(DocumentStyle.Latin, DocumentStyle.Thai).Bold().FontSize(9);
                        table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingHorizontal(2).Text(value).FontSize(9);
                    }
                });

                layers.Layer().PaddingHorizontal(10).PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(70).Height(25).Image(logo).FitArea();
                    row.RelativeItem().AlignRight().Text("PACKING SLIP")
                        .FontFamily(DocumentStyle.Latin, DocumentStyle.Thai).Bold().FontSize(20);
                });

                layers.Layer().PaddingHorizontal(10).PaddingBottom(8).AlignBottom().Row(row =>
                {
                    row.RelativeItem().Text("THANK YOU FOR YOUR ORDER").FontSize(8);
                    row.RelativeItem().AlignRight().Text($"PACKED: {DocumentFormat.Date(packedAt)}").FontSize(8);
                });
            });
        })).GeneratePdf();

    private static (string Key, string Value)[] Rows(LabelDocument label) =>
    [
        ("ORDER #", label.Id ?? string.Empty),
        ("NAME", label.Name ?? string.Empty),
        ("PROCESS", label.Process ?? string.Empty),
        ("MATERIAL", label.Material ?? string.Empty),
        ("QUANTITY", $"ORDERED: {label.OrderQuantity}, SHIPPED: {label.ManufactureQuantity}, REMAINING: {label.RemainingQuantity}"),
        ("COLOR", label.Color ?? string.Empty),
        ("POST", label.SurfaceFinish ?? string.Empty),
        ("DESCRIPTION", label.Description ?? string.Empty),
    ];
}
