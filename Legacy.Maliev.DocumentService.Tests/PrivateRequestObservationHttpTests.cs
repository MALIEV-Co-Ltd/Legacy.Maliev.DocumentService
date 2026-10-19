using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Legacy.Maliev.DocumentService.Tests;

// Marker-only actual normal host proof; no PDF rendering or external readiness acceptance.
[Collection("Renderer artifacts")]
public sealed class PrivateRequestObservationHttpTests
{
    private const string InstanceHeader = "X-Maliev-Health-Instance";
    private const string NonceHeader = "X-Maliev-Diagnostic-Id";
    private const string DiagnosticPath = "/internal/diagnostics/observability";

    [Fact]
    public async Task ProductionHealth_UsesOneHostIdentityOnlyOnRegisteredGetEndpoints()
    {
        await using var first = new ObservationHost();
        using var client = first.OpenClient();
        Assert.True(first.Services.GetRequiredService<IHostEnvironment>().IsProduction());
        Assert.IsNotType<TestServer>(first.Services.GetRequiredService<IServer>());
        string? identity = null;
        foreach (var path in new[] { "/documents/liveness", "/documents/readiness", "/documents/liveness" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var marker = Assert.Single(response.Headers.GetValues(InstanceHeader));
            Assert.True(Guid.TryParseExact(marker, "N", out _));
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.False(response.Headers.Contains(NonceHeader));
            identity ??= marker;
            Assert.Equal(identity, marker);
        }
        foreach (var entry in new[]
        {
            (Method: "POST", Path: "/documents/liveness"),
            (Method: "HEAD", Path: "/documents/liveness"),
            (Method: "POST", Path: "/documents/readiness"),
            (Method: "GET", Path: "/documents/aspire-liveness"),
            (Method: "GET", Path: "/documents/liveness/extra"),
            (Method: "GET", Path: "/liveness"),
            (Method: "GET", Path: "/pdfs/liveness"),
            (Method: "GET", Path: "/pdfs/readiness"),
            (Method: "GET", Path: "/not-a-health-endpoint"),
        })
        {
            using var request = new HttpRequestMessage(new HttpMethod(entry.Method), entry.Path);
            using var response = await client.SendAsync(request);
            Assert.False(response.Headers.Contains(InstanceHeader));
            Assert.False(response.Headers.Contains(NonceHeader));
        }
        await using var other = new ObservationHost();
        using var otherClient = other.OpenClient();
        using var independent = await otherClient.GetAsync("/documents/liveness");
        Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        var otherIdentity = Assert.Single(independent.Headers.GetValues(InstanceHeader));
        Assert.True(Guid.TryParseExact(otherIdentity, "N", out _));
        Assert.NotEqual(identity, otherIdentity);
        Assert.Empty(first.Records.Snapshot);
        Assert.Empty(other.Records.Snapshot);
        Assert.False(first.Records.Overflowed);
        Assert.False(other.Records.Overflowed);
    }

    [Fact]
    public async Task ProductionDiagnostic_RequiresDirectLoopbackSingleStrictNonceAndRetainsThrottle()
    {
        await using var host = new ObservationHost();
        using var client = host.OpenClient();
        using var health = await client.GetAsync("/documents/liveness");
        var identity = Assert.Single(health.Headers.GetValues(InstanceHeader));
        foreach (var nonce in new string?[] { null, "invalid", Guid.NewGuid().ToString("D") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, DiagnosticPath);
            if (nonce is not null) request.Headers.Add(NonceHeader, nonce);
            using var response = await client.SendAsync(request);
            AssertDenied(response);
        }
        using (var duplicate = new HttpRequestMessage(HttpMethod.Get, DiagnosticPath))
        {
            duplicate.Headers.Add(NonceHeader, new[] { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") });
            using var response = await client.SendAsync(duplicate);
            AssertDenied(response);
        }
        using (var post = new HttpRequestMessage(HttpMethod.Post, DiagnosticPath))
        {
            post.Headers.Add(NonceHeader, Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(post);
            AssertDenied(response);
        }
        Assert.Empty(host.Records.Snapshot);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var nonce = Guid.NewGuid().ToString("N");
            using var request = new HttpRequestMessage(HttpMethod.Get, DiagnosticPath);
            request.Headers.Add(NonceHeader, nonce);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(nonce, Assert.Single(response.Headers.GetValues(NonceHeader)));
            Assert.Equal(identity, Assert.Single(response.Headers.GetValues(InstanceHeader)));
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("noindex", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
            using var repeated = new HttpRequestMessage(HttpMethod.Get, DiagnosticPath);
            repeated.Headers.Add(NonceHeader, Guid.NewGuid().ToString("N"));
            using var rejected = await client.SendAsync(repeated);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.False(rejected.Headers.Contains(InstanceHeader));
            Assert.False(rejected.Headers.Contains(NonceHeader));
            host.Clock.Advance(TimeSpan.FromMinutes(1));
        }
        Assert.Equal(2, host.Records.Snapshot.Count(value => value == ObservationEvent.Probe));
        Assert.Equal(2, host.Records.Snapshot.Count(value => value == ObservationEvent.Unhandled));
        Assert.False(host.Records.Overflowed);

        // Only this negative transport-peer case uses TestServer. Positive identity/probe proof above uses real Kestrel.
        await using var spoof = new ObservationHost(kestrel: false);
        using var spoofClient = spoof.OpenClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        foreach (var peer in new[] { IPAddress.Parse("203.0.113.7"), null })
        {
            var denied = await spoof.Server.SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = peer;
                context.Request.Method = "GET";
                context.Request.Path = DiagnosticPath;
                context.Request.Headers[NonceHeader] = Guid.NewGuid().ToString("N");
                context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
            }, deadline.Token);
            Assert.Equal(StatusCodes.Status404NotFound, denied.Response.StatusCode);
            Assert.False(denied.Response.Headers.ContainsKey(InstanceHeader));
            Assert.False(denied.Response.Headers.ContainsKey(NonceHeader));
        }
        Assert.Empty(spoof.Records.Snapshot);
        Assert.False(spoof.Records.Overflowed);
    }

    [Fact]
    public async Task ProductionOrdinaryFailures_PreserveAuthDenialAndSanitizedErrorResponses()
    {
        await using var host = new ObservationHost();
        using var client = host.OpenClient();
        // No token or PDF body is sent; authorization must reject before the renderer is invoked.
        using var unauthorized = await client.PostAsync("/Pdfs/quotation", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.False(unauthorized.Headers.Contains(InstanceHeader));
        using var returned = await client.GetAsync("/__fixture/document-observation/returned");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, returned.StatusCode);
        using var returnedJson = JsonDocument.Parse(await returned.Content.ReadAsStringAsync());
        Assert.Equal("fixture-returned-failure", returnedJson.RootElement.GetProperty("error").GetString());
        Assert.False(returned.Headers.Contains(InstanceHeader));
        using var thrown = await client.GetAsync("/__fixture/document-observation/throw");
        Assert.Equal(HttpStatusCode.InternalServerError, thrown.StatusCode);
        Assert.Equal("application/json", thrown.Content.Headers.ContentType?.MediaType);
        var body = await thrown.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("An internal server error occurred", problem.RootElement.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, problem.RootElement.GetProperty("details").ValueKind);
        Assert.DoesNotContain("fixture-private-error-sentinel", body, StringComparison.Ordinal);
        Assert.False(thrown.Headers.Contains(InstanceHeader));
        Assert.False(thrown.Headers.Contains(NonceHeader));
        Assert.Single(host.Records.Snapshot, value => value == ObservationEvent.Handled);
        Assert.Single(host.Records.Snapshot, value => value == ObservationEvent.Unhandled);
        Assert.DoesNotContain(ObservationEvent.Probe, host.Records.Snapshot);
        Assert.False(host.Records.Overflowed);
    }

    private static void AssertDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains(InstanceHeader));
        Assert.False(response.Headers.Contains(NonceHeader));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("noindex", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }

    private sealed class ObservationHost : WebApplicationFactory<Program>
    {
        private readonly RSA signing = RSA.Create(2048);
        public ManualClock Clock { get; } = new();
        public EnumRecorder Records { get; } = new();

        public ObservationHost(bool kestrel = true)
        {
            try { if (kestrel) UseKestrel(options => options.Listen(IPAddress.Loopback, 0)); }
            catch { signing.Dispose(); throw; }
        }

        public HttpClient OpenClient()
        {
            StartServer();
            var server = Services.GetRequiredService<IServer>();
            var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };
            if (server is not TestServer)
            {
                var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
                    ?? throw new InvalidOperationException("Kestrel did not publish a bound address.");
                var address = new Uri(Assert.Single(addresses), UriKind.Absolute);
                Assert.Equal(Uri.UriSchemeHttp, address.Scheme);
                Assert.True(IPAddress.TryParse(address.Host, out var peer)
                    && peer is not null && IPAddress.IsLoopback(peer));
                Assert.InRange(address.Port, 1, 65535);
                Assert.Equal("/", address.AbsolutePath);
                Assert.Empty(address.UserInfo);
                Assert.Empty(address.Query);
                Assert.Empty(address.Fragment);
                options.BaseAddress = address;
            }
            var client = CreateClient(options);
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("CORS:AllowedOrigins", "https://localhost");
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://document-observation.invalid",
                ["Jwt:Audience"] = "document-observation-fixture",
                ["Jwt:PublicKey"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(signing.ExportSubjectPublicKeyInfoPem())),
                ["DocumentHost:TransportPolicy"] = "InternalHttpWithTrustedEdgeHttps",
                ["ForwardedHeaders:KnownProxies:0"] = "203.0.113.7",
                ["Observability:TracingEnabled"] = "false",
                ["Observability:RuntimeMetricsEnabled"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            };
            foreach (var setting in settings) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureLogging(logging => logging.AddProvider(Records));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.Configure<HostOptions>(options =>
                {
                    options.StartupTimeout = TimeSpan.FromSeconds(20);
                    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
                });
                services.AddControllers().AddApplicationPart(typeof(DocumentObservationFailureController).Assembly);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            try { await base.DisposeAsync(); }
            finally { signing.Dispose(); }
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }

    private enum ObservationEvent { Probe, Unhandled, Handled, HealthFailure }

    private sealed class EnumRecorder : ILoggerProvider
    {
        private readonly ConcurrentQueue<ObservationEvent> values = new();
        private int count;
        public ObservationEvent[] Snapshot => values.ToArray();
        public bool Overflowed => Volatile.Read(ref count) > 16;
        public ILogger CreateLogger(string categoryName) => new EnumLogger(this);
        public void Dispose() { }
        private void Record<TState>(TState state)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> properties) return;
            var name = properties.FirstOrDefault(property => property.Key == "EventName").Value as string;
            ObservationEvent? value = name switch
            {
                "ObservabilityPipelineProbe" => ObservationEvent.Probe,
                "UnhandledRequestFailure" => ObservationEvent.Unhandled,
                "HandledOperationFailure" => ObservationEvent.Handled,
                "HealthProbeFailure" => ObservationEvent.HealthFailure,
                _ => null
            };
            if (value is not null && Interlocked.Increment(ref count) <= 16) values.Enqueue(value.Value);
        }
        private sealed class EnumLogger(EnumRecorder owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Record(state);
        }
    }
}

// Test-only application part; no new endpoint is registered by the production application.
[ApiController]
[AllowAnonymous]
[Route("/__fixture/document-observation")]
public sealed class DocumentObservationFailureController : ControllerBase
{
    [HttpGet("returned")]
    public IActionResult Returned() => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "fixture-returned-failure" });
    [HttpGet("throw")]
    public IActionResult Throw() => throw new Exception("fixture-private-error-sentinel");
}
