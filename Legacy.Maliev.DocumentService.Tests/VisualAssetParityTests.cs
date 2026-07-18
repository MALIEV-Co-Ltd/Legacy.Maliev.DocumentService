using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using LabelDocument = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed class VisualAssetParityTests
{
    [Fact]
    public void Quotation_PreservesLegacyLogo()
    {
        new QuestRendererContractTests().Quotation_PreservesA4TitleAmountsAndThaiToneMarks();
        var currentPath = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", "quotation.pdf");

        using var current = PdfDocument.Open(currentPath);
        Assert.Single(current.GetPage(1).GetImages());
    }

    [Fact]
    public void OrderLabel_PreservesLegacyPortraitViewerRotation()
    {
        var pdf = new QuestDocumentRenderer().RenderOrderLabel(new LabelDocument
        {
            Id = "15324",
            Name = "ออเดอร์ทดสอบระบบ",
            Process = "FDM",
            Material = "PLA",
            Color = "แดง",
            SurfaceFinish = "Deburred",
            OrderQuantity = 5,
            ManufactureQuantity = 4,
            RemainingQuantity = 1,
            Description = "ชิ้นงานสำหรับทดสอบเครื่องหมายวรรณยุกต์ไทย",
        });

        using var document = PdfDocument.Open(pdf);
        var currentPage = document.GetPage(1);
        var currentTitle = FindWord(currentPage, "PACKING");
        var descriptionLabel = FindWord(currentPage, "DESCRIPTION");

        Assert.True(currentPage.Width < currentPage.Height, "The immutable label opens on a portrait viewer canvas.");
        Assert.NotEmpty(currentTitle.Text);
        Assert.NotEmpty(descriptionLabel.Text);
    }

    private static UglyToad.PdfPig.Content.Word FindWord(UglyToad.PdfPig.Content.Page page, string text) =>
        NearestNeighbourWordExtractor.Instance.GetWords(page.Letters)
            .First(word => word.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

    private static double CenterX(UglyToad.PdfPig.Content.Word word, UglyToad.PdfPig.Content.Page page) =>
        (word.BoundingBox.Left + (word.BoundingBox.Width / 2)) / page.Width;

    private static double CenterY(UglyToad.PdfPig.Content.Word word, UglyToad.PdfPig.Content.Page page) =>
        (word.BoundingBox.Bottom + (word.BoundingBox.Height / 2)) / page.Height;
}
