using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Legacy.Maliev.DocumentService.Tests;

// Actual Program, signed RS256 request and embedded-font QuestPDF renderer.
// Captures logging without replacing the renderer, middleware or authorization.
public sealed class DocumentRenderFailureHttpAcceptanceTests
{
    [Theory]
    [InlineData("invoice", "Remark")]
    [InlineData("quotation", "Comment")]
    [InlineData("receipt", "Remark")]
    [InlineData("purchaseorder", "Notes")]
    [InlineData("orderlabel", "Name")]
    public async Task MissingEmbeddedFontGlyph_ReturnsOpaque500AndOneSafeIncident(string route, string field)
    {
        using var logs = new IncidentProvider();
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory { AdditionalLoggerProvider = logs };
        using var client = factory.Client();
        const string privateMarker = "PRIVATE-DOCUMENT-CUSTOMER@example.invalid";
        // A valid Unicode scalar unsupported by the embedded fonts exercises
        // the documented ThrowOnMissingTextGlyphs rendering failure policy.
        using var response = await client.PostAsJsonAsync("/pdfs/" + route + "/",
            new Dictionary<string, string> { [field] = privateMarker + "\U0010FFFF" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("An internal server error occurred", body.RootElement.GetProperty("error").GetString());
        Assert.Equal(500, body.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null);
        Assert.DoesNotContain(privateMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestPDF", text, StringComparison.Ordinal);
        Assert.False(text.StartsWith("%PDF-", StringComparison.Ordinal));

        var incident = Assert.Single(logs.Incidents);
        Assert.Equal(LogLevel.Critical, incident.Level);
        Assert.Null(incident.Exception);
        Assert.Equal(500, incident.Values["StatusCode"]);
        Assert.Equal("POST", incident.Values["Method"]);
        Assert.Equal(body.RootElement.GetProperty("traceId").GetString(), incident.Values["IncidentId"]);
        Assert.DoesNotContain(privateMarker, incident.Message, StringComparison.Ordinal);
        Assert.All(incident.Values, value => Assert.DoesNotContain(privateMarker, value.Value?.ToString() ?? "", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("invoice")]
    [InlineData("quotation")]
    [InlineData("receipt")]
    [InlineData("purchaseorder")]
    [InlineData("orderlabel")]
    public async Task MalformedJson_Returns400WithoutRenderingIncident(string route)
    {
        using var logs = new IncidentProvider();
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory { AdditionalLoggerProvider = logs };
        using var client = factory.Client();
        using var content = new StringContent("{\"Number\":", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/" + route + "/", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
        Assert.Empty(logs.Incidents);
    }

    private sealed record Incident(LogLevel Level, Exception? Exception, string Message,
        IReadOnlyDictionary<string, object?> Values);

    private sealed class IncidentProvider : ILoggerProvider
    {
        public ConcurrentQueue<Incident> Incidents { get; } = new();
        public ILogger CreateLogger(string categoryName) => new IncidentLogger(this);
        public void Dispose() { }

        private sealed class IncidentLogger(IncidentProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
                var values = fields.ToArray();
                if (!values.Any(value => value.Key == "EventName" && Equals(value.Value, "UnhandledRequestFailure"))) return;
                owner.Incidents.Enqueue(new(logLevel, exception, formatter(state, exception), values.ToDictionary(value => value.Key, value => value.Value)));
            }
        }
    }
}
