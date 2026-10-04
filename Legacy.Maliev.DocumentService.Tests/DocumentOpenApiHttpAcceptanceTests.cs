using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Legacy.Maliev.DocumentService.Tests;

// Expected descriptions come from the committed legacy controller and API XML registration.
// Uses the actual startup and documentation pipeline without replacing application services.
public sealed class DocumentOpenApiHttpAcceptanceTests
{
    [Theory]
    [InlineData("invoice", "Create the invoice PDF.")]
    [InlineData("purchaseorder", "Create the purchase order PDF.")]
    [InlineData("quotation", "Create the quotation PDF.")]
    [InlineData("receipt", "Create the receipt PDF.")]
    [InlineData("orderlabel", "Create the order label PDF.")]
    public async Task DevelopmentOpenApi_PreservesOriginalRenderSummaryAndBodyDescription(
        string route, string summary)
    {
        await using var factory = new DocumentMetadataFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/documents/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var operation = document.GetProperty("paths").GetProperty("/Pdfs/" + route).GetProperty("post");
        Assert.True(operation.TryGetProperty("summary", out var actualSummary), route);
        Assert.Equal(summary, actualSummary.GetString());
        var body = operation.GetProperty("requestBody");
        Assert.True(body.TryGetProperty("description", out var description), route);
        Assert.Equal("The item.", description.GetString());
        Assert.True(body.GetProperty("content").TryGetProperty("application/json", out _));
    }

    [Fact]
    public async Task ProductionOpenApi_IsNotExposed()
    {
        await using var factory = new DocumentMetadataFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/documents/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class DocumentMetadataFactory(string environment) : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://document-metadata-fixture.invalid";
        private readonly RSA signingKey = RSA.Create(2048);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS:AllowedOrigins:0", Issuer);
            builder.UseSetting("Jwt:PublicKey", Convert.ToBase64String(
                Encoding.UTF8.GetBytes(signingKey.ExportSubjectPublicKeyInfoPem())));
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", "document-metadata-fixture");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) signingKey.Dispose();
        }
    }
}
