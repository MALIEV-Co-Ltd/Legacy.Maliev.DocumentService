using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class TotalsBlock
{
    internal static void Compose(IContainer container, string? currency, params (string Label, decimal? Value)[] values)
    {
        var displayed = values.Where(value => value.Value is not null).ToArray();
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn();
            });

            for (var index = 0; index < displayed.Length; index++)
            {
                var (label, value) = displayed[index];
                var (englishLabel, _) = BilingualText.Split(label);
                var strong = englishLabel is "Outstanding" or "Quoted Amount" or "Amount Received";
                if (strong)
                    table.Cell().ColumnSpan(2).LineHorizontal(0.75f).LineColor(DocumentStyle.Rule);

                var labelCell = table.Cell().PaddingVertical(2);
                var valueText = table.Cell().PaddingVertical(2).AlignRight()
                    .Text($"{DocumentFormat.Money(value!.Value)} {DocumentFormat.Safe(currency)}").FontSize(7);
                BilingualText.ComposeCombined(
                    labelCell,
                    label,
                    7,
                    6,
                    bold: strong,
                    alignment: BilingualTextAlignment.Right);
                if (strong)
                {
                    valueText.FontFamily(DocumentStyle.LatinBold, DocumentStyle.ThaiBold);
                }
            }
        });
    }
}
