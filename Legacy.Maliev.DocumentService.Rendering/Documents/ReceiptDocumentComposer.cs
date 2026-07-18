using QuestPDF.Fluent;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class ReceiptDocumentComposer
{
    internal static byte[] Render(ReceiptDocument receipt, byte[] logo) => Document.Create(document =>
    {
        QuestDocumentRenderer.ReceiptPage(document, receipt, logo, "ORIGINAL", "ต้นฉบับ", includeSignature: false);
        QuestDocumentRenderer.ReceiptPage(document, receipt, logo, "COPY", "สำเนา", includeSignature: true);
    }).GeneratePdf();
}
