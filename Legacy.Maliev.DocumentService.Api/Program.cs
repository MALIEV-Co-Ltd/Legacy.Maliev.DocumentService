using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Rendering;
using Maliev.Aspire.ServiceDefaults;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddStandardCors();
builder.AddJwtAuthentication();
builder.AddStandardMiddleware(options => options.EnableRequestLogging = true);
builder.AddStandardOpenApi(
    title: "Legacy MALIEV Document Service API",
    description: "Authenticated .NET 10 compatibility API for rendering legacy MALIEV documents with QuestPDF.");
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

public partial class Program;