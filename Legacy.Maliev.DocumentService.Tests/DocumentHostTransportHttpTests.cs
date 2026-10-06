using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Legacy.Maliev.DocumentService.Api.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

// Actual normal Program, forwarding/HSTS/CORS/auth, and unchanged singleton QuestPDF renderer.
// TestServer controls transport metadata; these are not live ingress/TLS proofs.
[Collection("Renderer artifacts")]
public sealed class DocumentHostTransportHttpTests
{
    private const string Proxy = "192.0.2.40";
    private const string Caller = "198.51.100.90";
    private const string ApiPath = "/Pdfs/quotation";

    [Theory]
    [InlineData("/documents/liveness", false, 200)]
    [InlineData("/documents/readiness", false, 200)]
    [InlineData(ApiPath, true, 200)]
    public async Task InternalHttp_PreservesProbeAndRealBearerAdmission(string path, bool bearer, int status)
    {
        await using var factory = Factory();
        var context = await SendAsync(factory, path, Caller, bearer: bearer);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Headers["Strict-Transport-Security"].Count);
        Assert.Equal(0, context.Response.Headers.Location.Count);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task DirectHttps_UsesProductionHstsWithoutRedirect(string environment, bool hsts)
    {
        await using var factory = Factory(environment: environment);
        var context = await SendAsync(factory, ApiPath, Caller, scheme: "https", bearer: true);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(hsts ? "max-age=2592000" : "", context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal(0, context.Response.Headers.Location.Count);
    }

    [Theory]
    [InlineData(Proxy)]
    [InlineData("::ffff:192.0.2.40")]
    public async Task TrustedEdgeHttps_UsesActualForwarderAndRealBearerAdmission(string remote)
    {
        await using var factory = Factory();
        var context = await SendAsync(factory, ApiPath, remote, forwardedScheme: "https", bearer: true);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse(Caller), context.Connection.RemoteIpAddress);
        Assert.Equal("max-age=2592000", context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal(0, context.Request.Headers["X-Document-Original-Scheme"].Count);
    }

    [Theory]
    [InlineData("http")]
    [InlineData(null)]
    [InlineData("not a scheme")]
    [InlineData("https,http")]
    public async Task TrustedEdge_InsecureOrUnprovenSchemeRejectsBeforeAuthentication(string? forwardedScheme)
    {
        await using var factory = Factory();
        var context = await SendAsync(factory, ApiPath, Proxy, forwardedScheme: forwardedScheme);
        Assert.Equal(426, context.Response.StatusCode);
        Assert.NotEqual("application/pdf", context.Response.ContentType);
        Assert.Equal(0, context.Response.Headers.WWWAuthenticate.Count);
        Assert.Equal(0, context.Response.Headers.Location.Count);
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(true, 403)]
    public async Task TrustedEdgeHttps_PreservesAnonymousAndWrongGrantDenial(bool signedWrongGrant, int status)
    {
        await using var factory = Factory();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var context = await factory.Server.SendAsync(context =>
        {
            ConfigureRequest(context, ApiPath, Proxy, "http", "https", false, factory);
            if (signedWrongGrant) context.Request.Headers.Authorization = "Bearer " + factory.Token(includePermission: false);
        }, deadline.Token);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal("max-age=2592000", context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal(0, context.Response.Headers.Location.Count);
        Assert.NotEqual("application/pdf", context.Response.ContentType);
    }

    [Theory]
    [InlineData(Caller, true)]
    [InlineData(Proxy, false)]
    [InlineData(null, true)]
    public async Task UntrustedOrUnconfiguredPeer_CannotSpoofForwardedHttpsOrOriginalMarker(string? remote, bool configuredProxy)
    {
        await using var factory = Factory(configuredProxy: configuredProxy);
        var context = await SendAsync(factory, ApiPath, remote, forwardedScheme: "https", bearer: true, spoofOriginal: true);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(remote is null ? null : IPAddress.Parse(remote), context.Connection.RemoteIpAddress);
        Assert.Equal(0, context.Response.Headers["Strict-Transport-Security"].Count);
        Assert.Equal(0, context.Response.Headers.Location.Count);
        Assert.Equal(0, context.Request.Headers["X-Document-Original-Scheme"].Count);
        Assert.Equal(0, context.Request.Headers["X-Original-Proto"].Count);
        if (!configuredProxy)
        {
            var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
            Assert.Empty(options.KnownProxies);
            Assert.Empty(options.KnownIPNetworks);
            Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
        }
    }

    [Theory]
    [InlineData("/documents/liveness")]
    [InlineData("/documents/readiness")]
    [InlineData("/documents/aspire-liveness")]
    public async Task RegisteredProbes_RemainReachableThroughTrustedHttp(string path)
    {
        await using var factory = Factory();
        var context = await SendAsync(factory, path, Proxy, forwardedScheme: "http");
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Headers.Location.Count);
        Assert.Equal(0, context.Response.Headers["Strict-Transport-Security"].Count);
    }

    [Theory]
    [InlineData("https://allowed.example.invalid", true)]
    [InlineData("https://unlisted.example.invalid", false)]
    public async Task ExplicitCorsPreflight_CompletesBeforeAuthenticationAtTrustedHttpsEdge(string origin, bool allowed)
    {
        await using var factory = Factory();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var context = await factory.Server.SendAsync(context =>
        {
            ConfigureRequest(context, ApiPath, Proxy, "http", "https", false, factory);
            context.Request.Method = "OPTIONS";
            context.Request.Headers.Origin = origin;
            context.Request.Headers["Access-Control-Request-Method"] = "POST";
            context.Request.Headers["Access-Control-Request-Headers"] = "authorization,content-type";
        }, deadline.Token);
        Assert.Equal(204, context.Response.StatusCode);
        Assert.Equal(allowed ? origin : "", context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Equal(0, context.Response.Headers.WWWAuthenticate.Count);
        Assert.Equal("max-age=2592000", context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task UnsupportedTransportPolicy_FailsNormalStartup()
    {
        await using var factory = Factory(policy: "trust-everything");
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    private DocumentHostFactory Factory(bool configuredProxy = true, string environment = "Production",
        string policy = "InternalHttpWithTrustedEdgeHttps") => new(configuredProxy, environment, policy);

    private static async Task<HttpContext> SendAsync(DocumentHostFactory factory, string path, string? remote,
        string scheme = "http", string? forwardedScheme = null, bool bearer = false, bool spoofOriginal = false)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"Comment\":\"HOST-POLICY ทดสอบ\"}"));
        using var output = new MemoryStream();
        var response = await factory.Server.SendAsync(context =>
        {
            ConfigureRequest(context, path, remote, scheme, forwardedScheme, bearer, factory);
            context.Response.Body = output;
            if (path == ApiPath)
            {
                context.Request.Body = body;
                context.Request.ContentLength = body.Length;
            }
            if (spoofOriginal)
            {
                context.Request.Headers["X-Document-Original-Scheme"] = "http";
                context.Request.Headers["X-Original-Proto"] = "https";
            }
        }, deadline.Token);
        if (path == ApiPath && bearer)
        {
            Assert.Equal("application/pdf", response.Response.ContentType);
            var bytes = output.ToArray();
            Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
            using var pdf = PdfDocument.Open(bytes);
            var text = string.Join(' ', pdf.GetPages().Select(page => page.Text));
            Assert.Contains("HOST-POLICY", text, StringComparison.Ordinal);
            Assert.Contains("ทดสอบ", text, StringComparison.Ordinal);
        }
        return response;
    }

    private static void ConfigureRequest(HttpContext context, string path, string? remote, string scheme,
        string? forwardedScheme, bool bearer, DocumentHostFactory factory)
    {
        context.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);
        context.Request.Method = path == ApiPath ? "POST" : "GET";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = 0;
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString("document.example.invalid");
        context.Request.Path = path;
        if (forwardedScheme is not null)
        {
            context.Request.Headers["X-Forwarded-Proto"] = forwardedScheme;
            context.Request.Headers["X-Forwarded-For"] = Caller;
        }
        if (bearer) context.Request.Headers.Authorization = "Bearer " + factory.Token();
    }

    private sealed class DocumentHostFactory(bool configuredProxy, string environment, string policy)
        : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://document-host-policy-fixture.invalid";
        private const string Audience = "document-host-policy";
        private readonly RSA signingKey = RSA.Create(2048);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:PublicKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(signingKey.ExportSubjectPublicKeyInfoPem())),
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["CORS:AllowedOrigins:0"] = "https://allowed.example.invalid",
                ["ForwardedHeaders:KnownProxies:0"] = configuredProxy ? Proxy : "",
                ["DocumentHost:TransportPolicy"] = policy,
            };
            foreach (var setting in settings) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        }

        public string Token(bool includePermission = true)
        {
            var now = DateTime.UtcNow;
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience,
                [new Claim(JwtRegisteredClaimNames.Sub, "document-host-policy-fixture"), new Claim("permissions", includePermission ? DocumentPermissions.Render : "unrelated.fixture")],
                now.AddMinutes(-1), now.AddMinutes(5), new SigningCredentials(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256)));
        }

        protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
            var host = builder.Build();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                host.StartAsync(timeout.Token).GetAwaiter().GetResult();
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) signingKey.Dispose(); }
        }
    }
}
