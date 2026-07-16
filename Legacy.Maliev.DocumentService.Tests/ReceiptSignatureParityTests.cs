using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class ReceiptSignatureParityTests
{
    [Fact]
    public void Receipt_EmbedsSignatureOnCopyOnly_LikeLegacyRenderer()
    {
        var receipt = new ReceiptDocument
        {
            Id = 88,
            InvoiceNumber = "INV-SIGNATURE",
            PaymentDate = new DateTime(2026, 7, 15),
            Currency = "THB",
            Signature = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nAAAAABJRU5ErkJggg=="),
        };

        var pdf = new QuestDocumentRenderer().RenderReceipt(receipt);

        using var document = PdfDocument.Open(pdf);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Single(document.GetPage(1).GetImages());
        Assert.Equal(2, document.GetPage(2).GetImages().Count());
    }
}
