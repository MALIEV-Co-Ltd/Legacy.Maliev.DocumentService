# Legacy.Maliev.DocumentService

Public, sanitized .NET 10 extraction of the private legacy PDF service. It preserves the five
authenticated legacy routes under `/Pdfs` while replacing iText 7 with QuestPDF 2026 and embedded
Noto Sans and Noto Sans Thai fonts for correct Thai tone-mark shaping.

The service is stateless. It does not own a database, Redis namespace, Google Cloud Storage bucket,
or CloudNativePG cluster. Callers continue to own storage and pass the legacy DTOs; responses remain
`application/pdf` bytes. This boundary avoids duplicate file metadata and cross-service database
access.

## API contract

- `POST /Pdfs/invoice`
- `POST /Pdfs/purchaseorder`
- `POST /Pdfs/quotation`
- `POST /Pdfs/receipt`
- `POST /Pdfs/orderlabel`

All routes require JWT authentication and the `legacy.documents.render` permission. Scalar exposes
the OpenAPI document through the shared MALIEV service defaults. JSON retains PascalCase property
names and omits null response properties.

## Parity and intentional changes

The test project keeps 22 immutable iText baseline PDFs and a SHA-256/page/text manifest. Renderer
tests verify legacy page geometry, document sections, calculations, two-copy receipts, deterministic
clock behavior, and Thai shaping. The runtime projects contain no iText dependency.

PayPal content is intentionally removed. The invoice keeps bank-transfer information and layout
space without advertising or executing PayPal. Payment provider execution remains outside this
service; future Omise/Opn work belongs to the separate new `Maliev.PaymentService`.

QuestPDF licensing and the Noto SIL OFL notice are documented in `THIRD-PARTY-NOTICES.md`. This
repository is MIT licensed and uses QuestPDF under its Community open-source eligibility category.

## Deployment boundary

Extraction does not deploy. The eventual workload must use the existing GKE cluster and
`maliev-legacy` namespace, with existing Workload Identity and cluster resources only. It requires no
new node pool, Cloud SQL instance, PostgreSQL database, Redis allocation, or paid GitHub feature.
Deployment remains gated on full visual parity evidence, authenticated workflow tests, malware/tag
remediation evidence for the web deployment, GitOps manifests, resource limits, rollback evidence,
and the consolidated `maliev-legacy-secrets` integration where secrets are actually required.

## Validate

```powershell
dotnet build Legacy.Maliev.DocumentService.slnx -c Release
dotnet test Legacy.Maliev.DocumentService.slnx -c Release --no-build
dotnet format Legacy.Maliev.DocumentService.slnx --verify-no-changes --no-restore
dotnet list Legacy.Maliev.DocumentService.slnx package --vulnerable --include-transitive
gitleaks git . --redact=100 --exit-code 1 --no-banner --no-color
```
