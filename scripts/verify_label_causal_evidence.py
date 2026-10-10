"""Strict packing-label receipts; synthetic controls are not renderer evidence."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

BASELINE = "35f66f4945ad35c40879b2f98fbdec1102bedc8b"
CLASS = "Legacy.Maliev.DocumentService.Tests.DocumentOrderLabelLiteralHttpTests"
GEOMETRY = "CallerLeadingBlankLine_ReachesActualPdfLayoutWithoutTrimming"
NULL = "NullTextFields_RetainExistingBlankCellRendering"
FIELDS = {"Id", "Name", "Process", "Material", "Color", "SurfaceFinish", "Description"}
TEST = "Legacy.Maliev.DocumentService.Tests/DocumentOrderLabelLiteralHttpTests.cs"
COMPOSER = "Legacy.Maliev.DocumentService.Rendering/Documents/OrderLabelDocumentComposer.cs"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify_trx(path, mode):
    require(mode in {"candidate", "baseline"}, "Unknown evidence mode")
    tree = ET.parse(path)
    definitions = tree.findall("./t:TestDefinitions/t:UnitTest", NS)
    results = tree.findall("./t:Results/t:UnitTestResult", NS)
    require(len(definitions) == len(results) == 8, "Expected exactly eight definitions/results")
    ids = {}
    executions = set()
    for definition in definitions:
        test_id = definition.get("id")
        method = definition.find("t:TestMethod", NS)
        execution = definition.find("t:Execution", NS)
        require(test_id and test_id not in ids and method is not None and execution is not None,
                "Missing/duplicate definition identity")
        execution_id = execution.get("id")
        require(execution_id and execution_id not in executions, "Missing/duplicate execution identity")
        executions.add(execution_id)
        require(method.get("className", "").split(",", 1)[0].strip() == CLASS, "Unexpected test class")
        require(method.get("name") in {GEOMETRY, NULL}, "Unexpected method")
        ids[test_id] = (method.get("name"), execution_id, definition.get("name"))
    seen_ids, seen_fields, observed = set(), set(), []
    null_count = 0
    for result in results:
        test_id = result.get("testId")
        require(test_id in ids and test_id not in seen_ids, "Missing/duplicate result join")
        seen_ids.add(test_id)
        method, execution_id, display = ids[test_id]
        require(result.get("executionId") == execution_id, "Execution identity mismatch")
        require(result.get("testName") == display, "Definition/result display mismatch")
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", default="", namespaces=NS)
        if method == GEOMETRY:
            match = re.fullmatch(re.escape(CLASS + "." + GEOMETRY) + r'\(field: "([A-Za-z]+)"\)', display or "")
            require(match is not None, "Unexpected geometry arguments")
            field = match.group(1)
            require(field in FIELDS and field not in seen_fields, "Missing/duplicate field")
            seen_fields.add(field)
            expected = "Failed" if mode == "baseline" else "Passed"
            if mode == "baseline":
                failure = re.fullmatch(r"Caller blank line was lost for " + re.escape(field)
                                       + r"; displacement=([0-9.eE+-]+)\.", message.strip())
                require(failure is not None, "Failure is not the geometry assertion")
                displacement = float(failure.group(1))
                require(math.isfinite(displacement) and 0 <= displacement < 5, "Invalid baseline displacement")
                stack = result.findtext("t:Output/t:ErrorInfo/t:StackTrace", default="", namespaces=NS)
                require(CLASS + "." + GEOMETRY in stack and "MarkerPositionAsync" not in stack,
                        "Wrong failure boundary")
        else:
            null_count += 1
            require(display == CLASS + "." + NULL, "Unexpected null case arguments")
            expected = "Passed"
        require(result.get("outcome") == expected, "Unexpected outcome")
        if expected == "Passed":
            require(not message and result.find("t:Output/t:ErrorInfo", NS) is None, "Passing case has error info")
        observed.append({"name": display, "outcome": expected, "testId": test_id, "executionId": execution_id})
    require(seen_fields == FIELDS and null_count == 1, "Incomplete exact roster")
    summary = tree.find("./t:ResultSummary", NS)
    require(summary is not None and summary.get("outcome") == ("Failed" if mode == "baseline" else "Completed"),
            "Unexpected summary outcome")
    counters = summary.find("t:Counters", NS)
    require(counters is not None, "Missing counters")
    expected_counts = {"total": 8, "executed": 8, "passed": 1 if mode == "baseline" else 8,
                       "failed": 7 if mode == "baseline" else 0}
    require(set(expected_counts).issubset(counters.attrib), "Missing required counters")
    for name, value in counters.attrib.items():
        require(int(value) == expected_counts.get(name, 0), "Contradictory counters")
    return observed


def git(checkout, *args):
    return subprocess.check_output(["git", "-C", str(checkout), *args], timeout=20).strip()


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["candidate", "baseline"])
    parser.add_argument("--checkout", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--candidate-sha", required=True)
    parser.add_argument("--trx", type=Path, required=True)
    parser.add_argument("--build-log", type=Path)
    parser.add_argument("--test-exit", type=int, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    args = parser.parse_args()
    require(git(args.candidate, "rev-parse", "HEAD").decode() == args.candidate_sha, "Candidate identity mismatch")
    expected_source = BASELINE if args.mode == "baseline" else args.candidate_sha
    require(git(args.checkout, "rev-parse", "HEAD").decode() == expected_source, "Production source identity mismatch")
    source_test = subprocess.check_output(["git", "-C", str(args.candidate), "show",
                                           args.candidate_sha + ":" + TEST], timeout=20)
    require((args.candidate / TEST).read_bytes() == source_test
            and (args.checkout / TEST).read_bytes() == (args.candidate / TEST).read_bytes(), "Test overlay differs")
    require(git(args.checkout, "status", "--porcelain", "--untracked-files=all", "--", COMPOSER) == b"",
            "Composer differs from pinned production source")
    if args.mode == "baseline":
        require(args.test_exit == 1, "Baseline test exit must be exactly one")
        require(args.build_log is not None, "Missing baseline build log")
        log = args.build_log.read_text()
        require("Build succeeded." in log and "0 Warning(s)" in log and "0 Error(s)" in log
                and not re.search(r"\b(?:warning|error) [A-Z]+\d+", log, re.I), "Baseline build not zero-warning/error")
        changed = git(args.checkout, "status", "--porcelain", "--untracked-files=all").decode().splitlines()
        require(changed == ["?? " + TEST], "Unexpected baseline source overlay")
    else:
        require(args.test_exit == 0, "Candidate test exit must be zero")
    cases = verify_trx(args.trx, args.mode)
    receipt = {"mode": args.mode, "candidateSha": args.candidate_sha, "productionSha": expected_source,
               "productionTree": git(args.checkout, "rev-parse", "HEAD^{tree}").decode(),
               "testSha256": digest(args.checkout / TEST), "composerSha256": digest(args.checkout / COMPOSER),
               "trxSha256": digest(args.trx), "testExit": args.test_exit, "cases": cases}
    if args.build_log:
        receipt["buildLogSha256"] = digest(args.build_log)
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    args.receipt.write_text(json.dumps(receipt, indent=2) + "\n")


if __name__ == "__main__":
    main()
