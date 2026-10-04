using System.Net;
using System.Text;
using UglyToad.PdfPig;

namespace Legacy.Maliev.DocumentService.Tests;

// Independent literal wire bodies match committed Intranet BFF serializers, not shared DTOs.
public sealed class DocumentCurrentConsumerHttpAcceptanceTests
{
    [Fact]
    public async Task IntranetPascalCaseQuotation_BindsNestedCustomerEmployeeAndLineMoney()
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        // The quotation gateway's TransportJson preserves PascalCase and nullable line fields.
        const string payload = """
            {"Comment":"HTTP-QUOTE-COMMENT ทดสอบ","CreatedDate":"2026-10-03T00:00:00","Currency":"THB",
             "Customer":{"Id":37,"FullName":"HTTP-QUOTE-CUSTOMER","Email":"fixture@example.invalid",
                         "CompanyName":"HTTP-QUOTE-COMPANY","BillingAddressCountry":"Thailand"},
             "Employee":{"Email":"staff@example.invalid","FullName":"HTTP-QUOTE-EMPLOYEE"},
             "ExpirationDate":"2026-10-10T00:00:00","Fob":null,"Id":37,"InvoiceNumber":null,
             "Orders":[{"Id":1,"Description":"HTTP-QUOTE-LINE","Discount":null,"LeadTime":null,
                        "Name":"HTTP-QUOTE-PART","Quantity":2,"Subtotal":20.50,"UnitPrice":10.25}],
             "Period":7,"QuotedAmount":21.94,"ShippedVia":null,"Subtotal":20.50,"Terms":null,
             "Total":21.94,"Vat":1.44,"WithholdingTax":0}
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/pdfs/quotation", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        var text = pdf.GetPage(1).Text;
        foreach (var value in new[]
        {
            "QUOTATION", "HTTP-QUOTE-COMMENT", "HTTP-QUOTE-CUSTOMER", "HTTP-QUOTE-EMPLOYEE",
            "HTTP-QUOTE-LINE", "HTTP-QUOTE-PART", "ทดสอบ", "10.25", "20.50", "21.94",
        }) Assert.Contains(value, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntranetCamelCaseOrderLabel_BindsAllThreeQuantityFieldsIntoRealPdf()
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        const string payload = """
            {"id":"HTTP-LABEL-37","name":"HTTP-CURRENT-LABEL ทดสอบ",
             "orderQuantity":3,"manufactureQuantity":1,"remainingQuantity":2,
             "process":"HTTP-PROCESS","material":"HTTP-MATERIAL","color":"HTTP-COLOR",
             "surfaceFinish":"HTTP-SURFACE","description":"HTTP-LABEL-DESCRIPTION"}
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/orderlabel", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        var page = pdf.GetPage(1);
        Assert.Equal(216, page.Width, precision: 0);
        Assert.Equal(288, page.Height, precision: 0);
        Assert.Contains("HTTP-CURRENT-LABEL", page.Text, StringComparison.Ordinal);
        Assert.Contains("ทดสอบ", page.Text, StringComparison.Ordinal);
        Assert.Contains("ORDERED: 3, SHIPPED: 1, REMAINING: 2", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntranetCamelCasePurchaseOrder_BindsNestedPartiesFobAndComputedLineAmount()
    {
        await using var factory = new DocumentHttpAcceptanceTests.DocumentFactory();
        using var client = factory.Client();
        // JsonContent.Create uses camelCase; the producer explicitly names its FOB property.
        const string payload = """
            {"billing":{"companyName":"HTTP-BILLING-37","address":{"addressLine1":"HTTP-BILLADDR"}},
             "date":"2026-10-03T00:00:00","FOB":"HTTP-FOB-37","notes":"HTTP-PO-NOTES ทดสอบ",
             "orderedBy":"HTTP-ORDERED-37","referenceNumber":37,"shippedVia":null,"terms":null,
             "shipping":{"companyName":"HTTP-SHIPPING-37","address":{"addressLine1":"HTTP-SHIPADDR"}},
             "supplier":{"companyName":"HTTP-SUPPLIER-37","address":{"addressLine1":"HTTP-SUPADDR"}},
             "orderItems":[{"currency":"THB","description":"HTTP-PO-LINE","partNumber":null,
                            "quantity":7,"subtotal":87.50,"unitPrice":12.50}]}
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Pdfs/purchaseorder", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        var text = pdf.GetPage(1).Text;
        foreach (var value in new[]
        {
            "PURCHASE ORDER", "HTTP-BILLING-37", "HTTP-SHIPPING-37", "HTTP-SUPPLIER-37",
            "HTTP-FOB-37", "HTTP-PO-NOTES", "HTTP-PO-LINE", "ทดสอบ", "12.50", "87.50",
        }) Assert.Contains(value, text, StringComparison.Ordinal);
    }
}
