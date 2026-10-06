"""Read actual runner files. Observation is separate from four-assembly acceptance."""
import hashlib
import json
import os
from collections import Counter
from pathlib import Path
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
cases = {}
errors = []
repository = Path(__file__).resolve().parent.parent
actual_head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip()
expected_head = os.environ.get("DOCUMENT_SOURCE_SHA", "")
if not re.fullmatch(r"[0-9a-f]{40}", expected_head) or actual_head != expected_head:
    errors.append("Actual git HEAD does not match DOCUMENT_SOURCE_SHA")
expected_focus = {
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "QuotationRows_RenderDescriptionWithoutAddingDistinctName"): 2,
    ("Legacy.Maliev.DocumentService.Tests.ThaiBahtAmountWordsTests", "RetainedSourceValues_PreserveThaiWords"): 81,
    ("Legacy.Maliev.DocumentService.Tests.ThaiBahtAmountWordsTests", "DecimalEdges_HaveExplicitDisplayWords"): 17,
    ("Legacy.Maliev.DocumentService.Tests.ThaiBahtAmountWordsTests", "DecimalExtremes_RetainEveryMillionGroupAndSign"): 1,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptThaiAmountContentTests", "ThbReceipt_UsesAmountPaidInBothCopiesWithCenteredVisibleWords"): 3,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptThaiAmountContentTests", "OtherCurrencies_OmitThaiAmountRow"): 5,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptThaiAmountContentTests", "LongReceipt_Preserves44ItemsAndOneAmountRowAtEndOfEachCopy"): 1,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptThaiAmountContentTests", "LegacyThbOracle_AndExplicitNewFixtureRetainFiveBahtNinetyNineSatang"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ActualRoutes_BindPascalCaseJsonAndReturnRealPdfBytes"): 5,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "InvalidBodies_AreRejectedByActualAdmissionWithoutPdf"): 15,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ActualJwtAndPermissionAdmission_PreventsReceiptRendering"): 6,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ReceiptQuantity_RejectsInvalidIntegerWireValue"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "DevelopmentMetadata_DescribesAllFiveActualPostRoutesAndPdfResponses"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ServedOpenApiExample_RendersThroughNormalAuthenticatedProductionRoute"): 5,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ProductionMetadata_IsNotPubliclyExposed"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ReceiptGet_DoesNotInvokePostRenderer"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ActualRendererFailure_ReturnsOpaqueProductionErrorWithoutPdf"): 1,
    ("Legacy.Maliev.DocumentService.Tests.DocumentRuntimeHttpTests", "ProductionRegistration_UsesActualSingletonRendererAndSystemTimeProvider"): 1,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptAmountBrandPatternTests", "EmbeddedArtwork_RetainsSixOrderedLettersFromTheBundledFont"): 1,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptAmountBrandPatternTests", "ThbReceipt_HasVisibleClippedBrandingOnTheFinalPageOfBothCopies"): 2,
    ("Legacy.Maliev.DocumentService.Tests.ReceiptAmountBrandPatternTests", "OtherCurrencies_OmitAmountWordsAndGrayBrandBand"): 5,
}
all_definitions = {}
for lane, name in (("focus", "receipt-focus.trx"), ("full", "full-suite.trx")):
    files = list((root / lane).rglob(name))
    if len(files) != 1:
        errors.append(f"Expected one {lane} TRX, observed {len(files)}")
        continue
    tree = ET.parse(files[0])
    definitions = {}
    execution_definitions = {}
    for definition in tree.findall(".//t:TestDefinitions/t:UnitTest", namespace):
        method = definition.find("t:TestMethod", namespace)
        try:
            test_id = str(uuid.UUID(definition.attrib["id"]))
            identity = (method.attrib["className"], method.attrib["name"])
            if not re.fullmatch(r"Legacy\.Maliev\.DocumentService\.Tests\.[A-Za-z0-9_.+]+", identity[0]) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", identity[1]):
                raise ValueError("Unsafe definition")
            if (test_id in definitions and definitions[test_id] != identity) or (test_id in all_definitions and all_definitions[test_id] != identity):
                raise ValueError("Conflicting definition")
            definitions[test_id] = identity
            all_definitions[test_id] = identity
            execution = definition.find("t:Execution", namespace)
            execution_id = str(uuid.UUID(execution.attrib["id"]))
            if execution_id in execution_definitions and execution_definitions[execution_id] != test_id:
                raise ValueError("Conflicting execution definition")
            execution_definitions[execution_id] = test_id
        except (ValueError, KeyError, AttributeError):
            errors.append(f"{lane} TRX contains invalid or conflicting test definitions")
    results = tree.findall(".//t:UnitTestResult", namespace)
    cases[lane] = []
    executions = set()
    for result in results:
        try:
            execution_id = str(uuid.UUID(result.attrib["executionId"]))
            test_id = str(uuid.UUID(result.attrib["testId"]))
            if execution_id in executions:
                raise ValueError("Duplicate execution")
            executions.add(execution_id)
            if execution_definitions.get(execution_id) != test_id:
                raise ValueError("Execution does not match its definition")
            cls, method = definitions[test_id]
            outcome = result.attrib["outcome"]
            if outcome not in {"Passed", "Failed", "NotExecuted"}:
                raise ValueError("Unknown outcome")
            cases[lane].append({"class": cls, "method": method, "test_id": test_id,
                                "execution_id": execution_id, "outcome": outcome})
        except (ValueError, KeyError):
            errors.append(f"{lane} TRX contains an invalid, duplicate or unmapped execution")
    summaries = tree.findall(".//t:ResultSummary", namespace)
    summary = summaries[0] if len(summaries) == 1 else None
    summary_counters = summary.findall("t:Counters", namespace) if summary is not None else []
    counters = summary_counters[0] if len(summary_counters) == 1 else None
    if (summary is None or summary.attrib.get("outcome") != "Completed" or
            len(tree.findall("./t:ResultSummary", namespace)) != 1 or
            len(summary_counters) != 1 or len(tree.findall(".//t:Counters", namespace)) != 1):
        errors.append(f"{lane} TRX requires exactly one Completed ResultSummary and one Counters")
    expected_counters = {"total": len(results), "executed": len(results), "passed": len(results)}
    expected_counters.update({name: 0 for name in ("failed", "error", "timeout", "aborted", "inconclusive",
        "passedButRunAborted", "notRunnable", "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending")})
    if counters is None or counters.attrib != {name: str(value) for name, value in expected_counters.items()}:
        errors.append(f"{lane} TRX counters do not reconcile with passed cases")
    if not results or any(result.attrib["outcome"] != "Passed" for result in results):
        errors.append(f"{lane} TRX has empty or unsuccessful results")

focus = cases.get("focus", [])
if Counter((case["class"], case["method"]) for case in focus) != Counter(expected_focus):
    errors.append("Focused inventory differs from the exact 156 allowlisted class/method cases")
full_inventory = Counter((case["test_id"], case["class"], case["method"]) for case in cases.get("full", []))
focus_inventory = Counter((case["test_id"], case["class"], case["method"]) for case in focus)
if focus_inventory - full_inventory:
    errors.append("Full suite does not retain every focused execution-definition identity")
expected_full = Counter({(row["className"], row["method"]): row["executions"]
                         for row in json.loads((repository / "docs/document-full-test-inventory.json").read_text())})
if Counter((case["class"], case["method"]) for case in cases.get("full", [])) != expected_full:
    errors.append("Full suite differs from the exact 361 merged HTTP, raster, receipt and evidence cases")

reports = list((root / "full").rglob("coverage.cobertura.xml"))
digests = {hashlib.sha256(report.read_bytes()).hexdigest() for report in reports}
assemblies = []
if len(digests) != 1:
    errors.append(f"Expected one unique full-suite raw coverage report, observed {len(digests)}")
else:
    packages = ET.parse(reports[0]).findall("./packages/package")
    for suffix in ("Api", "Application", "Domain", "Rendering"):
        name = f"Legacy.Maliev.DocumentService.{suffix}"
        inventory = {}
        for package in packages:
            if package.get("name") != name:
                continue
            for cls in package.findall("./classes/class"):
                for line in cls.findall("./lines/line"):
                    key = (cls.attrib["filename"], int(line.attrib["number"]))
                    inventory[key] = inventory.get(key, False) or int(line.attrib["hits"]) > 0
        valid = len(inventory)
        covered = sum(inventory.values())
        assemblies.append({"assembly": name, "covered": covered, "valid": valid,
                           "percent": covered * 100 / valid if valid else None,
                           "meets_80_percent": valid > 0 and covered * 100 >= valid * 80})

summary = {"head_sha": actual_head, "expected_head_sha": expected_head, "run_id": os.environ.get("GITHUB_RUN_ID"),
           "raw_sha256": next(iter(digests)) if len(digests) == 1 else None,
           "exclusions": [], "assemblies": assemblies, "cases": cases, "errors": errors,
           "four_assembly_acceptance": len(assemblies) == 4 and all(item["meets_80_percent"] for item in assemblies),
           "note": "This captures observations only. A zero denominator is unavailable, never 100%; no applicability waiver is granted."}
(root / "receipt-evidence.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"assemblies": assemblies, "case_counts": {lane: len(items) for lane, items in cases.items()}, "errors": errors}, indent=2))
raise SystemExit(1 if errors else 0)
