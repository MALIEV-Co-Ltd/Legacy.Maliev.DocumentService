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
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var evidenceRoot = Environment.GetEnvironmentVariable("VSTestResultsDirectory");
        if (string.IsNullOrWhiteSpace(evidenceRoot))
        {
            evidenceRoot = Path.Combine(AppContext.BaseDirectory, "structural-evidence");
        }
        else
        {
            Assert.Equal(Path.Combine(repositoryRoot, "runner-results"),
                Path.GetFullPath(evidenceRoot).TrimEnd(Path.DirectorySeparatorChar));
        }

        static void AssertUnlinkedOwnedPath(string path, string root)
        {
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var cursor = Path.GetFullPath(path);
            Assert.True(cursor.Equals(prefix, StringComparison.Ordinal)
                || cursor.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            while (cursor.Length >= prefix.Length)
            {
                // LinkTarget also detects dangling symbolic links, which Exists can miss.
                Assert.Null(new FileInfo(cursor).LinkTarget);
                Assert.Null(new DirectoryInfo(cursor).LinkTarget);
                if (File.Exists(cursor) || Directory.Exists(cursor))
                {
                    Assert.Equal(0, (int)(File.GetAttributes(cursor) & FileAttributes.ReparsePoint));
                }
                if (cursor.Equals(prefix, StringComparison.Ordinal))
                {
                    break;
                }
                cursor = Path.GetDirectoryName(cursor)!;
            }
        }

        AssertUnlinkedOwnedPath(evidenceRoot, repositoryRoot);
        Directory.CreateDirectory(evidenceRoot);
        AssertUnlinkedOwnedPath(evidenceRoot, repositoryRoot);
        long readBytes = 0;
        byte[] ReadBoundedBytes(string path)
        {
            const long fileLimit = 64L * 1024 * 1024;
            const long aggregateLimit = 256L * 1024 * 1024;
            using var stream = File.OpenRead(path);
            var length = stream.Length;
            Assert.InRange(length, 1, fileLimit);
            readBytes = checked(readBytes + length);
            Assert.InRange(readBytes, 1, aggregateLimit);
            var bytes = new byte[checked((int)length)];
            stream.ReadExactly(bytes);
            Assert.Equal(-1, stream.ReadByte());
            return bytes;
        }

        (string Sha256, int Bytes) CaptureFile(string sourcePath, string outputName)
        {
            AssertUnlinkedOwnedPath(sourcePath, repositoryRoot);
            var outputPath = Path.Combine(evidenceRoot, outputName);
            AssertUnlinkedOwnedPath(outputPath, repositoryRoot);
            Assert.False(File.Exists(outputPath));
            var beforeBytes = ReadBoundedBytes(sourcePath);
            var beforeHash = Convert.ToHexString(SHA256.HashData(beforeBytes));
            using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(beforeBytes);
            }
            AssertUnlinkedOwnedPath(outputPath, repositoryRoot);
            var copiedHash = Convert.ToHexString(SHA256.HashData(ReadBoundedBytes(outputPath)));
            AssertUnlinkedOwnedPath(sourcePath, repositoryRoot);
            var afterHash = Convert.ToHexString(SHA256.HashData(ReadBoundedBytes(sourcePath)));
            Assert.Equal(beforeHash, copiedHash);
            Assert.Equal(beforeHash, afterHash);
            return (copiedHash, beforeBytes.Length);
        }

        void WriteOwnedReceipt(object receipt)
        {
            var receiptPath = Path.Combine(evidenceRoot, "document-runtime-assembly-identity.json");
            AssertUnlinkedOwnedPath(receiptPath, repositoryRoot);
            Assert.False(File.Exists(receiptPath));
            using (var output = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
            }
            AssertUnlinkedOwnedPath(receiptPath, repositoryRoot);
        }

        // Coverlet can instrument files during this observation and restore them afterwards.
        // Preserve observed bytes separately; never assume these hashes equal pristine build output.
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Legacy.Maliev.DocumentService.Application.dll"), application.Location);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Legacy.Maliev.DocumentService.Api.dll"), api.Location);
        var applicationSnapshot = CaptureFile(application.Location, "document-runtime-application.dll");
        var applicationPdbSnapshot = CaptureFile(Path.ChangeExtension(application.Location, ".pdb"), "document-runtime-application.pdb");
        var apiSnapshot = CaptureFile(api.Location, "document-runtime-api.dll");
        var apiPdbSnapshot = CaptureFile(Path.ChangeExtension(api.Location, ".pdb"), "document-runtime-api.pdb");
        var evidence = new
        {
            CoveragePolicyActive = false,
            Observation = "During test execution; potentially collector-instrumented bytes",
            Application = new
            {
                application.Location,
                Sha256 = applicationSnapshot.Sha256,
                applicationSnapshot.Bytes,
                Mvid = application.ManifestModule.ModuleVersionId,
                PdbSha256 = applicationPdbSnapshot.Sha256,
                PdbBytes = applicationPdbSnapshot.Bytes,
            },
            Api = new
            {
                api.Location,
                Sha256 = apiSnapshot.Sha256,
                apiSnapshot.Bytes,
                Mvid = api.ManifestModule.ModuleVersionId,
                PdbSha256 = apiPdbSnapshot.Sha256,
                PdbBytes = apiPdbSnapshot.Bytes,
            },
            SnapshotSourceCopyAfterSha256Verified = true,
            SnapshotAggregateReadBytes = readBytes,
            PublicMethods = methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
        };
        WriteOwnedReceipt(evidence);
    }
}
