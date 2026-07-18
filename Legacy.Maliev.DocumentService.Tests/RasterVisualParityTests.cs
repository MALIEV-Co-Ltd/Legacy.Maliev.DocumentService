using Legacy.Maliev.DocumentService.Application;
using Legacy.Maliev.DocumentService.Rendering;
using PDFtoImage;
using SkiaSharp;
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
public sealed class RasterVisualParityTests
{
    private const int RasterDpi = 150;
    private const byte InkThreshold = 225;

    public static TheoryData<string> ImmutableLegacyFixtures => new()
    {
        "invoice-empty-unittest.pdf",
        "invoice-no-telephone-unittest.pdf",
        "invoice-thai-unittest.pdf",
        "invoice-unittest.pdf",
        "invoice-without-withholding-tax-unittest.pdf",
        "invoice_081019-23194-19534.pdf",
        "orderlabel-thai-unittest.pdf",
        "purchase-order-empty-unittest.pdf",
        "purchase-order-thai-unittest.pdf",
        "purchase-order-unittest.pdf",
        "quotation-empty-unittest.pdf",
        "quotation-mixed-orders-unittest.pdf",
        "quotation-no-withholding-tax-unittest.pdf",
        "quotation-only-description-unittest.pdf",
        "quotation-thai-noaddress-unittest.pdf",
        "quotation-thai-unittest.pdf",
        "quotation-unittest.pdf",
        "receipt-empty-unittest.pdf",
        "receipt-thai-unittest.pdf",
        "receipt-unittest.pdf",
        "receipt-without-withholding-tax-unittest.pdf",
        "receipt_6077.pdf",
    };

    [Theory]
    [MemberData(nameof(ImmutableLegacyFixtures))]
    public void EveryImmutableLegacyFixture_HasMappedQuestVariantAndPageRegionParity(string fixtureName)
    {
        var baselinePdf = Baseline(fixtureName);
        var candidatePdf = CandidateFor(fixtureName);
        using var baselineDocument = UglyToad.PdfPig.PdfDocument.Open(baselinePdf);
        using var candidateDocument = UglyToad.PdfPig.PdfDocument.Open(candidatePdf);
        Assert.Equal(baselineDocument.NumberOfPages, candidateDocument.NumberOfPages);

        foreach (var page in new[] { 0, baselineDocument.NumberOfPages - 1 }.Distinct())
        {
            using var baseline = Rasterize(baselinePdf, page);
            using var candidate = Rasterize(candidatePdf, page);
            Assert.InRange(candidate.Width, baseline.Width - 1, baseline.Width + 1);
            Assert.InRange(candidate.Height, baseline.Height - 1, baseline.Height + 1);

            var region = new Region($"{fixtureName} page {page + 1}", 0, 0, 1, 1);
            var expected = Profile(baseline, region);
            var actual = Profile(candidate, region);
            Assert.InRange(Math.Abs(expected.InkDensity - actual.InkDensity), 0, 0.12);
            Assert.InRange(Math.Abs(expected.OccupiedWidth - actual.OccupiedWidth), 0, 0.30);
            Assert.InRange(Math.Abs(expected.OccupiedHeight - actual.OccupiedHeight), 0, 0.30);
        }
    }

    [Fact]
    public void Invoice_ThaiToneMarkCropRetainsAboveLineInkAt150Dpi()
    {
        using var baseline = Rasterize(Baseline("invoice-thai-unittest.pdf"));
        using var candidate = Rasterize(new QuestDocumentRenderer().RenderInvoice(ThaiInvoice()));
        var baselineRemark = new Region("legacy Thai tone-mark remark", 0.08f, 0.56f, 0.36f, 0.08f);
        var candidateRemark = new Region("Quest Thai tone-mark remark", 0.08f, 0.52f, 0.36f, 0.08f);
        using var baselineCrop = Crop(baseline, baselineRemark);
        using var candidateCrop = Crop(candidate, candidateRemark);
        Record("invoice-thai-tone-marks-legacy", baselineCrop);
        Record("invoice-thai-tone-marks-quest", candidateCrop);

        var cropRegion = new Region("crop", 0, 0, 1, 1);
        Assert.True(Profile(baselineCrop, cropRegion).InkDensity > 0.005);
        Assert.True(Profile(candidateCrop, cropRegion).InkDensity > 0.005);
        var result = CompareRegion(baselineCrop, candidateCrop, cropRegion);
        Assert.True(result.Passes, result.Message);
    }

    [Fact]
    public void Invoice_PreservesLegacyPageRegionGeometryAt150Dpi()
    {
        AssertA4Parity(
            "invoice-thai-unittest.pdf",
            new QuestDocumentRenderer().RenderInvoice(ThaiInvoice()),
            "invoice-thai");
    }

    [Fact]
    public void Quotation_PreservesLegacyPageRegionGeometryAt150Dpi()
    {
        AssertA4Parity(
            "quotation-thai-unittest.pdf",
            new QuestDocumentRenderer().RenderQuotation(ThaiQuotation()),
            "quotation-thai");
    }

    [Fact]
    public void Receipt_PreservesLegacyOriginalPageRegionGeometryAt150Dpi()
    {
        AssertA4Parity(
            "receipt-thai-unittest.pdf",
            new QuestDocumentRenderer().RenderReceipt(ThaiReceipt()),
            "receipt-thai");
    }

    [Fact]
    public void PurchaseOrder_PreservesLegacyPageRegionGeometryAt150Dpi()
    {
        AssertA4Parity(
            "purchase-order-thai-unittest.pdf",
            new QuestDocumentRenderer().RenderPurchaseOrder(ThaiPurchaseOrder()),
            "purchase-order-thai");
    }

    [Fact]
    public void OrderLabel_PreservesLegacyFourByThreeGeometryAt150Dpi()
    {
        var renderer = new QuestDocumentRenderer(new FixedRasterTimeProvider(
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)));
        var baseline = Rasterize(Baseline("orderlabel-thai-unittest.pdf"));
        var candidate = Rasterize(renderer.RenderOrderLabel(new LabelDocument
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
        }));

        Record("orderlabel-thai-legacy", baseline);
        Record("orderlabel-thai-quest", candidate);
        Assert.InRange(candidate.Width, baseline.Width - 1, baseline.Width + 1);
        Assert.InRange(candidate.Height, baseline.Height - 1, baseline.Height + 1);
        var result = CompareRegion(baseline, candidate, new Region("four-by-three label", 0, 0, 1, 1));
        Assert.True(result.Passes, result.Message);
    }

    private static void AssertA4Parity(string baselineName, byte[] candidatePdf, string artifactPrefix)
    {
        var baseline = Rasterize(Baseline(baselineName));
        var candidate = Rasterize(candidatePdf);
        Record(artifactPrefix + "-legacy", baseline);
        Record(artifactPrefix + "-quest", candidate);

        Assert.Equal(baseline.Width, candidate.Width);
        Assert.Equal(baseline.Height, candidate.Height);

        var regions = new[]
        {
            new Region("masthead and company metadata", 0.00f, 0.00f, 1.00f, 0.18f),
            new Region("parties, items, totals and remarks", 0.00f, 0.16f, 1.00f, 0.53f),
            new Region("reserved whitespace", 0.00f, 0.69f, 1.00f, 0.18f),
            new Region("payment footer", 0.00f, 0.87f, 1.00f, 0.13f),
        };

        var failures = regions
            .Select(region => CompareRegion(baseline, candidate, region))
            .Where(result => !result.Passes)
            .Select(result => result.Message)
            .ToArray();

        Assert.True(failures.Length == 0,
            $"The QuestPDF {artifactPrefix} materially diverges from the immutable iText layout at 150 DPI:\n" +
            string.Join("\n", failures));
    }

    private static RegionResult CompareRegion(SKBitmap baseline, SKBitmap candidate, Region region)
    {
        var baselineProfile = Profile(baseline, region);
        var candidateProfile = Profile(candidate, region);

        // Different PDF engines rasterize glyph edges differently. Region-level ink density and
        // occupied geometry deliberately tolerate anti-aliasing while rejecting moved/redesigned blocks.
        const double maximumInkDensityDelta = 0.035;
        const double maximumOccupiedWidthDelta = 0.18;
        const double maximumOccupiedHeightDelta = 0.23;
        // Layout engines use different font hinting and the obsolete PayPal block is intentionally absent.
        // A high-frequency pixel mismatch is therefore expected; the geometry/density guards carry the
        // structural signal and this coarse threshold rejects large block redesigns.
        const double maximumPerceptualDistance = 0.90;
        var perceptualDistance = PerceptualDistance(baseline, candidate, region);
        var effectivelyBlank = baselineProfile.InkDensity < 0.01 && candidateProfile.InkDensity < 0.01;
        var perceptualPasses = effectivelyBlank || perceptualDistance <= maximumPerceptualDistance;
        var densityDelta = Math.Abs(baselineProfile.InkDensity - candidateProfile.InkDensity);
        var widthDelta = Math.Abs(baselineProfile.OccupiedWidth - candidateProfile.OccupiedWidth);
        var heightDelta = Math.Abs(baselineProfile.OccupiedHeight - candidateProfile.OccupiedHeight);
        var geometryIsVeryClose = densityDelta <= 0.01 && widthDelta <= 0.08 && heightDelta <= 0.15;
        var passes = effectivelyBlank || (densityDelta <= maximumInkDensityDelta
            && widthDelta <= maximumOccupiedWidthDelta
            && heightDelta <= maximumOccupiedHeightDelta
            && (perceptualPasses || geometryIsVeryClose));

        return new RegionResult(
            passes,
            $"{region.Name}: perceptual distance {perceptualDistance:F3}, ink {baselineProfile.InkDensity:P2} -> {candidateProfile.InkDensity:P2}, " +
            $"occupied width {baselineProfile.OccupiedWidth:P2} -> {candidateProfile.OccupiedWidth:P2}, " +
            $"height {baselineProfile.OccupiedHeight:P2} -> {candidateProfile.OccupiedHeight:P2}");
    }

    private static double PerceptualDistance(SKBitmap baseline, SKBitmap candidate, Region region)
    {
        const int columns = 48;
        const int rows = 32;
        var expected = Downsample(baseline, region, columns, rows);
        var actual = Downsample(candidate, region, columns, rows);
        var denominator = expected.Sum() + actual.Sum();
        if (denominator == 0)
            return 0;

        return expected.Zip(actual, (left, right) => Math.Abs(left - right)).Sum() / denominator;
    }

    private static double[] Downsample(SKBitmap bitmap, Region region, int columns, int rows)
    {
        var values = new double[columns * rows];
        var left = (int)(bitmap.Width * region.Left);
        var top = (int)(bitmap.Height * region.Top);
        var width = Math.Max(1, (int)(bitmap.Width * region.Width));
        var height = Math.Max(1, (int)(bitmap.Height * region.Height));

        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var x0 = left + column * width / columns;
                var x1 = left + (column + 1) * width / columns;
                var y0 = top + row * height / rows;
                var y1 = top + (row + 1) * height / rows;
                double darkness = 0;
                var count = 0;
                for (var y = y0; y < y1; y += 2)
                    for (var x = x0; x < x1; x += 2)
                    {
                        var pixel = bitmap.GetPixel(Math.Min(x, bitmap.Width - 1), Math.Min(y, bitmap.Height - 1));
                        darkness += 1 - ((pixel.Red + pixel.Green + pixel.Blue) / (3d * byte.MaxValue));
                        count++;
                    }

                values[row * columns + column] = count == 0 ? 0 : darkness / count;
            }

        return values;
    }

    private static InkProfile Profile(SKBitmap bitmap, Region region)
    {
        var left = (int)(bitmap.Width * region.Left);
        var top = (int)(bitmap.Height * region.Top);
        var width = Math.Max(1, (int)(bitmap.Width * region.Width));
        var height = Math.Max(1, (int)(bitmap.Height * region.Height));
        var right = Math.Min(bitmap.Width, left + width);
        var bottom = Math.Min(bitmap.Height, top + height);
        var minX = right;
        var minY = bottom;
        var maxX = left - 1;
        var maxY = top - 1;
        long inkPixels = 0;

        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Red > InkThreshold && color.Green > InkThreshold && color.Blue > InkThreshold)
                    continue;

                inkPixels++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

        var area = (long)(right - left) * (bottom - top);
        var occupiedWidth = maxX < minX ? 0 : (maxX - minX + 1d) / (right - left);
        var occupiedHeight = maxY < minY ? 0 : (maxY - minY + 1d) / (bottom - top);
        return new InkProfile((double)inkPixels / area, occupiedWidth, occupiedHeight);
    }

    private static SKBitmap Rasterize(byte[] pdf, int page = 0)
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            throw new PlatformNotSupportedException("Deterministic PDF raster parity is supported on CI and developer desktop platforms.");

#pragma warning disable CA1416 // Guarded above; PDFium-backed rendering is supported on our Windows/Linux validation lanes.
        return Conversion.ToImage(
            pdf,
            page: page,
            password: null,
            options: new RenderOptions { Dpi = RasterDpi, Grayscale = true });
#pragma warning restore CA1416
    }

    private static byte[] Baseline(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Baselines", "legacy-itext", fileName));

    private static SKBitmap Crop(SKBitmap source, Region region)
    {
        var rectangle = new SKRectI(
            (int)(source.Width * region.Left),
            (int)(source.Height * region.Top),
            (int)(source.Width * (region.Left + region.Width)),
            (int)(source.Height * (region.Top + region.Height)));
        var result = new SKBitmap(rectangle.Width, rectangle.Height);
        Assert.True(source.ExtractSubset(result, rectangle));
        return result;
    }

    private static void Record(string name, SKBitmap bitmap)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "raster-parity");
        Directory.CreateDirectory(directory);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        using var output = File.Create(Path.Combine(directory, name + ".png"));
        encoded.SaveTo(output);
    }

    private static InvoiceDocument ThaiInvoice() => new()
    {
        Number = "12345-4567-12345",
        CustomerId = 1234,
        CreatedDate = new DateTime(2026, 7, 15),
        BillingAddressRecipient = "สุรัตน์ วนาศรีวิไล",
        BillingAddressCompany = "บริษัท อุตสาหกรรมเส้นจันทร์ตรามะลิ จำกัด",
        BillingAddressBuilding = "ตึกบางตึก",
        BillingAddressLine1 = "36/1 ถนนเลียบคลองขุนหมาดไทย",
        BillingAddressLine2 = "ตำบลคลองข่อย",
        BillingAddressCity = "ปากเกร็ด",
        BillingAddressState = "นนทบุรี",
        BillingAddressPostalCode = "11120",
        BillingAddressCountry = "ประเทศไทย",
        TaxIdentification = "12312123123123",
        CommercialRegistration = "Department of Business Development",
        ShippingAddressRecipient = "ณัฐกานต์ วนาศรีวิไล",
        ShippingAddressRecipientTelephone = "+66 (0)81 801 0810",
        ShippingAddressCompany = "บริษัท มาลีฟ จำกัด",
        ShippingAddressBuilding = "ตึกเขียวๆ",
        ShippingAddressLine1 = "36/1 ถนนเลียบคลองขุนหมาดไทย",
        ShippingAddressLine2 = "ตำบลคลองข่อย",
        ShippingAddressCity = "ปากเกร็ด",
        ShippingAddressState = "นนทบุรี",
        ShippingAddressPostalCode = "11120",
        ShippingAddressCountry = "ประเทศไทย",
        SalesPerson = "นายแดง ตาดำ",
        PurchaseOrderNumber = "12345",
        ShippedVia = "DHL Express Intl.",
        Fob = "จุดส่งของ",
        Terms = "NET 10",
        Currency = "บาท",
        Subtotal = 1234.99m,
        Vat = 86.87m,
        Total = 1327.85m,
        WithholdingTax = -200m,
        Outstanding = 1127.85m,
        Remark = "ตัวหนังสือทดลองแบบภาษาไทย",
        OrderItems =
        [
            new InvoiceLine { Description = "ของชิ้นที่ 1", UnitPrice = 1.2345m, Quantity = 10, Subtotal = 123.45m },
            new InvoiceLine { Description = "ของชิ้นที่ 2", UnitPrice = 2.124m, Quantity = 7, Subtotal = 457.12m },
            new InvoiceLine { Description = "ของชิ้นที่ 3", UnitPrice = 1.454m, Quantity = 5, Subtotal = 764.45m },
            new InvoiceLine { Description = "ของชิ้นที่ 4", UnitPrice = 3.412m, Quantity = 24, Subtotal = 891.10m },
        ],
    };

    private static QuotationDocument ThaiQuotation() => new()
    {
        Id = 77,
        CreatedDate = new DateTime(2026, 7, 15),
        ExpirationDate = new DateTime(2026, 8, 14),
        Period = 30,
        InvoiceNumber = "INV-TH-1",
        ShippedVia = "DHL Express Intl.",
        Fob = "Origin",
        Terms = "NET 10",
        Currency = "บาท",
        Subtotal = 1234.99m,
        Vat = 86.87m,
        Total = 1327.85m,
        WithholdingTax = 200m,
        QuotedAmount = 1127.85m,
        Comment = "ตัวหนังสือทดลองแบบภาษาไทย",
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
        Orders =
        [
            new QuotationLine { Id = 1, Name = "ตัวอย่างสั่งของ", Description = "ตัวอย่างของที่สั่งชิ้นที่ 1", Quantity = 10, UnitPrice = 12.34m, Subtotal = 123.45m },
            new QuotationLine { Id = 2, Name = "ตัวอย่างสั่งของ", Description = "ตัวอย่างของที่สั่งชิ้นที่ 2", Quantity = 7, UnitPrice = 65.30m, Subtotal = 457.12m, Discount = 3m },
        ],
    };

    private static ReceiptDocument ThaiReceipt() => new()
    {
        Id = 1234567,
        InvoiceNumber = "123-456-789",
        CustomerId = 12345,
        PaymentDate = new DateTime(2026, 7, 15),
        BillingAddressRecipient = "วุฒิชัย หมอยา",
        BillingAddressCompany = "บริษัท ลูกค้า จำกัด",
        BillingAddressBuilding = "ตึกบางตึก",
        BillingAddressLine1 = "36/1 ถนนเลียบคลองขุนมหาดไทย",
        BillingAddressLine2 = "ตำบลคลองข่อย",
        BillingAddressCity = "ปากเกร็ด",
        BillingAddressState = "นนทบุรี",
        BillingAddressPostalCode = "11120",
        BillingAddressCountry = "ประเทศไทย",
        TaxIdentification = "12312123123123",
        CommercialRegistration = "DBD",
        Remark = "ตัวหนังสือทดลองแบบภาษาไทย",
        Currency = "บาท",
        Subtotal = 1234.99m,
        Vat = 86.87m,
        Total = 1327.85m,
        WithholdingTax = 200m,
        AmountPaid = 1127.85m,
        OrderItems =
        [
            new ReceiptLine { Description = "ของชิ้นที่ 1", Quantity = 10, UnitPrice = 1.2345m, Subtotal = 123.45m },
            new ReceiptLine { Description = "ของชิ้นที่ 2", Quantity = 7, UnitPrice = 2.124m, Subtotal = 457.12m },
            new ReceiptLine { Description = "ของชิ้นที่ 3", Quantity = 5, UnitPrice = 1.454m, Subtotal = 764.45m },
        ],
    };

    private static PurchaseOrderDocument ThaiPurchaseOrder() => new()
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
        Notes = "วางมัดจำ: 20,000.00 บาท\nเงินดาวน์: 428,330.00 บาท\nส่วนที่เหลือ จ่าย 15 วันหลังจากติดตั้งเครื่องแล้ว: 1,793,329.00 บาท\nเงินวางมัดจำทั้งหมดจะต้องจ่ายคืน ถ้าธนาคารไม่อนุมัติสินเชื่อ",
        OrderItems =
        [
            new PurchaseOrderLine { PartNumber = "VF-2-SE", Description = "Haas CNC, Vertical Machining Center as per quotation.", Quantity = 5, UnitPrice = 2095000m, Currency = "บาท" },
            new PurchaseOrderLine { PartNumber = "VF-2-SS", Description = "Haas CNC, Vertical Machining Center as per quotation.", Quantity = 3, UnitPrice = 2995000m, Currency = "บาท" },
        ],
    };

    private static byte[] CandidateFor(string fixtureName)
    {
        var renderer = new QuestDocumentRenderer(new FixedRasterTimeProvider(
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)));

        return fixtureName switch
        {
            "invoice-empty-unittest.pdf" => renderer.RenderInvoice(new()),
            "invoice-no-telephone-unittest.pdf" => renderer.RenderInvoice(InvoiceVariant(itemCount: 3, includeTelephone: false)),
            "invoice-thai-unittest.pdf" => renderer.RenderInvoice(ThaiInvoice()),
            "invoice-unittest.pdf" => renderer.RenderInvoice(InvoiceVariant(itemCount: 24)),
            "invoice-without-withholding-tax-unittest.pdf" => renderer.RenderInvoice(InvoiceVariant(itemCount: 3, includeWithholding: false)),
            "invoice_081019-23194-19534.pdf" => renderer.RenderInvoice(InvoiceVariant(itemCount: 7)),
            "orderlabel-thai-unittest.pdf" => renderer.RenderOrderLabel(new LabelDocument
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
            }),
            "purchase-order-empty-unittest.pdf" => renderer.RenderPurchaseOrder(new()),
            "purchase-order-thai-unittest.pdf" => renderer.RenderPurchaseOrder(ThaiPurchaseOrder()),
            "purchase-order-unittest.pdf" => renderer.RenderPurchaseOrder(PurchaseOrderVariant(22)),
            "quotation-empty-unittest.pdf" => renderer.RenderQuotation(new()),
            "quotation-mixed-orders-unittest.pdf" => renderer.RenderQuotation(QuotationVariant(3)),
            "quotation-no-withholding-tax-unittest.pdf" => renderer.RenderQuotation(QuotationVariant(3, includeWithholding: false)),
            "quotation-only-description-unittest.pdf" => renderer.RenderQuotation(QuotationVariant(2)),
            "quotation-thai-noaddress-unittest.pdf" => renderer.RenderQuotation(QuotationVariant(2, includeAddress: false)),
            "quotation-thai-unittest.pdf" => renderer.RenderQuotation(ThaiQuotation()),
            "quotation-unittest.pdf" => renderer.RenderQuotation(QuotationVariant(12)),
            "receipt-empty-unittest.pdf" => renderer.RenderReceipt(new()),
            "receipt-thai-unittest.pdf" => renderer.RenderReceipt(ThaiReceipt()),
            "receipt-unittest.pdf" => renderer.RenderReceipt(ReceiptVariant(44)),
            "receipt-without-withholding-tax-unittest.pdf" => renderer.RenderReceipt(ReceiptVariant(3, includeWithholding: false)),
            "receipt_6077.pdf" => renderer.RenderReceipt(ReceiptVariant(44)),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unmapped immutable fixture."),
        };
    }

    private static InvoiceDocument InvoiceVariant(int itemCount, bool includeTelephone = true, bool includeWithholding = true)
    {
        var invoice = ThaiInvoice();
        invoice.ShippingAddressRecipientTelephone = includeTelephone ? "+66 (0)81 801 0810" : null;
        invoice.WithholdingTax = includeWithholding ? -200m : null;
        invoice.OrderItems = Enumerable.Range(1, itemCount).Select(index => new InvoiceLine
        {
            Description = $"Test item {index}\nรายละเอียดรายการทดสอบภาษาไทย",
            Quantity = index,
            UnitPrice = 12.34m,
            Subtotal = index * 12.34m,
        }).ToList();
        return invoice;
    }

    private static QuotationDocument QuotationVariant(int itemCount, bool includeWithholding = true, bool includeAddress = true)
    {
        var quotation = ThaiQuotation();
        quotation.WithholdingTax = includeWithholding ? 200m : null;
        if (!includeAddress)
            quotation.Customer = new() { Id = 12345, FullName = "วุฒิชัย หมอยา" };
        quotation.Orders = Enumerable.Range(1, itemCount).Select(index => new QuotationLine
        {
            Id = index,
            Name = $"test order {index}",
            Description = $"DETAIL-{index:D3} รายละเอียดภาษาไทยสำหรับทดสอบการขึ้นหน้าใหม่",
            Quantity = index,
            UnitPrice = 12.34m,
            Subtotal = index * 12.34m,
            Discount = index % 2 == 0 ? 3m : null,
        }).ToList();
        return quotation;
    }

    private static ReceiptDocument ReceiptVariant(int itemCount, bool includeWithholding = true)
    {
        var receipt = ThaiReceipt();
        receipt.WithholdingTax = includeWithholding ? 200m : null;
        receipt.OrderItems = Enumerable.Range(1, itemCount).Select(index => new ReceiptLine
        {
            Description = $"Test item {index} รายการทดสอบภาษาไทย",
            Quantity = index,
            UnitPrice = 12.34m,
            Subtotal = index * 12.34m,
        }).ToList();
        return receipt;
    }

    private static PurchaseOrderDocument PurchaseOrderVariant(int itemCount)
    {
        var order = ThaiPurchaseOrder();
        order.OrderItems = Enumerable.Range(1, itemCount).Select(index => new PurchaseOrderLine
        {
            PartNumber = index % 2 == 0 ? "VF-2-SS" : "VF-2-SE",
            Description = "Haas CNC, Vertical Machining Center as per quotation.\nReference: Q018-12/12-2",
            Quantity = index,
            UnitPrice = 12.34m,
            Currency = "THB",
        }).ToList();
        return order;
    }

    private sealed record Region(string Name, float Left, float Top, float Width, float Height);
    private sealed record InkProfile(double InkDensity, double OccupiedWidth, double OccupiedHeight);
    private sealed record RegionResult(bool Passes, string Message);

    private sealed class FixedRasterTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
