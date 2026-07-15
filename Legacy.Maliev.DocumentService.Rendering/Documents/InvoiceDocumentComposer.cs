using System.Globalization;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class InvoiceDocumentComposer
{
    internal static byte[] Render(InvoiceDocument invoice, byte[] logo)
    {
        var items = (invoice.OrderItems ?? []).Select((item, index) => new BusinessDocumentItem(
            (index + 1).ToString(CultureInfo.InvariantCulture),
            DocumentFormat.Safe(item.Description),
            $"{DocumentFormat.Money(item.UnitPrice)} {DocumentFormat.Safe(invoice.Currency)}",
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            $"{DocumentFormat.Money(item.Subtotal)} {DocumentFormat.Safe(invoice.Currency)}")).ToArray();

        var spec = new BusinessDocumentSpec(
            "INVOICE",
            "ใบวางบิล | ใบแจ้งหนี้",
            invoice.Number,
            logo,
            24,
            [
                ("INVOICE No. / เลขที่ใบแจ้งหนี้", invoice.Number),
                ("CUSTOMER No. / รหัสลูกค้า", DocumentFormat.Identifier(invoice.CustomerId)),
                ("DATE / วันที่", DocumentFormat.Date(invoice.CreatedDate)),
                ("CURRENCY / สกุลเงิน", invoice.Currency),
            ],
            [
                new("Billing / วางบิล", QuestDocumentRenderer.InvoiceBilling(invoice)),
                new("Shipping / จัดส่ง", QuestDocumentRenderer.InvoiceShipping(invoice)),
            ],
            null,
            [
                ("SALESPERSON / พนักงานขาย", invoice.SalesPerson),
                ("P.O. NUMBER / เลขที่ใบสั่งซื้อ", invoice.PurchaseOrderNumber),
                ("SHIPPED VIA / วิธีจัดส่ง", invoice.ShippedVia),
                ("FOB / เงื่อนไขส่งมอบ", invoice.Fob),
                ("TERMS / เงื่อนไขชำระเงิน", invoice.Terms),
            ],
            items,
            "Late payments accrue interest at 1.25% per month. / การชำระล่าช้ามีดอกเบี้ย 1.25% ต่อเดือน",
            [
                ("Subtotal / ยอดรวมก่อนภาษี", invoice.Subtotal),
                ("VAT 7% / ภาษีมูลค่าเพิ่ม 7%", invoice.Vat),
                ("Grand Total / ยอดรวมสุทธิ", invoice.Total),
                ("Withholding Tax / ภาษีหัก ณ ที่จ่าย", invoice.WithholdingTax),
                ("Outstanding / ยอดค้างชำระ", invoice.Outstanding),
            ],
            invoice.Currency,
            "Remark / หมายเหตุ:",
            invoice.Remark,
            QuestDocumentRenderer.InvoiceFooter);

        return ModernBusinessDocumentComposer.Render(spec);
    }
}
