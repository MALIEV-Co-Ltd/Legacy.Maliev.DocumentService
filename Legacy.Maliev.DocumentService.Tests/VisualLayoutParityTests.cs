using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Legacy.Maliev.DocumentService.Tests;

[CollectionDefinition("Renderer artifacts", DisableParallelization = true)]
public sealed class RendererArtifactCollection;

[Collection("Renderer artifacts")]
public sealed class VisualLayoutParityTests
{
    [Fact]
    public void RepresentativeDocuments_KeepReadableTopToBottomSectionFlow()
    {
        GenerateRepresentativeArtifacts();

        AssertTopToBottom("invoice.pdf", "INVOICE", "Billing", "SALESPERSON", "Outstanding");
        AssertTopToBottom("quotation.pdf", "QUOTATION", "Prepared", "INVOICE", "Remark");
        AssertTopToBottom("receipt.pdf", "RECEIPT", "Customer", "Item", "Remark");
        AssertTopToBottom("purchase-order.pdf", "PURCHASE", "Supplier", "ORDERED", "Notes");
    }

    private static void AssertTopToBottom(string currentName, params string[] anchors)
    {
        var currentPath = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", currentName);
        using var current = PdfDocument.Open(currentPath);
        var currentPage = current.GetPage(1);
        var points = anchors.Select(anchor => (Anchor: anchor, Point: Anchor(currentPage, anchor, currentName))).ToArray();

        Assert.All(points, item =>
        {
            Assert.InRange(item.Point.X, 0, 1);
            Assert.InRange(item.Point.Y, 0, 1);
        });
        for (var index = 1; index < points.Length; index++)
            Assert.True(points[index - 1].Point.Y > points[index].Point.Y,
                $"{currentName} section '{points[index - 1].Anchor}' must appear above '{points[index].Anchor}'.");
    }

    private static (double X, double Y) Anchor(Page page, string text, string file)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
        var expected = Normalize(text);
        var matches = words
            // PDF text extractors may keep the adjacent Thai line in the same token.
            // Match the stable English anchor without depending on that tokenization detail.
            .Where(word => Normalize(word.Text).Contains(expected, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(word => word.BoundingBox.Height)
            .ThenBy(word => word.BoundingBox.Left)
            .ToArray();
        Assert.True(matches.Length > 0,
            $"Anchor '{text}' was not found in {file}. Available words: {string.Join(", ", words.Select(word => word.Text).Distinct().Take(40))}");
        var word = matches[0];
        return (
            (word.BoundingBox.Left + (word.BoundingBox.Width / 2)) / page.Width,
            (word.BoundingBox.Bottom + (word.BoundingBox.Height / 2)) / page.Height);
    }

    private static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit));

    private static void GenerateRepresentativeArtifacts()
    {
        var contracts = new QuestRendererContractTests();
        contracts.Invoice_PreservesA4TitleTotalsAndThaiToneMarks();
        contracts.Quotation_PreservesA4TitleAmountsAndThaiToneMarks();
        contracts.Receipt_PreservesTwoA4CopiesTitleAmountsAndThaiToneMarks();
        contracts.PurchaseOrder_PreservesA4TitleAmountAndThaiToneMarks();
        contracts.OrderLabel_PreservesFourByThreeGeometryAndThaiToneMarks();
    }
}
