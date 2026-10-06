using Legacy.Maliev.DocumentService.Domain.Invoice;
using Legacy.Maliev.DocumentService.Domain.OrderLabel;
using Legacy.Maliev.DocumentService.Domain.PurchaseOrder;
using Legacy.Maliev.DocumentService.Domain.Quotations;
using Legacy.Maliev.DocumentService.Domain.Receipt;

namespace Legacy.Maliev.DocumentService.Application;

#if CONCRETE
public sealed class IDocumentRenderer
{
    public byte[] RenderInvoice(Invoice invoice) => new byte[0];
    public byte[] RenderPurchaseOrder(PurchaseOrder purchaseOrder) => new byte[0];
    public byte[] RenderQuotation(Quotation quotation) => new byte[0];
    public byte[] RenderReceipt(Receipt receipt) => new byte[0];
    public byte[] RenderOrderLabel(OrderLabel orderLabel) => new byte[0];
}
#else
public interface IDocumentRenderer
{
#if DEFAULT_METHOD
    byte[] RenderInvoice(Invoice invoice) => new byte[0];
#else
    byte[] RenderInvoice(Invoice invoice);
#endif
    byte[] RenderPurchaseOrder(PurchaseOrder purchaseOrder);
    byte[] RenderQuotation(Quotation quotation);
    byte[] RenderReceipt(Receipt receipt);
    byte[] RenderOrderLabel(OrderLabel orderLabel);
#if STATIC_METHOD
    static byte[] Extra(Invoice invoice) => new byte[0];
#endif
}
#endif

#if EXTRA_TYPE
public sealed class AdditionalType
{
}
#endif
