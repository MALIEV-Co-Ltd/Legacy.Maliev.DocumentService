using Legacy.Maliev.DocumentService.Application;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Reflection;
using Legacy.Maliev.DocumentService.Rendering.Components;
using Legacy.Maliev.DocumentService.Rendering.Documents;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using LabelDocument = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Rendering;

public sealed class QuestDocumentRenderer : IDocumentRenderer
{
    private const string LatinFont = DocumentStyle.Latin;
    private const string LatinBoldFont = DocumentStyle.LatinBold;
    private const string ThaiFont = DocumentStyle.Thai;
    private const string ThaiBoldFont = DocumentStyle.ThaiBold;
    private const string Dark = DocumentStyle.Ink;
    private const string Grey = DocumentStyle.MutedInk;
    private const string Light = DocumentStyle.HeaderFill;
    internal static readonly byte[] Logo;
    private readonly TimeProvider timeProvider;

    static QuestDocumentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        FontManager.RegisterFontWithCustomName(LatinFont, Resource("Fonts.NotoSans-Regular.ttf"));
        FontManager.RegisterFontWithCustomName(LatinBoldFont, Resource("Fonts.NotoSans-Bold.ttf"));
        FontManager.RegisterFontWithCustomName(ThaiFont, Resource("Fonts.NotoSansThai-Regular.ttf"));
        FontManager.RegisterFontWithCustomName(ThaiBoldFont, Resource("Fonts.NotoSansThai-Bold.ttf"));
        using var logo = Resource("logo.png");
        using var memory = new MemoryStream();
        logo.CopyTo(memory);
        Logo = memory.ToArray();
    }

    public QuestDocumentRenderer() : this(TimeProvider.System)
    {
    }

    public QuestDocumentRenderer(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] RenderInvoice(InvoiceDocument invoice) => InvoiceDocumentComposer.Render(invoice, Logo);

    public byte[] RenderQuotation(QuotationDocument quotation) => QuotationDocumentComposer.Render(quotation, Logo);

    public byte[] RenderReceipt(ReceiptDocument receipt) => ReceiptDocumentComposer.Render(receipt, Logo);

    public byte[] RenderPurchaseOrder(PurchaseOrderDocument purchaseOrder) => PurchaseOrderDocumentComposer.Render(purchaseOrder, Logo);

    public byte[] RenderOrderLabel(LabelDocument label) =>
        OrderLabelDocumentComposer.Render(label, Logo, timeProvider.GetUtcNow().UtcDateTime);

    private static void ConfigureA4(PageDescriptor page, float bottomMargin = 30)
    {
        A4Page.Configure(page, bottomMargin);
    }

    private static void Header(IContainer container, string title, string thaiTitle, string? reference)
    {
        DocumentHeader.Compose(container, Logo, title, thaiTitle, reference);
    }

    private static void TitleOnlyHeader(IContainer container, string title, string thaiTitle)
    {
        container.AlignRight().Column(column =>
        {
            column.Item().AlignRight().Text(title).FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(18);
            column.Item().AlignRight().Text(thaiTitle).FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(9);
        });
    }

    private static void Footer(IContainer container)
    {
        container.BorderTop(0.5f).BorderColor(Light).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text("Maliev Co., Ltd. | www.maliev.com | info@maliev.com").FontSize(6).FontColor(Grey);
            row.AutoItem()
                .DefaultTextStyle(style => style.FontSize(6).FontColor(Grey))
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
        });
    }

    internal static void InvoiceFooter(IContainer container)
    {
        container
            .BorderTop(0.5f)
            .BorderColor(DocumentStyle.HeaderFill)
            .PaddingTop(4)
            .Column(column =>
            {
                column.Item().Row(verification =>
                {
                    verification.RelativeItem().Text(
                            "Please verify the content of this invoice before payment / กรุณาตรวจสอบความถูกต้องของข้อมูลก่อนชำระเงิน")
                        .FontFamily(LatinBoldFont, ThaiBoldFont)
                        .FontSize(5.5f);
                    verification.AutoItem().DefaultTextStyle(style => style.FontSize(5.5f)).Element(PageNumber.Compose);
                });

                column.Item().PaddingTop(7).Row(details =>
                {
                    details.RelativeItem().PaddingRight(8).Column(bank =>
                    {
                        bank.Item().Text("Siam Commercial Bank Public Company Limited").FontFamily(LatinBoldFont).FontSize(5.5f);
                        bank.Item().Text("Recipient: Maliev Co., Ltd.").FontSize(5.5f);
                        bank.Item().Text("Savings account: 417-108808-2").FontSize(5.5f);
                        bank.Item().Text("SWIFT: SICOTHBK").FontSize(5.5f);
                    });

                    details.RelativeItem().PaddingHorizontal(8).Column(thaiBank =>
                    {
                        thaiBank.Item().Text("ธนาคารไทยพาณิชย์ จำกัด (มหาชน)").FontFamily(ThaiBoldFont).FontSize(5.5f);
                        thaiBank.Item().Text("ผู้รับ: บริษัท มาลีฟ จำกัด").FontFamily(ThaiFont).FontSize(5.5f);
                        thaiBank.Item().Text("บัญชีออมทรัพย์: 417-108808-2").FontFamily(ThaiFont).FontSize(5.5f);
                    });

                    details.RelativeItem().PaddingHorizontal(8).Column(contact =>
                    {
                        contact.Item().Text("Tel.: +66(0)81-803-0404").FontSize(5.5f);
                        contact.Item().Text("Tel.: +66(0)89-895-0690").FontSize(5.5f);
                        contact.Item().Text("E-mail: info@maliev.com").FontSize(5.5f);
                        contact.Item().Text("www.maliev.com").FontSize(5.5f);
                    });

                    details.RelativeItem().PaddingLeft(8).Column(company =>
                    {
                        company.Item().Text("Maliev Co., Ltd.").FontSize(5.5f);
                        company.Item().Text("36/1 Moo 3").FontSize(5.5f);
                        company.Item().Text("Khlong Khoi, Pak Kret").FontSize(5.5f);
                        company.Item().Text("Nonthaburi 11120, Thailand").FontSize(5.5f);
                        company.Item().Text("Registration number: 0125561001573").FontSize(5.5f);
                    });
                });
            });
    }

    internal static void QuotationFooter(IContainer container, DateTime date)
    {
        container
            .BorderTop(0.5f)
            .BorderColor(DocumentStyle.HeaderFill)
            .PaddingTop(4)
            .Column(column =>
            {
                column.Item().Row(metadata =>
                {
                    metadata.RelativeItem().Text($"Quoted on: {Date(date)}").FontSize(5).FontColor(Grey);
                    metadata.AutoItem().DefaultTextStyle(style => style.FontSize(6).FontColor(Grey)).Element(PageNumber.Compose);
                });
                column.Item().PaddingTop(4).Text("The information in this document is confidential to the person to whom it is addressed and should not be disclosed to any other person. It may not be reproduced in whole, or in part, nor may any of the information contained therein be disclosed without the prior consent of the directors of MALIEV Co., Ltd. ('the Company'). A recipient may not solicit, directly or indirectly (whether through an agent or otherwise) the participation of another institution or person without the prior approval of the directors of the Company. The contents of this document have not been independently verified and they do not purport to be comprehensive, or to contain all the information that a prospective investor may need. No representation, warranty or undertaking, expressed or implied is or will be made or given and no responsibility or liability is or will be accepted by the Company or by any of its directors, employees or advisors in relation to the accuracy or completeness of this document or any other written or oral information made available in connection with the Company. Any form of reproduction, dissemination, copying, disclosure, modification, distribution and or publication of this material is strictly prohibited.").FontSize(5);
            });
    }

    internal static void ReceiptFooter(IContainer container, ReceiptDocument receipt, bool includeSignature)
    {
        container
            .BorderTop(0.5f)
            .BorderColor(DocumentStyle.HeaderFill)
            .PaddingTop(4)
            .Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Element(item => BilingualText.Compose(
                    item,
                    "Please verify the content of this receipt",
                    "กรุณาตรวจสอบความถูกต้องของข้อมูลก่อนทุกครั้ง",
                    6,
                    5,
                    bold: true));
                left.Item().PaddingTop(7).Element(item => BilingualText.Compose(
                    item,
                    "Machine printed and is valid without signature",
                    "เอกสารออกด้วยระบบอิเล็กทรอนิกส์ไม่มีการลงนามผู้รับมอบอำนาจ",
                    5,
                    4.5f));
            });

            row.ConstantItem(190).Column(right =>
            {
                right.Item().AlignRight().Text("ผู้รับเงิน: ______________________").FontSize(7);
                right.Item().Height(22).AlignRight().Element(signature =>
                {
                    if (includeSignature && receipt.Signature is { Length: > 0 })
                        signature.Width(100).Image(receipt.Signature).FitArea();
                });
                right.Item()
                    .PaddingTop(3)
                    .AlignRight()
                    .DefaultTextStyle(style => style.FontSize(6).FontColor(Grey))
                    .Element(PageNumber.Compose);
            });
        });
    }

    internal static void PurchaseOrderFooter(IContainer container, DateTime date)
    {
        container
            .BorderTop(0.5f)
            .BorderColor(DocumentStyle.HeaderFill)
            .PaddingTop(4)
            .Row(row =>
        {
            row.RelativeItem().AlignLeft().Text(Date(date)).FontSize(6).FontColor(Grey);
            row.ConstantItem(78)
                .AlignRight()
                .DefaultTextStyle(style => style.FontSize(6).FontColor(Grey))
                .Element(PageNumber.Compose);
        });
    }

    internal static void InvoiceContent(IContainer container, InvoiceDocument invoice)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.RelativeItem(65).Text(CompanyIdentity()).LineHeight(1.35f);
                row.RelativeItem(35).Column(right =>
                {
                    right.Item().Element(box => KeyValues(box,
                    ("INVOICE No.", invoice.Number),
                    ("CUSTOMER No.", invoice.CustomerId.ToString(CultureInfo.InvariantCulture)),
                    ("DATE", Date(invoice.CreatedDate))));
                    right.Item().AlignRight().Text("These information must be given for all payments and questions.").FontSize(5);
                    right.Item().AlignRight().Text("กรุณาให้ข้อมูลเหล่านี้กับเจ้าหน้าที่ทุกครั้ง").FontSize(5);
                });
            });

            column.Item().PaddingTop(5).Row(row =>
            {
                row.ConstantItem(42).Text("Billing:\nวางบิล").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(1.35f);
                row.RelativeItem().Text(InvoiceBilling(invoice)).LineHeight(1.35f);
                row.ConstantItem(42).Text("Shipping:\nจัดส่ง").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(1.35f);
                row.RelativeItem().Text(InvoiceShipping(invoice)).LineHeight(1.35f);
            });

            column.Item().PaddingTop(8).Element(box => InvoiceShippingTable(box, invoice));
            column.Item().Element(box => InvoiceTable(box, invoice));
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Late payments will accrue interest at a rate of 1.25% per month").FontSize(7);
                    left.Item().Text("หากชำระล่าช้ากว่ากำหนดหนี้ตกลงไว้ ต้องเสียดอกเบี้ย 1.25% ต่อเดือน").FontSize(7);
                });
                row.ConstantItem(190).Element(box => LegacyTotals(box, invoice.Currency,
                    ("Subtotal", invoice.Subtotal), ("VAT 7%", invoice.Vat), ("Grand Total", invoice.Total),
                    ("Withholding Tax", invoice.WithholdingTax), ("Outstanding", invoice.Outstanding)));
            });
            column.Item().PaddingTop(16).Text("Remark / หมายเหตุ:").FontFamily(LatinBoldFont, ThaiBoldFont);
            column.Item().Text(Safe(invoice.Remark));
        });
    }

    internal static void QuotationContent(IContainer container, QuotationDocument quotation)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.RelativeItem(65).Text(CompanyIdentity()).LineHeight(1.35f);
                row.RelativeItem(35).Column(right =>
                {
                    right.Item().Element(box => KeyValues(box,
                        ("QUOTATION No.", quotation.Id.ToString(CultureInfo.InvariantCulture)),
                        ("CUSTOMER No.", quotation.Customer?.Id.ToString(CultureInfo.InvariantCulture)),
                        ("PERIOD", $"{quotation.Period} days"),
                        ("VALID UNTIL", Date(quotation.ExpirationDate))));
                    right.Item().AlignRight().Text("These information must be given for all questions.").FontSize(5);
                    right.Item().AlignRight().Text("กรุณาให้ข้อมูลเหล่านี้กับเจ้าหน้าที่ทุกครั้ง").FontSize(5);
                });
            });

            column.Item().PaddingTop(15).Row(row =>
            {
                row.ConstantItem(70).Text("Prepared for:\nจัดทำให้").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(1.35f);
                row.RelativeItem().Text(QuotationCustomer(quotation)).LineHeight(1.35f);
                row.ConstantItem(70).Text("Prepared by:\nจัดทำโดย").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(1.35f);
                row.RelativeItem().Text(Lines(
                    quotation.Employee?.FullName,
                    quotation.Employee?.Email,
                    Prefix("Mobile: ", quotation.Employee?.Mobile))).LineHeight(1.35f);
            });
            column.Item().PaddingTop(28).Text("We are pleased to quote as follows / เรายินดีเสนอราคาตามรายละเอียดต่อไปนี้:");
            column.Item().Element(box => QuotationShippingTable(box, quotation));
            column.Item().Element(box => QuotationTable(box, quotation));
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Payment is to be paid in full prior to start of production unless stated otherwise.").FontSize(7);
                    left.Item().Text("หากมิได้ระบุไว้เป็นอย่างอื่น กรุณาชำระเงินเต็มจำนวนก่อนเริ่มการผลิต").FontSize(7);
                });
                row.ConstantItem(190).Element(box => LegacyTotals(box, quotation.Currency,
                    ("Subtotal", quotation.Subtotal), ("VAT 7%", quotation.Vat), ("Grand Total", quotation.Total),
                    ("Withholding Tax", quotation.WithholdingTax is null ? null : -quotation.WithholdingTax), ("Quoted Amount", quotation.QuotedAmount)));
            });
            column.Item().PaddingTop(15).Text("Remark / หมายเหตุ:").FontFamily(LatinBoldFont, ThaiBoldFont);
            column.Item().Text(Safe(quotation.Comment));
        });
    }

    internal static void ReceiptPage(IDocumentContainer document, ReceiptDocument receipt, byte[] logo, string copy, string thaiCopy, bool includeSignature)
    {
        document.Page(page =>
        {
            ConfigureA4(page, 58);
            page.Header().Row(row =>
            {
                row.ConstantItem(130).Height(38).Image(logo).FitArea();
                row.RelativeItem().AlignCenter().Column(center =>
                {
                    center.Item().AlignCenter().Text(copy).FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(9);
                    center.Item().AlignCenter().Text(thaiCopy).FontSize(8);
                });
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("TAX INVOICE | RECEIPT").FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(14);
                    right.Item().AlignRight().Text("ใบกำกับภาษี | ใบเสร็จรับเงิน").FontSize(8);
                });
            });
            page.Content().PaddingTop(6).Column(column =>
            {
                column.Spacing(5);
                column.Item().Row(row =>
                {
                    row.RelativeItem(65).Text(CompanyIdentity()).LineHeight(1.35f);
                    row.RelativeItem(35).Column(right =>
                    {
                        right.Item().Element(box => KeyValues(box,
                        ("INVOICE No.", receipt.InvoiceNumber),
                        ("RECEIPT No.", receipt.Id.ToString(CultureInfo.InvariantCulture)),
                        ("CUSTOMER No.", receipt.CustomerId?.ToString(CultureInfo.InvariantCulture)),
                        ("PAYMENT DATE", Date(receipt.PaymentDate))));
                        right.Item().AlignRight().Text("These information must be given for all payments and questions.").FontSize(5);
                        right.Item().AlignRight().Text("กรุณาให้ข้อมูลเหล่านี้กับเจ้าหน้าที่ทุกครั้ง").FontSize(5);
                    });
                });
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(42).Text("Customer:\nลูกค้า").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(1.35f);
                    row.RelativeItem().Text(ReceiptCustomer(receipt)).LineHeight(1.35f);
                });
                column.Item().PaddingTop(19).Element(box => ReceiptTable(box, receipt));
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("กรณีชำระเงินด้วยเช็ค ใบเสร็จรับเงินฉบับนี้จะสมบูรณ์ก็ต่อเมื่อเช็คได้รับการชำระเงินเรียบร้อยแล้ว").FontSize(7);
                        left.Item().Text("If payment is made by cheque, this receipt will be valid when the cheque has been honored by the bank.").FontSize(7);
                    });
                    row.ConstantItem(190).Element(box => LegacyTotals(box, receipt.Currency,
                        ("Subtotal", receipt.Subtotal), ("VAT 7%", receipt.Vat), ("Grand Total", receipt.Total),
                        ("Withholding Tax", receipt.WithholdingTax is null ? null : -receipt.WithholdingTax), ("Amount Received", receipt.AmountPaid)));
                });
                column.Item().PaddingTop(6).Text("Remark / หมายเหตุ:").FontFamily(LatinBoldFont, ThaiBoldFont);
                column.Item().Text(Safe(receipt.Remark));
            });
            page.Footer().Element(container => ReceiptFooter(container, receipt, includeSignature));
        });
    }

    internal static void PurchaseOrderContent(IContainer container, PurchaseOrderDocument order)
    {
        container.PaddingTop(70).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.RelativeItem(65).Text("MALIEV Co., Ltd.\n36/1 Moo 3\nKhlong Khoi, Pak Kret\nNonthaburi 11120, Thailand\nwww.maliev.com | email: info@maliev.com | tel: +6681-803-0404\nCourt of Registry: Department of Business Development\nCommercial Register No.: 0125561001573 (สำนักงานใหญ่)").LineHeight(1.1f);
                row.RelativeItem(35).Column(right =>
                {
                    right.Item().Border(0.75f).BorderColor(DocumentStyle.Rule).Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
                        table.Cell().PaddingHorizontal(3).Text("NUMBER");
                        table.Cell().PaddingHorizontal(3).Text(order.ReferenceNumber.ToString(CultureInfo.InvariantCulture));
                        table.Cell().PaddingHorizontal(3).Text("DATE");
                        table.Cell().PaddingHorizontal(3).Text(Date(order.Date));
                    });
                    right.Item().AlignRight().Text("These information must be given for all questions.").FontSize(5);
                    right.Item().AlignRight().Text("กรุณาให้ข้อมูลเหล่านี้กับเจ้าหน้าที่ทุกครั้ง").FontSize(5);
                });
            });

            column.Item().PaddingTop(8).Row(row =>
            {
                row.ConstantItem(42).Text("Supplier:").FontFamily(LatinBoldFont, ThaiBoldFont);
                row.RelativeItem().Text(Company(order.Supplier)).LineHeight(1.5f);
            });

            column.Item().PaddingTop(3).Row(row =>
            {
                row.ConstantItem(42).Text("Billing:\nวางบิล").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(0.9f);
                row.RelativeItem().Text(Company(order.Billing)).LineHeight(1.5f);
                row.ConstantItem(42).Text("Shipping:\nจัดส่ง").FontFamily(LatinBoldFont, ThaiBoldFont).LineHeight(0.9f);
                row.RelativeItem().Text(Company(order.Shipping)).LineHeight(1.5f);
            });

            column.Item().PaddingTop(5).Element(box => PurchaseOrderShippingTable(box, order));
            column.Item().Element(box => PurchaseOrderTable(box, order));
            column.Item().PaddingTop(8).Text("Notes").FontFamily(LatinBoldFont, ThaiBoldFont);
            column.Item().Text(Safe(order.Notes)).LineHeight(0.9f);
        });
    }

    private static void Address(IContainer container, string heading, params string?[] lines)
    {
        container.Column(column =>
        {
            column.Item().Text(heading).FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(9);
            foreach (var line in lines.Where(value => !string.IsNullOrWhiteSpace(value))) column.Item().Text(line!);
        });
    }

    private static void KeyValues(IContainer container, params (string Key, string? Value)[] values)
    {
        MetadataTable.Compose(container, values);
    }

    private static void InvoiceTable(IContainer container, InvoiceDocument invoice) => container.Table(table =>
    {
        LegacyItemColumns(table);
        LegacyItemHeader(table);
        var index = 1;
        foreach (var item in invoice.OrderItems ?? [])
        {
            LegacyItemRow(table, index++, item.Description, item.UnitPrice, item.Quantity, item.Subtotal, invoice.Currency);
        }

        if (invoice.OrderItems is null || invoice.OrderItems.Count == 0) LegacyEmptyItemRow(table);
    });

    private static void InvoiceShippingTable(IContainer container, InvoiceDocument invoice) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
        });
        foreach (var heading in new[] { "SALESPERSON", "P.O. NUMBER", "SHIPPED VIA", "FOB", "TERMS" })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Background(DocumentStyle.HeaderFill).AlignCenter().Text(heading).FontSize(7);
        foreach (var value in new[] { invoice.SalesPerson, invoice.PurchaseOrderNumber, invoice.ShippedVia, invoice.Fob, invoice.Terms })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).AlignCenter().Text(Safe(value)).FontSize(7);
    });

    private static void ReceiptTable(IContainer container, ReceiptDocument receipt) => container.Table(table =>
    {
        LegacyItemColumns(table);
        LegacyItemHeader(table);
        var index = 1;
        foreach (var item in receipt.OrderItems ?? [])
            LegacyItemRow(table, index++, item.Description, item.UnitPrice, item.Quantity, item.Subtotal, receipt.Currency);
        if (receipt.OrderItems is null || receipt.OrderItems.Count == 0) LegacyEmptyItemRow(table);
    });

    private static void QuotationTable(IContainer container, QuotationDocument quotation) => container.Table(table =>
    {
        LegacyItemColumns(table);
        LegacyItemHeader(table);
        var index = 1;
        foreach (var item in quotation.Orders ?? [])
        {
            var pricing = $"{Money(item.UnitPrice)} {Safe(quotation.Currency)}";
            if (item.Discount is not null and not 0)
            {
                var discountPerUnit = item.UnitPrice * item.Discount.Value / 100m;
                pricing += $"\n-{Money(discountPerUnit)} {Safe(quotation.Currency)}/UNIT\nDiscount: {item.Discount:N2}%";
            }
            LegacyQuotationRow(table, index++, Lines(item.Name, item.Description), pricing, item.Quantity, item.Subtotal, quotation.Currency);
        }

        if (quotation.Orders is null || quotation.Orders.Count == 0) LegacyEmptyItemRow(table);
    });

    private static void QuotationShippingTable(IContainer container, QuotationDocument quotation) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
        });
        foreach (var heading in new[] { "INVOICE NUMBER", "SHIPPING", "FOB", "TERMS" })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Background(DocumentStyle.HeaderFill).AlignCenter().Text(heading).FontSize(7);
        foreach (var value in new[] { quotation.InvoiceNumber, quotation.ShippedVia, quotation.Fob, quotation.Terms })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).AlignCenter().Text(Safe(value)).FontSize(7);
    });

    private static void LegacyQuotationRow(TableDescriptor table, int index, string description, string pricing, int quantity, decimal amount, string? currency)
    {
        foreach (var (value, alignment) in new[]
        {
            (index.ToString(CultureInfo.InvariantCulture), "center"),
            (description, "left"),
            (pricing, "right"),
            (quantity.ToString(CultureInfo.InvariantCulture), "center"),
            ($"{Money(amount)} {Safe(currency)}", "right"),
        })
        {
            var cell = table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingHorizontal(3).PaddingVertical(2).DefaultTextStyle(style => style.FontSize(7));
            if (alignment == "center") cell = cell.AlignCenter();
            if (alignment == "right") cell = cell.AlignRight();
            cell.Text(value);
        }
    }

    private static void PurchaseOrderShippingTable(IContainer container, PurchaseOrderDocument order) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
        });

        foreach (var heading in new[] { "ORDERED BY", "SHIPPED VIA", "FOB", "TERMS" })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Background(DocumentStyle.HeaderFill).AlignCenter().Text(heading).FontSize(7);

        foreach (var value in new[] { order.OrderedBy, order.ShippedVia, order.FOB, order.Terms })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(7);
    });

    private static void PurchaseOrderTable(IContainer container, PurchaseOrderDocument order) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(1.2f);
            columns.RelativeColumn(5.3f);
            columns.RelativeColumn(2f);
            columns.RelativeColumn(1.5f);
            columns.RelativeColumn(2f);
        });

        table.Header(header =>
        {
            foreach (var heading in new[] { "Item / รหัส", "Description / รายละเอียด", "Unit Price / หน่วยละ", "Quantity / จำนวน", "Amount / จำนวนเงิน" })
                header.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Background(DocumentStyle.HeaderFill).AlignCenter().Text(heading).FontSize(7);
        });

        decimal subtotal = 0;
        var currency = string.Empty;
        foreach (var item in order.OrderItems ?? [])
        {
            var amount = item.Quantity * item.UnitPrice;
            subtotal += amount;
            currency = Safe(item.Currency);
            PurchaseOrderCell(table).AlignCenter().Text(string.IsNullOrWhiteSpace(item.PartNumber) ? "-" : item.PartNumber);
            PurchaseOrderCell(table).Text(string.IsNullOrWhiteSpace(item.Description) ? "-" : item.Description);
            PurchaseOrderCell(table).AlignRight().Text($"{Money(item.UnitPrice)} {currency}");
            PurchaseOrderCell(table).AlignCenter().Text(item.Quantity.ToString(CultureInfo.InvariantCulture));
            PurchaseOrderCell(table).AlignRight().Text($"{Money(amount)} {currency}");
        }

        if (order.OrderItems is null || order.OrderItems.Count == 0)
        {
            foreach (var value in new[] { "-", "-", "-", "-", "-" })
                PurchaseOrderCell(table).AlignCenter().Text(value);
        }

        var vat = subtotal * 0.07m;
        PurchaseOrderTotal(table, "Subtotal", subtotal, currency);
        PurchaseOrderTotal(table, "VAT 7%", vat, currency);
        PurchaseOrderTotal(table, "Grand Total", subtotal + vat, currency);
    });

    private static IContainer PurchaseOrderCell(TableDescriptor table) =>
        table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingHorizontal(3).PaddingVertical(2)
            .DefaultTextStyle(style => style.FontSize(7));

    private static void PurchaseOrderTotal(TableDescriptor table, string label, decimal amount, string currency)
    {
        table.Cell().ColumnSpan(4).Border(0).PaddingTop(2).AlignRight().Text(label);
        table.Cell().Border(0).PaddingTop(2).AlignRight().Text($"{Money(amount)} {currency}");
    }

    private static void Columns(TableDescriptor table) => table.ColumnsDefinition(columns =>
    {
        columns.RelativeColumn(6);
        columns.RelativeColumn(1);
        columns.RelativeColumn(2);
        columns.RelativeColumn(2);
    });

    private static void TableHeader(TableDescriptor table, params string[] values)
    {
        foreach (var value in values)
            table.Cell().Background(Dark).Padding(4).Text(value).FontFamily(LatinBoldFont, ThaiBoldFont).FontColor(Colors.White).FontSize(7);
    }

    private static void TableRow(TableDescriptor table, string? description, int quantity, decimal unitPrice, decimal amount)
    {
        Cell(table).Text(Safe(description));
        Cell(table).AlignRight().Text(quantity.ToString(CultureInfo.InvariantCulture));
        Cell(table).AlignRight().Text(Money(unitPrice));
        Cell(table).AlignRight().Text(Money(amount));
    }

    private static IContainer Cell(TableDescriptor table) => table.Cell().BorderBottom(0.5f).BorderColor(Light).Padding(4);

    private static void Totals(IContainer container, params (string Label, decimal? Value)[] values)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); });
            foreach (var (label, value) in values)
            {
                if (value is null) continue;
                table.Cell().Padding(2).Text(label).FontFamily(LatinBoldFont, ThaiBoldFont).FontSize(7);
                table.Cell().Padding(2).AlignRight().Text(Money(value.Value));
            }
        });
    }

    private static void LegacyTotals(IContainer container, string? currency, params (string Label, decimal? Value)[] values)
    {
        TotalsBlock.Compose(container, currency, values);
    }

    private static void LegacyItemColumns(TableDescriptor table) => table.ColumnsDefinition(columns =>
    {
        columns.RelativeColumn(1);
        columns.RelativeColumn(4.7f);
        columns.RelativeColumn(1.7f);
        columns.RelativeColumn(1.3f);
        columns.RelativeColumn(1.7f);
    });

    private static void LegacyItemHeader(TableDescriptor table)
    {
        table.Header(header =>
        {
            header.Cell().ColumnSpan(5).AlignRight().Text("Errors & Omissions Excepted / ผิด ตก ยกเว้น").FontSize(5);
            foreach (var heading in new[] { "Item / รหัส", "Description / รายละเอียด", "Unit Price / หน่วยละ", "Quantity / จำนวน", "Amount / จำนวนเงิน" })
                header.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Background(DocumentStyle.HeaderFill).AlignCenter().Text(heading).FontSize(7);
        });
    }

    private static void LegacyItemRow(TableDescriptor table, int index, string? description, decimal unitPrice, int quantity, decimal amount, string? currency)
    {
        foreach (var (value, alignment) in new[]
        {
            (index.ToString(CultureInfo.InvariantCulture), "center"),
            (Safe(description), "left"),
            ($"{Money(unitPrice)} {Safe(currency)}", "right"),
            (quantity.ToString(CultureInfo.InvariantCulture), "center"),
            ($"{Money(amount)} {Safe(currency)}", "right"),
        })
        {
            var cell = table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).PaddingHorizontal(3).PaddingVertical(2).DefaultTextStyle(style => style.FontSize(7));
            if (alignment == "center") cell = cell.AlignCenter();
            if (alignment == "right") cell = cell.AlignRight();
            cell.Text(value);
        }
    }

    private static void LegacyEmptyItemRow(TableDescriptor table)
    {
        foreach (var value in new[] { "1", "-", "-", "-", "-" })
            table.Cell().Border(0.75f).BorderColor(DocumentStyle.Rule).Padding(3).AlignCenter().Text(value).FontSize(7);
    }

    internal static string CompanyIdentity() =>
        "MALIEV Co., Ltd.\n36/1 Moo 3\nKhlong Khoi, Pak Kret\nNonthaburi 11120, Thailand\nwww.maliev.com | email: info@maliev.com | tel: +6681-803-0404\nCourt of Registry: Department of Business Development\nCommercial Register No.: 0125561001573 (สำนักงานใหญ่)";

    internal static string InvoiceBilling(InvoiceDocument invoice) => Lines(
        invoice.BillingAddressRecipient,
        invoice.BillingAddressCompany,
        Join(invoice.BillingAddressBuilding, invoice.BillingAddressLine1, ", "),
        Join(invoice.BillingAddressLine2, invoice.BillingAddressCity, ", "),
        Join(invoice.BillingAddressState, invoice.BillingAddressPostalCode, " "),
        invoice.BillingAddressCountry,
        Prefix("Registry: ", invoice.CommercialRegistration),
        Prefix("Tax ID: ", invoice.TaxIdentification));

    internal static string InvoiceShipping(InvoiceDocument invoice) => Lines(
        invoice.ShippingAddressRecipient,
        Prefix("Tel: ", invoice.ShippingAddressRecipientTelephone),
        invoice.ShippingAddressCompany,
        Join(invoice.ShippingAddressBuilding, invoice.ShippingAddressLine1, ", "),
        Join(invoice.ShippingAddressLine2, invoice.ShippingAddressCity, ", "),
        Join(invoice.ShippingAddressState, invoice.ShippingAddressPostalCode, " "),
        invoice.ShippingAddressCountry);

    internal static string QuotationCustomer(QuotationDocument quotation)
    {
        var customer = quotation.Customer;
        return Lines(
            customer?.FullName, customer?.CompanyName, customer?.BillingAddressBuilding,
            customer?.BillingAddressLine1, customer?.BillingAddressLine2,
            Join(customer?.BillingAddressCity, customer?.BillingAddressState, ", "),
            Join(customer?.BillingAddressPostalCode, customer?.BillingAddressCountry, " "),
            customer?.Email, Prefix("Telephone: ", customer?.Telephone), Prefix("Mobile: ", customer?.Mobile),
            Prefix("Fax: ", customer?.Fax), Prefix("Court of Registry: ", customer?.CommercialRegistrar),
            Prefix("Commercial Register No.: ", customer?.TaxNumber));
    }

    internal static string ReceiptCustomer(ReceiptDocument receipt) => Lines(
        receipt.BillingAddressRecipient, receipt.BillingAddressCompany, receipt.BillingAddressBuilding,
        receipt.BillingAddressLine1, receipt.BillingAddressLine2,
        Join(receipt.BillingAddressCity, receipt.BillingAddressState, ", "),
        Join(receipt.BillingAddressPostalCode, receipt.BillingAddressCountry, " "),
        Prefix("Commercial Register No.: ", receipt.TaxIdentification));

    private static string Lines(params string?[] lines) => string.Join('\n', lines.Where(value => !string.IsNullOrWhiteSpace(value)));

    internal static string Company(Legacy.Maliev.DocumentService.Domain.PurchaseOrder.CompanyInformation? company)
    {
        if (company is null) return string.Empty;

        var address = company.Address;
        var lines = new[]
        {
            company.ContactName,
            company.CompanyName,
            address?.Building,
            address?.AddressLine1,
            address?.AddressLine2,
            Join(address?.City, address?.State, ", "),
            Join(address?.PostalCode, address?.Country, " "),
            Prefix("Telephone: ", company.Telephone),
            Prefix("Mobile: ", company.Mobile),
            Prefix("Fax: ", company.Fax),
        };

        return string.Join('\n', lines.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? Join(string? left, string? right, string separator)
    {
        if (string.IsNullOrWhiteSpace(left)) return right;
        if (string.IsNullOrWhiteSpace(right)) return left;
        return $"{left}{separator}{right}";
    }

    private static string? Prefix(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : prefix + value;

    private static Stream Resource(string suffix)
    {
        var assembly = typeof(QuestDocumentRenderer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(value => value.EndsWith(suffix, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Embedded resource {suffix} was not found.");
    }

    private static string Safe(string? value) => value ?? string.Empty;
    private static string Date(DateTime? value) => DocumentFormat.Date(value);
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
