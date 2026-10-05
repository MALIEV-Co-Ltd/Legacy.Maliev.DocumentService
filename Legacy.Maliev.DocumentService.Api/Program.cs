using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Rendering;
using Maliev.Aspire.ServiceDefaults;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddDefaultApiVersioning();
builder.AddStandardCors();
builder.AddJwtAuthentication();
builder.AddStandardMiddleware(options => options.EnableRequestLogging = true);
builder.AddStandardOpenApi(
    title: "Legacy MALIEV Document Service API",
    description: "Authenticated .NET 10 compatibility API for rendering legacy MALIEV documents with QuestPDF.");
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        };
        foreach (var path in document.Paths.Where(path => path.Key.StartsWith("/Pdfs/", StringComparison.Ordinal)))
        {
            foreach (var operation in path.Value.Operations!.Values)
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document, externalResource: null)] = [],
                    },
                ];
            }
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});
builder.Services.AddSingleton<IDocumentRenderer, QuestDocumentRenderer>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();
app.UseStandardMiddleware();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints("documents");
app.MapControllers();
app.MapApiDocumentation(servicePrefix: "documents");
await app.RunAsync();

/// <summary>Legacy Document Service entry point.</summary>
public partial class Program;
