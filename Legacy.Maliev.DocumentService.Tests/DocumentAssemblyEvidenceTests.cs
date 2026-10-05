using System.Security.Cryptography;
using System.Text.Json;
using Legacy.Maliev.DocumentService.Application;

namespace Legacy.Maliev.DocumentService.Tests;

// Records the real assemblies loaded by the same full-suite test process.
// Evidence collection does not change coverage applicability or invoke generated helpers.
public sealed class DocumentAssemblyEvidenceTests
{
    [Fact]
    public void RuntimeApplicationContract_PreservesAbstractPublicRenderingSurface()
    {
        var application = typeof(IDocumentRenderer).Assembly;
        var contract = Assert.Single(application.GetExportedTypes());
        Assert.Equal(typeof(IDocumentRenderer), contract);
        Assert.True(contract.IsInterface);
        var methods = contract.GetMethods();
        Assert.Equal(new[] { "RenderInvoice", "RenderOrderLabel", "RenderPurchaseOrder", "RenderQuotation", "RenderReceipt" },
            methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.All(methods, method => Assert.True(method.IsAbstract));

        var api = typeof(Program).Assembly;
        var evidenceRoot = Environment.GetEnvironmentVariable("VSTestResultsDirectory");
        if (string.IsNullOrWhiteSpace(evidenceRoot))
        {
            evidenceRoot = Path.Combine(AppContext.BaseDirectory, "structural-evidence");
        }
        Directory.CreateDirectory(evidenceRoot);
        // Coverlet can instrument files during this observation and restore them afterwards.
        // Preserve observed bytes separately; never assume these hashes equal pristine build output.
        var applicationBytes = File.ReadAllBytes(application.Location);
        var apiBytes = File.ReadAllBytes(api.Location);
        File.WriteAllBytes(Path.Combine(evidenceRoot, "document-runtime-application.dll"), applicationBytes);
        File.WriteAllBytes(Path.Combine(evidenceRoot, "document-runtime-api.dll"), apiBytes);
        var evidence = new
        {
            CoveragePolicyActive = false,
            Observation = "During test execution; potentially collector-instrumented bytes",
            Application = new
            {
                application.Location,
                Sha256 = Convert.ToHexString(SHA256.HashData(applicationBytes)),
                Mvid = application.ManifestModule.ModuleVersionId,
            },
            Api = new
            {
                api.Location,
                Sha256 = Convert.ToHexString(SHA256.HashData(apiBytes)),
                Mvid = api.ManifestModule.ModuleVersionId,
            },
            PublicMethods = methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
        };
        File.WriteAllText(Path.Combine(evidenceRoot, "document-runtime-assembly-identity.json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }
}
