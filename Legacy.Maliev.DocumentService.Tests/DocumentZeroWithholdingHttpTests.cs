using System.Net;
using System.Text;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentZeroWithholdingHttpTests
{
    [Theory]
    [InlineData("invoice", 1)]
    [InlineData("quotation", 1)]
    [InlineData("receipt", 2)]
    public async Task ExplicitZeroWithholding_RemainsDistinctFromUnknownInRealPdf(string route, int pages)
    {
        Assert.False(HasZeroWithholdingRow([
            ("Withholding", 100d, 10d), ("Tax", 100d, 50d), ("3.00", 100d, 100d),
        ]));
        Assert.False(HasZeroWithholdingRow([
            ("Withholding", 100d, 10d), ("Tax", 100d, 50d), ("3.00", 100d, 100d),
            ("0.00", 80d, 100d),
        ]));
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        // Retained source fixtures send 0.00; existing raster adaptations use null.
        // Exercise the actual JSON binding and renderer for both financial meanings.
        foreach (var withholding in new[] { "0.00", "null" })
        {
            var payload = "{\"Currency\":\"THB\",\"Subtotal\":123.45,\"Vat\":8.64,"
                + "\"Total\":132.09,\"Outstanding\":132.09,\"QuotedAmount\":132.09,"
                + "\"AmountPaid\":132.09,\"WithholdingTax\":" + withholding + "}";
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/Pdfs/" + route, content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
            using var pdf = PdfDocument.Open(bytes);
            Assert.Equal(pages, pdf.NumberOfPages);
            Assert.All(pdf.GetPages(), page =>
            {
                Assert.Contains("132.09", page.Text, StringComparison.Ordinal);
                if (withholding == "0.00")
                {
                    Assert.Contains("Withholding Tax", page.Text, StringComparison.Ordinal);
                    Assert.True(HasZeroWithholdingRow(page.GetWords().Select(word =>
                        (word.Text, word.Letters[0].StartBaseLine.Y, word.BoundingBox.Left))),
                        "Expected zero to the right of the withholding label on the same PDF baseline.");
                }
                else
                {
                    Assert.DoesNotContain("Withholding Tax", page.Text, StringComparison.Ordinal);
                }
            });
        }
    }

    private static bool HasZeroWithholdingRow(IEnumerable<(string Text, double Baseline, double Left)> words)
    {
        var rowWords = words.ToArray();
        foreach (var label in rowWords.Where(word => word.Text.Contains("Withholding", StringComparison.Ordinal)))
        {
            var sameLine = rowWords.Where(word => Math.Abs(word.Baseline - label.Baseline) < 0.5).ToArray();
            var labelText = string.Concat(sameLine.Where(word => word.Left >= label.Left)
                .OrderBy(word => word.Left).Select(word => word.Text)).Replace(" ", string.Empty, StringComparison.Ordinal);
            if (labelText.StartsWith("WithholdingTax", StringComparison.Ordinal)
                && sameLine.Any(word => word.Text == "0.00" && word.Left > label.Left))
                return true;
        }

        return false;
    }
}
