"""Negative synthetic receipts exercise validation only; no renderer/SDK runs."""
from copy import deepcopy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
from verify_label_causal_evidence import BASELINE, CLASS, COMPOSER, FIELDS, GEOMETRY, NULL, TEST, main, verify_trx

N = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


class LabelCausalEvidenceTests(unittest.TestCase):
    def fixture(self, mode):
        root = ET.Element(N + "TestRun")
        definitions = ET.SubElement(root, N + "TestDefinitions")
        results = ET.SubElement(root, N + "Results")
        for index, field in enumerate(sorted(FIELDS) + [None]):
            method = GEOMETRY if field else NULL
            name = CLASS + "." + method + (f'(field: "{field}")' if field else "")
            definition = ET.SubElement(definitions, N + "UnitTest", id=str(index), name=name)
            ET.SubElement(definition, N + "Execution", id="execution" + str(index))
            ET.SubElement(definition, N + "TestMethod", className=CLASS, name=method)
            failed = mode == "baseline" and field is not None
            result = ET.SubElement(results, N + "UnitTestResult", testId=str(index),
                                   executionId="execution" + str(index), testName=name,
                                   outcome="Failed" if failed else "Passed")
            if failed:
                error = ET.SubElement(ET.SubElement(result, N + "Output"), N + "ErrorInfo")
                ET.SubElement(error, N + "Message").text = f"Caller blank line was lost for {field}; displacement=0."
                ET.SubElement(error, N + "StackTrace").text = "at " + CLASS + "." + GEOMETRY + "(String field)"
        summary = ET.SubElement(root, N + "ResultSummary", outcome="Failed" if mode == "baseline" else "Completed")
        ET.SubElement(summary, N + "Counters", total="8", executed="8", passed="1" if mode == "baseline" else "8",
                      failed="7" if mode == "baseline" else "0", notExecuted="0", error="0", timeout="0")
        return root

    def check(self, root, mode="baseline"):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "focus.trx"
            ET.ElementTree(root).write(path, encoding="utf-8")
            return verify_trx(path, mode)

    def test_exact_candidate_and_baseline_receipts(self):
        for mode in ("candidate", "baseline"):
            self.assertEqual(8, len(self.check(self.fixture(mode), mode)))

    def test_wrong_failure_taxonomy_is_rejected(self):
        for message in ("Assert.Single() Failure", "HTTP status was 500", "Operation timed out",
                        "error CS1000", "Caller blank line was lost for Wrong; displacement=0.",
                        "Caller blank line was lost for Color; displacement=5.",
                        "Caller blank line was lost for Color; displacement=NaN."):
            with self.subTest(message=message):
                root = self.fixture("baseline")
                root.find(".//" + N + "Message").text = message
                with self.assertRaises(ValueError):
                    self.check(root)

    def test_identity_arguments_outcomes_and_counters_are_rejected(self):
        mutations = [
            lambda r: r.find(".//" + N + "TestMethod").set("className", "Wrong.Class"),
            lambda r: r.find(".//" + N + "TestMethod").set("name", NULL),
            lambda r: r.find(".//" + N + "UnitTestResult").set("executionId", "wrong"),
            lambda r: r.find(".//" + N + "UnitTestResult").set("testId", "unknown"),
            lambda r: r.find(".//" + N + "UnitTestResult").set("testName", "wrong"),
            lambda r: r.find(".//" + N + "UnitTestResult").set("outcome", "Passed"),
            lambda r: r.find(".//" + N + "Counters").set("passed", "8"),
            lambda r: r.find(".//" + N + "Counters").set("timeout", "1"),
            lambda r: r.find(".//" + N + "StackTrace").__setattr__("text", CLASS + ".MarkerPositionAsync"),
            lambda r: r.find(N + "ResultSummary").set("outcome", "Completed"),
            lambda r: r.find(N + "Results").remove(r.find(".//" + N + "UnitTestResult")),
        ]
        for index, mutation in enumerate(mutations):
            with self.subTest(mutation=index):
                root = self.fixture("baseline")
                mutation(root)
                with self.assertRaises(ValueError):
                    self.check(root)

    def test_duplicate_fields_and_results_are_rejected(self):
        for kind in ("field", "result"):
            root = self.fixture("baseline")
            results = root.find(N + "Results")
            if kind == "result":
                results.remove(results[-1])
                results.append(deepcopy(results[0]))
            else:
                definitions = root.find(N + "TestDefinitions")
                definitions[1].set("name", definitions[0].get("name"))
                results[1].set("testName", results[0].get("testName"))
            with self.assertRaises(ValueError):
                self.check(root)

    def test_cli_requires_source_hash_overlay_build_and_exit(self):
        for mutation in (None, "candidate-sha", "baseline-sha", "test-bytes", "composer",
                         "extra-overlay", "exit", "build-warning", "build-error", "build-missing"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                candidate, baseline = root / "candidate", root / "baseline"
                for checkout in (candidate, baseline):
                    (checkout / TEST).parent.mkdir(parents=True)
                    (checkout / TEST).write_bytes(b"immutable test\n")
                    (checkout / COMPOSER).parent.mkdir(parents=True)
                    (checkout / COMPOSER).write_bytes(b"pinned composer\n")
                if mutation == "test-bytes":
                    (baseline / TEST).write_bytes(b"changed test\n")
                trx, log, receipt = root / "focus.trx", root / "build.log", root / "receipt.json"
                ET.ElementTree(self.fixture("baseline")).write(trx)
                log.write_text({"build-warning": "Build succeeded.\n1 Warning(s)\n0 Error(s)",
                                "build-error": "Build succeeded.\n0 Warning(s)\n1 Error(s)",
                                "build-missing": "no build result"}.get(mutation,
                                "Build succeeded.\n0 Warning(s)\n0 Error(s)"))

                def fake_git(checkout, *args):
                    if args == ("rev-parse", "HEAD"):
                        if mutation == ("candidate-sha" if checkout == candidate else "baseline-sha"):
                            return b"wrong"
                        return ("a" * 40 if checkout == candidate else BASELINE).encode()
                    if args == ("rev-parse", "HEAD^{tree}"):
                        return b"b" * 40
                    if COMPOSER in args:
                        return b" M composer" if mutation == "composer" else b""
                    return ("?? " + TEST + ("\n?? unexpected.cs" if mutation == "extra-overlay" else "")).encode()

                argv = ["validator", "baseline", "--checkout", str(baseline), "--candidate", str(candidate),
                        "--candidate-sha", "a" * 40, "--trx", str(trx), "--build-log", str(log),
                        "--test-exit", "0" if mutation == "exit" else "1", "--receipt", str(receipt)]
                with patch("sys.argv", argv), patch("verify_label_causal_evidence.git", side_effect=fake_git), \
                        patch("verify_label_causal_evidence.subprocess.check_output", return_value=b"immutable test\n"):
                    if mutation is None:
                        main()
                        self.assertTrue(receipt.exists())
                    else:
                        with self.assertRaises(ValueError):
                            main()
                        self.assertFalse(receipt.exists())


if __name__ == "__main__":
    unittest.main()
