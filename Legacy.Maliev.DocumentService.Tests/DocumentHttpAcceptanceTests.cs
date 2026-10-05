using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

// Uses the real stateless runtime, RS256 JWT admission and singleton QuestPDF renderer.
// No renderer, authorization handler or controller replacement is registered.
public sealed class DocumentHttpAcceptanceTests
{
    [Theory]
    [InlineData("invoice", "Remark", "INVOICE", 1, 595, 842)]
    [InlineData("quotation", "Comment", "QUOTATION", 1, 595, 842)]
    [InlineData("receipt", "Remark", "TAX INVOICE | RECEIPT", 2, 595, 842)]
    [InlineData("purchaseorder", "Notes", "PURCHASE ORDER", 1, 595, 842)]
    [InlineData("orderlabel", "Name", "HTTP-BOUNDARY", 1, 216, 288)]
    public async Task PascalCaseJson_ProducesRealBilingualPdfWithExpectedGeometry(
        string route, string markerField, string title, int pages, int width, int height)
    {
        await using var factory = new DocumentFactory();
        using var client = factory.Client();
        // Committed Web/Intranet consumers use lowercase paths with a trailing slash.
        foreach (var path in new[] { "/Pdfs/" + route, "/pdfs/" + route + "/" })
        {
            using var content = new StringContent("{\"" + markerField + "\":\"HTTP-BOUNDARY ทดสอบ\"}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(path, content);

            await AssertStatusAsync(response, HttpStatusCode.OK);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
            using var pdf = PdfDocument.Open(bytes);
            Assert.Equal(pages, pdf.NumberOfPages);
            Assert.All(pdf.GetPages(), page =>
            {
                Assert.Equal(width, page.Width, precision: 0);
                Assert.Equal(height, page.Height, precision: 0);
            });
            var text = string.Join(' ', pdf.GetPages().Select(page => page.Text));
            Assert.Contains(title, text, StringComparison.Ordinal);
            Assert.Contains("HTTP-BOUNDARY", text, StringComparison.Ordinal);
            Assert.Contains("ทดสอบ", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("invoice")]
    [InlineData("quotation")]
    [InlineData("receipt")]
    [InlineData("purchaseorder")]
    [InlineData("orderlabel")]
    public async Task NullDocument_Returns400WithoutPdf(string route)
    {
        await using var factory = new DocumentFactory();
        using var client = factory.Client();
        using var content = new StringContent("null", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/" + route, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
    }

    [Fact]
    public async Task ConsumerInvoiceJson_BindsNestedIntegerAndDecimalFieldsIntoPdfBytes()
    {
        await using var factory = new DocumentFactory();
        using var client = factory.Client();
        // Literal wire fields match the committed Intranet invoice producer, without sharing its serializer.
        const string payload = """
            {"Number":"HTTP-INVOICE-37","CreatedDate":"2026-10-03T00:00:00","Currency":"THB",
             "Remark":null,"Subtotal":246.90,"Vat":17.28,"Total":264.18,"Outstanding":264.18,
             "OrderItems":[{"Description":"HTTP-LINE-37 ทดสอบ","Quantity":2,"UnitPrice":123.45,"Subtotal":246.90}]}
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/invoice/", content);

        await AssertStatusAsync(response, HttpStatusCode.OK);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        var text = pdf.GetPage(1).Text;
        Assert.Contains("HTTP-INVOICE-37", text, StringComparison.Ordinal);
        Assert.Contains("HTTP-LINE-37", text, StringComparison.Ordinal);
        Assert.Contains("ทดสอบ", text, StringComparison.Ordinal);
        Assert.Contains("123.45", text, StringComparison.Ordinal);
        Assert.Contains("246.90", text, StringComparison.Ordinal);
        Assert.Contains("264.18", text, StringComparison.Ordinal);

        // Observe the bound integer in its PDF table row, rather than any digit in a date or total.
        var words = pdf.GetPage(1).GetWords().ToArray();
        var price = Assert.Single(words, word => word.Text == "123.45");
        var amount = Assert.Single(words, word => word.Text == "246.90"
            && Math.Abs(word.Letters[0].StartBaseLine.Y - price.Letters[0].StartBaseLine.Y) < 0.5
            && word.BoundingBox.Centroid.X > price.BoundingBox.Centroid.X);
        Assert.Single(words, word => word.Text == "2"
            && Math.Abs(word.Letters[0].StartBaseLine.Y - price.Letters[0].StartBaseLine.Y) < 0.5
            && word.BoundingBox.Centroid.X > price.BoundingBox.Centroid.X
            && word.BoundingBox.Centroid.X < amount.BoundingBox.Centroid.X);
    }

    [Fact]
    public async Task ConsumerInvoiceJson_WithNonIntegerQuantity_ReturnsBinding400WithoutPdf()
    {
        await using var factory = new DocumentFactory();
        using var client = factory.Client();
        using var content = new StringContent(
            """{"OrderItems":[{"Description":"synthetic","Quantity":"not-an-integer","UnitPrice":123.45,"Subtotal":246.90}]}""",
            Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/invoice/", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(true, 403)]
    public async Task AnonymousOrMissingRenderPermission_CannotObtainPdf(bool authenticated, int expected)
    {
        await using var factory = new DocumentFactory();
        using var client = authenticated ? factory.Client(includePermission: false) : factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/invoice", content);

        await AssertStatusAsync(response, (HttpStatusCode)expected);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected}, received {(int)response.StatusCode}: {body}");
        }
    }

    [Theory]
    [InlineData("invoice")]
    [InlineData("purchaseorder")]
    [InlineData("quotation")]
    [InlineData("receipt")]
    [InlineData("orderlabel")]
    public async Task EveryRenderRoute_RejectsInvalidCredentialsBeforeProducingPdf(string route)
    {
        await using var factory = new DocumentFactory();
        foreach (var fault in new[] { "signature", "issuer", "audience", "expired", "permission" })
        {
            using var client = factory.Client(includePermission: fault != "permission", tokenFault: fault);
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/Pdfs/" + route, content);

            await AssertStatusAsync(response, fault == "permission" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
            Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.False((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
            if (fault != "permission")
            {
                Assert.Contains("Bearer", response.Headers.WwwAuthenticate.Select(header => header.Scheme));
            }
        }
    }

    internal sealed class DocumentFactory : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://document-fixture.invalid";
        private const string Audience = "document-fixture";
        private readonly RSA signingKey = RSA.Create(2048);

        public HttpClient Client(bool includePermission = true, string? tokenFault = null)
        {
            var client = CreateClient();
            var now = DateTime.UtcNow;
            var claims = new List<Claim> { new("sub", "service:document-fixture"), new("identity_kind", "service") };
            if (includePermission) claims.Add(new("permissions", "legacy.documents.render"));
            using var untrustedKey = tokenFault == "signature" ? RSA.Create(2048) : null;
            var token = new JwtSecurityToken(
                tokenFault == "issuer" ? "https://untrusted-document-fixture.invalid" : Issuer,
                tokenFault == "audience" ? "untrusted-document-fixture" : Audience,
                claims,
                now.AddMinutes(tokenFault == "expired" ? -30 : -1),
                now.AddMinutes(tokenFault == "expired" ? -20 : 5),
                new SigningCredentials(new RsaSecurityKey(untrustedKey ?? signingKey), SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("CORS:AllowedOrigins:0", Issuer);
            builder.UseSetting("Jwt:PublicKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(signingKey.ExportSubjectPublicKeyInfoPem())));
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) signingKey.Dispose();
        }
    }
}
