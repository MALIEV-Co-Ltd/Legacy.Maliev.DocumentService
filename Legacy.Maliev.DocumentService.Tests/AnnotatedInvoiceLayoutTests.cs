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

        Assert.InRange(logo.Bottom - company.BoundingBox.Top, 17, 20);
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
    public void InvoiceTotals_PreserveLegacyFinancialLabelsAndThaiRemark()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var text = string.Join(' ', document.GetPages().Select(page => page.Text));

        Assert.Contains("Subtotal", text, StringComparison.Ordinal);
        Assert.Contains("VAT 7%", text, StringComparison.Ordinal);
        Assert.Contains("Grand Total", text, StringComparison.Ordinal);
        Assert.Contains("หมายเหตุ", text, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(Root(), "Legacy.Maliev.DocumentService.Rendering", "QuestDocumentRenderer.cs"));
        Assert.Contains("(\"Subtotal\", invoice.Subtotal)", source, StringComparison.Ordinal);
        Assert.Contains("(\"VAT 7%\", invoice.Vat)", source, StringComparison.Ordinal);
        Assert.Contains("(\"Grand Total\", invoice.Total)", source, StringComparison.Ordinal);
        Assert.Contains("(\"Withholding Tax\", invoice.WithholdingTax)", source, StringComparison.Ordinal);
        Assert.Contains("(\"Outstanding\", invoice.Outstanding)", source, StringComparison.Ordinal);
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
    public void InvoiceFooter_UsesVerifiedScbAccountAndLegacyFourColumnContactBlock()
    {
        using var document = PdfDocument.Open(new QuestDocumentRenderer().RenderInvoice(Invoice()));
        var text = string.Join(' ', document.GetPages().Select(page => page.Text));

        Assert.Contains("Siam", text, StringComparison.Ordinal);
        Assert.Contains("417-108808-2", text, StringComparison.Ordinal);
        Assert.Contains("Savings account", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kasikornbank", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("049-878-2612", text, StringComparison.Ordinal);
        Assert.DoesNotContain("KASITHBKXXX", text, StringComparison.Ordinal);
        Assert.Equal(2, Count(text, "36/1 Moo 3"));
        Assert.Equal(2, Count(text, "www.maliev.com"));
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

            Assert.InRange(pageLabel.BoundingBox.Bottom, 98, 104);
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

        Assert.InRange(
            thaiBank.BoundingBox.Left - englishBank.BoundingBox.Left,
            100,
            170);
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
