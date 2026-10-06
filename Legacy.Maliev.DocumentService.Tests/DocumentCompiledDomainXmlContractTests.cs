using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Invoice = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using OrderLabel = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;
using PurchaseOrder = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using Quotation = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using Receipt = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Tests;

// Original entity projects enabled public compiler XML output.
// Compiler-output assertions are separate from HTTP acceptance and coverage applicability.
public sealed class DocumentCompiledDomainXmlContractTests
{
    [Fact]
    public void CompiledDomainXml_IdentifiesTheActualPublicModelAssembly()
    {
        var document = ReadCompilerOutput();
        Assert.Equal(typeof(Invoice).Assembly.GetName().Name,
            document.Root?.Element("assembly")?.Element("name")?.Value);
    }

    [Theory]
    [InlineData(typeof(Invoice), "Number", "Invoice Model.", "Gets or sets the number.", "The number.")]
    [InlineData(typeof(Quotation), "Id", "Quotation.", "Gets or sets the identifier.", "The identifier.")]
    [InlineData(typeof(Receipt), "Id", "Receipt Model.", "Gets or sets the identifier.", "The identifier.")]
    [InlineData(typeof(PurchaseOrder), "ReferenceNumber", "Purchase Order.", "Gets or sets the reference number.", "The reference number.")]
    [InlineData(typeof(OrderLabel), "OrderQuantity", "Invoice Model.", "Gets or sets the order quantity.", "The order quantity.")]
    public void CompiledDomainXml_PreservesPublicDocumentTypeAndPropertyDocumentation(
        Type model, string property, string typeSummary, string propertySummary, string propertyValue)
    {
        var document = ReadCompilerOutput();
        var members = document.Root?.Element("members")?.Elements("member").ToArray();
        Assert.NotNull(members);
        var typeMember = Assert.Single(members,
            member => member.Attribute("name")?.Value == "T:" + model.FullName);
        var propertyMember = Assert.Single(members,
            member => member.Attribute("name")?.Value == "P:" + model.FullName + "." + property);
        Assert.Equal(typeSummary, Normalize(typeMember.Element("summary")?.Value));
        Assert.Equal(propertySummary, Normalize(propertyMember.Element("summary")?.Value));
        Assert.Equal(propertyValue, Normalize(propertyMember.Element("value")?.Value));
    }

    private static string Normalize(string? text) => Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();

    private static XDocument ReadCompilerOutput()
    {
        var path = Path.ChangeExtension(typeof(Invoice).Assembly.Location, ".xml");
        Assert.True(File.Exists(path),
            "The retained public entity compiler XML contract is missing from build output: " + path);
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = 8 * 1024 * 1024,
        });
        return XDocument.Load(reader);
    }
}
