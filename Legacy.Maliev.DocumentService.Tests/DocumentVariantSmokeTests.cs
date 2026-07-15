using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using InvoiceLine = Legacy.Maliev.DocumentService.Domain.Invoice.OrderItem;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using PurchaseOrderLine = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.OrderItem;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using QuotationLine = Legacy.Maliev.DocumentService.Domain.Quotations.Order;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;
using ReceiptLine = Legacy.Maliev.DocumentService.Domain.Receipt.OrderItem;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentVariantSmokeTests
{
    private readonly QuestDocumentRenderer renderer = new();

    [Fact]
    public void EmptyA4Documents_RenderPrintablePages()
    {
        var invoice = renderer.RenderInvoice(new InvoiceDocument());
        var quotation = renderer.RenderQuotation(new QuotationDocument());
        var receipt = renderer.RenderReceipt(new ReceiptDocument());
        var purchaseOrder = renderer.RenderPurchaseOrder(new PurchaseOrderDocument());

        AssertA4(invoice);
        AssertA4(quotation);
        AssertA4(receipt, minimumPages: 2);
        AssertA4(purchaseOrder);
        Assert.All(new[] { invoice, quotation, receipt, purchaseOrder }, bytes =>
            Assert.DoesNotContain("0001-01-01", Text(bytes), StringComparison.Ordinal));
    }

    [Fact]
    public void LongItemCollections_FlowAcrossPagesWithoutLosingHeadersOrLogo()
    {
        AssertRepeatingFlow(renderer.RenderInvoice(new InvoiceDocument
        {
            Currency = "THB",
            Subtotal = 39_972m,
            Vat = 2_798.04m,
            Total = 42_770.04m,
            WithholdingTax = 1_199.16m,
            Outstanding = 41_570.88m,
            Remark = "Final totals and remarks remain together after the flowing item table.",
            OrderItems = Enumerable.Range(1, 80).Select(index => new InvoiceLine
            {
                Description = $"Invoice item {index} รายการทดสอบภาษาไทย",
                Quantity = index,
                UnitPrice = 12.34m,
                Subtotal = index * 12.34m,
            }).ToList(),
        }), "INVOICE", "invoice-long", minimumPages: 2);

        AssertRepeatingFlow(renderer.RenderQuotation(new QuotationDocument
        {
            Currency = "THB",
            Subtotal = 39_972m,
            Vat = 2_798.04m,
            Total = 42_770.04m,
            WithholdingTax = 1_199.16m,
            QuotedAmount = 41_570.88m,
            Comment = "Final quotation conditions remain with the totals.",
            Orders = Enumerable.Range(1, 80).Select(index => new QuotationLine
            {
                Id = index,
                Name = $"Quotation item {index}",
                Description = $"DETAIL-{index:D3} รายละเอียดภาษาไทยสำหรับทดสอบการขึ้นหน้าใหม่",
                Quantity = index,
                UnitPrice = 12.34m,
                Subtotal = index * 12.34m,
                Discount = index % 2 == 0 ? 3m : null,
            }).ToList(),
        }), "QUOTATION", "quotation-long", minimumPages: 2, assertItemRowsStayTogether: true);

        AssertRepeatingFlow(renderer.RenderReceipt(new ReceiptDocument
        {
            Currency = "THB",
            Subtotal = 39_972m,
            Vat = 2_798.04m,
            Total = 42_770.04m,
            WithholdingTax = 1_199.16m,
            AmountPaid = 41_570.88m,
            Remark = "Receipt totals remain legible on both original and copy.",
            OrderItems = Enumerable.Range(1, 80).Select(index => new ReceiptLine
            {
                Description = $"Receipt item {index} รายการทดสอบภาษาไทย",
                Quantity = index,
                UnitPrice = 12.34m,
                Subtotal = index * 12.34m,
            }).ToList(),
        }), "TAX INVOICE | RECEIPT", "receipt-long", minimumPages: 4, maximumPages: 4);

        AssertRepeatingFlow(renderer.RenderPurchaseOrder(new PurchaseOrderDocument
        {
            OrderItems = Enumerable.Range(1, 80).Select(index => new PurchaseOrderLine
            {
                PartNumber = $"PART-{index:D3}",
                Description = "Purchase-order item รายละเอียดภาษาไทยสำหรับทดสอบการขึ้นหน้าใหม่",
                Quantity = index,
                UnitPrice = 12.34m,
                Currency = "THB",
            }).ToList(),
        }), "PURCHASE ORDER", "purchase-order-long", minimumPages: 2, maximumPages: 2);
    }

    private static void AssertA4(byte[] bytes, int minimumPages = 1)
    {
        using var pdf = PdfDocument.Open(bytes);
        Assert.True(pdf.NumberOfPages >= minimumPages, $"Expected at least {minimumPages} pages, found {pdf.NumberOfPages}.");
        Assert.All(pdf.GetPages(), page =>
        {
            Assert.Equal(595, page.Width, precision: 0);
            Assert.Equal(842, page.Height, precision: 0);
            Assert.NotEmpty(page.GetImages());
        });
    }

    private static void AssertRepeatingFlow(
        byte[] bytes,
        string title,
        string artifactName,
        int minimumPages,
        bool assertItemRowsStayTogether = false,
        int? maximumPages = null)
    {
        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf");
        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllBytes(Path.Combine(artifactDirectory, $"{artifactName}.pdf"), bytes);
        AssertA4(bytes, minimumPages);
        using var pdf = PdfDocument.Open(bytes);
        if (maximumPages is not null)
            Assert.True(pdf.NumberOfPages <= maximumPages, $"Expected at most {maximumPages} pages, found {pdf.NumberOfPages}.");
        var itemRowMarker = artifactName switch
        {
            "invoice-long" => "Invoice item ",
            "quotation-long" => "Quotation item ",
            "receipt-long" => "Receipt item ",
            "purchase-order-long" => "PART-",
            _ => throw new ArgumentOutOfRangeException(nameof(artifactName), artifactName, "Unknown flowing document artifact."),
        };
        Assert.All(pdf.GetPages(), page =>
        {
            Assert.Contains(title, page.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Page", page.Text, StringComparison.OrdinalIgnoreCase);
            if (page.Text.Contains(itemRowMarker, StringComparison.OrdinalIgnoreCase))
                Assert.Contains("Description", page.Text, StringComparison.OrdinalIgnoreCase);
        });

        if (assertItemRowsStayTogether)
        {
            var pageTexts = pdf.GetPages().Select(page => page.Text).ToArray();
            foreach (var index in Enumerable.Range(1, 80))
            {
                Assert.Contains(pageTexts, text =>
                    text.Contains($"Quotation item {index}", StringComparison.Ordinal)
                    && text.Contains($"DETAIL-{index:D3}", StringComparison.Ordinal));
            }
        }
    }

    private static string Text(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join(' ', pdf.GetPages().Select(page => page.Text));
    }
}
