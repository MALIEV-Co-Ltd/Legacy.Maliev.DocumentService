# QuestPDF 2026.9.1 independent acceptance

## Frozen scope

Dependabot PR34 head `3d2d13d1b2964cf1fe14162486e818d8589e8ec2`, against
canonical base `0ac95bbbd1b16408ddb989dfe3b7fbe0d348c6c8`. Committed diff is
exactly one PackageReference: QuestPDF2026.9.0 to2026.9.1 in the rendering
project. Existing runtime, API, tests, fonts and baseline bytes were not edited.
The only local follow-up files are this document and the root-approved new
`ConcurrentQuestPdfAcceptanceTests.cs`. No commits/push or external writes.

Official release source: https://github.com/QuestPDF/QuestPDF/releases/tag/2026.9.1
(read2026-10-01). Relevant changes are Skia m154, qpdf12.4.1, concurrent PDF tag
attribute GC corruption repair, and stream-writing exception handling. Context7
was unavailable; documentation lookup used only this official release page.

## Boundary inspection

All five routes remain authenticated POST `/Pdfs/invoice`, `/Pdfs/purchaseorder`,
`/Pdfs/quotation`, `/Pdfs/receipt`, `/Pdfs/orderlabel`. Every action retains
`legacy.documents.render`; there is no forced-live metadata change. Program uses
normal shared JWT authentication, unchanged PascalCase JSON property/dictionary
policies and null omission. Null document yields400; successful rendering uses
`File(byte[], application/pdf)` with unchanged body DTOs. Controller source and
the existing API/JWT contract tests were inspected/executed. This is not a new
normal signed-JWT HTTP or live IAM acceptance claim: existing route tests inspect
metadata. Callers own persistence/storage; no database, Redis, payment, cloud,
upload or infrastructure boundary was introduced.

Renderer remains the singleton QuestDocumentRenderer with embedded Noto Sans and
Noto Sans Thai regular/bold, UseSystemFonts=false and missing-glyph failure=true.
No font download, external font or rendering dependency was added.

## Immutable visual authority

All22 committed iText PDFs remain unchanged:6invoice,7quotation,5receipt,
3purchase-order,1order-label. Baselines Git-tree identity:
`d920bdeac38e410b428b41d522438f7cbc4e6fd4`.
Existing RasterVisualParityTests executes22 mapped variants plus6 dedicated
150-DPI geometry/Thai-tone-mark cases. PDF page counts match; dimensions tolerate
one raster pixel. Whole-page profiles tolerate density0.12 and occupied
width/height0.30; dedicated regions retain density0.035, width0.18, height0.23,
perceptual0.90 with the existing very-close geometry alternative. No tolerance
was changed. These are documented perceptual/geometry comparisons, not pixel or
PDF byte equality. Actual candidate Thai tone-mark crop was directly inspected;
12 legacy/candidate PNG artifacts remain in the owned test output raster-parity
directory. The22 cases rasterize comparisons without exporting all images.

Instruction discrepancy preserved rather than repaired: AGENTS says long
invoice24/quotation12 items, but committed DocumentVariantSmokeTests uses80/80;
receipt44 and purchase-order22 match. Existing fixtures/assertions are unchanged.
This package acceptance does not resolve that pre-existing instruction mismatch.

## Concurrent production renderer proof and limitations

New test starts20 dedicated workers at a shared barrier (four instances of each
of five kinds), while the coordinator performs forced compacting GC/finalizer
collection until completion (at most200 collections), then a bounded60-second
worker join. No timing sleeps, mocks, provider or runtime edits.
Each output has unique BOUNDARY marker plus Thai text, correct literal title,
page count (receipt2, others1) and A4/label dimensions; every foreign marker must
be absent. All20 outputs are independently opened by PdfPig and compared with
same-input sequential content/tagging-state control. Exact PDFs are retained in
owned `bin/Release/net10.0/TestResults/questpdf-concurrency/document-00..19.pdf`.

PdfPig exposes public CatalogDictionary and Structure.GetObject, allowing
resolved structural-role/attribute comparison if StructTreeRoot exists. Parent,
page and index back-links are excluded to avoid cycles; this is not a complete
PDF/UA validator. Actual production candidate catalogs contain only Type/Pages:
**no StructTreeRoot**. Therefore this regression proves concurrent production
PDF integrity/content isolation under GC, NOT the upstream tagged-PDF corruption
repair or full tag-attribute/PDF/UA conformance. No tagged fixture or production
tagging configuration was invented to make that claim. No genuine product RED
or runtime repair was demonstrated. Initial new-test compilation had one
CS1061 (PdfPig dictionary keys are strings); only new test code was corrected,
then rebuilt before execution. This was a test implementation diagnostic.

## Isolated graph and repeatable commands

Owned worktree `B:/maliev-legacy/.worktrees/document-questpdf-2691-20261001`.
Private ignored `.dependencies` clones, exact workflow pins and clean status:

- Defaults `8f4f5f27b226ffe406c4c79b1903742e8c2e7dd3`.
- CompatibilityContracts `78e48ffc4ee000df0510cba5e7c7a3c4c4d539d7`.

The initially prepared TestResults clone directory was moved (resolved paths
verified inside this worktree) to the existing ignored `.dependencies` convention
before tests, because repository source-inventory tests exclude that directory.
No sibling outputs or original/canonical source changes.

Each process sets UseLocalMalievDependencies=true and MalievWorkspaceRoot to the
absolute owned `.dependencies` path. Commands:

```powershell
dotnet build Legacy.Maliev.DocumentService.slnx -c Release -p:UseLocalMalievDependencies=true -p:MalievWorkspaceRoot=$env:MalievWorkspaceRoot -nodeReuse:false
dotnet test Legacy.Maliev.DocumentService.Tests/Legacy.Maliev.DocumentService.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ConcurrentQuestPdfAcceptanceTests|FullyQualifiedName~RasterVisualParityTests|FullyQualifiedName~QuestRendererContractTests|FullyQualifiedName~DocumentVariantSmokeTests|FullyQualifiedName~LegacyBaselineTests|FullyQualifiedName~ApiContractTests' --logger trx --results-directory TestResults/questpdf-2691-final-focus
dotnet test Legacy.Maliev.DocumentService.slnx -c Release --no-build --no-restore --logger trx --results-directory TestResults/questpdf-2691-final-full
dotnet format Legacy.Maliev.DocumentService.slnx --verify-no-changes --no-restore
dotnet list Legacy.Maliev.DocumentService.slnx package --vulnerable --include-transitive --no-restore
git diff --check
gitleaks git . --redact --no-banner
```

## Executed evidence

Sequential build-first baseline: Release0warnings/0errors; original focused43
passed and original full93 passed, zero failures/skips. New concurrency test1
passed. Fresh final Release0warnings/0errors; focused44 passed and full94 passed,
zero failures/skips. Final full duration10seconds. TRX counters were read back.

- Baseline full: `TestResults/questpdf-2691-baseline-full/natth_MALIEV-31USFIV_2026-10-01_13_30_37_net10.0.trx`.
- Final focus: `TestResults/questpdf-2691-final-focus/natth_MALIEV-31USFIV_2026-10-01_13_34_23_net10.0.trx`.
- Final full: `TestResults/questpdf-2691-final-full/natth_MALIEV-31USFIV_2026-10-01_13_34_34_net10.0.trx`.

Final test-only bounded-loop refinement was followed by another fresh Release
0warnings/0errors, focused44 passed and full94 passed, zero skips:

- Frozen focus: `TestResults/questpdf-2691-frozen-focus/natth_MALIEV-31USFIV_2026-10-01_13_37_42_net10.0.trx`.
- Frozen full: `TestResults/questpdf-2691-frozen-full/natth_MALIEV-31USFIV_2026-10-01_13_37_50_net10.0.trx`.

Whole-solution format verification/diff check passed. Transitive audit: all five
solution projects report no vulnerable packages against current NuGet source.
Gitleaks history42commits passed; new-file stdin scan passed. Exact workflow
`73dd7304ffe85ec504389fd7664cc39070b9f148` signing scanner, read from its Git
object, evaluated Test-JwtSigningResourceMaterial across tracked plus new files:
no signing material. Final doc-plus-test scoped stdin scan also passed.

No system fonts/payment/cloud/runtime activation were exercised. Ubuntu/native
platform and independent protected PR acceptance remain root's integration gate;
this local Windows evidence does not replace that gate.

## Independent root acceptance — child 35

Root checked the exact one-package committed diff and inspected the new concurrent
test, immutable baseline identity and unchanged authenticated rendering boundary.
The official release was independently read. Fresh Release build: zero warnings
and errors; focused 44 and unfiltered 94 tests passed, zero failures/skips. Root
TRX files: `TestResults/root-questpdf-focus/2026-10-01_13_40_17_net10.0.trx`
and `TestResults/root-questpdf-full/2026-10-01_13_40_26_net10.0.trx`
(machine prefixes omitted). Whole formatting, five transitive audits and diff
check passed. Private dependency clones remain exact workflow pins; the existing
solution dependency mapping emits their Debug outputs while the affected service
projects build in Release, not an assertion that every external project used
Release. Scoped/staged scans and exact-head/post-main CI remain integration gates.
The concurrent integrity test does not prove tagged-PDF/PDF-UA conformance.
All new generated PDFs/raster outputs and dependency clones remain ignored.
