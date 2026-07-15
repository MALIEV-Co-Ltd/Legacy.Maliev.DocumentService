using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using Legacy.Maliev.DocumentService.Rendering.Components;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class FoldingMarkParityTests
{
    [Fact]
    public void A4Documents_UseExactOneThirdFoldCoordinates()
    {
        var current = new QuestDocumentRenderer().RenderInvoice(new InvoiceDocument
        {
            Number = "INV-FOLD",
            CreatedDate = new DateTime(2026, 7, 15),
            Currency = "THB",
        });
        using var currentDocument = PdfDocument.Open(current);
        var page = currentDocument.GetPage(1);
        Assert.Equal(842, page.Height, precision: 0);
        Assert.Equal(page.Height / 3d, A4Page.FirstFoldFromTop, precision: 3);
        Assert.Equal(page.Height * 2d / 3d, A4Page.SecondFoldFromTop, precision: 3);
        Assert.DoesNotContain("_", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FoldIndicators_UseReservedLeftGutterInsideThePrintableArea()
    {
        Assert.Equal(A4Page.PrintableLeftEdge, Millimetres(14), precision: 3);
        Assert.Equal(A4Page.ContentLeftMargin, Millimetres(18), precision: 3);
        Assert.Equal(A4Page.ContentRightMargin, Millimetres(14), precision: 3);
        Assert.Equal(A4Page.FoldIndicatorWidth, Millimetres(3), precision: 3);

        Assert.True(A4Page.FoldIndicatorLeft >= A4Page.PrintableLeftEdge);
        Assert.True(
            A4Page.FoldIndicatorLeft + A4Page.FoldIndicatorWidth < A4Page.ContentLeftMargin,
            "Fold indicators must remain inside the printable area without overlapping document text.");
    }

    private static double Millimetres(double value) => value * 72d / 25.4d;
}
