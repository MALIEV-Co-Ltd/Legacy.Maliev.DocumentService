using System.Net;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentQuotationPreparerHttpTests
{
    [Fact]
    public async Task QuotationPreparerContacts_RenderInTheirOwnColumnAndOmitUnknownFields()
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var present in new[] { true, false })
        {
            using var content = new StringContent(JsonSerializer.Serialize(new
            {
                Customer = new
                {
                    FullName = "CUSTOMER",
                    Telephone = "CUSTOMERPHONE",
                    Fax = "CUSTOMERFAX",
                },
                Employee = new
                {
                    FullName = "PREPARER",
                    Email = "preparer@example.invalid",
                    Telephone = present ? "PREPARERPHONE" : null,
                    Mobile = "PREPARERMOBILE",
                    Fax = present ? "PREPARERFAX" : null,
                },
            }), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/pdfs/quotation/", content, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token);
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
            using var pdf = PdfDocument.Open(bytes);
            Assert.Equal(1, pdf.NumberOfPages);
            var page = pdf.GetPage(1);
            var words = page.GetWords().ToArray();
            var marker = Assert.Single(words, word => word.Text == "PREPARER");
            // Customer telephone/fax remain visible in another column and cannot mask omissions.
            var preparerText = string.Concat(words.Where(word =>
                word.BoundingBox.Left >= marker.BoundingBox.Left - 1
                && word.BoundingBox.Right <= marker.BoundingBox.Left + 160
                && word.Letters[0].StartBaseLine.Y <= marker.Letters[0].StartBaseLine.Y + 0.5
                && word.Letters[0].StartBaseLine.Y >= marker.Letters[0].StartBaseLine.Y - 70)
                .Select(word => word.Text));
            Assert.Contains("preparer@example.invalid", preparerText, StringComparison.Ordinal);
            Assert.Contains("Mobile:PREPARERMOBILE", preparerText, StringComparison.Ordinal);
            Assert.DoesNotContain("CUSTOMERPHONE", preparerText, StringComparison.Ordinal);
            Assert.DoesNotContain("CUSTOMERFAX", preparerText, StringComparison.Ordinal);
            if (present)
            {
                Assert.Contains("Telephone:PREPARERPHONE", preparerText, StringComparison.Ordinal);
                Assert.Contains("Fax:PREPARERFAX", preparerText, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain("Telephone:", preparerText, StringComparison.Ordinal);
                Assert.DoesNotContain("Fax:", preparerText, StringComparison.Ordinal);
            }
        }
    }
}
