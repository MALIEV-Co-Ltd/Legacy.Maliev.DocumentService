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

/// <summary>Renders the five authenticated legacy PDF document types.</summary>
/// <param name="renderer">Stateless document renderer.</param>
[ApiController, Route("[controller]"), Authorize]
public sealed class PdfsController(IDocumentRenderer renderer) : ControllerBase
{
    /// <summary>Create the invoice PDF.</summary>
    /// <param name="item">The item.</param>
    [HttpPost("invoice"), RequirePermission(DocumentPermissions.Render)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesDefaultResponseType]
    public ActionResult CreateInvoiceAsync([FromBody] Invoice? item) =>
        item is null ? BadRequest() : File(renderer.RenderInvoice(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the purchase order PDF.</summary>
    /// <param name="item">The item.</param>
    [HttpPost("purchaseorder"), RequirePermission(DocumentPermissions.Render)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesDefaultResponseType]
    public ActionResult CreatePurchaseOrderAsync([FromBody] PurchaseOrder? item) =>
        item is null ? BadRequest() : File(renderer.RenderPurchaseOrder(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the quotation PDF.</summary>
    /// <param name="item">The item.</param>
    [HttpPost("quotation"), RequirePermission(DocumentPermissions.Render)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesDefaultResponseType]
    public ActionResult CreateQuotationAsync([FromBody] Quotation? item) =>
        item is null ? BadRequest() : File(renderer.RenderQuotation(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the receipt PDF.</summary>
    /// <param name="item">The item.</param>
    [HttpPost("receipt"), RequirePermission(DocumentPermissions.Render)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesDefaultResponseType]
    public ActionResult CreateReceiptAsync([FromBody] Receipt? item) =>
        item is null ? BadRequest() : File(renderer.RenderReceipt(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the order label PDF.</summary>
    /// <param name="item">The item.</param>
    [HttpPost("orderlabel"), RequirePermission(DocumentPermissions.Render)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesDefaultResponseType]
    public ActionResult CreateOrderLabelAsync([FromBody] OrderLabel? item) =>
        item is null ? BadRequest() : File(renderer.RenderOrderLabel(item), MediaTypeNames.Application.Pdf);
}
