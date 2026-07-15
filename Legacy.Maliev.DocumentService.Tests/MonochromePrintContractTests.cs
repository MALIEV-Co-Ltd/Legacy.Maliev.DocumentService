using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed partial class MonochromePrintContractTests
{
    [Fact]
    public void RenderingSource_UsesOnlyApprovedMonochromeColorTokens()
    {
        var rendering = Path.Combine(FindRepositoryRoot(), "Legacy.Maliev.DocumentService.Rendering");
        var colors = Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => HexColor().Matches(File.ReadAllText(path)).Select(match => match.Value.ToUpperInvariant()))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var approved = new[] { "#111111", "#333333", "#4A4A4A", "#D9D9D9" };
        Assert.All(colors, color => Assert.Contains(color, approved));
    }

    [Fact]
    public void RepresentativeOutputs_ArePrintableAndContainRequiredAssets()
    {
        var contracts = new QuestRendererContractTests();
        contracts.Invoice_PreservesA4TitleTotalsAndThaiToneMarks();
        contracts.Quotation_PreservesA4TitleAmountsAndThaiToneMarks();
        contracts.Receipt_PreservesTwoA4CopiesTitleAmountsAndThaiToneMarks();
        contracts.PurchaseOrder_PreservesA4TitleAmountAndThaiToneMarks();
        contracts.OrderLabel_PreservesFourByThreeGeometryAndThaiToneMarks();

        foreach (var name in new[] { "invoice", "quotation", "receipt", "purchase-order" })
        {
            using var pdf = PdfDocument.Open(Artifact(name));
            Assert.All(pdf.GetPages(), page =>
            {
                Assert.Equal(595, page.Width, precision: 0);
                Assert.Equal(842, page.Height, precision: 0);
                Assert.NotEmpty(page.GetImages());
                Assert.DoesNotContain(
                    NearestNeighbourWordExtractor.Instance.GetWords(page.Letters),
                    word => word.Text == "_");
            });
        }

        using var label = PdfDocument.Open(Artifact("order-label"));
        var labelPage = label.GetPage(1);
        Assert.Equal(216, Math.Min(labelPage.Width, labelPage.Height), precision: 0);
        Assert.Equal(288, Math.Max(labelPage.Width, labelPage.Height), precision: 0);
        Assert.Single(labelPage.GetImages());
    }

    private static string Artifact(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", $"{name}.pdf");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Legacy.Maliev.DocumentService.Rendering")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("DocumentService repository root was not found.");
    }

    [GeneratedRegex("#[0-9A-Fa-f]{6}")]
    private static partial Regex HexColor();
}
