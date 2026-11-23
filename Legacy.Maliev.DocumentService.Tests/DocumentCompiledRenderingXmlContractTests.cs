using System.Xml;
using System.Xml.Linq;
using Legacy.Maliev.DocumentService.Rendering;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class DocumentCompiledRenderingXmlContractTests
{
    [Fact]
    public void CompiledRenderingXml_ContainsAmountWordsAndReceiptFooterDocumentation()
    {
        var path = Path.ChangeExtension(typeof(ThaiBahtAmountWords).Assembly.Location, ".xml");
        Assert.True(File.Exists(path), "Rendering compiler XML is missing from build output: " + path);
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = 8 * 1024 * 1024,
        });
        var document = XDocument.Load(reader);
        Assert.Equal(typeof(ThaiBahtAmountWords).Assembly.GetName().Name,
            document.Root?.Element("assembly")?.Element("name")?.Value);
        var members = document.Root?.Element("members")?.Elements("member").ToArray();
        Assert.NotNull(members);
        var amount = Assert.Single(members,
            member => member.Attribute("name")?.Value == "T:" + typeof(ThaiBahtAmountWords).FullName);
        Assert.Equal("Formats the displayed receipt amount as Thai baht and satang words.",
            amount.Element("summary")?.Value.Trim());
        var footer = Assert.Single(members,
            member => member.Attribute("name")?.Value.StartsWith(
                "M:Legacy.Maliev.DocumentService.Rendering.QuestDocumentRenderer.ReceiptFooter(",
                StringComparison.Ordinal) == true);
        Assert.Equal("Composes the receipt footer for the selected copy.", footer.Element("summary")?.Value.Trim());
        Assert.Equal(new[] { "container", "receipt", "includeSignature" },
            footer.Elements("param").Select(element => element.Attribute("name")?.Value));
    }
}