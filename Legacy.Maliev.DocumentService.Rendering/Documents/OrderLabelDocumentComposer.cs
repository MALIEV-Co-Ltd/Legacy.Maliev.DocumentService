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
            page.Size(new PageSize(288, 216));
            page.Margin(0);
            page.DefaultTextStyle(style => style
                .FontFamily(DocumentStyle.Latin, DocumentStyle.Thai)
                .FontSize(9)
                .FontColor(DocumentStyle.Ink));
            page.Content().Layers(layers =>
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
                            .FontFamily(DocumentStyle.LatinBold, DocumentStyle.ThaiBold).FontSize(9);
                        table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingHorizontal(2).Text(value).FontSize(9);
                    }
                });

                layers.Layer().PaddingHorizontal(10).PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(70).Height(25).Image(logo).FitArea();
                    row.RelativeItem().AlignRight().Text("PACKING SLIP")
                        .FontFamily(DocumentStyle.LatinBold, DocumentStyle.ThaiBold).FontSize(20);
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
        ("ORDER #", DocumentFormat.Safe(label.Id)),
        ("NAME", DocumentFormat.Safe(label.Name)),
        ("PROCESS", DocumentFormat.Safe(label.Process)),
        ("MATERIAL", DocumentFormat.Safe(label.Material)),
        ("QUANTITY", $"ORDERED: {label.OrderQuantity}, SHIPPED: {label.ManufactureQuantity}, REMAINING: {label.RemainingQuantity}"),
        ("COLOR", DocumentFormat.Safe(label.Color)),
        ("POST", DocumentFormat.Safe(label.SurfaceFinish)),
        ("DESCRIPTION", DocumentFormat.Safe(label.Description)),
    ];
}
