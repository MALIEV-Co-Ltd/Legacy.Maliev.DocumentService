using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using PurchaseOrderCompany = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.CompanyInformation;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class SharedBusinessDocumentLayoutTests
{
    private readonly QuestDocumentRenderer renderer = new();

    [Fact]
    public void PurchaseOrder_PresentsSupplierBillingAndShippingOnOneBalancedRow()
    {
        using var document = PdfDocument.Open(renderer.RenderPurchaseOrder(PurchaseOrder()));
        var words = Words(document.GetPage(1));
        var supplier = Find(words, "Supplier");
        var billing = Find(words, "Billing");
        var shipping = Find(words, "Shipping");

        Assert.InRange(Math.Abs(supplier.BoundingBox.Bottom - billing.BoundingBox.Bottom), 0, 3);
        Assert.InRange(Math.Abs(supplier.BoundingBox.Bottom - shipping.BoundingBox.Bottom), 0, 3);
    }

    [Fact]
    public void Metadata_DoesNotRenderRowsForMissingValues()
    {
        var order = PurchaseOrder();
        order.OrderItems = [];

        using var document = PdfDocument.Open(renderer.RenderPurchaseOrder(order));

        Assert.DoesNotContain("CURRENCY", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("ITEMS", document.GetPage(1).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BusinessDocumentFooters_KeepPaginationAtBottomWithoutRepeatingCompanyIdentity()
    {
        var documents = new[]
        {
            (Bytes: renderer.RenderPurchaseOrder(PurchaseOrder()), ContainsLegalCompanyReference: false),
            (Bytes: renderer.RenderQuotation(new QuotationDocument { CreatedDate = new DateTime(2026, 7, 15) }), ContainsLegalCompanyReference: true),
            (Bytes: renderer.RenderReceipt(new ReceiptDocument { PaymentDate = new DateTime(2026, 7, 15) }), ContainsLegalCompanyReference: false),
        };

        foreach (var (bytes, containsLegalCompanyReference) in documents)
        {
            using var document = PdfDocument.Open(bytes);
            foreach (var page in document.GetPages())
            {
                var words = Words(page);
                var pageLabel = words.Single(word => word.Text.Equals("Page", StringComparison.OrdinalIgnoreCase));

                Assert.InRange(pageLabel.BoundingBox.Bottom, 12, 30);
                if (!containsLegalCompanyReference)
                    Assert.DoesNotContain(words, word =>
                        word.BoundingBox.Bottom < 45
                        && word.Text.Contains("Maliev", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void EveryBusinessDocument_LocalizesMetadataAndOperationalLabels()
    {
        var root = Root();
        var documentDirectory = Path.Combine(root, "Legacy.Maliev.DocumentService.Rendering", "Documents");
        var invoice = File.ReadAllText(Path.Combine(documentDirectory, "InvoiceDocumentComposer.cs"));
        var quotation = File.ReadAllText(Path.Combine(documentDirectory, "QuotationDocumentComposer.cs"));
        var receipt = File.ReadAllText(Path.Combine(documentDirectory, "ReceiptDocumentComposer.cs"));
        var purchaseOrder = File.ReadAllText(Path.Combine(documentDirectory, "PurchaseOrderDocumentComposer.cs"));

        Assert.Contains("INVOICE No. / เลขที่ใบแจ้งหนี้", invoice, StringComparison.Ordinal);
        Assert.Contains("SALESPERSON / พนักงานขาย", invoice, StringComparison.Ordinal);
        Assert.Contains("QUOTATION No. / เลขที่ใบเสนอราคา", quotation, StringComparison.Ordinal);
        Assert.Contains("VALID UNTIL / ใช้ได้ถึง", quotation, StringComparison.Ordinal);
        Assert.Contains("RECEIPT No. / เลขที่ใบเสร็จรับเงิน", receipt, StringComparison.Ordinal);
        Assert.Contains("PAYMENT DATE / วันที่ชำระเงิน", receipt, StringComparison.Ordinal);
        Assert.Contains("NUMBER / เลขที่ใบสั่งซื้อ", purchaseOrder, StringComparison.Ordinal);
        Assert.Contains("ORDERED BY / ผู้สั่งซื้อ", purchaseOrder, StringComparison.Ordinal);
    }

    [Fact]
    public void Receipt_DoesNotRepeatMetadataInsideASecondPaymentCard()
    {
        using var document = PdfDocument.Open(renderer.RenderReceipt(new ReceiptDocument
        {
            Id = 88,
            InvoiceNumber = "INV-TH-1",
            CustomerId = 12345,
            PaymentDate = new DateTime(2026, 7, 15),
        }));

        foreach (var page in document.GetPages())
            Assert.Equal(1, Count(page.Text, "INV-TH-1"));

        var composer = File.ReadAllText(Path.Combine(
            Root(),
            "Legacy.Maliev.DocumentService.Rendering",
            "Documents",
            "ReceiptDocumentComposer.cs"));
        Assert.DoesNotContain("new(\"Payment / การชำระเงิน\"", composer, StringComparison.Ordinal);
    }

    private static PurchaseOrderDocument PurchaseOrder() => new()
    {
        ReferenceNumber = 99,
        Date = new DateTime(2026, 7, 15),
        Supplier = Company("Supplier Company", "Supplier Contact"),
        Billing = Company("Billing Company", "Billing Contact"),
        Shipping = Company("Shipping Company", "Shipping Contact"),
        OrderedBy = "Purchasing Team",
        ShippedVia = "Carrier",
        FOB = "Origin",
        Terms = "NET 10",
    };

    private static PurchaseOrderCompany Company(string company, string contact) => new()
    {
        CompanyName = company,
        ContactName = contact,
        Telephone = "02-000-0000",
    };

    private static Word[] Words(UglyToad.PdfPig.Content.Page page) =>
        NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();

    private static Word Find(IEnumerable<Word> words, string text) =>
        words.Where(word => word.Text.Equals(text, StringComparison.OrdinalIgnoreCase))
            .MaxBy(word => word.BoundingBox.Bottom)!;

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Legacy.Maliev.DocumentService.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
}
