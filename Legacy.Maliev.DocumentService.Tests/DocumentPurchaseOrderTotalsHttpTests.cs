using System.Globalization;
using System.Net;
using System.Text;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentPurchaseOrderTotalsHttpTests
{
    [Fact]
    public async Task PurchaseOrder_RecomputesMultipleLinesAndSevenPercentVatDespiteSuppliedSubtotals()
    {
        using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        const string payload = """
            {"Date":"2026-10-07T00:00:00","Notes":"PO-TOTALS ทดสอบ",
             "OrderItems":[
               {"Description":"PO-LINE-A","Currency":"THB","Quantity":2,"UnitPrice":12.50,"Subtotal":9999},
               {"Description":"PO-LINE-B","Currency":"THB","Quantity":3,"UnitPrice":7.10,"Subtotal":8888}]}
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/purchaseorder", content, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(deadline.Token));
        Assert.Equal(1, pdf.NumberOfPages);
        var page = pdf.GetPage(1);
        Assert.Contains("ทดสอบ", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("9,999.00", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("8,888.00", page.Text, StringComparison.Ordinal);
        var words = page.GetWords().ToArray();
        foreach (var (description, unitPrice, quantity, lineAmount) in new[]
        {
            ("PO-LINE-A", "12.50", "2", "25.00"),
            ("PO-LINE-B", "7.10", "3", "21.30"),
        })
        {
            var anchor = Assert.Single(words, word => word.Text == description);
            var pricing = words.Where(word => word.BoundingBox.Left > anchor.BoundingBox.Right
                    && Math.Abs(word.Letters[0].StartBaseLine.Y - anchor.Letters[0].StartBaseLine.Y) < 0.5)
                .OrderBy(word => word.BoundingBox.Left)
                .Select(word => word.Text)
                .ToArray();
            Assert.Equal(new[] { unitPrice, "THB", quantity, lineAmount, "THB" }, pricing);
        }

        foreach (var (labelText, expected) in new[] { ("Subtotal", "46.30"), ("VAT", "3.24"), ("Grand", "49.54") })
        {
            var label = Assert.Single(words, word => word.Text == labelText);
            var amount = Assert.Single(words, word => decimal.TryParse(word.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                && word.BoundingBox.Left > label.BoundingBox.Right
                && Math.Abs(word.Letters[0].StartBaseLine.Y - label.Letters[0].StartBaseLine.Y) < 0.5);
            Assert.Equal(expected, amount.Text);
            var currency = Assert.Single(words, word => word.Text == "THB"
                && word.BoundingBox.Left > amount.BoundingBox.Right
                && Math.Abs(word.Letters[0].StartBaseLine.Y - amount.Letters[0].StartBaseLine.Y) < 0.5);
            Assert.Equal("THB", currency.Text);
        }
    }
}
