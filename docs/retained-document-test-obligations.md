# Retained Document test obligations

The frozen private checkpoint `135e526d0dab85c415b3afdcefd7b70fe2c82e2f`
contains 36 active methods across five PDF fixture classes and nine amount-word
groups. The commented duplicate receipt method is not an active test.

The current suite retains all 81 exact Thai amount-word input/output pairs,
22 immutable PDF oracles with mapped 150-DPI raster checks, actual authenticated
HTTP null-input rejection, bilingual PDF geometry/content checks, and the
description-only quotation behavior. Fixtures are adapted to QuestPDF with
embedded Noto fonts and fixed time; historical fixture bytes and arbitrary
customer-specific inputs are not claimed identical.

One source distinction was not covered by the raster adaptation: the historical
without-withholding fixtures explicitly supplied `0.00`, while their current
raster variants supply `null`. `DocumentZeroWithholdingHttpTests` exercises both
literal JSON values through the existing Production test host and real renderer
for invoice, quotation and receipt. Zero remains a displayed financial value;
unknown withholding omits the row. Both receipt copies are checked. No production
code, DTO, monetary calculation, baseline PDF or asset is changed.

The existing SCB invoice-footer test now also checks the Thai bank and recipient
names. The older private combined bilingual wording is adapted to separate
current English and Thai footer columns rather than copied verbatim.

Hosted Release build, formatting, full suite and unchanged raster/receipt evidence
must pass at the reviewed candidate and fresh main before acceptance. No local
SDK or PDF runtime execution substitutes for that evidence. Per-path ownership
and whole-source dispositions remain separately reviewed.
