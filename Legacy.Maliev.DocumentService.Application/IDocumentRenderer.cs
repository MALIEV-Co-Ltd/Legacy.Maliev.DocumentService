using Legacy.Maliev.DocumentService.Domain.Invoice;
using Legacy.Maliev.DocumentService.Domain.OrderLabel;
using Legacy.Maliev.DocumentService.Domain.PurchaseOrder;
using Legacy.Maliev.DocumentService.Domain.Quotations;
using Legacy.Maliev.DocumentService.Domain.Receipt;

namespace Legacy.Maliev.DocumentService.Application;

public interface IDocumentRenderer
{
    byte[] RenderInvoice(Invoice invoice);
    byte[] RenderPurchaseOrder(PurchaseOrder purchaseOrder);
    byte[] RenderQuotation(Quotation quotation);
    byte[] RenderReceipt(Receipt receipt);
    byte[] RenderOrderLabel(OrderLabel orderLabel);
}