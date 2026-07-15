using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class MetadataTable
{
    internal static void Compose(IContainer container, params (string Key, string? Value)[] values)
    {
        var populatedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .ToArray();

        if (populatedValues.Length == 0)
            return;

        container.Column(column =>
        {
            foreach (var (key, value) in populatedValues)
            {
                column.Item()
                    .BorderBottom(0.5f)
                    .BorderColor(DocumentStyle.HeaderFill)
                    .PaddingVertical(1.5f)
                    .Row(row =>
                    {
                        row.RelativeItem(55).Element(item => BilingualText.ComposeCombined(
                            item,
                            key,
                            6.5f,
                            5,
                            bold: true,
                            BilingualTextAlignment.Right));
                        row.RelativeItem(45).PaddingLeft(10).Element(item => BilingualText.ComposeCombined(
                            item,
                            value!,
                            7,
                            5.5f));
                    });
            }
        });
    }
}
