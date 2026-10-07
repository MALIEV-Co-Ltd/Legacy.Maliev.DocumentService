using System.Net;
using System.Text;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentInvoiceWithholdingHttpTests
{
    [Theory]
    [InlineData("3", "-3.00")]
    [InlineData("-3", "3.00")]
    [InlineData("0", "0.00")]
    [InlineData("null", null)]
    public async Task InvoiceWithholding_RendersNumericDeductionAndPreservesSuppliedOutstanding(string withholding, string? expected)
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var payload = "{\"Currency\":\"THB\",\"Subtotal\":100,\"Vat\":7,\"Total\":107,"
            + "\"Outstanding\":91.23,\"WithholdingTax\":" + withholding + "}";
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/invoice", content, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(deadline.Token));
        var page = pdf.GetPage(1);
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("91.23", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("--3.00", page.Text, StringComparison.Ordinal);
        var words = page.GetWords().ToArray();
        if (expected is null)
        {
            Assert.DoesNotContain("Withholding Tax", page.Text, StringComparison.Ordinal);
        }
        else
        {
            var label = Assert.Single(words, word => word.Text.Contains("Withholding", StringComparison.Ordinal));
            var sameRow = words.Where(word => word.BoundingBox.Left > label.BoundingBox.Left
                && Math.Abs(word.Letters[0].StartBaseLine.Y - label.Letters[0].StartBaseLine.Y) < 0.5).ToArray();
            var amount = Assert.Single(sameRow, word => word.Text.EndsWith("3.00", StringComparison.Ordinal)
                || word.Text == "0.00");
            Assert.Equal(expected, amount.Text);
        }
    }
}
