using System.Reflection;
using Legacy.Maliev.DocumentService.Rendering;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class SharedComponentContractTests
{
    [Theory]
    [InlineData(null, "-")]
    [InlineData(0L, "-")]
    [InlineData(-1L, "-")]
    [InlineData(42L, "42")]
    public void DocumentFormat_UsesReadableIdentifiersForUnassignedValues(long? value, string expected)
    {
        Assert.Equal(expected, DocumentFormat.Identifier(value));
    }

    [Theory]
    [InlineData(0, "-")]
    [InlineData(30, "30 days / 30 วัน")]
    public void DocumentFormat_UsesReadableDurationsForUnassignedValues(int days, string expected)
    {
        Assert.Equal(expected, DocumentFormat.DurationDays(days));
    }

    [Fact]
    public void RenderingAssembly_ExposesFocusedInternalDocumentComponents()
    {
        var assembly = Assembly.Load("Legacy.Maliev.DocumentService.Rendering");
        var expectedTypes = new[]
        {
            "Legacy.Maliev.DocumentService.Rendering.DocumentFormat",
            "Legacy.Maliev.DocumentService.Rendering.Components.DocumentHeader",
            "Legacy.Maliev.DocumentService.Rendering.Components.MetadataTable",
            "Legacy.Maliev.DocumentService.Rendering.Components.TotalsBlock",
            "Legacy.Maliev.DocumentService.Rendering.Components.PageNumber",
            "Legacy.Maliev.DocumentService.Rendering.Documents.InvoiceDocumentComposer",
            "Legacy.Maliev.DocumentService.Rendering.Documents.QuotationDocumentComposer",
            "Legacy.Maliev.DocumentService.Rendering.Documents.ReceiptDocumentComposer",
            "Legacy.Maliev.DocumentService.Rendering.Documents.PurchaseOrderDocumentComposer",
            "Legacy.Maliev.DocumentService.Rendering.Documents.OrderLabelDocumentComposer",
        };

        Assert.All(expectedTypes, name => Assert.NotNull(assembly.GetType(name)));
    }
}
