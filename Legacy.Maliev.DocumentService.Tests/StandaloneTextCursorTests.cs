using Legacy.Maliev.DocumentService.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed class StandaloneTextCursorTests
{
    private const string FirstSpan = "Cedar ";
    private const string ThaiSpan = "ภาษาไทย";
    private const string LastSpan = " meadow";
    private const string NextLine = "River ภาษาไทย";
    private const string Expected = FirstSpan + ThaiSpan + LastSpan + "\n" + NextLine;
    private const double PositionTolerance = 0.1;

    [Fact]
    public void ConsecutiveSpansAdvanceAndNewlineResetsWithoutChangingLogicalText()
    {
        // Use the runtime's embedded fonts and license settings without invoking a template.
        _ = new QuestDocumentRenderer();
        using var split = PdfDocument.Open(Render(splitSpans: true));
        using var single = PdfDocument.Open(Render(splitSpans: false));
        var splitPage = Assert.Single(split.GetPages());
        var singlePage = Assert.Single(single.GetPages());

        Assert.Equal(Expected, ContentOrderTextExtractor.GetText(splitPage).ReplaceLineEndings("\n").TrimEnd('\n'));
        Assert.Equal(Expected, ContentOrderTextExtractor.GetText(singlePage).ReplaceLineEndings("\n").TrimEnd('\n'));

        var letters = splitPage.Letters;
        var reference = singlePage.Letters;
        Assert.Equal(reference.Count, letters.Count);
        for (var i = 0; i < letters.Count; i++)
        {
            Assert.Equal(reference[i].Value, letters[i].Value);
            AssertNear(reference[i].StartBaseLine.X, letters[i].StartBaseLine.X);
            AssertNear(reference[i].StartBaseLine.Y, letters[i].StartBaseLine.Y);
            AssertNear(reference[i].EndBaseLine.X, letters[i].EndBaseLine.X);
            AssertNear(reference[i].EndBaseLine.Y, letters[i].EndBaseLine.Y);
        }

        var first = Assert.Single(letters, letter => letter.Value == "C");
        var thai = letters.First(letter => letter.Value == "ภ");
        var last = Assert.Single(letters, letter => letter.Value == "m");
        var reset = Assert.Single(letters, letter => letter.Value == "R");
        AssertNear(first.StartBaseLine.Y, thai.StartBaseLine.Y);
        AssertNear(first.StartBaseLine.Y, last.StartBaseLine.Y);
        var firstSpanEnd = letters.First(letter => letter.Value == "r").EndBaseLine.X;
        var thaiSpanEnd = letters.First(letter => letter.Value == "ย").EndBaseLine.X;
        Assert.True(thai.StartBaseLine.X > firstSpanEnd + PositionTolerance);
        Assert.True(last.StartBaseLine.X > thaiSpanEnd + PositionTolerance);
        AssertNear(first.StartBaseLine.X, reset.StartBaseLine.X);
        Assert.True(reset.StartBaseLine.Y < first.StartBaseLine.Y - PositionTolerance);
    }

    private static byte[] Render(bool splitSpans) => Document.Create(document =>
        document.Page(page =>
        {
            page.Size(400, 200);
            page.Margin(24);
            page.DefaultTextStyle(TextStyle.Default.FontFamily(DocumentStyle.Latin, DocumentStyle.Thai).FontSize(14));
            page.Content().Text(text =>
            {
                if (splitSpans)
                {
                    text.Span(FirstSpan);
                    text.Span(ThaiSpan);
                    text.Span(LastSpan);
                    text.Span("\n");
                    text.Span(NextLine);
                }
                else
                {
                    text.Span(Expected);
                }
            });
        })).GeneratePdf();

    private static void AssertNear(double expected, double actual) =>
        Assert.InRange(actual, expected - PositionTolerance, expected + PositionTolerance);
}
