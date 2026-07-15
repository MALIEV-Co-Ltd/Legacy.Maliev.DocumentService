using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using InvoiceLine = Legacy.Maliev.DocumentService.Domain.Invoice.OrderItem;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class AnnotatedInvoiceLayoutTests
{
    [Fact]
    public void InvoiceMasthead_HasNoDeadSpaceBelowLogoOrMetadata()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var page = document.GetPage(1);
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
        var logo = Assert.Single(page.GetImages()).BoundingBox;
        var company = Find(words, "MALIEV", minimumX: 0, maximumX: 200);
        var currency = Find(words, "THB", minimumX: 300, maximumX: 595);
        var billing = Find(words, "Billing", minimumX: 0, maximumX: 300);

        Assert.InRange(logo.Bottom - company.BoundingBox.Top, 6, 16);
        Assert.InRange(currency.BoundingBox.Bottom - billing.BoundingBox.Top, 0, 18);
    }

    [Fact]
    public void InvoiceMasthead_ShowsReferenceOnceAndKeepsMetadataPairsCompact()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var page = document.GetPage(1);
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
        var dateLabel = Find(words, "DATE", minimumX: 300, maximumX: page.Width);
        var dateValue = Find(words, "2026-07-15", minimumX: 300, maximumX: page.Width);

        Assert.Equal(1, Count(page.Text, "INV-TH-1"));
        Assert.InRange(dateValue.BoundingBox.Left - dateLabel.BoundingBox.Right, 6, 24);
    }

    [Fact]
    public void InvoiceTotals_IncludeThaiSubtitleForEveryFinancialLabel()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var text = string.Join(' ', document.GetPages().Select(page => page.Text));

        Assert.Contains("ยอดรวม", text, StringComparison.Ordinal);
        Assert.Contains("ภาษี", text, StringComparison.Ordinal);
        Assert.Contains("ยอดค", text, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(Root(), "Legacy.Maliev.DocumentService.Rendering", "Documents", "InvoiceDocumentComposer.cs"));
        Assert.Contains("Subtotal / ยอดรวมก่อนภาษี", source, StringComparison.Ordinal);
        Assert.Contains("VAT 7% / ภาษีมูลค่าเพิ่ม 7%", source, StringComparison.Ordinal);
        Assert.Contains("Grand Total / ยอดรวมสุทธิ", source, StringComparison.Ordinal);
        Assert.Contains("Withholding Tax / ภาษีหัก ณ ที่จ่าย", source, StringComparison.Ordinal);
        Assert.Contains("Outstanding / ยอดค้างชำระ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BillingAddress_UsesCompactLabelsAndLineGrouping()
    {
        var billing = QuestDocumentRenderer.InvoiceBilling(Invoice());

        Assert.DoesNotContain("Court of Registry:", billing, StringComparison.Ordinal);
        Assert.DoesNotContain("Commercial Register No.:", billing, StringComparison.Ordinal);
        Assert.Contains("Registry:", billing, StringComparison.Ordinal);
        Assert.Contains("Tax ID:", billing, StringComparison.Ordinal);
        Assert.InRange(billing.Split('\n').Length, 1, 8);
    }

    [Fact]
    public void SharedComponents_DoNotCreatePartyRulesOrHeaderFillGutters()
    {
        var root = Root();
        var composer = File.ReadAllText(Path.Combine(root, "Legacy.Maliev.DocumentService.Rendering", "Documents", "ModernBusinessDocumentComposer.cs"));
        var tableStyle = File.ReadAllText(Path.Combine(root, "Legacy.Maliev.DocumentService.Rendering", "Components", "ItemTableStyle.cs"));

        Assert.DoesNotContain("BorderTop", composer, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderBottom", composer, StringComparison.Ordinal);
        var headerCell = tableStyle[tableStyle.IndexOf("internal static IContainer HeaderCell", StringComparison.Ordinal)..];
        Assert.True(
            headerCell.IndexOf("Background(DocumentStyle.HeaderFill)", StringComparison.Ordinal)
            < headerCell.IndexOf("PaddingHorizontal", StringComparison.Ordinal),
            "The header background must be painted before padding so it reaches every border.");
    }

    [Fact]
    public void ItemTableStyle_DrawsEachSharedEdgeOnlyOnce()
    {
        var tableStyle = File.ReadAllText(Path.Combine(
            Root(),
            "Legacy.Maliev.DocumentService.Rendering",
            "Components",
            "ItemTableStyle.cs"));

        Assert.DoesNotContain(".Border(", tableStyle, StringComparison.Ordinal);
        Assert.Contains("BorderBottom", tableStyle, StringComparison.Ordinal);
        Assert.Contains("BorderRight", tableStyle, StringComparison.Ordinal);
        Assert.Contains("BorderLeft", tableStyle, StringComparison.Ordinal);
    }

    [Fact]
    public void InvoiceFooter_UsesVerifiedScbAccountWithoutRepeatingCompanyContactBlock()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var text = string.Join(' ', document.GetPages().Select(page => page.Text));

        Assert.Contains("Siam Commercial Bank Public Company Limited", text, StringComparison.Ordinal);
        Assert.Contains("417-108808-2", text, StringComparison.Ordinal);
        Assert.Contains("Savings account", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kasikornbank", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("049-878-2612", text, StringComparison.Ordinal);
        Assert.DoesNotContain("KASITHBKXXX", text, StringComparison.Ordinal);
        Assert.Equal(1, Count(text, "36/1 Moo 3"));
        Assert.Equal(1, Count(text, "www.maliev.com"));
    }

    [Fact]
    public void InvoiceFooter_AnchorsPaginationAtPrintableBottomOnEveryPage()
    {
        var invoice = Invoice();
        invoice.OrderItems = Enumerable.Range(1, 75)
            .Select(index => new InvoiceLine
            {
                Description = $"Line item {index}",
                Quantity = index,
                UnitPrice = 10m,
                Subtotal = index * 10m,
            })
            .ToList();

        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(invoice));
        Assert.True(document.NumberOfPages > 1);

        foreach (var page in document.GetPages())
        {
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();
            var pageLabel = Find(words, "Page", minimumX: 400, maximumX: page.Width);

            Assert.InRange(pageLabel.BoundingBox.Bottom, 12, 30);
            Assert.True(pageLabel.BoundingBox.Left >= 480, "Pagination must remain inside its isolated right-hand footer column.");
            Assert.Contains(words, word => word.Text.Contains("Siam", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void InvoiceFooter_UsesParallelEnglishAndThaiPaymentColumns()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var words = NearestNeighbourWordExtractor.Instance.GetWords(document.GetPage(1).Letters).ToArray();
        var englishBank = words.First(word => word.Text.Contains("Siam", StringComparison.OrdinalIgnoreCase));
        var thaiBank = words.First(word => word.Text.Contains("ธนาคาร", StringComparison.Ordinal));

        Assert.True(
            thaiBank.BoundingBox.Left - englishBank.BoundingBox.Left >= 190,
            "Thai payment details must occupy a distinct right-hand language column.");
        Assert.InRange(Math.Abs(englishBank.BoundingBox.Bottom - thaiBank.BoundingBox.Bottom), 0, 6);
    }

    private static Word Find(IEnumerable<Word> words, string text, double minimumX, double maximumX) =>
        words.First(word =>
            word.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
            && word.BoundingBox.Left >= minimumX
            && word.BoundingBox.Right <= maximumX);

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static InvoiceDocument Invoice() => new()
    {
        Number = "INV-TH-1",
        CreatedDate = new DateTime(2026, 7, 15),
        Currency = "THB",
        BillingAddressRecipient = "ณัฐกานต์ วนาศรีวิไล",
        BillingAddressCompany = "บริษัท มาลีฟ จำกัด",
        BillingAddressBuilding = "สำนักงานใหญ่",
        BillingAddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย",
        BillingAddressLine2 = "ตำบลคลองข่อย",
        BillingAddressCity = "ปากเกร็ด",
        BillingAddressState = "นนทบุรี",
        BillingAddressPostalCode = "11120",
        BillingAddressCountry = "ประเทศไทย",
        CommercialRegistration = "Department of Business Development",
        TaxIdentification = "0125561001573",
        Subtotal = 100,
        Vat = 7,
        Total = 107,
        WithholdingTax = 3,
        Outstanding = 104,
    };

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Legacy.Maliev.DocumentService.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}