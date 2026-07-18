using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class DocumentHeader
{
    internal static void Compose(IContainer container, byte[] logo, string title, string thaiTitle, string? reference)
    {
        container.Row(row =>
        {
            row.ConstantItem(130).Height(38).Image(logo).FitArea();
            row.RelativeItem().Element(item => Title(item, title, thaiTitle, reference));
        });
    }

    private static void Title(IContainer container, string title, string thaiTitle, string? reference)
    {
        var titleFontSize = title.Length > 18 ? 16 : 18;
        container.AlignRight().Column(right =>
        {
            right.Item().Element(item => BilingualText.Compose(
                item,
                title,
                thaiTitle,
                titleFontSize,
                9,
                bold: true,
                BilingualTextAlignment.Right));
            if (!string.IsNullOrWhiteSpace(reference))
                right.Item().PaddingTop(2).Element(item => BilingualText.ComposeCombined(
                    item,
                    reference,
                    8,
                    6.5f,
                    alignment: BilingualTextAlignment.Right));
        });
    }
}
