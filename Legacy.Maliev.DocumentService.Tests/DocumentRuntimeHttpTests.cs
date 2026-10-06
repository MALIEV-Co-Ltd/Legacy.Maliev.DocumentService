using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed class DocumentRuntimeHttpTests
{
    [Theory]
    [InlineData("invoice", "Remark", "INVOICE", 1)]
    [InlineData("purchaseorder", "Notes", "PURCHASE ORDER", 1)]
    [InlineData("quotation", "Comment", "QUOTATION", 1)]
    [InlineData("receipt", "Remark", "TAX INVOICE | RECEIPT", 2)]
    [InlineData("orderlabel", "Name", "HTTP-DOCUMENT", 1)]
    public async Task ActualRoutes_BindPascalCaseJsonAndReturnRealPdfBytes(string route, string field, string title, int pages)
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client();
        foreach (var path in new[] { "/Pdfs/" + route, "/pdfs/" + route + "/" })
        {
            var payload = route == "receipt"
                ? """{"Id":89,"InvoiceNumber":"HTTP-DOCUMENT","Currency":"THB","AmountPaid":5.99,"WithholdingTax":null,"Remark":"HTTP-DOCUMENT ทดสอบ"}"""
                : "{\"" + field + "\":\"HTTP-DOCUMENT ทดสอบ\"}";
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(path, content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
            using var document = PdfDocument.Open(bytes);
            Assert.Equal(pages, document.NumberOfPages);
            var text = string.Join('\n', document.GetPages().Select(page => page.Text));
            Assert.Contains(title, text, StringComparison.Ordinal);
            Assert.Contains("HTTP-DOCUMENT", text, StringComparison.Ordinal);
            Assert.Contains("ทดสอบ", text, StringComparison.Ordinal);
            if (route == "receipt")
            {
                Assert.All(document.GetPages(), page =>
                {
                    Assert.Contains("5.99", page.Text, StringComparison.Ordinal);
                    Assert.Contains("ห้าบาทเก้าสิบเก้าสตางค์", Compact(page.Text), StringComparison.Ordinal);
                });
            }
        }
    }

    [Theory]
    [InlineData("invoice", "null", "application/json", 400)]
    [InlineData("purchaseorder", "null", "application/json", 400)]
    [InlineData("quotation", "null", "application/json", 400)]
    [InlineData("receipt", "null", "application/json", 400)]
    [InlineData("orderlabel", "null", "application/json", 400)]
    [InlineData("invoice", "{", "application/json", 400)]
    [InlineData("purchaseorder", "{", "application/json", 400)]
    [InlineData("quotation", "{", "application/json", 400)]
    [InlineData("receipt", "{", "application/json", 400)]
    [InlineData("orderlabel", "{", "application/json", 400)]
    [InlineData("invoice", "{}", "text/plain", 415)]
    [InlineData("purchaseorder", "{}", "text/plain", 415)]
    [InlineData("quotation", "{}", "text/plain", 415)]
    [InlineData("receipt", "{}", "text/plain", 415)]
    [InlineData("orderlabel", "{}", "text/plain", 415)]
    public async Task InvalidBodies_AreRejectedByActualAdmissionWithoutPdf(string route, string body, string mediaType, int status)
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client();
        using var content = new StringContent(body, Encoding.UTF8, mediaType);
        using var response = await client.PostAsync("/pdfs/" + route + "/", content);
        await AssertRejectedAsync(response, (HttpStatusCode)status);
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("permission", 403)]
    [InlineData("expired", 401)]
    [InlineData("audience", 401)]
    [InlineData("issuer", 401)]
    [InlineData("signature", 401)]
    public async Task ActualJwtAndPermissionAdmission_PreventsReceiptRendering(string fault, int status)
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client(fault);
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/receipt/", content);
        await AssertRejectedAsync(response, (HttpStatusCode)status);
        if (status == 401)
        {
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.Select(header => header.Scheme));
        }
    }

    [Fact]
    public async Task ReceiptQuantity_RejectsInvalidIntegerWireValue()
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client();
        using var content = new StringContent("""{"OrderItems":[{"Quantity":"not-an-integer"}]}""", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/receipt/", content);
        await AssertRejectedAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DevelopmentMetadata_DescribesAllFiveActualPostRoutesAndPdfResponses()
    {
        await using var factory = new RuntimeFactory("Development");
        using var client = factory.Client("anonymous");
        using var response = await client.GetAsync("/documents/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadataJson = await response.Content.ReadAsStringAsync();
        var metadataDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults", "openapi-owned-xml");
        Directory.CreateDirectory(metadataDirectory);
        await File.WriteAllTextAsync(Path.Combine(metadataDirectory, "v1.json"), metadataJson);
        using var document = JsonDocument.Parse(metadataJson);
        var info = document.RootElement.GetProperty("info");
        Assert.Equal("Legacy MALIEV Document Service API", info.GetProperty("title").GetString());
        Assert.Equal("Authenticated .NET 10 compatibility API for rendering legacy MALIEV documents with QuestPDF.", info.GetProperty("description").GetString());
        Assert.Equal("1.0", info.GetProperty("version").GetString());
        var paths = document.RootElement.GetProperty("paths");
        var summaries = new Dictionary<string, string>
        {
            ["invoice"] = "Create the invoice PDF.",
            ["purchaseorder"] = "Create the purchase order PDF.",
            ["quotation"] = "Create the quotation PDF.",
            ["receipt"] = "Create the receipt PDF.",
            ["orderlabel"] = "Create the order label PDF.",
        };
        foreach (var (route, summary) in summaries)
        {
            var operation = paths.GetProperty("/Pdfs/" + route).GetProperty("post");
            Assert.Equal(summary, operation.GetProperty("summary").GetString());
            Assert.True(operation.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
            Assert.True(operation.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/pdf", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
        }
        var receiptSchema = paths.GetProperty("/Pdfs/receipt").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var nullableAlternatives = receiptSchema.GetProperty("oneOf");
        Assert.Equal(2, nullableAlternatives.GetArrayLength());
        Assert.Equal("null", nullableAlternatives[0].GetProperty("type").GetString());
        receiptSchema = nullableAlternatives[1];
        if (receiptSchema.TryGetProperty("$ref", out var reference))
        {
            var schemaName = reference.GetString()!.Split('/').Last();
            receiptSchema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }
        Assert.Equal("Receipt Model.", receiptSchema.GetProperty("description").GetString());
        Assert.Equal("Gets or sets the amount paid.\nThe amount paid.", receiptSchema.GetProperty("properties").GetProperty("AmountPaid").GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("invoice", "Remark", "EXAMPLE-INVOICE")]
    [InlineData("purchaseorder", "Notes", "EXAMPLE-PURCHASE-ORDER")]
    [InlineData("quotation", "Comment", "EXAMPLE-QUOTATION")]
    [InlineData("receipt", "Remark", "EXAMPLE-RECEIPT")]
    [InlineData("orderlabel", "Name", "EXAMPLE-ORDER-LABEL")]
    public async Task ServedOpenApiExample_RendersThroughNormalAuthenticatedProductionRoute(string route, string field, string marker)
    {
        await using var metadataFactory = new RuntimeFactory("Development");
        using var metadataClient = metadataFactory.Client("anonymous");
        using var metadataResponse = await metadataClient.GetAsync("/documents/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        var operation = metadata.RootElement.GetProperty("paths").GetProperty("/Pdfs/" + route).GetProperty("post");
        Assert.Equal("Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.",
            operation.GetProperty("description").GetString());
        Assert.Equal("The rendered PDF document.", operation.GetProperty("responses").GetProperty("200").GetProperty("description").GetString());
        var pdfSchema = operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/pdf").GetProperty("schema");
        if (pdfSchema.TryGetProperty("$ref", out var pdfReference))
        {
            pdfSchema = metadata.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(pdfReference.GetString()!.Split('/').Last());
        }
        Assert.Equal("string", pdfSchema.GetProperty("type").GetString());
        Assert.Equal("binary", pdfSchema.GetProperty("format").GetString());
        Assert.Equal("The JSON body is missing or invalid.", operation.GetProperty("responses").GetProperty("400").GetProperty("description").GetString());
        var errorContent = operation.GetProperty("responses").GetProperty("400").GetProperty("content");
        Assert.True(errorContent.TryGetProperty("application/problem+json", out _));
        Assert.False(errorContent.TryGetProperty("application/pdf", out _));
        var example = operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example");
        Assert.Equal(JsonValueKind.Object, example.ValueKind);
        Assert.Equal(marker, example.GetProperty(field).GetString());
        if (route == "receipt")
        {
            Assert.Equal(5.99m, example.GetProperty("AmountPaid").GetDecimal());
            Assert.Equal(JsonValueKind.Null, example.GetProperty("WithholdingTax").ValueKind);
        }

        await using var productionFactory = new RuntimeFactory();
        using var productionClient = productionFactory.Client();
        using var body = new StringContent(example.GetRawText(), Encoding.UTF8, "application/json");
        using var response = await productionClient.PostAsync("/Pdfs/" + route, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Join('\n', pdf.GetPages().Select(page => page.Text));
        Assert.Contains(marker, text, StringComparison.Ordinal);
        if (route == "receipt") Assert.Contains("ห้าบาทเก้าสิบเก้าสตางค์", Compact(text), StringComparison.Ordinal);

        using var invalidBody = new StringContent("null", Encoding.UTF8, "application/json");
        using var invalidResponse = await productionClient.PostAsync("/Pdfs/" + route, invalidBody);
        await AssertRejectedAsync(invalidResponse, HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", invalidResponse.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ProductionMetadata_IsNotPubliclyExposed()
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client("anonymous");
        using var response = await client.GetAsync("/documents/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptGet_DoesNotInvokePostRenderer()
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/pdfs/receipt/");
        await AssertRejectedAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ActualRendererFailure_ReturnsOpaqueProductionErrorWithoutPdf()
    {
        await using var factory = new RuntimeFactory();
        using var client = factory.Client();
        const string privateMarker = "DOCUMENT-FIXTURE@example.invalid";
        using var content = new StringContent(JsonSerializer.Serialize(new { Remark = privateMarker + "\U0010FFFF" }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/receipt/", content);
        await AssertRejectedAsync(response, HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        Assert.Equal("An internal server error occurred", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(500, document.RootElement.GetProperty("statusCode").GetInt32());
        Assert.DoesNotContain(privateMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestPDF", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRegistration_UsesActualSingletonRendererAndSystemTimeProvider()
    {
        using var factory = new RuntimeFactory();
        var renderer = factory.Services.GetRequiredService<IDocumentRenderer>();
        Assert.IsType<QuestDocumentRenderer>(renderer);
        Assert.Same(renderer, factory.Services.GetRequiredService<IDocumentRenderer>());
        Assert.Same(TimeProvider.System, factory.Services.GetRequiredService<TimeProvider>());
    }

    private static async Task AssertRejectedAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
    }

    private static string Compact(string value) => string.Concat(value.Normalize(NormalizationForm.FormC).Where(character => !char.IsWhiteSpace(character)));

    private sealed class RuntimeFactory(string environment = "Production") : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://document-runtime-fixture.invalid";
        private const string Audience = "document-runtime-fixture";
        private readonly RSA signingKey = RSA.Create(2048);

        internal HttpClient Client(string? fault = null)
        {
            var client = CreateClient();
            if (fault == "anonymous") return client;
            var now = DateTime.UtcNow;
            var claims = new List<Claim> { new("sub", "service:document-runtime-fixture"), new("identity_kind", "service") };
            if (fault != "permission") claims.Add(new("permissions", "legacy.documents.render"));
            using var untrusted = fault == "signature" ? RSA.Create(2048) : null;
            var token = new JwtSecurityToken(
                fault == "issuer" ? "https://wrong-fixture.invalid" : Issuer,
                fault == "audience" ? "wrong-fixture" : Audience,
                claims, now.AddMinutes(fault == "expired" ? -30 : -1),
                now.AddMinutes(fault == "expired" ? -20 : 5),
                new SigningCredentials(new RsaSecurityKey(untrusted ?? signingKey), SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
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
