using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;

namespace Legacy.Maliev.DocumentService.Rendering.Documents;

internal static class QuotationDocumentComposer
{
    internal static byte[] Render(QuotationDocument quotation, byte[] logo) => Create(quotation, logo).GeneratePdf();

    internal static IDocument Create(QuotationDocument quotation, byte[] logo)
    {
        var items = (quotation.Orders ?? []).Select((item, index) =>
        {
            var price = $"{DocumentFormat.Money(item.UnitPrice)} {DocumentFormat.Safe(quotation.Currency)}";
            if (item.Discount is not null and not 0)
            {
                var discountPerUnit = item.UnitPrice * item.Discount.Value / 100m;
                price += $"\n-{DocumentFormat.Money(discountPerUnit)} {DocumentFormat.Safe(quotation.Currency)}/UNIT";
                price += $"\nDiscount: {item.Discount:N2}%";
            }

            return new BusinessDocumentItem(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                DocumentFormat.Lines(item.Name, item.Description),
                price,
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                $"{DocumentFormat.Money(item.Subtotal)} {DocumentFormat.Safe(quotation.Currency)}");
        }).ToArray();

        var spec = new BusinessDocumentSpec(
            "QUOTATION",
            "ใบเสนอราคา",
            DocumentFormat.Identifier(quotation.Id),
            logo,
            24,
            [
                ("QUOTATION No. / เลขที่ใบเสนอราคา", DocumentFormat.Identifier(quotation.Id)),
                ("CUSTOMER No. / รหัสลูกค้า", DocumentFormat.Identifier(quotation.Customer?.Id)),
                ("PERIOD / ระยะเวลา", DocumentFormat.DurationDays(quotation.Period)),
                ("VALID UNTIL / ใช้ได้ถึง", DocumentFormat.Date(quotation.ExpirationDate)),
            ],
            [
                new("Prepared for / จัดทำให้", QuestDocumentRenderer.QuotationCustomer(quotation)),
                new("Prepared by / จัดทำโดย", DocumentFormat.Lines(
                    quotation.Employee?.FullName,
                    quotation.Employee?.Email,
                    DocumentFormat.Prefix("Mobile: ", quotation.Employee?.Mobile))),
            ],
            "Quotation details / รายละเอียดใบเสนอราคา",
            [
                ("INVOICE NUMBER / เลขที่ใบแจ้งหนี้", quotation.InvoiceNumber),
                ("SHIPPING / วิธีจัดส่ง", quotation.ShippedVia),
                ("FOB / เงื่อนไขส่งมอบ", quotation.Fob),
                ("TERMS / เงื่อนไขชำระเงิน", quotation.Terms),
            ],
            items,
            "Payment is due in full before production unless otherwise agreed. / กรุณาชำระเงินเต็มจำนวนก่อนเริ่มการผลิต เว้นแต่มีข้อตกลงอื่น",
            [
                ("Subtotal / ยอดรวมก่อนภาษี", quotation.Subtotal),
                ("VAT 7% / ภาษีมูลค่าเพิ่ม 7%", quotation.Vat),
                ("Grand Total / ยอดรวมสุทธิ", quotation.Total),
                ("Withholding Tax / ภาษีหัก ณ ที่จ่าย", quotation.WithholdingTax is null ? null : -quotation.WithholdingTax),
                ("Quoted Amount / ยอดชำระตามใบเสนอราคา", quotation.QuotedAmount),
            ],
            quotation.Currency,
            "Remark / หมายเหตุ:",
            quotation.Comment,
            container => QuestDocumentRenderer.QuotationFooter(container, quotation.CreatedDate));

        return Document.Create(document => ModernBusinessDocumentComposer.ComposePageSet(document, spec));
    }
}
