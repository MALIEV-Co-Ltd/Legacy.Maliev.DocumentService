# Receipt amount branding adaptation

The legacy receipt generator at source checkpoint `135e526d0dab85c415b3afdcefd7b70fe2c82e2f`
(`Maliev.PdfService.Pdf/Services/ReceiptGenerator.cs`, blob `17df6b26f88a3082902212c39cf21136a45d5961`,
lines 549–575) fills the amount-in-words row with repeated gray `MALIEV` lettering whenever
the currency is ordinal `THB`. Original and copy use the same generator path. This is a
branding background; no authenticity, security or tamper-resistance promise is made.

The modern receipt retains its centered Thai amount words and existing row height, margins,
font style and pagination. A background SVG layer is clipped to that row's actual dimensions.
The static six-letter artwork uses outlines from the existing embedded Noto Sans Regular
font (SHA-256 `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5`).
No environment fonts, runtime glyph conversion, font download or production dependency is added.
The resource retains its font copyright attribution. The original/copy route, DTOs,
authorization, byte response and numeric totals remain unchanged.

Source geometry is 4-point letters, a 2-point horizontal gap, 5-point row pitch, a 60-point
vertical tile and a horizontal stagger of one twelfth of the tile width per row. The source
Sarabun word advance is 13.55 points, so its horizontal tile is 15.55 points. The bundled
Noto Sans word advance is 14.26 points, making this adaptation's tile 16.26 points (a 0.71-point
increase). The letters are not compressed. SVG's downward vertical axis sets the top of the
amount band as the repeat origin; this changes the phase/orientation of the stagger from the
source PDF's upward axis. Exact old glyph shape, phase and pixels are not claimed.

The immutable source oracle `receipt-without-withholding-tax-unittest.pdf` has SHA-256
`823b1c490a0b0eb045833cea7417613d5cceab7c820b5d80a53aeebea1328f19` and shows the repeated
gray words behind the 5.99 THB amount on both pages. Its pattern has a 15.55 × 60-point tile,
24 glyph shows and RGB 190. It is retained unchanged, as are all 22 existing baseline PDFs
and their established tolerances. New synthetic PDF/PNG artifacts use the separate
`TestResults/questpdf/receipt-brand-pattern-v1` directory, never the immutable oracle folder.

The eight new focused cases cover embedded resource provenance, actual rendered one-item
5.99 THB and 44-item receipts, both final copy pages, and five non-THB currency values.
At 150 DPI, two 80-point empty horizontal regions within the amount glyph height must contain
1.5–40% gray pixels (channel values 150–210), while the centered Thai foreground retains more
than 30 pixels below channel 80. Empty left-margin strips outside the amount band and above
non-THB remarks must contain no gray pixels. Center tolerance remains ±3 points around the
actual asymmetric A4 content center; Thai text must lie below Amount Received and above
Remark. These explicit new-fixture checks detect a missing or leaking background without
loosening existing golden tolerances. The new fixture also compares gray density against
both immutable 5.99 oracle pages:
the absolute occupancy difference is at most 15 percentage points in empty side regions,
allowing the stated font/pitch/phase adaptation while separately requiring visible gray
lettering in each image. This is a new pattern-specific tolerance, not a golden relaxation.
Native render/crop inspection is still required to
validate the chosen sampling regions and visible lettering; synthetic evidence-reader
fixtures are not PDF execution evidence.

The independent receipt evidence lane retains all 141 prior cases, including the 32 actual HTTP cases and adds the exact eight
class/method cases to its 149-case inventory and full-suite membership checks. The full lane
requires 243 cases: the predecessor's actual suite count of 235 plus eight new cases, so a
focused-only TRX cannot masquerade as a full suite. This checks cardinality and focused case
identities; it does not independently freeze every one of the prior 235 case identities.
The unfiltered native full-suite invocation remains necessary. All sixteen execution counters
must agree with passed results, including zero failed, unexecuted, pending and other outcomes.
It uploads
actual PDF/PNG, TRX and generated-inclusive raw coverage. Four production assembly floors
remain 80% each with no exclusions; an empty denominator remains unavailable and cannot be
accepted as 100%. The scoped compiled contract-only applicability decision now uses
the separately reviewed policy and fresh compiled proof; it never converts Application
0/0 into a numerical pass. The five served examples and typed receipt field examples
passed native head6191a98 with generated-inclusive API351/438; each future accepted
head still requires fresh compiled proof, tests and all three executable raw floors.
