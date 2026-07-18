# Legacy.Maliev.DocumentService agent guidance

- Preserve the five authenticated `POST /Pdfs/*` routes for invoice, purchase order, quotation,
  receipt, and order-label rendering. Keep PascalCase DTOs, null omission, and `application/pdf`
  byte responses compatible with legacy callers.
- The service is stateless. It owns no database, Redis namespace, Google Cloud Storage bucket,
  upload session, file metadata, or payment workflow. Callers own persistence and storage.
- Every route requires JWT authentication and the `legacy.documents.render` permission. Do not
  weaken controller authorization or move credentials into document payloads.
- QuestPDF is the only production PDF renderer. Do not add iText, PayPal, environment fonts,
  browser rendering, or external font downloads. Keep the embedded Noto Sans and Noto Sans Thai
  fonts and fail on missing glyphs.
- Treat the 22 PDFs under `Legacy.Maliev.DocumentService.Tests/Baselines/legacy-itext` as immutable
  visual and functional oracles. Page counts, margins, tables, totals, signatures, pagination,
  order-label rotation, and Thai tone-mark regions require 150-DPI raster evidence with documented
  perceptual and geometry tolerances; byte-for-byte equality is not required.
- Keep time-dependent output behind `TimeProvider`. Long-document tests must preserve the frozen
  fixture counts: 24 invoice items, 12 quotation items, 44 receipt items across original/copy, and
  22 purchase-order items.
- Use .NET 10, Scalar/OpenAPI, shared legacy service defaults, built-in logging, pinned CI actions,
  and grouped Dependabot updates. The API/storage boundary must remain deployment-independent.
- Existing GKE and `maliev-legacy` only. Do not create infrastructure, deploy to production, write
  cloud storage, mutate secrets/IAM/databases, or access current-generation `Maliev.*` repositories.
- Before a coherent commit, validate release restore/build/test, 150-DPI raster parity, formatting,
  vulnerable packages, gitleaks, and the staged diff. Do not commit generated `TestResults` or
  temporary raster artifacts.
