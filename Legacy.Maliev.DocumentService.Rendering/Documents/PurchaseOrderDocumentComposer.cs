using Legacy.Maliev.DocumentService.Rendering.Components;
using QuestPDF.Fluent;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class PurchaseOrderDocumentComposer
{
    internal static byte[] Render(PurchaseOrderDocument order, byte[] logo) => Document.Create(document =>
        document.Page(page =>
        {
            A4Page.Configure(page, 30);
            page.Header().Element(container => DocumentHeader.Compose(
                container,
                logo,
                "PURCHASE ORDER",
                "ใบสั่งของ | ใบสั่งซื้อ",
                reference: null));
            page.Content().Element(container => QuestDocumentRenderer.PurchaseOrderContent(container, order));
            page.Footer().Element(container => QuestDocumentRenderer.PurchaseOrderFooter(container, order.Date));
        })).GeneratePdf();
}
