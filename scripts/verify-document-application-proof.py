"""Recheck the approved contract-only proof from the same candidate and workflow run."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

from capture_document_contract_support import sha


def validate_receipt(root, head, run, attempt):
    receipt = json.loads((root / 'contract-applicability-acceptance.json').read_text())
    assert receipt['schemaVersion'] == 'document-contract-applicability-acceptance/v1'
    assert (receipt['head'], receipt['runId'], receipt['runAttempt']) == (head, run, attempt)
    assert receipt['policyActive'] is True and receipt['applicabilityAcceptance'] is True
    assert receipt['applicationStatus'] == 'N/A contract-only'
    assert receipt['applicationNumericalPercent'] is None and receipt['applicationNumericalPassed'] is False
    assert receipt['fourAssemblyNumericalAcceptance'] is False and receipt['deployed'] is False
    assert receipt['executableFloorsPassed'] is True and receipt['actualHttpPassed'] == 37
    assert receipt['exclusions'] == []
    assert receipt['policySha256'] == sha(Path('docs/document-contract-applicability-policy.json'))
    assert receipt['compiledProofSha256'] == sha(root / 'contract-applicability-proposal.json')
    assert receipt['controlsSha256'] == sha(root / 'contract-negative-controls.json')
    return receipt


def verify(root):
    head, run, attempt = (os.environ[n] for n in ('DOCUMENT_SOURCE_SHA', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'))
    receipt = validate_receipt(root, head, run, attempt)
    spec = importlib.util.spec_from_file_location('approved_contract_gate', Path(__file__).with_name('read-document-contract-applicability.py'))
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)
    gate.validate_policy(Path('docs/document-contract-applicability-policy.json'))
    observed = gate.inspect(root)
    archived = json.loads((root / 'contract-applicability-proposal.json').read_text())
    assert observed == archived, 'Independent compiled/full-suite proof changed after capture'
    assert observed['executableFloorsPassed'] is True
    # Replay the twenty native rejection controls against the archived compiled fixtures.
    # Assemblies remain passive PE/PDB inputs; no build or assembly execution is involved.
    with tempfile.TemporaryDirectory() as temporary:
        snapshot = Path(temporary) / 'application-provenance'
        shutil.copytree(root / 'application-provenance', snapshot)
        subprocess.run([sys.executable, '-B', 'scripts/test-document-contract-controls.py',
                        str(snapshot), str(root / 'contract-fixtures')], check=True, timeout=180)
        controls = json.loads((snapshot.parent / 'contract-negative-controls.json').read_text())
    assert controls == json.loads((root / 'contract-negative-controls.json').read_text()), 'Native rejection controls changed'
    return receipt


if __name__ == '__main__':
    print(json.dumps(verify(Path(sys.argv[1])), indent=2))
