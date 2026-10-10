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
            ("DocumentRuntimeHttpTests", "QuotationRows_RenderDescriptionWithoutAddingDistinctName", 2),
            ("DocumentRuntimeHttpTests", "QuotationDiscount_RendersSignedPriceAdjustmentWithoutDuplicatingMinus", 4),
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
        inventory = json.loads((Path(__file__).resolve().parent.parent / "docs/document-full-test-inventory.json").read_text())
        self.full_names = [(row["className"], row["method"], str(index))
                           for row in inventory for index in range(row["executions"])]
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
        summary = ET.SubElement(document, namespace + "ResultSummary", outcome="Completed")
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

    def test_reviewed_previous_upload_pin_identity_is_required_exactly_once(self):
        identity = ("Legacy.Maliev.DocumentService.Tests.Workflows.WorkflowContractTests",
                    "BuildAndTest_RejectsPreviousUploadArtifactPin", "0")
        self.assertEqual(1, self.full_names.count(identity))
        self.assertEqual(375, len(self.full_names))
        code, result = self.read()
        self.assertEqual(0, code)
        self.assertEqual([], result["errors"])
        for mutation in ("missing", "substituted", "duplicated"):
            with self.subTest(mutation=mutation):
                names = list(self.full_names)
                names.remove(identity)
                if mutation == "substituted":
                    names.append((identity[0], "UnreviewedUploadPinReplacement", "0"))
                elif mutation == "duplicated":
                    names.extend([identity, (identity[0], identity[1], "1")])
                self.trx("full", "full-suite.trx", names)
                code, result = self.read()
                self.assertEqual(1, code)
                self.assertIn("Full suite differs from the exact 375 merged HTTP, raster, receipt and evidence cases",
                              result["errors"])

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
        self.assertIn("Full suite differs from the exact 375 merged HTTP, raster, receipt and evidence cases", result["errors"])
        self.trx("full", "full-suite.trx", self.full_names)
        for name in ("executed", "failed", "notExecuted", "warning"):
            with self.subTest(counter=name):
                self.trx("focus", "receipt-focus.trx", self.names)
                self.mutate_focus(lambda tree: tree.find(".//{*}Counters").set(name, "1"))
                code, result = self.read()
                self.assertEqual(1, code)
                self.assertIn("focus TRX counters do not reconcile with passed cases", result["errors"])

    def test_same_count_substituted_runtime_method_fails_closed(self):
        substituted = list(self.names)
        index = next(index for index, item in enumerate(substituted)
                     if item[1] == "QuotationDiscount_RendersSignedPriceAdjustmentWithoutDuplicatingMinus")
        class_name, _, count = substituted[index]
        substituted[index] = (class_name, "UnreviewedReplacement", count)
        self.assertEqual(len(self.names), len(substituted))
        self.trx("focus", "receipt-focus.trx", substituted)
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("Focused inventory differs from the exact 160 allowlisted class/method cases", result["errors"])

    def test_missing_brand_pattern_case_fails_closed(self):
        self.trx("focus", "receipt-focus.trx", self.names[:-1])
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("Focused inventory differs from the exact 160 allowlisted class/method cases", result["errors"])

    def test_missing_execution_counter_fails_closed(self):
        self.mutate_focus(lambda tree: tree.find(".//{*}Counters").attrib.pop("failed"))
        code, result = self.read()
        self.assertEqual(1, code)
        self.assertIn("focus TRX counters do not reconcile with passed cases", result["errors"])

    def test_missing_duplicate_or_incomplete_summary_fails_closed(self):
        def missing(tree):
            tree.getroot().remove(tree.find("./{*}ResultSummary"))
        def duplicate(tree):
            tree.getroot().append(ET.fromstring(ET.tostring(tree.find("./{*}ResultSummary"))))
        for mutation in (missing, duplicate, lambda tree: tree.find("./{*}ResultSummary").set("outcome", "Aborted"),
                         lambda tree: tree.find("./{*}ResultSummary").attrib.pop("outcome")):
            self.trx("focus", "receipt-focus.trx", self.names)
            self.mutate_focus(mutation)
            code, result = self.read()
            self.assertEqual(1, code)
            self.assertIn("focus TRX requires exactly one Completed ResultSummary and one Counters", result["errors"])

    def test_duplicate_or_orphan_counters_fail_closed(self):
        def duplicate(tree):
            summary = tree.find("./{*}ResultSummary")
            summary.append(ET.fromstring(ET.tostring(summary.find("{*}Counters"))))
        def orphan(tree):
            tree.getroot().append(ET.fromstring(ET.tostring(tree.find(".//{*}Counters"))))
        for mutation in (duplicate, orphan):
            self.trx("focus", "receipt-focus.trx", self.names)
            self.mutate_focus(mutation)
            code, result = self.read()
            self.assertEqual(1, code)
            self.assertIn("focus TRX requires exactly one Completed ResultSummary and one Counters", result["errors"])

    def test_scoped_contract_policy_rejects_changed_or_expanded_acceptance(self):
        import importlib.util
        policy_path = Path(__file__).resolve().parent.parent / "docs/document-contract-applicability-policy.json"
        spec = importlib.util.spec_from_file_location("contract_policy_test", Path(__file__).with_name("read-document-contract-applicability.py"))
        gate = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gate)
        policy = gate.validate_policy(policy_path)
        mutations = [("active", False), ("active", 1), ("executableFloorPercent", 79),
                     ("includeGeneratedLines", False), ("applicationNumericalPercent", 100),
                     ("applicationNumericalPassed", True), ("exclusions", ["generated"]),
                     ("sourceHashes", {}), ("approvedSourceHead", "0" * 40), ("unknown", "field")]
        for name, value in mutations:
            with self.subTest(field=name, value=value):
                changed = dict(policy)
                changed[name] = value
                path = self.root / "changed-policy.json"
                path.write_text(json.dumps(changed), encoding="utf-8")
                with self.assertRaises(AssertionError):
                    gate.validate_policy(path)

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
            # Select two rows of the same method even when the merged full inventory is sorted.
            repeated = [index for index, definition in enumerate(definitions)
                        if definition.find("{*}TestMethod").get("name") == "RetainedSourceValues_PreserveThaiWords"][:2]
            first, second = repeated
            test_id = definitions[first].get("id")
            definitions[second].set("id", test_id)
            results[second].set("testId", test_id)
            tree.write(path, encoding="utf-8")
        code, result = self.read()
        self.assertEqual(0, code)
        self.assertEqual([], result["errors"])


if __name__ == "__main__":
    unittest.main()
