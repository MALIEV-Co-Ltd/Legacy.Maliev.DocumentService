using Legacy.Maliev.DocumentService.Rendering.Components;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class QuotationDocumentComposer
{
    internal static byte[] Render(QuotationDocument quotation, byte[] logo) => Create(quotation, logo).GeneratePdf();

    internal static IDocument Create(QuotationDocument quotation, byte[] logo) => Document.Create(document =>
        document.Page(page =>
        {
            A4Page.Configure(page, 60);
            page.Header().Element(container => DocumentHeader.Compose(
                container,
                logo,
                "QUOTATION",
                "ใบเสนอราคา",
                reference: null));
            page.Content().Element(container => QuestDocumentRenderer.QuotationContent(container, quotation));
            page.Footer().Element(container => QuestDocumentRenderer.QuotationFooter(container, quotation.CreatedDate));
        }));
}
