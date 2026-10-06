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

/// <summary>Controller.</summary>
/// <param name="renderer">The document renderer.</param>
[ApiController, Route("[controller]"), Authorize]
public sealed class PdfsController(IDocumentRenderer renderer) : ControllerBase
{
    /// <summary>Create the invoice PDF.</summary>
    /// <remarks>Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.</remarks>
    /// <param name="item" example="{&quot;Remark&quot;:&quot;EXAMPLE-INVOICE&quot;}">The item.</param>
    /// <response code="200">The rendered PDF document.</response>
    /// <response code="400">The JSON body is missing or invalid.</response>
    /// <returns>The PDF response or a bad request for missing input.</returns>
    [HttpPost("invoice"), RequirePermission(DocumentPermissions.Render)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult CreateInvoiceAsync([FromBody] Invoice? item) =>
        item is null ? BadRequest() : File(renderer.RenderInvoice(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the purchase order PDF.</summary>
    /// <remarks>Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.</remarks>
    /// <param name="item" example="{&quot;Notes&quot;:&quot;EXAMPLE-PURCHASE-ORDER&quot;}">The item.</param>
    /// <response code="200">The rendered PDF document.</response>
    /// <response code="400">The JSON body is missing or invalid.</response>
    /// <returns>The PDF response or a bad request for missing input.</returns>
    [HttpPost("purchaseorder"), RequirePermission(DocumentPermissions.Render)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult CreatePurchaseOrderAsync([FromBody] PurchaseOrder? item) =>
        item is null ? BadRequest() : File(renderer.RenderPurchaseOrder(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the quotation PDF.</summary>
    /// <remarks>Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.</remarks>
    /// <param name="item" example="{&quot;Comment&quot;:&quot;EXAMPLE-QUOTATION&quot;}">The item.</param>
    /// <response code="200">The rendered PDF document.</response>
    /// <response code="400">The JSON body is missing or invalid.</response>
    /// <returns>The PDF response or a bad request for missing input.</returns>
    [HttpPost("quotation"), RequirePermission(DocumentPermissions.Render)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult CreateQuotationAsync([FromBody] Quotation? item) =>
        item is null ? BadRequest() : File(renderer.RenderQuotation(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the receipt PDF.</summary>
    /// <remarks>Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.</remarks>
    /// <param name="item" example="{&quot;Id&quot;:89,&quot;InvoiceNumber&quot;:&quot;EXAMPLE-RECEIPT&quot;,&quot;Currency&quot;:&quot;THB&quot;,&quot;AmountPaid&quot;:5.99,&quot;WithholdingTax&quot;:null,&quot;Remark&quot;:&quot;EXAMPLE-RECEIPT&quot;}">The item.</param>
    /// <response code="200">The rendered PDF document.</response>
    /// <response code="400">The JSON body is missing or invalid.</response>
    /// <returns>The PDF response or a bad request for missing input.</returns>
    [HttpPost("receipt"), RequirePermission(DocumentPermissions.Render)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult CreateReceiptAsync([FromBody] Receipt? item) =>
        item is null ? BadRequest() : File(renderer.RenderReceipt(item), MediaTypeNames.Application.Pdf);

    /// <summary>Create the order label PDF.</summary>
    /// <remarks>Requires the legacy.documents.render permission. Send PascalCase JSON to receive an application/pdf document.</remarks>
    /// <param name="item" example="{&quot;Name&quot;:&quot;EXAMPLE-ORDER-LABEL&quot;}">The item.</param>
    /// <response code="200">The rendered PDF document.</response>
    /// <response code="400">The JSON body is missing or invalid.</response>
    /// <returns>The PDF response or a bad request for missing input.</returns>
    [HttpPost("orderlabel"), RequirePermission(DocumentPermissions.Render)]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult CreateOrderLabelAsync([FromBody] OrderLabel? item) =>
        item is null ? BadRequest() : File(renderer.RenderOrderLabel(item), MediaTypeNames.Application.Pdf);
}
