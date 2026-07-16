using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class ItemTableStyle
{
    internal static IContainer Cell(IContainer container, bool firstColumn = false) => Borders(container, firstColumn, includeTop: false)
        .BorderColor(DocumentStyle.Rule)
        .PaddingHorizontal(3)
        .PaddingVertical(1.5f)
        .DefaultTextStyle(style => style.FontSize(7));

    internal static IContainer HeaderCell(IContainer container, bool firstColumn = false) => Borders(container, firstColumn, includeTop: true)
        .Background(DocumentStyle.HeaderFill)
        .BorderColor(DocumentStyle.Rule)
        .PaddingHorizontal(3)
        .PaddingVertical(2)
        .DefaultTextStyle(style => style.FontSize(7))
        .AlignCenter();

    private static IContainer Borders(IContainer container, bool firstColumn, bool includeTop)
    {
        var bordered = container
            .BorderBottom(0.75f)
            .BorderRight(0.75f);

        if (firstColumn)
            bordered = bordered.BorderLeft(0.75f);
        if (includeTop)
            bordered = bordered.BorderTop(0.75f);

        return bordered;
    }
}
