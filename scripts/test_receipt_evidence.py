"""Synthetic reader fixtures verify rejection and arithmetic, not renderer execution."""
from pathlib import Path
import json
import os
import subprocess
import sys
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET


class ReceiptEvidenceReaderTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        methods = [
            ("ThaiBahtAmountWordsTests", "RetainedSourceValues_PreserveThaiWords", 81),
            ("ThaiBahtAmountWordsTests", "DecimalEdges_HaveExplicitDisplayWords", 17),
            ("ThaiBahtAmountWordsTests", "DecimalExtremes_RetainEveryMillionGroupAndSign", 1),
            ("ReceiptThaiAmountContentTests", "ThbReceipt_UsesAmountPaidInBothCopiesWithCenteredVisibleWords", 3),
            ("ReceiptThaiAmountContentTests", "OtherCurrencies_OmitThaiAmountRow", 5),
            ("ReceiptThaiAmountContentTests", "LongReceipt_Preserves44ItemsAndOneAmountRowAtEndOfEachCopy", 1),
            ("ReceiptThaiAmountContentTests", "LegacyThbOracle_AndExplicitNewFixtureRetainFiveBahtNinetyNineSatang", 1),
            ("DocumentRuntimeHttpTests", "ActualRoutes_BindPascalCaseJsonAndReturnRealPdfBytes", 5),
            ("DocumentRuntimeHttpTests", "InvalidBodies_AreRejectedByActualAdmissionWithoutPdf", 15),
            ("DocumentRuntimeHttpTests", "ActualJwtAndPermissionAdmission_PreventsReceiptRendering", 6),
            ("DocumentRuntimeHttpTests", "ReceiptQuantity_RejectsInvalidIntegerWireValue", 1),
            ("DocumentRuntimeHttpTests", "DevelopmentMetadata_DescribesAllFiveActualPostRoutesAndPdfResponses", 1),
            ("DocumentRuntimeHttpTests", "ServedOpenApiExample_RendersThroughNormalAuthenticatedProductionRoute", 5),
            ("DocumentRuntimeHttpTests", "ProductionMetadata_IsNotPubliclyExposed", 1),
            ("DocumentRuntimeHttpTests", "ReceiptGet_DoesNotInvokePostRenderer", 1),
            ("DocumentRuntimeHttpTests", "ActualRendererFailure_ReturnsOpaqueProductionErrorWithoutPdf", 1),
            ("DocumentRuntimeHttpTests", "ProductionRegistration_UsesActualSingletonRendererAndSystemTimeProvider", 1),
            ("ReceiptAmountBrandPatternTests", "EmbeddedArtwork_RetainsSixOrderedLettersFromTheBundledFont", 1),
            ("ReceiptAmountBrandPatternTests", "ThbReceipt_HasVisibleClippedBrandingOnTheFinalPageOfBothCopies", 2),
            ("ReceiptAmountBrandPatternTests", "OtherCurrencies_OmitAmountWordsAndGrayBrandBand", 5),
        ]
        self.names = [(f"Legacy.Maliev.DocumentService.Tests.{cls}", method, str(index))
                      for cls, method, count in methods for index in range(count)]
        self.trx("focus", "receipt-focus.trx", self.names)
        self.full_names = self.names + [("Legacy.Maliev.DocumentService.Tests.InheritedSuiteFixture", "RetainedCase", str(index))
                                       for index in range(94)]
        self.trx("full", "full-suite.trx", self.full_names)
        self.coverage(application_lines=0)

    def trx(self, lane, filename, names, failed=False):
        namespace = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
        document = ET.Element(namespace + "TestRun")
        results = ET.SubElement(document, namespace + "Results")
        definitions = ET.SubElement(document, namespace + "TestDefinitions")
        for index, name in enumerate(names):
            cls, method, ordinal = name
            test_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, ".".join(name)))
            execution_id = str(uuid.uuid4())
            ET.SubElement(results, namespace + "UnitTestResult", testName="PRIVATE DISPLAY PARAMETER",
                          testId=test_id, executionId=execution_id,
                          outcome="Failed" if failed and index == 0 else "Passed")
            definition = ET.SubElement(definitions, namespace + "UnitTest", id=test_id)
            ET.SubElement(definition, namespace + "Execution", id=execution_id)
            ET.SubElement(definition, namespace + "TestMethod", className=cls, name=method)
        summary = ET.SubElement(document, namespace + "ResultSummary")
        counters = {"total": str(len(names)), "executed": str(len(names)), "passed": str(len(names) - int(failed))}
        counters.update({name: "0" for name in ("failed", "error", "timeout", "aborted", "inconclusive",
            "passedButRunAborted", "notRunnable", "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending")})
        counters["failed"] = str(int(failed))
        ET.SubElement(summary, namespace + "Counters", **counters)
        directory = self.root / lane
        directory.mkdir(exist_ok=True)
        ET.ElementTree(document).write(directory / filename, encoding="utf-8")

    def coverage(self, application_lines):
        document = ET.Element("coverage")
        packages = ET.SubElement(document, "packages")
        for suffix in ("Api", "Application", "Domain", "Rendering"):
            package = ET.SubElement(packages, "package", name=f"Legacy.Maliev.DocumentService.{suffix}")
            classes = ET.SubElement(package, "classes")
            cls = ET.SubElement(classes, "class", filename=f"{suffix}.cs")
            lines = ET.SubElement(cls, "lines")
            for number in range(application_lines if suffix == "Application" else 10):
                ET.SubElement(lines, "line", number=str(number + 1), hits="1")
        ET.ElementTree(document).write(self.root / "full" / "coverage.cobertura.xml", encoding="utf-8")

    def read(self, expected_head=None):
        repository = Path(__file__).resolve().parent.parent
        actual_head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip()
        environment = dict(os.environ, DOCUMENT_SOURCE_SHA=actual_head if expected_head is None else expected_head)
        result = subprocess.run([sys.executable, "-B", str(Path(__file__).with_name("read-receipt-evidence.py")), str(self.root)],
                                capture_output=True, text=True, env=environment)
        return result.returncode, json.loads((self.root / "receipt-evidence.json").read_text(encoding="utf-8"))

    def test_zero_denominator_is_unavailable_without_four_assembly_acceptance(self):
        code, result = self.read()
        self.assertEqual(0, code)
        application = next(item for item in result["assemblies"] if item["assembly"].endswith(".Application"))
        self.assertIsNone(application["percent"])
        self.assertFalse(application["meets_80_percent"])
        self.assertFalse(result["four_assembly_acceptance"])
        self.assertEqual([], result["exclusions"])

    def test_nonempty_four_assembly_coverage_is_reconciled(self):
        self.coverage(application_lines=10)
        code, result = self.read()
        self.assertEqual(0, code)
        self.assertTrue(result["four_assembly_acceptance"])

    def test_missing_raw_report_fails_closed(self):
        (self.root / "full" / "coverage.cobertura.xml").unlink()
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertTrue(result["errors"])

    def test_failed_focused_case_fails_closed(self):
        self.trx("focus", "receipt-focus.trx", self.names, failed=True)
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertTrue(result["errors"])

    def test_full_suite_missing_focused_case_fails_closed(self):
        self.trx("full", "full-suite.trx", self.names[:-1])
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("Full suite does not retain every focused execution-definition identity", result["errors"])

    def mutate_focus(self, mutation):
        path = self.root / "focus" / "receipt-focus.trx"
        tree = ET.parse(path)
        mutation(tree)
        tree.write(path, encoding="utf-8")

    def test_wrong_head_fails_closed(self):
        code, result = self.read(expected_head="0" * 40)
        self.assertEqual(1, code)
        self.assertIn("Actual git HEAD does not match DOCUMENT_SOURCE_SHA", result["errors"])

    def test_partial_full_suite_and_contradictory_counters_fail_closed(self):
        self.trx("full", "full-suite.trx", self.names)
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("Full suite differs from 248 expected cases: prior suite count 243 plus five served-example HTTP cases", result["errors"])
        self.trx("full", "full-suite.trx", self.full_names)
        for name in ("executed", "failed", "notExecuted", "warning"):
            with self.subTest(counter=name):
                self.trx("focus", "receipt-focus.trx", self.names)
                self.mutate_focus(lambda tree: tree.find(".//{*}Counters").set(name, "1"))
                code, result = self.read()
                self.assertEqual(1, code)
                self.assertIn("focus TRX counters do not reconcile with passed cases", result["errors"])

    def test_missing_brand_pattern_case_fails_closed(self):
        self.trx("focus", "receipt-focus.trx", self.names[:-1])
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("Focused inventory differs from the exact 154 allowlisted class/method cases", result["errors"])

    def test_missing_execution_counter_fails_closed(self):
        self.mutate_focus(lambda tree: tree.find(".//{*}Counters").attrib.pop("failed"))
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("focus TRX counters do not reconcile with passed cases", result["errors"])

    def test_duplicate_execution_fails_closed(self):
        def mutation(tree):
            results = tree.findall(".//{*}UnitTestResult")
            results[1].set("executionId", results[0].get("executionId"))
        self.mutate_focus(mutation)
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertTrue(result["errors"])

    def test_foreign_focused_class_fails_closed(self):
        self.mutate_focus(lambda tree: tree.find(".//{*}TestMethod").set("className", "Legacy.Maliev.DocumentService.Tests.ForeignClass"))
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertTrue(result["errors"])

    def test_conflicting_definition_fails_closed(self):
        def mutation(tree):
            definitions = tree.find(".//{*}TestDefinitions")
            duplicate = ET.fromstring(ET.tostring(definitions[0]))
            duplicate.find("{*}TestMethod").set("name", "ConflictingMethod")
            definitions.append(duplicate)
        self.mutate_focus(mutation)
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertTrue(result["errors"])

    def test_display_parameters_are_not_emitted(self):
        code, result = self.read()
        self.assertEqual(0, code)
        self.assertNotIn("PRIVATE DISPLAY PARAMETER", json.dumps(result))
        self.assertEqual({"class", "method", "test_id", "execution_id", "outcome"}, set(result["cases"]["focus"][0]))

    def test_repeated_theory_ids_with_consistent_definitions_are_accepted(self):
        for lane, filename in (("focus", "receipt-focus.trx"), ("full", "full-suite.trx")):
            path = self.root / lane / filename
            tree = ET.parse(path)
            definitions = tree.findall(".//{*}UnitTest")
            results = tree.findall(".//{*}UnitTestResult")
            test_id = definitions[0].get("id")
            definitions[1].set("id", test_id)
            results[1].set("testId", test_id)
            tree.write(path, encoding="utf-8")
        code, result = self.read()
        self.assertEqual(0, code)
        self.assertEqual([], result["errors"])


if __name__ == "__main__":
    unittest.main()
