using Legacy.Maliev.DocumentService.Rendering;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;
using Invoice = Legacy.Maliev.DocumentService.Domain.Invoice.Invoice;
using OrderLabel = Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel;
using PurchaseOrder = Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder;
using Quotation = Legacy.Maliev.DocumentService.Domain.Quotations.Quotation;
using Receipt = Legacy.Maliev.DocumentService.Domain.Receipt.Receipt;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class ConcurrentQuestPdfAcceptanceTests
{
    [Fact]
    public async Task FiveDocumentKinds_ConcurrentGenerationUnderCollection_PreservesIsolatedContentAndCatalogTaggingState()
    {
        const int count = 20;
        var oracle = Enumerable.Range(0, count).Select(index => Inspect(Render(index), index % 5)).ToArray();
        using var start = new Barrier(count + 1);
        using var complete = new CountdownEvent(count);
        var output = Path.Combine(AppContext.BaseDirectory, "TestResults", "questpdf-concurrency");
        Directory.CreateDirectory(output);
        var workers = Enumerable.Range(0, count).Select(index => Task.Factory.StartNew(() =>
        {
            try
            {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(60)));
                var bytes = Render(index);
                File.WriteAllBytes(Path.Combine(output, $"document-{index:D2}.pdf"), bytes);
                return Inspect(bytes, index % 5);
            }
            finally
            {
                complete.Signal();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(60)));
        var collections = 0;
        do
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            collections++;
        } while (!complete.IsSet && collections < 200);

        var results = await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(count, results.Length);
        Assert.True(collections > 0);
        for (var index = 0; index < count; index++)
        {
            var actual = results[index];
            Assert.Equal(index % 5 == 2 ? 2 : 1, actual.Pages);
            Assert.Contains($"BOUNDARY-{index:D2}", actual.Text, StringComparison.Ordinal);
            Assert.Contains("ทดสอบ", actual.Text, StringComparison.Ordinal);
            Assert.Contains((index % 5) switch
            {
                0 => "INVOICE",
                1 => "QUOTATION",
                2 => "RECEIPT",
                3 => "PURCHASE ORDER",
                _ => "BOUNDARY",
            }, actual.Text, StringComparison.Ordinal);
            Assert.Equal(oracle[index].Tags, actual.Tags);
            Assert.Equal(oracle[index].Text, actual.Text);
            foreach (var other in Enumerable.Range(0, count).Where(other => other != index))
                Assert.DoesNotContain($"BOUNDARY-{other:D2}", actual.Text, StringComparison.Ordinal);
        }
    }

    private static byte[] Render(int index)
    {
        var marker = $"BOUNDARY-{index:D2} ทดสอบ";
        var renderer = new QuestDocumentRenderer(new FixedTimeProvider());
        return (index % 5) switch
        {
            0 => renderer.RenderInvoice(new Invoice { Remark = marker }),
            1 => renderer.RenderQuotation(new Quotation { Comment = marker }),
            2 => renderer.RenderReceipt(new Receipt { Remark = marker }),
            3 => renderer.RenderPurchaseOrder(new PurchaseOrder { Notes = marker }),
            _ => renderer.RenderOrderLabel(new OrderLabel { Id = index.ToString(), Name = marker }),
        };
    }

    private static Snapshot Inspect(byte[] bytes, int kind)
    {
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var document = PdfDocument.Open(bytes);
        var page = document.GetPage(1);
        var label = kind == 4;
        Assert.Equal(label ? 216 : 595, page.Width, precision: 0);
        Assert.Equal(label ? 288 : 842, page.Height, precision: 0);
        var catalog = document.Structure.Catalog.CatalogDictionary;
        var tags = catalog.Data.TryGetValue(NameToken.Create("StructTreeRoot"), out var root)
            ? CanonicalTag(document, root, 0)
            : "untagged";
        return new Snapshot(document.NumberOfPages,
            string.Join(' ', document.GetPages().Select(item => item.Text)), tags);
    }

    // Inspect public raw PDF structure, resolving object numbers rather than comparing them.
    // Parent/page/tree-index links are excluded: they are not tag roles/attributes and form cycles.
    private static string CanonicalTag(PdfDocument document, IToken token, int depth)
    {
        Assert.InRange(depth, 0, 100);
        if (token is IndirectReferenceToken reference)
            return CanonicalTag(document, document.Structure.GetObject(reference.Data).Data, depth + 1);
        if (token is ArrayToken array)
            return "[" + string.Join(',', array.Data.Select(item => CanonicalTag(document, item, depth + 1))) + "]";
        if (token is DictionaryToken dictionary)
            return "{" + string.Join(',', dictionary.Data
                .Where(item => item.Key is not ("P" or "Pg" or "ParentTree" or "ParentTreeNextKey" or "IDTree"))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => item.Key + ":" + CanonicalTag(document, item.Value, depth + 1))) + "}";
        return token.ToString() ?? throw new InvalidOperationException("Missing PDF token representation.");
    }

    private sealed record Snapshot(int Pages, string Text, string Tags);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
