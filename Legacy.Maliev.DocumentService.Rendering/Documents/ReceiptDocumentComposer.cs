using System.Globalization;
using QuestPDF.Fluent;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class ReceiptDocumentComposer
{
    internal static byte[] Render(ReceiptDocument receipt, byte[] logo)
    {
        var items = (receipt.OrderItems ?? []).Select((item, index) => new BusinessDocumentItem(
            (index + 1).ToString(CultureInfo.InvariantCulture),
            DocumentFormat.Safe(item.Description),
            $"{DocumentFormat.Money(item.UnitPrice)} {DocumentFormat.Safe(receipt.Currency)}",
            item.Quantity.ToString(CultureInfo.InvariantCulture),
            $"{DocumentFormat.Money(item.Subtotal)} {DocumentFormat.Safe(receipt.Currency)}")).ToArray();

        return Document.Create(document =>
        {
            ModernBusinessDocumentComposer.ComposePageSet(document, Spec(receipt, logo, items, "ORIGINAL / ต้นฉบับ", includeSignature: false));
            ModernBusinessDocumentComposer.ComposePageSet(document, Spec(receipt, logo, items, "COPY / สำเนา", includeSignature: true));
        }).GeneratePdf();
    }

    private static BusinessDocumentSpec Spec(
        ReceiptDocument receipt,
        byte[] logo,
        IReadOnlyList<BusinessDocumentItem> items,
        string copy,
        bool includeSignature) => new(
            "TAX INVOICE | RECEIPT",
            "ใบกำกับภาษี | ใบเสร็จรับเงิน",
            copy,
            logo,
            24,
            [
                ("INVOICE No. / เลขที่ใบแจ้งหนี้", receipt.InvoiceNumber),
                ("RECEIPT No. / เลขที่ใบเสร็จรับเงิน", DocumentFormat.Identifier(receipt.Id)),
                ("CUSTOMER No. / รหัสลูกค้า", DocumentFormat.Identifier(receipt.CustomerId)),
                ("PAYMENT DATE / วันที่ชำระเงิน", DocumentFormat.Date(receipt.PaymentDate)),
            ],
            [
                new("Customer / ลูกค้า", QuestDocumentRenderer.ReceiptCustomer(receipt)),
            ],
            null,
            [],
            items,
            "Cheque payments become valid after bank clearance. / การชำระด้วยเช็คสมบูรณ์เมื่อธนาคารเรียกเก็บเงินแล้ว",
            [
                ("Subtotal / ยอดรวมก่อนภาษี", receipt.Subtotal),
                ("VAT 7% / ภาษีมูลค่าเพิ่ม 7%", receipt.Vat),
                ("Grand Total / ยอดรวมสุทธิ", receipt.Total),
                ("Withholding Tax / ภาษีหัก ณ ที่จ่าย", receipt.WithholdingTax is null ? null : -receipt.WithholdingTax),
                ("Amount Received / จำนวนเงินที่ได้รับ", receipt.AmountPaid),
            ],
            receipt.Currency,
            "Remark / หมายเหตุ:",
            receipt.Remark,
            container => QuestDocumentRenderer.ReceiptFooter(container, receipt, includeSignature));
}
