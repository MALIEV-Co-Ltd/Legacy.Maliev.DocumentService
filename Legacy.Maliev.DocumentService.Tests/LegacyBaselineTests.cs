using System.Text.Json;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class LegacyBaselineTests
{
    [Fact]
    public void ITextBaselines_FreezeAllLegacyDocumentVariants()
    {
        var manifest = LoadManifest();

        Assert.Equal(22, manifest.Count);
        Assert.Equal(6, manifest.Count(item => item.File.StartsWith("invoice", StringComparison.Ordinal)));
        Assert.Equal(7, manifest.Count(item => item.File.StartsWith("quotation", StringComparison.Ordinal)));
        Assert.Equal(5, manifest.Count(item => item.File.StartsWith("receipt", StringComparison.Ordinal)));
        Assert.Equal(3, manifest.Count(item => item.File.StartsWith("purchase-order", StringComparison.Ordinal)));
        Assert.Single(manifest, item => item.File.StartsWith("orderlabel", StringComparison.Ordinal));
    }

    [Fact]
    public void ITextBaselines_FreezePageCountsAndGeometry()
    {
        foreach (var baseline in LoadManifest())
        {
            using var document = PdfDocument.Open(BaselinePath("legacy-itext", baseline.File));
            Assert.Equal(baseline.Pages, document.NumberOfPages);
            var page = document.GetPage(1);
            Assert.Equal(Math.Min(baseline.PageWidth, baseline.PageHeight), Math.Min(page.Width, page.Height), precision: 1);
            Assert.Equal(Math.Max(baseline.PageWidth, baseline.PageHeight), Math.Max(page.Width, page.Height), precision: 1);
        }
    }

    [Fact]
    public void QuestPdfMigration_HasAProductionRendererForEveryLegacyRoute()
    {
        var repository = FindRepositoryRoot();
        var projects = Directory.Exists(repository)
            ? Directory.GetFiles(repository, "*.csproj", SearchOption.AllDirectories)
            : [];
        var production = projects.Where(path => !path.Contains(".Tests", StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = string.Join('\n', production.Select(File.ReadAllText));

        Assert.Contains("QuestPDF", source, StringComparison.Ordinal);
        Assert.DoesNotContain("itext", source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, production.Length);
    }

    private static IReadOnlyList<BaselineManifest> LoadManifest() =>
        JsonSerializer.Deserialize<List<BaselineManifest>>(File.ReadAllText(BaselinePath("manifest.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static string BaselinePath(params string[] segments) =>
        Path.Combine([AppContext.BaseDirectory, "Baselines", .. segments]);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Legacy.Maliev.DocumentService.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("DocumentService repository root not found.");
    }

    private sealed record BaselineManifest(string File, int Pages, double PageWidth, double PageHeight);
}