using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Legacy.Maliev.DocumentService.Tests;

// Original controller response attributes and ConfigureSwagger security facts are retained inputs.
public sealed class DocumentOpenApiResponseSecurityHttpContractTests
{
    [Theory]
    [InlineData("invoice")]
    [InlineData("purchaseorder")]
    [InlineData("quotation")]
    [InlineData("receipt")]
    [InlineData("orderlabel")]
    public async Task DevelopmentOpenApi_PreservesOriginalResponseDeclarations(string route)
    {
        await using var factory = new DocumentMetadataFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/documents/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var operation = document.GetProperty("paths").GetProperty("/Pdfs/" + route).GetProperty("post");
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "default" })
        {
            Assert.True(responses.TryGetProperty(status, out var declaration),
                $"Original response {status} is missing for {route}; actual keys: " +
                string.Join(",", responses.EnumerateObject().Select(property => property.Name)));
            Assert.Equal(JsonValueKind.Object, declaration.ValueKind);
        }
    }

    [Fact]
    public async Task DevelopmentOpenApi_PreservesBearerAuthorizationHeaderAndEffectiveSecurity()
    {
        await using var factory = new DocumentMetadataFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/documents/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("Authorization", scheme.GetProperty("name").GetString());
        Assert.Equal("JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            scheme.GetProperty("description").GetString());

        foreach (var route in new[] { "invoice", "purchaseorder", "quotation", "receipt", "orderlabel" })
        {
            var operation = document.GetProperty("paths").GetProperty("/Pdfs/" + route).GetProperty("post");
            // Per-operation metadata overrides the original global requirement when present.
            var security = operation.TryGetProperty("security", out var local)
                ? local
                : document.GetProperty("security");
            var requirement = Assert.Single(security.EnumerateArray());
            var bearer = Assert.Single(requirement.EnumerateObject());
            Assert.Equal("Bearer", bearer.Name);
            Assert.Equal(JsonValueKind.Array, bearer.Value.ValueKind);
            Assert.Equal(0, bearer.Value.GetArrayLength());
        }
    }

    private sealed class DocumentMetadataFactory : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://document-response-fixture.invalid";
        private readonly RSA signingKey = RSA.Create(2048);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("CORS:AllowedOrigins:0", Issuer);
            builder.UseSetting("Jwt:PublicKey", Convert.ToBase64String(
                Encoding.UTF8.GetBytes(signingKey.ExportSubjectPublicKeyInfoPem())));
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", "document-response-fixture");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) signingKey.Dispose();
        }
    }
}
