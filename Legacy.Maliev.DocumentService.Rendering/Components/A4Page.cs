using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Legacy.Maliev.DocumentService.Rendering.Components;

internal static class A4Page
{
    private const float PointsPerMillimetre = 72f / 25.4f;
    internal const float PrintableLeftEdge = 14 * PointsPerMillimetre;
    internal const float ContentLeftMargin = 18 * PointsPerMillimetre;
    internal const float ContentRightMargin = 14 * PointsPerMillimetre;
    internal const float TopMargin = 10 * PointsPerMillimetre;
    internal const float FoldIndicatorLeft = 14 * PointsPerMillimetre;
    internal const float FoldIndicatorWidth = 3 * PointsPerMillimetre;
    internal static float FirstFoldFromTop => PageSizes.A4.Height / 3f;
    internal static float SecondFoldFromTop => PageSizes.A4.Height * 2f / 3f;

    internal static void Configure(PageDescriptor page, float bottomMargin)
    {
        page.Size(PageSizes.A4);
        page.MarginLeft(ContentLeftMargin);
        page.MarginRight(ContentRightMargin);
        page.MarginTop(TopMargin);
        page.MarginBottom(bottomMargin);
        page.DefaultTextStyle(style => style
            .FontFamily(DocumentStyle.Latin, DocumentStyle.Thai)
            .FontSize(8)
            .FontColor(DocumentStyle.Ink));
        page.Background().Element(FoldMarks);
    }

    internal static void FoldMarks(IContainer container)
    {
        container.Column(column =>
        {
            column.Item()
                .Height(FirstFoldFromTop)
                .PaddingLeft(FoldIndicatorLeft)
                .AlignBottom()
                .Width(FoldIndicatorWidth)
                .LineHorizontal(0.75f)
                .LineColor(DocumentStyle.Rule);

            column.Item()
                .Height(SecondFoldFromTop - FirstFoldFromTop)
                .PaddingLeft(FoldIndicatorLeft)
                .AlignBottom()
                .Width(FoldIndicatorWidth)
                .LineHorizontal(0.75f)
                .LineColor(DocumentStyle.Rule);
        });
    }
}
