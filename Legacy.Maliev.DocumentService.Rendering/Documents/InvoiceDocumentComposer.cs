using Legacy.Maliev.DocumentService.Rendering.Components;
using QuestPDF.Fluent;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class InvoiceDocumentComposer
{
    internal static byte[] Render(InvoiceDocument invoice, byte[] logo)
    {
        return Document.Create(document => document.Page(page =>
        {
            A4Page.Configure(page, 54);
            page.Header().Element(container => DocumentHeader.Compose(
                container,
                logo,
                "INVOICE",
                "ใบวางบิล | ใบแจ้งหนี้",
                reference: null));
            page.Content().Element(container => QuestDocumentRenderer.InvoiceContent(container, invoice));
            page.Footer().Element(QuestDocumentRenderer.InvoiceFooter);
        })).GeneratePdf();
    }
}
