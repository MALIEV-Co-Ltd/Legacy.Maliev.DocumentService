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
    public void RepresentativeDocuments_KeepLegacySectionAnchorGeometry()
    {
        GenerateRepresentativeArtifacts();

        AssertAnchors("invoice.pdf", "invoice-thai-unittest.pdf", "INVOICE", "Billing:", "SALESPERSON", "Remark");
        AssertAnchors("quotation.pdf", "quotation-thai-unittest.pdf", "QUOTATION", "Prepared", "INVOICE", "Remark");
        AssertAnchors("receipt.pdf", "receipt-thai-unittest.pdf", "RECEIPT", "Customer:", "Item", "Remark");
        AssertAnchors("purchase-order.pdf", "purchase-order-thai-unittest.pdf", "PURCHASE", "Supplier:", "ORDERED", "Notes");
    }

    private static void AssertAnchors(string currentName, string legacyName, params string[] anchors)
    {
        var currentPath = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", currentName);
        var legacyPath = Path.Combine(AppContext.BaseDirectory, "Baselines", "legacy-itext", legacyName);
        using var current = PdfDocument.Open(currentPath);
        using var legacy = PdfDocument.Open(legacyPath);
        var currentPage = current.GetPage(1);
        var legacyPage = legacy.GetPage(1);

        foreach (var anchor in anchors)
        {
            var currentPoint = Anchor(currentPage, anchor, currentName);
            var legacyPoint = Anchor(legacyPage, anchor, legacyName);
            Assert.InRange(Math.Abs(currentPoint.X - legacyPoint.X), 0, 0.09);
            Assert.True(Math.Abs(currentPoint.Y - legacyPoint.Y) <= 0.09,
                $"{currentName} anchor '{anchor}' moved vertically: current={currentPoint.Y:F3}, legacy={legacyPoint.Y:F3}.");
        }
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