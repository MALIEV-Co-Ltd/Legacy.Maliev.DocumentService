using System.Globalization;
using System.Text;
using Legacy.Maliev.DocumentService.Rendering;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;
using ReceiptLine = Legacy.Maliev.DocumentService.Domain.Receipt.OrderItem;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed class ReceiptThaiAmountContentTests
{
    private const string PaidWords = "สองร้อยสิบสองบาทถ้วน";

    [Theory]
    [InlineData("en-US")]
    [InlineData("th-TH")]
    [InlineData("de-DE")]
    public void ThbReceipt_UsesAmountPaidInBothCopiesWithCenteredVisibleWords(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var bytes = new QuestDocumentRenderer().RenderReceipt(Receipt("THB"));
            using var document = PdfDocument.Open(bytes);
            Assert.Equal(2, document.NumberOfPages);
            Assert.Contains("ORIGINAL", document.GetPage(1).Text, StringComparison.Ordinal);
            Assert.Contains("COPY", document.GetPage(2).Text, StringComparison.Ordinal);
            foreach (var page in document.GetPages())
            {
                Assert.Contains("212.00", page.Text, StringComparison.Ordinal);
                Assert.Contains("214.00", page.Text, StringComparison.Ordinal);
                Assert.Contains("2.00", page.Text, StringComparison.Ordinal);
                Assert.Contains("(" + PaidWords + ")", Compact(page.Text), StringComparison.Ordinal);
                Assert.DoesNotContain("สองร้อยสิบสี่บาทถ้วน", page.Text, StringComparison.Ordinal);
                Assert.DoesNotContain("\uFFFD", page.Text, StringComparison.Ordinal);
                AssertAmountGeometryAndRaster(bytes, page, cultureName);
            }

            Record($"receipt-thb-amount-paid-212-{cultureName}.pdf", bytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("thb")]
    [InlineData("บาท")]
    [InlineData(null)]
    [InlineData("")]
    public void OtherCurrencies_OmitThaiAmountRow(string? currency)
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderReceipt(Receipt(currency)));
        Assert.Equal(2, document.NumberOfPages);
        Assert.All(document.GetPages(), page => Assert.DoesNotContain(PaidWords, page.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void LongReceipt_Preserves44ItemsAndOneAmountRowAtEndOfEachCopy()
    {
        var bytes = new QuestDocumentRenderer().RenderReceipt(Receipt("THB", 44));
        using var document = PdfDocument.Open(bytes);
        Assert.Equal(4, document.NumberOfPages);
        for (var index = 1; index <= document.NumberOfPages; index++)
        {
            var text = Compact(document.GetPage(index).Text);
            Assert.Equal(index is 2 or 4 ? 1 : 0, Count(text, PaidWords));
        }

        var allText = string.Join('\n', document.GetPages().Select(page => page.Text));
        for (var index = 1; index <= 44; index++)
        {
            Assert.Equal(2, Count(allText, $"RECEIPT-ITEM-{index:000}"));
        }

        Record("receipt-thb-amount-paid-212-items-44.pdf", bytes);
    }

    [Fact]
    public void LegacyThbOracle_AndExplicitNewFixtureRetainFiveBahtNinetyNineSatang()
    {
        var baseline = Path.Combine(AppContext.BaseDirectory, "Baselines", "legacy-itext", "receipt-without-withholding-tax-unittest.pdf");
        using var legacy = PdfDocument.Open(baseline);
        var receipt = Receipt("THB");
        receipt.AmountPaid = 5.99m;
        receipt.WithholdingTax = null;
        var bytes = new QuestDocumentRenderer().RenderReceipt(receipt);
        using var current = PdfDocument.Open(bytes);
        const string words = "ห้าบาทเก้าสิบเก้าสตางค์";
        Assert.Equal(legacy.NumberOfPages, current.NumberOfPages);
        Assert.All(legacy.GetPages(), page => Assert.Contains(words, Compact(page.Text), StringComparison.Ordinal));
        Assert.All(current.GetPages(), page => Assert.Contains(words, Compact(page.Text), StringComparison.Ordinal));
        Record("receipt-thb-amount-paid-5-99-explicit-feature-fixture.pdf", bytes);
    }

    private static void AssertAmountGeometryAndRaster(byte[] bytes, Page page, string culture)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
        // Thai combining glyphs need not be grouped into one word by the spatial extractor.
        var logicalLetters = page.Letters.SelectMany(letter => Compact(letter.Value)
            .Select(character => (Character: character, Letter: letter))).ToArray();
        var logicalText = new string(logicalLetters.Select(item => item.Character).ToArray());
        var amountStart = logicalText.IndexOf(PaidWords, StringComparison.Ordinal);
        Assert.True(amountStart >= 0, "The actual PDF must contain the complete Thai amount phrase.");
        Assert.Equal(amountStart, logicalText.LastIndexOf(PaidWords, StringComparison.Ordinal));
        var amountLetters = logicalLetters.Skip(amountStart).Take(PaidWords.Length).Select(item => item.Letter).ToArray();
        var received = Assert.Single(words, word => Compact(word.Text) == "Received");
        var remark = Assert.Single(words, word => word.Text.StartsWith("Remark", StringComparison.Ordinal));
        var bounds = (Left: amountLetters.Min(letter => letter.BoundingBox.Left),
            Right: amountLetters.Max(letter => letter.BoundingBox.Right),
            Bottom: amountLetters.Min(letter => letter.BoundingBox.Bottom),
            Top: amountLetters.Max(letter => letter.BoundingBox.Top));
        // A4Page uses an 18 mm left and 14 mm right content margin.
        var contentCenter = (18 * 72d / 25.4 + page.Width - 14 * 72d / 25.4) / 2;
        Assert.InRange((bounds.Left + bounds.Right) / 2, contentCenter - 3, contentCenter + 3);
        Assert.True(bounds.Top < received.BoundingBox.Bottom, "Thai words must be below numeric Amount Received.");
        Assert.True(bounds.Bottom > remark.BoundingBox.Top, "Thai words must precede the remark without overlap.");
        Assert.InRange(bounds.Left, 20, page.Width - 20);
        Assert.InRange(bounds.Right, 20, page.Width - 20);

        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            throw new PlatformNotSupportedException("Receipt raster evidence requires PDFium on the supported desktop/CI platforms.");
        }

#pragma warning disable CA1416 // Supported platforms guarded above, matching existing raster parity tests.
        using var raster = Conversion.ToImage(bytes, page: page.Number - 1, password: null,
            options: new RenderOptions { Dpi = 150, Grayscale = true });
#pragma warning restore CA1416
        var scale = 150d / 72;
        var region = new SKRectI(
            Math.Max(0, (int)Math.Floor(bounds.Left * scale) - 3),
            Math.Max(0, (int)Math.Floor((page.Height - bounds.Top) * scale) - 3),
            Math.Min(raster.Width, (int)Math.Ceiling(bounds.Right * scale) + 3),
            Math.Min(raster.Height, (int)Math.Ceiling((page.Height - bounds.Bottom) * scale) + 3));
        using var crop = new SKBitmap();
        Assert.True(raster.ExtractSubset(crop, region));
        var ink = 0;
        for (var y = 0; y < crop.Height; y++)
        {
            for (var x = 0; x < crop.Width; x++)
            {
                if (crop.GetPixel(x, y).Red < 225)
                {
                    ink++;
                }
            }
        }

        Assert.True(ink > 30, "The extracted Thai amount row must contain visible raster ink at 150 DPI.");
        using var image = SKImage.FromBitmap(crop);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        Record($"receipt-thb-amount-row-{culture}-page-{page.Number}-150dpi.png", encoded.ToArray());
    }

    private static ReceiptDocument Receipt(string? currency, int itemCount = 1) => new()
    {
        Id = 88,
        InvoiceNumber = "INV-THB-AMOUNT-FEATURE",
        PaymentDate = new DateTime(2026, 7, 15),
        Currency = currency!,
        AmountPaid = 212m,
        Subtotal = 200m,
        Vat = 14m,
        Total = 214m,
        WithholdingTax = 2m,
        Remark = "Explicit synthetic receipt feature fixture",
        OrderItems = Enumerable.Range(1, itemCount).Select(index => new ReceiptLine
        {
            Description = $"RECEIPT-ITEM-{index:000}",
            Quantity = 1,
            UnitPrice = 200m,
            Subtotal = 200m,
        }).ToList(),
    };

    private static string Compact(string value) => string.Concat(value.Normalize(NormalizationForm.FormC).Where(character => !char.IsWhiteSpace(character)));

    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static void Record(string name, byte[] bytes)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", "receipt-thai-amount-v1");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }
}
