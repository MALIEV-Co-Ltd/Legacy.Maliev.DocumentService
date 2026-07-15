using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class PartyBlock
{
    internal static void Compose(IContainer container, string heading, string content)
    {
        container.Row(row =>
        {
            row.ConstantItem(70).Text(heading).FontFamily(DocumentStyle.LatinBold, DocumentStyle.ThaiBold).LineHeight(1.35f);
            row.RelativeItem().Text(content).LineHeight(1.35f);
        });
    }
}
