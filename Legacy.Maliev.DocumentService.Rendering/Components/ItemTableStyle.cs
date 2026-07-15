using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class ItemTableStyle
{
    internal static IContainer Cell(IContainer container) => container
        .Border(0.75f)
        .BorderColor(DocumentStyle.Rule)
        .PaddingHorizontal(3)
        .PaddingVertical(1.5f)
        .DefaultTextStyle(style => style.FontSize(7));

    internal static IContainer HeaderCell(IContainer container) => container
        .Background(DocumentStyle.HeaderFill)
        .Border(0.75f)
        .BorderColor(DocumentStyle.Rule)
        .PaddingHorizontal(3)
        .PaddingVertical(2)
        .DefaultTextStyle(style => style.FontSize(7))
        .AlignCenter();
}
