using System.Net;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentOrderLabelLiteralHttpTests
{
    [Theory]
    [InlineData("Id")]
    [InlineData("Name")]
    [InlineData("Process")]
    [InlineData("Material")]
    [InlineData("Color")]
    [InlineData("SurfaceFinish")]
    [InlineData("Description")]
    public async Task CallerLeadingBlankLine_ReachesActualPdfLayoutWithoutTrimming(string field)
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var unpadded = await MarkerPositionAsync(client, field, "LITERAL-MARKER", deadline.Token);
        var padded = await MarkerPositionAsync(client, field, "\nLITERAL-MARKER", deadline.Token);
        // The legacy Paragraph receives the leading blank line literally. Because
        // the label content is rotated, compare baseline displacement in both axes.
        var displacement = Math.Sqrt(Math.Pow(padded.X - unpadded.X, 2) + Math.Pow(padded.Y - unpadded.Y, 2));
        Assert.True(displacement >= 5, $"Caller blank line was lost for {field}; displacement={displacement}.");
    }

    [Fact]
    public async Task NullTextFields_RetainExistingBlankCellRendering()
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var content = new StringContent("{\"Id\":null,\"Name\":null,\"Process\":null,\"Material\":null,\"Color\":null,\"SurfaceFinish\":null,\"Description\":null}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/orderlabel", content, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(deadline.Token));
        Assert.Equal(1, pdf.NumberOfPages);
        var text = pdf.GetPage(1).Text;
        foreach (var label in new[] { "ORDER", "NAME", "PROCESS", "MATERIAL", "QUANTITY", "COLOR", "POST", "DESCRIPTION" })
        {
            Assert.Contains(label, text, StringComparison.Ordinal);
        }
    }

    private static async Task<(double X, double Y)> MarkerPositionAsync(HttpClient client, string field, string value, CancellationToken token)
    {
        // Literal JSON exercises the existing controller binder and actual renderer.
        using var content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, string> { [field] = value }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/orderlabel", content, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(1, pdf.NumberOfPages);
        var page = pdf.GetPage(1);
        Assert.InRange(page.Width, 215.5, 216.5);
        Assert.InRange(page.Height, 287.5, 288.5);
        var marker = Assert.Single(NearestNeighbourWordExtractor.Instance.GetWords(page.Letters), word => word.Text == "LITERAL-MARKER");
        return (marker.Letters[0].StartBaseLine.X, marker.Letters[0].StartBaseLine.Y);
    }
}
