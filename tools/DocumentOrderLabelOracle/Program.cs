using System.Buffers;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;

if (Environment.Version != new Version(10, 0, 15)
    || !typeof(DefaultHttpContext).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.StartsWith("10.0.15", StringComparison.Ordinal)
    || !typeof(NewtonsoftJsonInputFormatter).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.StartsWith("10.0.12", StringComparison.Ordinal)
    || !typeof(JsonConvert).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.StartsWith("13.0.3", StringComparison.Ordinal))
    throw new InvalidOperationException("Unexpected isolated formatter oracle identity.");
var request = System.Text.Json.JsonSerializer.Deserialize<Request>(await Console.In.ReadToEndAsync())
    ?? throw new InvalidOperationException("Missing synthetic input.");
var json = new MvcNewtonsoftJsonOptions();
json.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
json.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
var formatter = new NewtonsoftJsonInputFormatter(NullLogger.Instance, json.SerializerSettings,
    ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), new MvcOptions(), json);
using var body = new MemoryStream((request.Utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(request.Body));
var http = new DefaultHttpContext();
http.Request.Body = body;
http.Request.ContentType = request.ContentType;
var context = new InputFormatterContext(http, "item", new ModelStateDictionary(),
    new EmptyModelMetadataProvider().GetMetadataForType(typeof(OriginalOrderLabel)),
    (stream, encoding) => new StreamReader(stream, encoding, true, 1024, true));
Readback output;
try
{
    var result = await formatter.ReadAsync(context);
    output = new Readback(result.HasError, result.IsModelSet,
        result.HasError || !result.IsModelSet ? null : System.Text.Json.JsonSerializer.Serialize(result.Model, typeof(OriginalOrderLabel)));
}
catch (InvalidOperationException exception) when (
    exception.Source == "Newtonsoft.Json" &&
    exception.Message == "Current error context error is different to requested error.")
{
    output = new Readback(true, false, null, ReaderRecoveryFailed: true);
}
await Console.Out.WriteAsync(System.Text.Json.JsonSerializer.Serialize(output));

internal sealed record Request(string Body, string ContentType, bool Utf16);
internal sealed record Readback(bool HasError, bool IsModelSet, string? Json, bool ReaderRecoveryFailed = false);
#pragma warning disable CS8618
internal sealed class OriginalOrderLabel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int OrderQuantity { get; set; }
    public int ManufactureQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public string Process { get; set; }
    public string Material { get; set; }
    public string Color { get; set; }
    public string SurfaceFinish { get; set; }
    public string Description { get; set; }
}
#pragma warning restore CS8618
