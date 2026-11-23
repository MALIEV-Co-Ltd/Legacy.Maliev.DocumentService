using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Legacy.Maliev.DocumentService.Api.Controllers;
using Legacy.Maliev.DocumentService.Api.Serialization;
using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Domain.OrderLabel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IdentityModel.Tokens;

using Xunit.Abstractions;

namespace Legacy.Maliev.DocumentService.Tests;

// Isolated net10 stock oracle with MVC 10.0.12 +
// Newtonsoft 13.0.3. This is not an assertion about an emitted net8 deployment.
public sealed class DocumentOrderLabelInputContractTests(ITestOutputHelper output)
{
    private static readonly string[] TextFields =
        ["Id", "Name", "Process", "Material", "Color", "SurfaceFinish", "Description"];
    private static readonly string[] QuantityFields =
        ["OrderQuantity", "ManufactureQuantity", "RemainingQuantity"];

    public static IEnumerable<object[]> Bodies()
    {
        foreach (var field in TextFields)
        {
            foreach (var token in new[] { "null", "\"\"", "\"plain ทดสอบ\"", "\" \\nkept\\t \"", "7", "-7", "1.25", "1e2", "true", "false", "{}", "[]", "[\"x\"]" })
                yield return [field + ":" + token, "{\"" + field + "\":" + token + "}"];
            yield return [field + ":duplicate-null", "{\"" + field + "\":\"kept\",\"" + field + "\":null}"];
            yield return [field + ":case-duplicate", "{\"" + field + "\":\"first\",\"" + field.ToLowerInvariant() + "\":null,\"" + field.ToUpperInvariant() + "\":\"last\"}"];
        }
        foreach (var field in QuantityFields)
        {
            foreach (var token in new[] { "null", "0", "-1", "2147483647", "-2147483648", "2147483648", "-2147483649", "7.0", "7.5", "1e2", "\"7\"", "\" 7 \"", "\"\"", "\"1.5\"", "true", "false", "{}", "[]" })
                yield return [field + ":" + token, "{\"" + field + "\":" + token + "}"];
            yield return [field + ":duplicate-null", "{\"" + field + "\":7,\"" + field + "\":null}"];
            yield return [field + ":case-duplicate", "{\"" + field + "\":1,\"" + field.ToLowerInvariant() + "\":null,\"" + field.ToUpperInvariant() + "\":2}"];
        }
        foreach (var body in new[]
        {
            "", " ", "null", "true", "7", "\"text\"", "[]", "{}", "{", "{\"Name\":",
            "/*leading*/{\"Name\":\"kept\"}", "{Name:'kept'}",
            "{\"Name\":\"kept\",}", "{\"Name\":\"kept\"} {\"Name\":\"other\"}",
            "{\"$id\":\"1\",\"Name\":\"kept\"}",
            "{\"$type\":\"Unknown.Type, Unknown\",\"Name\":\"kept\"}",
            "{\"Unknown\":{\"nested\":[1,null,{}]},\"Name\":\"kept\"}",
            "{\"Name\":\"2026-10-18T01:02:03Z\"}",
            "{\"Id\":7,\"Name\":true,\"OrderQuantity\":\"3\",\"ManufactureQuantity\":null,\"RemainingQuantity\":2,\"Process\":\" P \",\"Material\":\"ไทย\",\"Color\":null,\"SurfaceFinish\":\"\",\"Description\":\"\\nline\"}"
        }) yield return ["root:" + body, body];
        foreach (var depth in new[] { 31, 32, 33, 63, 64, 65 })
            yield return ["depth:" + depth, "{\"Unknown\":" + new string('[', depth) + "0" + new string(']', depth) + ",\"Name\":\"kept\"}"];
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task CompleteInputFamily_MatchesIndependentOriginalShape(string caseName, string body)
    {
        Assert.NotNull(caseName);
        var expected = await ReadStockAsync(body);
        var actual = await ReadAsync(Candidate(), typeof(OrderLabel), body);
        var native = await ReadAsync(new SystemTextJsonInputFormatter(new JsonOptions(),
            NullLogger<SystemTextJsonInputFormatter>.Instance), typeof(OrderLabel), body);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { caseName, expected, native, actual }));
        Assert.Equal(caseName is "depth:32" or "depth:33" or "depth:63" or "depth:64" or "depth:65",
            expected.ReaderRecoveryFailed);
        Assert.Equal(expected.HasError, actual.HasError);
        Assert.Equal(expected.IsModelSet, actual.IsModelSet);
        Assert.Equal(expected.Json, actual.Json);
    }

    [Theory]
    [InlineData("{\"Name\":7}", "\"Name\":\"7\"")]
    [InlineData("{\"Name\":\"kept\",\"Name\":null}", "\"Name\":\"kept\"")]
    [InlineData("{\"OrderQuantity\":7,\"OrderQuantity\":null}", "\"OrderQuantity\":7")]
    public async Task KnownSourceDifferences_AreNotHiddenByStockTargetReader(string body, string expected)
    {
        var original = await ReadStockAsync(body);
        var candidate = await ReadAsync(Candidate(), typeof(OrderLabel), body);
        var native = await ReadAsync(new SystemTextJsonInputFormatter(new JsonOptions(),
            NullLogger<SystemTextJsonInputFormatter>.Instance), typeof(OrderLabel), body);
        Assert.False(original.HasError);
        Assert.Contains(expected, original.Json!, StringComparison.Ordinal);
        Assert.Equal(original, candidate);
        Assert.True(native.HasError || native.Json != original.Json);
    }

    [Theory]
    [InlineData("application/json; charset=utf-8", false)]
    [InlineData("text/json; charset=utf-16", true)]
    [InlineData("application/vnd.label+json; charset=utf-8", false)]
    [InlineData("application/json; charset=caller-private-charset", false)]
    public async Task StockCharsetSelection_IsRetainedAndPrivate(string contentType, bool utf16)
    {
        var expected = await ReadStockAsync("{\"Name\":7}", contentType, utf16);
        var actual = await ReadAsync(Candidate(), typeof(OrderLabel), "{\"Name\":7}", contentType, utf16);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatterSelection_RequiresExactModelAndExactAction()
    {
        var formatter = Candidate();
        using var body = new MemoryStream();
        var owned = Context(typeof(OrderLabel), body);
        Assert.True(formatter.CanRead(owned));
        owned.HttpContext.SetEndpoint(null);
        Assert.False(formatter.CanRead(owned));
        var other = Context(typeof(OrderLabel), body, nameof(PdfsController.CreateInvoiceAsync));
        Assert.False(formatter.CanRead(other));
        // Metadata-only isolation, no financial request or renderer invocation.
        var wrongModel = Context(typeof(string), body);
        Assert.False(formatter.CanRead(wrongModel));
    }

    [Fact]
    public async Task OwnedBuffer_RestoresCallerStreamAndPosition()
    {
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"kept\"}"));
        var context = Context(typeof(OrderLabel), body);
        var original = context.HttpContext.Request.Body;
        var result = await Candidate().ReadAsync(context);
        Assert.False(result.HasError);
        Assert.Same(original, context.HttpContext.Request.Body);
        Assert.Equal(0, original.Position);
        Assert.True(original.CanRead);
    }

    [Theory]
    [InlineData("plain", "/Pdfs/orderlabel")]
    [InlineData("primitive", "/pdfs/orderlabel/")]
    [InlineData("duplicates", "/Pdfs/orderlabel")]
    public async Task ActualProgram_BindsAllTenFieldsBeforeCaptureOnlyRenderer(string scenario, string route)
    {
        await using var factory = new InputFactory();
        using var client = factory.Client(true);
        var body = scenario switch
        {
            "primitive" => "{\"Id\":7,\"Name\":true,\"OrderQuantity\":\"3\",\"ManufactureQuantity\":1,\"RemainingQuantity\":2,\"Process\":\"P\",\"Material\":\"ไทย\",\"Color\":\"C\",\"SurfaceFinish\":\"S\",\"Description\":\"D\"}",
            "duplicates" => "{\"Id\":\"I\",\"Name\":\"N\",\"Name\":null,\"OrderQuantity\":3,\"OrderQuantity\":null,\"ManufactureQuantity\":1,\"RemainingQuantity\":2,\"Process\":\"P\",\"Material\":\"M\",\"Color\":\"C\",\"SurfaceFinish\":\"S\",\"Description\":\"D\"}",
            _ => "{\"id\":\"I\",\"name\":\"N\",\"orderQuantity\":3,\"manufactureQuantity\":1,\"remainingQuantity\":2,\"process\":\"P\",\"material\":\"M\",\"color\":\"C\",\"surfaceFinish\":\"S\",\"description\":\"D\"}"
        };
        var expected = await ReadStockAsync(body);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(route, content, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CaptureRenderer.Bytes, await response.Content.ReadAsByteArrayAsync(deadline.Token));
        Assert.Equal(1, factory.Capture.Calls);
        Assert.Equal(expected.Json, System.Text.Json.JsonSerializer.Serialize(factory.Capture.Last));
    }

    [Theory]
    [InlineData("{\"OrderQuantity\":\"PRIVATE-LABEL-INPUT\"}", "application/json", 400)]
    [InlineData("{", "application/json", 400)]
    [InlineData("null", "application/json", 400)]
    [InlineData("", "application/json", 400)]
    [InlineData("{}", "text/plain", 415)]
    [InlineData("{}", "application/json; charset=PRIVATE-LABEL-INPUT", 415)]
    public async Task InvalidBodies_ArePrivateAndNeverReachRenderer(string body, string contentType, int expectedStatus)
    {
        await using var factory = new InputFactory();
        using var client = factory.Client(true);
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await client.PostAsync("/Pdfs/orderlabel", content, deadline.Token);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(0, factory.Capture.Calls);
        Assert.DoesNotContain("PRIVATE-LABEL-INPUT", await response.Content.ReadAsStringAsync(deadline.Token), StringComparison.Ordinal);
    }

    [Fact]
    public Task OverDepthBody_IsRedactedAndNeverReachesRenderer()
    {
        var body = "{\"PRIVATE-LABEL-INPUT\":" + new string('[', 32) + "0" + new string(']', 32) + "}";
        return InvalidBodies_ArePrivateAndNeverReachRenderer(body, "application/json", 400);
    }

    [Theory]
    [InlineData(false, false, "{}")]
    [InlineData(false, false, "{")]
    [InlineData(true, false, "{}")]
    [InlineData(true, false, "{")]
    public async Task ActualAuthenticationAndPermission_PrecedeBodyAdmission(bool signed, bool permission, string body)
    {
        await using var factory = new InputFactory();
        using var client = factory.Client(permission, signed);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await client.PostAsync("/Pdfs/orderlabel", content, deadline.Token);
        Assert.Equal(signed ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Capture.Calls);
    }


    [Fact]
    public async Task Cancellation_RestoresOwnedRequestBody()
    {
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"kept\"}"));
        var context = Context(typeof(OrderLabel), body);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.HttpContext.RequestAborted = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Candidate().ReadAsync(context));
        Assert.Same(body, context.HttpContext.Request.Body);
        Assert.Equal(0, body.Position);
        Assert.True(body.CanRead);
    }

    [Fact]
    public async Task EffectiveRequestLimit_CannotBeWidenedByOwnedBuffer()
    {
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"" + new string('x', 256) + "\"}"));
        var context = Context(typeof(OrderLabel), body);
        context.HttpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(new RequestLimit(32));
        await Assert.ThrowsAsync<IOException>(() => Candidate().ReadAsync(context));
        Assert.Same(body, context.HttpContext.Request.Body);
        Assert.Equal(0, body.Position);
        Assert.True(body.CanRead);
    }

    [Fact]
    public void Registration_IsIdempotentAndLeavesOutputAndDomainShapeIntact()
    {
        var options = new MvcOptions();
        var setup = new OrderLabelOriginalBodyMvcOptionsSetup(NullLoggerFactory.Instance, new DefaultObjectPoolProvider());
        setup.Configure(options);
        setup.Configure(options);
        Assert.Single(options.InputFormatters);
        Assert.Empty(options.OutputFormatters);
        Assert.Equal(InputFormatterExceptionPolicy.MalformedInputExceptions,
            ((IInputFormatterExceptionPolicy)options.InputFormatters[0]).ExceptionPolicy);
        var fields = typeof(OrderLabel).GetProperties().Select(property => (property.Name, property.PropertyType))
            .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        var original = typeof(OriginalOrderLabel).GetProperties().Select(property => (property.Name, property.PropertyType))
            .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(original, fields);
        Assert.Equal(10, fields.Length);
    }

    private sealed class RequestLimit(long? value) : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; } = value;
    }


    [Fact]
    public async Task MalformedSourceValues_AreAbsentFromFormatterLogsAndPublicState()
    {
        using var captured = new InputLogProvider();
        using var loggers = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(captured));
        var options = new MvcOptions();
        new OrderLabelOriginalBodyMvcOptionsSetup(loggers, new DefaultObjectPoolProvider()).Configure(options);
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"OrderQuantity\":\"PRIVATE-LABEL-INPUT\"}"));
        var context = Context(typeof(OrderLabel), body);
        var result = await Assert.Single(options.InputFormatters).ReadAsync(context);
        Assert.True(result.HasError);
        Assert.NotEmpty(captured.Records);
        Assert.All(captured.Records, record => Assert.DoesNotContain("PRIVATE-LABEL-INPUT", record, StringComparison.Ordinal));
        Assert.All(context.ModelState.Values.SelectMany(value => value.Errors), error =>
        {
            Assert.Null(error.Exception);
            Assert.Equal("The request body contains an invalid value.", error.ErrorMessage);
        });
    }

    [Fact]
    public async Task NonseekableBody_IsReadAsynchronouslyAndOwnedSpillIsRemoved()
    {
        InputFormatterContext? context = null;
        string? spill = null;
        using var body = new AsyncBody(Encoding.UTF8.GetBytes("{\"Name\":\"" + new string('x', 100_000) + "\"}"), () =>
        {
            if (context?.HttpContext.Request.Body is FileBufferingReadStream buffer)
                spill ??= buffer.TempFileName;
        });
        context = Context(typeof(OrderLabel), body);
        var result = await Candidate().ReadAsync(context);
        Assert.False(result.HasError);
        Assert.Equal(new string('x', 100_000), Assert.IsType<OrderLabel>(result.Model).Name);
        Assert.True(body.ReadCalls > 1);
        Assert.NotNull(spill);
        Assert.False(File.Exists(spill));
        Assert.Same(body, context.HttpContext.Request.Body);
        Assert.False(body.WasDisposed);
    }

    [Fact]
    public async Task CancellationDuringAsyncDrain_RestoresCallerOwnedBody()
    {
        using var cancellation = new CancellationTokenSource();
        using var body = new AsyncBody(Encoding.UTF8.GetBytes("{\"Name\":\"kept\"}"), cancellation.Cancel);
        var context = Context(typeof(OrderLabel), body);
        context.HttpContext.RequestAborted = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Candidate().ReadAsync(context));
        Assert.True(body.ReadCalls > 0);
        Assert.Same(body, context.HttpContext.Request.Body);
        Assert.False(body.WasDisposed);
    }

    private sealed class AsyncBody(byte[] bytes, Action beforeRead) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public int ReadCalls { get; private set; }
        public bool WasDisposed { get; private set; }
        public override bool CanRead => !WasDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous caller-body read.");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            beforeRead();
            cancellationToken.ThrowIfCancellationRequested();
            return await inner.ReadAsync(buffer[..Math.Min(buffer.Length, 4096)], cancellationToken);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class InputLogProvider : ILoggerProvider
    {
        public List<string> Records { get; } = [];
        public ILogger CreateLogger(string categoryName) => new InputLogger(Records);
        public void Dispose() { }

        private sealed class InputLogger(List<string> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                records.Add(formatter(state, exception) + "|" + exception?.ToString());
        }
    }

    private sealed record Readback(bool HasError, bool IsModelSet, string? Json, bool ReaderRecoveryFailed = false);

    private static async Task<Readback> ReadAsync(IInputFormatter formatter, Type model, string text,
        string contentType = "application/json; charset=utf-8", bool utf16 = false)
    {
        using var body = new MemoryStream((utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes(text));
        var context = Context(model, body);
        context.HttpContext.Request.ContentType = contentType;
        var result = await formatter.ReadAsync(context);
        return new(result.HasError, result.IsModelSet,
            result.HasError || !result.IsModelSet ? null : System.Text.Json.JsonSerializer.Serialize(result.Model, model));
    }

    private static InputFormatterContext Context(Type model, Stream body,
        string action = nameof(PdfsController.CreateOrderLabelAsync))
    {
        var http = new DefaultHttpContext();
        http.Request.Body = body;
        http.Request.ContentType = "application/json; charset=utf-8";
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(PdfsController).GetTypeInfo(),
            MethodInfo = typeof(PdfsController).GetMethod(action)!,
            ActionName = action
        };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(descriptor), "OrderLabel input test"));
        return new InputFormatterContext(http, "item", new ModelStateDictionary(),
            new EmptyModelMetadataProvider().GetMetadataForType(model),
            (stream, encoding) => new StreamReader(stream, encoding, true, 1024, true));
    }

    private static IInputFormatter Candidate()
    {
        var options = new MvcOptions();
        new OrderLabelOriginalBodyMvcOptionsSetup(NullLoggerFactory.Instance, new DefaultObjectPoolProvider()).Configure(options);
        return Assert.Single(options.InputFormatters);
    }

    private static readonly SemaphoreSlim OracleGate = new(1, 1);

    private static async Task<Readback> ReadStockAsync(string body,
        string contentType = "application/json; charset=utf-8", bool utf16 = false)
    {
        if (!await OracleGate.WaitAsync(TimeSpan.FromMinutes(10)))
            throw new TimeoutException("Timed out waiting for the isolated formatter oracle.");
        try
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var oracle = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tools/DocumentOrderLabelOracle/bin", configuration, "net10.0/DocumentOrderLabelOracle.dll"));
            var root = Environment.GetEnvironmentVariable("DOCUMENT_ORACLE_DOTNET_ROOT") ?? Environment.GetEnvironmentVariable("DOTNET_ROOT");
            var host = root is null ? "dotnet" : Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            var start = new ProcessStartInfo(host) { RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(oracle);
            start.Environment["DOTNET_ROLL_FORWARD"] = "LatestPatch";
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated formatter oracle.");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                process.StandardInput.AutoFlush = true;
                await process.StandardInput.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { Body = body, ContentType = contentType, Utf16 = utf16 }).AsMemory(), deadline.Token);
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                var errors = process.StandardError.ReadToEndAsync(deadline.Token);
                await process.WaitForExitAsync(deadline.Token);
                var result = await output;
                _ = await errors;
                if (process.ExitCode != 0) throw new InvalidOperationException("Isolated formatter oracle failed.");
                return System.Text.Json.JsonSerializer.Deserialize<Readback>(result)
                    ?? throw new InvalidOperationException("Isolated formatter oracle returned no result.");
            }
            finally
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
        }
        finally { OracleGate.Release(); }
    }
    private sealed class InputFactory : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://label-input.invalid";
        private const string Audience = "label-input";
        private readonly RSA key = RSA.Create(2048);
        public CaptureRenderer Capture { get; } = new();

        public HttpClient Client(bool permission, bool signed = true)
        {
            var client = CreateClient();
            if (!signed) return client;
            var now = DateTime.UtcNow;
            var claims = new List<Claim> { new("sub", "service:label-input"), new("identity_kind", "service") };
            if (permission) claims.Add(new("permissions", "legacy.documents.render"));
            var token = new JwtSecurityToken(Issuer, Audience, claims, now.AddMinutes(-1), now.AddMinutes(5),
                new SigningCredentials(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("CORS:AllowedOrigins:0", Issuer);
            builder.UseSetting("Jwt:PublicKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ExportSubjectPublicKeyInfoPem())));
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDocumentRenderer>();
                services.AddSingleton<IDocumentRenderer>(Capture);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            try { await base.DisposeAsync(); }
            finally { key.Dispose(); }
        }

        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) key.Dispose(); }
        }
    }

    private sealed class CaptureRenderer : IDocumentRenderer
    {
        public static byte[] Bytes => [37, 80, 68, 70, 45, 67, 65, 80, 84, 85, 82, 69];
        public int Calls { get; private set; }
        public OrderLabel? Last { get; private set; }
        public byte[] RenderOrderLabel(OrderLabel value) { Calls++; Last = value; return Bytes; }
        public byte[] RenderInvoice(Legacy.Maliev.DocumentService.Domain.Invoice.Invoice value) => throw new InvalidOperationException("Unowned renderer call.");
        public byte[] RenderPurchaseOrder(Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder value) => throw new InvalidOperationException("Unowned renderer call.");
        public byte[] RenderQuotation(Legacy.Maliev.DocumentService.Domain.Quotations.Quotation value) => throw new InvalidOperationException("Unowned renderer call.");
        public byte[] RenderReceipt(Legacy.Maliev.DocumentService.Domain.Receipt.Receipt value) => throw new InvalidOperationException("Unowned renderer call.");
    }

#nullable disable
    // Independently transcribed original 7ef4d7ee... public scalar declarations.
    private sealed class OriginalOrderLabel
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
#nullable restore
}
