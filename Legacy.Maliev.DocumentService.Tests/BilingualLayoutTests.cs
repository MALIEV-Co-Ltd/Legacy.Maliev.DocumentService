using Legacy.Maliev.DocumentService.Rendering;
using Legacy.Maliev.DocumentService.Rendering.Components;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class BilingualLayoutTests
{
    [Fact]
    public void BilingualHeading_RendersEnglishAboveSmallerThaiSubtitle()
    {
        _ = new QuestDocumentRenderer();
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.Content().Element(container => BilingualText.Compose(
                container,
                "English heading",
                "หัวข้อภาษาไทย",
                englishFontSize: 9,
                thaiFontSize: 7,
                bold: true));
        })).GeneratePdf();

        using var document = PdfDocument.Open(pdf);
        var words = NearestNeighbourWordExtractor.Instance.GetWords(document.GetPage(1).Letters).ToArray();
        var english = Assert.Single(words, word => word.Text.Contains("English", StringComparison.Ordinal));
        var thai = Assert.Single(words, word => word.Text.Contains("ภาษาไทย", StringComparison.Ordinal));

        Assert.True(english.BoundingBox.Bottom > thai.BoundingBox.Top,
            "The English label must occupy the line above the Thai subtitle.");
        Assert.True(english.BoundingBox.Height > thai.BoundingBox.Height,
            "The Thai subtitle must use a smaller type size than the English label.");
    }
}
