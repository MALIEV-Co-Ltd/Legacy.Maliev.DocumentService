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
    [InlineData("invoice", "Number", "string", "Total", "number")]
    [InlineData("quotation", "Number", "string", "Total", "number")]
    [InlineData("receipt", "InvoiceNumber", "string", "AmountPaid", "number")]
    [InlineData("purchaseorder", "ReferenceNumber", "integer", "Date", "string")]
    [InlineData("orderlabel", "Name", "string", "OrderQuantity", "integer")]
    public async Task DevelopmentOpenApi_DescribesExistingPascalCaseConsumerFieldTypes(
        string route, string firstField, string firstType, string secondField, string secondType)
    {
        await using var factory = new DocumentMetadataFactory();
        using var response = await factory.CreateClient().GetAsync("/documents/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var schema = document.GetProperty("paths").GetProperty("/Pdfs/" + route).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/components/schemas/";
            var target = reference.GetString()!;
            Assert.StartsWith(prefix, target, StringComparison.Ordinal);
            schema = document.GetProperty("components").GetProperty("schemas").GetProperty(target[prefix.Length..]);
        }
        var candidates = ObjectProperties(schema, document, 0);
        Assert.True(candidates.Length == 1, "Expected one actual document object schema: " + schema.GetRawText());
        var properties = candidates[0];
        foreach (var (field, expectedType) in new[] { (firstField, firstType), (secondField, secondType) })
        {
            Assert.True(properties.TryGetProperty(field, out var property), route + "." + field);
            Assert.False(properties.TryGetProperty(char.ToLowerInvariant(field[0]) + field[1..], out _));
            Assert.Contains(expectedType, ScalarTypes(property, document, 0));
        }
    }

    private static JsonElement[] ObjectProperties(JsonElement schema, JsonElement document, int depth)
    {
        Assert.InRange(depth, 0, 8);
        if (schema.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/components/schemas/";
            var target = reference.GetString()!;
            Assert.StartsWith(prefix, target, StringComparison.Ordinal);
            return ObjectProperties(document.GetProperty("components").GetProperty("schemas")
                .GetProperty(target[prefix.Length..]), document, depth + 1);
        }
        if (schema.TryGetProperty("properties", out var properties)) return [properties];
        foreach (var composition in new[] { "anyOf", "oneOf", "allOf" })
        {
            if (schema.TryGetProperty(composition, out var alternatives))
                return alternatives.EnumerateArray().SelectMany(value => ObjectProperties(value, document, depth + 1)).ToArray();
        }
        return [];
    }

    private static string[] ScalarTypes(JsonElement schema, JsonElement document, int depth)
    {
        Assert.InRange(depth, 0, 8);
        if (schema.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/components/schemas/";
            var target = reference.GetString()!;
            Assert.StartsWith(prefix, target, StringComparison.Ordinal);
            return ScalarTypes(document.GetProperty("components").GetProperty("schemas")
                .GetProperty(target[prefix.Length..]), document, depth + 1);
        }
        if (schema.TryGetProperty("type", out var type))
            return type.ValueKind == JsonValueKind.String ? [type.GetString()!]
                : type.EnumerateArray().Select(value => value.GetString()!).ToArray();
        // Nullable scalars can use schema composition rather than a type union.
        foreach (var composition in new[] { "anyOf", "oneOf", "allOf" })
        {
            if (schema.TryGetProperty(composition, out var alternatives))
                return alternatives.EnumerateArray().SelectMany(value => ScalarTypes(value, document, depth + 1)).ToArray();
        }
        Assert.Fail("Existing scalar schema has no type, local reference or composition: " + schema.GetRawText());
        return [];
    }

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
    public async Task DevelopmentOpenApi_PreservesOriginalApiOnlyXmlDocumentationScope()
    {
        await using var factory = new DocumentMetadataFactory();
        using var response = await factory.CreateClient().GetAsync("/documents/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var schema in document.GetProperty("components").GetProperty("schemas").EnumerateObject())
        {
            if (schema.Value.TryGetProperty("description", out var description))
            {
                Assert.DoesNotContain(description.GetString(), new[]
                {
                    "Invoice Model.", "Quotation.", "Receipt Model.", "Purchase Order.",
                });
            }
            if (schema.Value.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (property.Value.TryGetProperty("description", out var propertyDescription))
                    {
                        Assert.False(propertyDescription.GetString()?.StartsWith("Gets or sets ", StringComparison.Ordinal) == true,
                            "Original API-only XML registration must not import Domain property comments: " + schema.Name + "." + property.Name);
                    }
                }
            }
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
