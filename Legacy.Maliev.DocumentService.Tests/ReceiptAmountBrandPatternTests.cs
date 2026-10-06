using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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
public sealed class ReceiptAmountBrandPatternTests
{
    [Fact]
    public void EmbeddedArtwork_RetainsSixOrderedLettersFromTheBundledFont()
    {
        var assembly = typeof(QuestDocumentRenderer).Assembly;
        using var font = assembly.GetManifestResourceStream("Legacy.Maliev.DocumentService.Rendering.Resources.Fonts.NotoSans-Regular.ttf")!;
        using var artwork = assembly.GetManifestResourceStream("Legacy.Maliev.DocumentService.Rendering.Resources.ReceiptAmountBrandWord.svg")!;
        var resource = XDocument.Load(artwork);
        var root = resource.Root!;
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Equal("MALIEV", root.Attribute("data-word")!.Value);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(font)).ToLowerInvariant(), root.Attribute("data-font-sha256")!.Value);
        Assert.Equal("4", root.Attribute("data-font-size-pt")!.Value);
        Assert.Equal("14.26", root.Attribute("width")!.Value);
        Assert.Equal("MALIEV", string.Concat(root.Descendants(svg + "path").Select(path => path.Attribute("data-letter")!.Value)));
        Assert.Equal(6, root.Descendants(svg + "path").Count());
        Assert.All(root.Descendants(svg + "path"), path => Assert.False(string.IsNullOrWhiteSpace(path.Attribute("d")!.Value)));
        Assert.All(root.Descendants(), element => Assert.Contains(element.Name.LocalName, new[] { "g", "path" }));
        Assert.DoesNotContain(root.Descendants().Attributes(), attribute => attribute.Name.LocalName is "href" or "src");
    }

    [Theory]
    [InlineData(1, "5.99")]
    [InlineData(44, "212")]
    public void ThbReceipt_HasVisibleClippedBrandingOnTheFinalPageOfBothCopies(int items, string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        var bytes = new QuestDocumentRenderer().RenderReceipt(Receipt("THB", items, amount));
        using var document = PdfDocument.Open(bytes);
        var expectedPages = items == 44 ? 4 : 2;
        Assert.Equal(expectedPages, document.NumberOfPages);
        var amountWords = ThaiBahtAmountWords.Format(amount);
        foreach (var page in document.GetPages())
        {
            var finalPage = items == 1 || page.Number is 2 or 4;
            Assert.Equal(finalPage ? 1 : 0, Count(Compact(page.Text), amountWords));
            if (finalPage)
            {
                AssertVisibleClippedBand(bytes, page, amountWords, items);
            }
        }

        var allText = string.Join('\n', document.GetPages().Select(page => page.Text));
        for (var index = 1; index <= items; index++)
        {
            Assert.Equal(2, Count(allText, $"BRAND-ITEM-{index:000}"));
        }

        Record($"receipt-brand-pattern-v1-items-{items}.pdf", bytes);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("thb")]
    [InlineData("บาท")]
    [InlineData(null)]
    [InlineData("")]
    public void OtherCurrencies_OmitAmountWordsAndGrayBrandBand(string? currency)
    {
        var bytes = new QuestDocumentRenderer().RenderReceipt(Receipt(currency, 1, 5.99m));
        using var document = PdfDocument.Open(bytes);
        Assert.Equal(2, document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            Assert.DoesNotContain("ห้าบาทเก้าสิบเก้าสตางค์", Compact(page.Text), StringComparison.Ordinal);
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
            var remark = Assert.Single(words, word => word.Text.StartsWith("Remark", StringComparison.Ordinal));
            using var raster = Raster(bytes, page.Number);
            var left = 18 * 72d / 25.4 + 8;
            // Empty left margin immediately above the remark: a stray branding layer must not remain.
            var region = Region(raster, page, left, left + 80, remark.BoundingBox.Top + 3, remark.BoundingBox.Top + 8);
            Assert.Equal(0, GrayPixels(raster, region));
        }
    }

    private static void AssertVisibleClippedBand(byte[] bytes, Page page, string amountWords, int items)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
        var logicalLetters = page.Letters.SelectMany(letter => Compact(letter.Value)
            .Select(character => (Character: character, Letter: letter))).ToArray();
        var logicalText = new string(logicalLetters.Select(item => item.Character).ToArray());
        var amountStart = logicalText.IndexOf(amountWords, StringComparison.Ordinal);
        Assert.True(amountStart >= 0);
        Assert.Equal(amountStart, logicalText.LastIndexOf(amountWords, StringComparison.Ordinal));
        var amountLetters = logicalLetters.Skip(amountStart).Take(amountWords.Length).Select(item => item.Letter).ToArray();
        var received = Assert.Single(words, word => Compact(word.Text) == "Received");
        var remark = Assert.Single(words, word => word.Text.StartsWith("Remark", StringComparison.Ordinal));
        var bounds = (Left: amountLetters.Min(letter => letter.BoundingBox.Left),
            Right: amountLetters.Max(letter => letter.BoundingBox.Right),
            Bottom: amountLetters.Min(letter => letter.BoundingBox.Bottom),
            Top: amountLetters.Max(letter => letter.BoundingBox.Top));
        var contentLeft = 18 * 72d / 25.4;
        var contentRight = page.Width - 14 * 72d / 25.4;
        var center = (contentLeft + contentRight) / 2;
        Assert.InRange((bounds.Left + bounds.Right) / 2, center - 3, center + 3);
        Assert.True(bounds.Top < received.BoundingBox.Bottom);
        Assert.True(bounds.Bottom > remark.BoundingBox.Top);
        using var raster = Raster(bytes, page.Number);
        foreach (var (left, right) in new[] { (contentLeft + 8, contentLeft + 88), (contentRight - 88, contentRight - 8) })
        {
            var band = Region(raster, page, left, right, bounds.Bottom + 0.5, bounds.Top - 0.5);
            var fraction = GrayPixels(raster, band) / (double)(band.Width * band.Height);
            Assert.InRange(fraction, 0.015, 0.4);
            if (items == 1)
            {
                AssertLegacyGrayDensity(fraction, page.Number, left == contentLeft + 8);
            }
        }

        var above = Region(raster, page, contentLeft + 8, contentLeft + 88, bounds.Top + 8, bounds.Top + 10);
        var below = Region(raster, page, contentLeft + 8, contentLeft + 88, bounds.Bottom - 7, bounds.Bottom - 5);
        Assert.Equal(0, GrayPixels(raster, above));
        Assert.Equal(0, GrayPixels(raster, below));
        var outsideLeft = Region(raster, page, contentLeft - 6, contentLeft - 3, bounds.Bottom, bounds.Top);
        var outsideRight = Region(raster, page, contentRight + 3, contentRight + 6, bounds.Bottom, bounds.Top);
        Assert.Equal(0, GrayPixels(raster, outsideLeft));
        Assert.Equal(0, GrayPixels(raster, outsideRight));
        var text = Region(raster, page, bounds.Left, bounds.Right, bounds.Bottom, bounds.Top);
        var dark = 0;
        for (var y = text.Top; y < text.Bottom; y++)
        {
            for (var x = text.Left; x < text.Right; x++)
            {
                if (raster.GetPixel(x, y).Red < 80)
                {
                    dark++;
                }
            }
        }

        Assert.True(dark > 30, "Dark Thai foreground must remain visible above the gray branding.");
        var fullBand = Region(raster, page, contentLeft, contentRight, bounds.Bottom - 2, bounds.Top + 2);
        using var crop = new SKBitmap();
        Assert.True(raster.ExtractSubset(crop, fullBand));
        using var image = SKImage.FromBitmap(crop);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        Record($"receipt-brand-pattern-v1-items-{items}-page-{page.Number}-150dpi.png", encoded.ToArray());
    }

    private static void AssertLegacyGrayDensity(double currentFraction, int pageNumber, bool leftSide)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Baselines", "legacy-itext", "receipt-without-withholding-tax-unittest.pdf");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("823b1c490a0b0eb045833cea7417613d5cceab7c820b5d80a53aeebea1328f19", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var document = PdfDocument.Open(bytes);
        Assert.Equal(2, document.NumberOfPages);
        var page = document.GetPage(pageNumber);
        using var raster = Raster(bytes, pageNumber);
        // Frozen oracle: Thai glyph bounds are y=401.394..410.494 from the page top.
        // Sample only gray lettering in the empty margins of that known amount band.
        var left = leftSide ? 60d : 460d;
        var region = Region(raster, page, left, left + 80, page.Height - 409, page.Height - 402);
        var legacyFraction = GrayPixels(raster, region) / (double)(region.Width * region.Height);
        Assert.InRange(legacyFraction, 0.015, 0.4);
        Assert.InRange(Math.Abs(currentFraction - legacyFraction), 0, 0.15);
    }

    private static SKBitmap Raster(byte[] bytes, int page)
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            throw new PlatformNotSupportedException("Brand-pattern evidence requires PDFium on supported platforms.");
        }

#pragma warning disable CA1416 // Runtime platform guard, matching the existing raster tests.
        return Conversion.ToImage(bytes, page: page - 1, password: null,
            options: new RenderOptions { Dpi = 150, Grayscale = true });
#pragma warning restore CA1416
    }

    private static SKRectI Region(SKBitmap raster, Page page, double left, double right, double bottom, double top)
    {
        const double scale = 150d / 72;
        var region = new SKRectI(
            Math.Max(0, (int)Math.Ceiling(left * scale)),
            Math.Max(0, (int)Math.Ceiling((page.Height - top) * scale)),
            Math.Min(raster.Width, (int)Math.Floor(right * scale)),
            Math.Min(raster.Height, (int)Math.Floor((page.Height - bottom) * scale)));
        Assert.True(region.Width > 0 && region.Height > 0);
        return region;
    }

    private static int GrayPixels(SKBitmap raster, SKRectI region)
    {
        var count = 0;
        for (var y = region.Top; y < region.Bottom; y++)
        {
            for (var x = region.Left; x < region.Right; x++)
            {
                if (raster.GetPixel(x, y).Red is >= 150 and <= 210)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static ReceiptDocument Receipt(string? currency, int items, decimal amount) => new()
    {
        Id = 89,
        InvoiceNumber = "INV-BRAND-PATTERN-FEATURE",
        PaymentDate = new DateTime(2026, 7, 15),
        Currency = currency!,
        AmountPaid = amount,
        Subtotal = 200m,
        Vat = 14m,
        Total = 214m,
        WithholdingTax = null,
        Remark = "Synthetic receipt branding fixture",
        OrderItems = Enumerable.Range(1, items).Select(index => new ReceiptLine
        {
            Description = $"BRAND-ITEM-{index:000}",
            Quantity = 1,
            UnitPrice = 200m,
            Subtotal = 200m,
        }).ToList(),
    };

    private static string Compact(string value) => string.Concat(value.Normalize(NormalizationForm.FormC).Where(character => !char.IsWhiteSpace(character)));

    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static void Record(string name, byte[] bytes)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf", "receipt-brand-pattern-v1");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }
}
