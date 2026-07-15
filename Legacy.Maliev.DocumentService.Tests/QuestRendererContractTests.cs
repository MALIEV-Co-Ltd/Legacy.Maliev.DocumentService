using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Rendering;
using System.Reflection;
using UglyToad.PdfPig;
using InvoiceDocument = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using InvoiceLine = Legacy.Maliev.DocumentService.Domain.Invoice.OrderItem;
using LabelDocument = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;
using PurchaseOrderDocument = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using PurchaseOrderLine = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.OrderItem;
using QuotationDocument = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using QuotationLine = Legacy.Maliev.DocumentService.Domain.Quotations.Order;
using ReceiptDocument = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;
using ReceiptLine = Legacy.Maliev.DocumentService.Domain.Receipt.OrderItem;

namespace Legacy.Maliev.DocumentService.Tests;

[Collection("Renderer artifacts")]
public sealed class QuestRendererContractTests
{
    private const double MaximumContentFontSizeInPixels = 13;
    private const double MaximumContentFontSizeInPoints = MaximumContentFontSizeInPixels * 72 / 96;
    private const double HeaderBandHeightRatio = 0.18;

    [Fact]
    public void FixedTimeProvider_ProducesDeterministicOrderLabelContent()
    {
        var renderer = new QuestDocumentRenderer(new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)));
        var label = new LabelDocument { Id = "1", Name = "ทดสอบ", Description = "deterministic" };

        var first = Text(renderer.RenderOrderLabel(label));
        var second = Text(renderer.RenderOrderLabel(label));

        Assert.Equal(first, second);
        Assert.Contains("2026-07-15", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoice_PreservesA4TitleTotalsAndThaiToneMarks()
    {
        var pdf = Renderer().RenderInvoice(new InvoiceDocument
        {
            Number = "INV-TH-1",
            CreatedDate = new DateTime(2026, 7, 15),
            BillingAddressCompany = "บริษัท มาลีฟ จำกัด",
            BillingAddressRecipient = "ณัฐกานต์ วนาศรีวิไล",
            BillingAddressBuilding = "ตึกบางตึก",
            BillingAddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย",
            BillingAddressLine2 = "ตำบลคลองข่อย",
            BillingAddressCity = "ปากเกร็ด",
            BillingAddressState = "นนทบุรี",
            BillingAddressPostalCode = "11120",
            BillingAddressCountry = "ประเทศไทย",
            ShippingAddressRecipient = "ณัฐกานต์ วนาศรีวิไล",
            ShippingAddressRecipientTelephone = "+66 (0)81 801 0810",
            ShippingAddressCompany = "บริษัท มาลีฟ จำกัด",
            ShippingAddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย",
            ShippingAddressLine2 = "ตำบลคลองข่อย",
            ShippingAddressCity = "ปากเกร็ด",
            ShippingAddressState = "นนทบุรี",
            ShippingAddressPostalCode = "11120",
            ShippingAddressCountry = "ประเทศไทย",
            PurchaseOrderNumber = "PO-12345",
            SalesPerson = "นายแดง ตาดำ",
            ShippedVia = "DHL Express Intl.",
            Fob = "จุดส่งของ",
            Terms = "NET 10",
            TaxIdentification = "0125561001573",
            CommercialRegistration = "Department of Business Development",
            Currency = "บาท",
            Subtotal = 100m,
            Vat = 7m,
            Total = 107m,
            WithholdingTax = 3m,
            Outstanding = 104m,
            OrderItems =
            [
                new InvoiceLine { Description = "ของชิ้นที่ 1", Quantity = 10, UnitPrice = 1.2345m, Subtotal = 123.45m },
                new InvoiceLine { Description = "ของชิ้นที่ 2", Quantity = 7, UnitPrice = 2.124m, Subtotal = 457.12m },
                new InvoiceLine { Description = "ของชิ้นที่ 3", Quantity = 5, UnitPrice = 1.454m, Subtotal = 764.45m },
                new InvoiceLine { Description = "ของชิ้นที่ 4", Quantity = 24, UnitPrice = 3.412m, Subtotal = 891.1m },
            ],
        });
        Record("invoice", pdf);

        AssertContentFontSize(pdf);
        AssertA4(pdf, "INVOICE", "ใบวางบิล", "ณัฐกานต์", "104.00", "MALIEV Co., Ltd.", "Shipping", "จัดส่ง", "SALESPERSON", "TERMS");
        Assert.DoesNotContain("PayPal", Text(pdf), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quotation_PreservesA4TitleAmountsAndThaiToneMarks()
    {
        var pdf = Renderer().RenderQuotation(new QuotationDocument
        {
            Id = 77,
            CreatedDate = new DateTime(2026, 7, 15),
            ExpirationDate = new DateTime(2026, 8, 14),
            Customer = new()
            {
                Id = 12345,
                FullName = "วุฒิชัย หมอยา",
                CompanyName = "บริษัท ลูกค้า จำกัด",
                BillingAddressBuilding = "อาคาร",
                BillingAddressLine1 = "ถนน 1",
                BillingAddressLine2 = "ถนน 2",
                BillingAddressCity = "เมือง",
                BillingAddressState = "จังหวัด",
                BillingAddressPostalCode = "12345",
                BillingAddressCountry = "ประเทศไทย",
                Email = "customer@example.com",
                Telephone = "0123-321-1232",
                Mobile = "089-895-0690",
                CommercialRegistrar = "Department of Business Development",
                TaxNumber = "12345123412443",
            },
            Employee = new() { FullName = "ณัฐพล วนาศรีวิไล", Email = "employee@maliev.com", Mobile = "089-895-0690" },
            Currency = "บาท",
            Subtotal = 100m,
            Vat = 7m,
            Total = 107m,
            QuotedAmount = 104m,
            Period = 30,
            InvoiceNumber = "INV-TH-1",
            ShippedVia = "DHL Express Intl.",
            Fob = "Origin",
            Terms = "NET 10",
            Comment = "ไม่มีอะไรเพิ่มเติม",
            Orders =
            [
                new QuotationLine { Id = 1, Name = "ตัวอย่างสั่งของ", Description = "ตัวอย่างของที่สั่งชิ้นที่ 1", Quantity = 500, UnitPrice = 10m, Subtotal = 5000m, Discount = 78m },
                new QuotationLine { Id = 2, Name = "ตัวอย่างสั่งของ", Description = "ตัวอย่างของที่สั่งชิ้นที่ 2", Quantity = 500, UnitPrice = 75240m, Subtotal = 37620000m, Discount = 34m },
            ],
        });
        Record("quotation", pdf);

        AssertContentFontSize(pdf);
        AssertA4(pdf, "QUOTATION", "ใบเสนอราคา", "MALIEV Co., Ltd.", "36/1 Moo 3", "วุฒิชัย", "104.00", "-7.80", "-25,581.60", "Prepared for", "Prepared by", "INVOICE NUMBER", "Description", "รายละเอียด");
    }

    [Fact]
    public void Receipt_PreservesTwoA4CopiesTitleAmountsAndThaiToneMarks()
    {
        var pdf = Renderer().RenderReceipt(new ReceiptDocument
        {
            Id = 88,
            InvoiceNumber = "INV-TH-1",
            PaymentDate = new DateTime(2026, 7, 15),
            BillingAddressCompany = "บริษัท ลูกค้า จำกัด",
            BillingAddressRecipient = "วุฒิชัย หมอยา",
            BillingAddressBuilding = "ตึกบางตึก",
            BillingAddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย",
            BillingAddressLine2 = "ตำบลคลองข่อย",
            BillingAddressCity = "ปากเกร็ด",
            BillingAddressState = "นนทบุรี",
            BillingAddressPostalCode = "11120",
            BillingAddressCountry = "ประเทศไทย",
            CustomerId = 12345,
            TaxIdentification = "12312123123123",
            CommercialRegistration = "DBD",
            Remark = "ตัวหนังสือทดลองแบบภาษาไทย",
            Currency = "บาท",
            Subtotal = 100m,
            Vat = 7m,
            Total = 107m,
            WithholdingTax = 3m,
            AmountPaid = 104m,
            OrderItems =
            [
                new ReceiptLine { Description = "ของชิ้นที่ 1", Quantity = 10, UnitPrice = 1.2345m, Subtotal = 123.45m },
                new ReceiptLine { Description = "ของชิ้นที่ 2", Quantity = 7, UnitPrice = 2.124m, Subtotal = 457.12m },
                new ReceiptLine { Description = "ของชิ้นที่ 3", Quantity = 5, UnitPrice = 1.454m, Subtotal = 764.45m },
                new ReceiptLine { Description = "ของชิ้นที่ 4", Quantity = 24, UnitPrice = 3.412m, Subtotal = 891.1m },
            ],
        });
        Record("receipt", pdf);

        AssertContentFontSize(pdf);
        using var document = PdfDocument.Open(pdf);
        Assert.Equal(2, document.NumberOfPages);
        AssertA4(pdf, "TAX INVOICE", "ใบเสร็จรับเงิน", "วุฒิชัย", "104.00", "ORIGINAL", "Customer", "ลูกค้า", "Description", "รายละเอียด", "bank clearance");
    }

    [Fact]
    public void PurchaseOrder_PreservesA4TitleAmountAndThaiToneMarks()
    {
        var pdf = Renderer().RenderPurchaseOrder(new PurchaseOrderDocument
        {
            ReferenceNumber = 99,
            Date = new DateTime(2026, 7, 15),
            Supplier = new()
            {
                CompanyName = "บริษัท ผู้ขาย จำกัด",
                ContactName = "กฤช พัชรารัตน์",
                Telephone = "02-726-7191-5",
                Mobile = "081-140-6076",
                Fax = "02-726-7197",
                Address = new() { AddressLine1 = "62 เฉลิมพระเกียรติที่ 9 ซอย 34", AddressLine2 = "หนองบอน", City = "ประเวศ", State = "กรุงเทพ", PostalCode = "10250", Country = "ประเทศไทย" },
            },
            Billing = new()
            {
                CompanyName = "Maliev Co., Ltd.",
                ContactName = "ณัฐพล วนาศรีวิไล",
                Telephone = "02-926-0569",
                Mobile = "089-895-0690",
                Address = new() { AddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย", AddressLine2 = "ตำบลคลองข่อย", City = "ปากเกร็ด", State = "นนทบุรี", PostalCode = "11120", Country = "ประเทศไทย" },
            },
            Shipping = new()
            {
                CompanyName = "Maliev Co., Ltd.",
                ContactName = "สุรัตน์ วนาศรีวิไล",
                Telephone = "02-926-0569",
                Mobile = "081-720-2087",
                Address = new() { AddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย", AddressLine2 = "ตำบลคลองข่อย", City = "ปากเกร็ด", State = "นนทบุรี", PostalCode = "11120", Country = "ประเทศไทย" },
            },
            OrderedBy = "ณัฐพล วนาศรีวิไล",
            ShippedVia = "Supplier เป็นผู้เลือก",
            FOB = "บริษัท ผู้ขาย จำกัด",
            Terms = "NET 10",
            Notes = "วางมัดจำ: 20,000.00 บาท\nเงินดาวน์: 428,330.00 บาท",
            OrderItems =
            [
                new PurchaseOrderLine { PartNumber = "VF-2-SE", Description = "Haas CNC, Vertical Machining Center as per quotation.", Quantity = 5, UnitPrice = 2095000m, Currency = "บาท" },
                new PurchaseOrderLine { PartNumber = "VF-2-SS", Description = "Haas CNC, Vertical Machining Center as per quotation.", Quantity = 3, UnitPrice = 2995000m, Currency = "บาท" },
            ],
        });
        Record("purchase-order", pdf);

        AssertContentFontSize(pdf);
        AssertA4(pdf, "PURCHASE ORDER", "ใบสั", "กฤช", "19,460,000.00", "Supplier", "ผู้ขาย", "Billing", "วางบิล", "Shipping", "จัดส่ง", "FOB", "Grand Total");
        Assert.True(Text(pdf).Count(value => value == '\0') >= 2,
            "PdfPig should expose the shaped Thai combining-mark glyph clusters for visual parity validation.");
    }

    [Fact]
    public void OrderLabel_PreservesFourByThreeGeometryAndThaiToneMarks()
    {
        var pdf = Renderer().RenderOrderLabel(new LabelDocument
        {
            Id = "15324",
            Name = "ออเดอร์ทดสอบระบบ",
            Process = "FDM",
            Material = "PLA",
            Color = "แดง",
            SurfaceFinish = "Deburred",
            OrderQuantity = 5,
            ManufactureQuantity = 4,
            RemainingQuantity = 1,
            Description = "ชิ้นงานสำหรับทดสอบเครื่องหมายวรรณยุกต์ไทย",
        });
        Record("order-label", pdf);

        AssertContentFontSize(pdf);
        using var document = PdfDocument.Open(pdf);
        var page = document.GetPage(1);
        Assert.Equal(288, page.Width, precision: 0);
        Assert.Equal(216, page.Height, precision: 0);
        Assert.Contains("ออเดอร์ทดสอบระบบ", Text(pdf), StringComparison.Ordinal);
    }

    private static IDocumentRenderer Renderer()
    {
        var assembly = Assembly.Load("Legacy.Maliev.DocumentService.Rendering");
        var type = assembly.GetType("Legacy.Maliev.DocumentService.Rendering.QuestDocumentRenderer");
        Assert.NotNull(type);
        return Assert.IsAssignableFrom<IDocumentRenderer>(Activator.CreateInstance(type));
    }

    private static void AssertA4(byte[] pdf, params string[] expected)
    {
        using var document = PdfDocument.Open(pdf);
        var page = document.GetPage(1);
        Assert.Equal(595, page.Width, precision: 0);
        Assert.Equal(842, page.Height, precision: 0);
        var text = Text(pdf);
        Assert.All(expected, value => Assert.Contains(value, text, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertContentFontSize(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        foreach (var page in document.GetPages())
        {
            var headerBoundary = page.Height * (1 - HeaderBandHeightRatio);
            var rotatedLabelHeaderBoundary = page.Width * (1 - HeaderBandHeightRatio);
            var oversizedContent = page.Letters
                .Where(letter => letter.FontSize > MaximumContentFontSizeInPoints)
                .Where(letter => letter.BoundingBox.Top < headerBoundary)
                .Where(letter => page.Width >= page.Height || letter.BoundingBox.Left < rotatedLabelHeaderBoundary)
                .ToArray();

            Assert.True(
                oversizedContent.Length == 0,
                $"Page {page.Number} contains non-header glyphs larger than {MaximumContentFontSizeInPixels}px " +
                $"({MaximumContentFontSizeInPoints:F2}pt): " +
                string.Join(", ", oversizedContent.Take(20).Select(letter =>
                    $"'{letter.Value}'={letter.FontSize:F2}pt at x={letter.BoundingBox.Left:F2}, y={letter.BoundingBox.Top:F2}")));
        }
    }

    private static string Text(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(' ', document.GetPages().Select(page => page.Text));
    }

    private static void Record(string name, byte[] pdf)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"{name}.pdf"), pdf);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
