using Legacy.Maliev.DocumentService.Api.Authorization;
using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Domain.Invoice;
using Legacy.Maliev.DocumentService.Domain.OrderLabel;
using Legacy.Maliev.DocumentService.Domain.PurchaseOrder;
using Legacy.Maliev.DocumentService.Domain.Quotations;
using Legacy.Maliev.DocumentService.Domain.Receipt;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace Legacy.Maliev.DocumentService.Api.Controllers;

[ApiController, Route("[controller]"), Authorize]
public sealed class PdfsController(IDocumentRenderer renderer) : ControllerBase
{
    [HttpPost("invoice"), RequirePermission(DocumentPermissions.Render, RequireLiveCheck = true)]
    [Produces(MediaTypeNames.Application.Pdf)]
    public ActionResult CreateInvoiceAsync([FromBody] Invoice? item) =>
        item is null ? BadRequest() : File(renderer.RenderInvoice(item), MediaTypeNames.Application.Pdf);

    [HttpPost("purchaseorder"), RequirePermission(DocumentPermissions.Render, RequireLiveCheck = true)]
    [Produces(MediaTypeNames.Application.Pdf)]
    public ActionResult CreatePurchaseOrderAsync([FromBody] PurchaseOrder? item) =>
        item is null ? BadRequest() : File(renderer.RenderPurchaseOrder(item), MediaTypeNames.Application.Pdf);

    [HttpPost("quotation"), RequirePermission(DocumentPermissions.Render, RequireLiveCheck = true)]
    [Produces(MediaTypeNames.Application.Pdf)]
    public ActionResult CreateQuotationAsync([FromBody] Quotation? item) =>
        item is null ? BadRequest() : File(renderer.RenderQuotation(item), MediaTypeNames.Application.Pdf);

    [HttpPost("receipt"), RequirePermission(DocumentPermissions.Render, RequireLiveCheck = true)]
    [Produces(MediaTypeNames.Application.Pdf)]
    public ActionResult CreateReceiptAsync([FromBody] Receipt? item) =>
        item is null ? BadRequest() : File(renderer.RenderReceipt(item), MediaTypeNames.Application.Pdf);

    [HttpPost("orderlabel"), RequirePermission(DocumentPermissions.Render, RequireLiveCheck = true)]
    [Produces(MediaTypeNames.Application.Pdf)]
    public ActionResult CreateOrderLabelAsync([FromBody] OrderLabel? item) =>
        item is null ? BadRequest() : File(renderer.RenderOrderLabel(item), MediaTypeNames.Application.Pdf);
}