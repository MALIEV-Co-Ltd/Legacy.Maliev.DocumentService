using System.Globalization;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class PurchaseOrderDocumentComposer
{
    internal static byte[] Render(PurchaseOrderDocument order, byte[] logo)
    {
        var sourceItems = order.OrderItems ?? [];
        var currency = sourceItems.Select(item => item.Currency).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var subtotal = sourceItems.Sum(item => item.Quantity * item.UnitPrice);
        var vat = subtotal * 0.07m;
        var items = sourceItems.Select(item => new BusinessDocumentItem(
            string.IsNullOrWhiteSpace(item.PartNumber) ? "-" : item.PartNumber,
            DocumentFormat.Safe(item.Description),
            $"{DocumentFormat.Money(item.UnitPrice)} {DocumentFormat.Safe(item.Currency)}",
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            $"{DocumentFormat.Money(item.Quantity * item.UnitPrice)} {DocumentFormat.Safe(item.Currency)}")).ToArray();

        var spec = new BusinessDocumentSpec(
            "PURCHASE ORDER",
            "ใบสั่งของ | ใบสั่งซื้อ",
            DocumentFormat.Identifier(order.ReferenceNumber),
            logo,
            24,
            [
                ("NUMBER / เลขที่ใบสั่งซื้อ", DocumentFormat.Identifier(order.ReferenceNumber)),
                ("DATE / วันที่", DocumentFormat.Date(order.Date)),
                ("CURRENCY / สกุลเงิน", currency),
                ("ITEMS / จำนวนรายการ", sourceItems.Count.ToString(CultureInfo.InvariantCulture)),
            ],
            [
                new("Supplier / ผู้ขาย", QuestDocumentRenderer.Company(order.Supplier)),
                new("Billing / วางบิล", QuestDocumentRenderer.Company(order.Billing)),
                new("Shipping / จัดส่ง", QuestDocumentRenderer.Company(order.Shipping)),
            ],
            "Purchase order details / รายละเอียดใบสั่งซื้อ",
            [
                ("ORDERED BY / ผู้สั่งซื้อ", order.OrderedBy),
                ("SHIPPED VIA / วิธีจัดส่ง", order.ShippedVia),
                ("FOB / เงื่อนไขส่งมอบ", order.FOB),
                ("TERMS / เงื่อนไขชำระเงิน", order.Terms),
            ],
            items,
            null,
            [
                ("Subtotal / ยอดรวมก่อนภาษี", subtotal),
                ("VAT 7% / ภาษีมูลค่าเพิ่ม 7%", vat),
                ("Grand Total / ยอดรวมสุทธิ", subtotal + vat),
            ],
            currency,
            "Notes / หมายเหตุ:",
            order.Notes,
            container => QuestDocumentRenderer.PurchaseOrderFooter(container, order.Date));

        return ModernBusinessDocumentComposer.Render(spec);
    }
}
