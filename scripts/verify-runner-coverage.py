"""Fail closed on missing/empty production coverage; retain generated lines."""
import json
import hashlib
import pathlib
import sys
import xml.etree.ElementTree as ET
import subprocess

expected = {
    "Legacy.Maliev.DocumentService.Api",
    "Legacy.Maliev.DocumentService.Application",
    "Legacy.Maliev.DocumentService.Domain",
    "Legacy.Maliev.DocumentService.Rendering",
}
root = pathlib.Path(sys.argv[1])
reports = sorted(root.rglob("coverage.cobertura.xml"))
digests = {hashlib.sha256(report.read_bytes()).hexdigest() for report in reports}
if len(digests) != 1:
    raise SystemExit(f"Expected one unique full-suite raw report, found {len(digests)}")
lines = {name: {} for name in expected}
for package in ET.parse(reports[0]).findall("./packages/package"):
    name = package.get("name", "")
    if name not in expected:
        continue
    for cls in package.findall("./classes/class"):
        filename = cls.get("filename")
        if not filename:
            raise SystemExit("Missing production source filename")
        for line in cls.findall("./lines/line"):
            key = (filename, int(line.attrib["number"]))
            hit = int(line.attrib["hits"]) > 0
            lines[name][key] = lines[name].get(key, False) or hit
summary = []
for name, inventory in sorted(lines.items()):
    valid = len(inventory)
    covered = sum(inventory.values())
    passed = valid > 0 and covered * 100 >= valid * 80
    summary.append({"assembly": name, "covered": covered, "valid": valid,
                    "percent": covered * 100 / valid if valid else None,
                    "threshold": 80, "passed": passed})
application = next(item for item in summary if item["assembly"].endswith(".Application"))
assert application["valid"] == 0, "Executable Application lines contradict the approved contract-only policy"
subprocess.run([sys.executable, "-B", "scripts/verify-document-application-proof.py", sys.argv[2]], check=True)
executable_passed = all(item["passed"] for item in summary if not item["assembly"].endswith(".Application"))
output = {"raw_report": str(reports[0]), "raw_sha256": next(iter(digests)),
          "identical_raw_copies": len(reports), "exclusions": [], "assemblies": summary,
          "applicationStatus": "N/A contract-only", "applicationNumericalPassed": False,
          "fourAssemblyNumericalAcceptance": False, "applicabilityAcceptance": executable_passed,
          "note": "Application N/A requires independently rechecked same-candidate/run compiled proof; it is not 100%."}
(root / "coverage-gate.json").write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
print(json.dumps(output, indent=2))
raise SystemExit(0 if executable_passed else 1)
