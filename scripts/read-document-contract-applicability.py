"""Independently re-read compiled metadata and raw lines; scoped N/A never means zero=100."""
from collections import Counter
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from capture_document_contract_support import ASSEMBLY, SOURCE_HASHES, sha, snapshot_name

def validate_policy(path):
    policy = json.loads(path.read_text(encoding='utf-8'))
    expected = dict(schemaVersion='document-contract-applicability-policy/v1', active=True,
        applicationAssembly=ASSEMBLY, applicationStatus='N/A contract-only',
        applicationNumericalPercent=None, applicationNumericalPassed=False,
        executableFloorPercent=80, includeGeneratedLines=True, exclusions=[],
        approvedSourceHead='6191a98c57becd7270ede83a7dfc95cb0b52912d', approvalNativeRun='37425501847',
        sourceHashes=SOURCE_HASHES)
    assert json.dumps(policy, sort_keys=True) == json.dumps(expected, sort_keys=True), 'Scoped applicability policy differs from the reviewed decision'
    return policy

def validate_capture(snapshot, head, run_id, attempt):
    manifest = json.loads((snapshot / 'manifest.json').read_text(encoding='utf-8'))
    assert manifest['schemaVersion'] == 'document-contract-capture/v1'
    assert (manifest['head'], manifest['runId'], manifest['runAttempt']) == (head, run_id, attempt)
    assert manifest['phase'] == 'release-build-before-test-instrumentation' and manifest['policyActive'] is False
    expected_files = {snapshot_name(n) for n in SOURCE_HASHES} | {ASSEMBLY + '.dll', ASSEMBLY + '.pdb'}
    assert set(manifest['files']) == expected_files
    assert set(manifest['sourceInventory']) == {f'{ASSEMBLY}/{n}' for n in SOURCE_HASHES}
    for name in expected_files:
        assert sha(snapshot / name) == manifest['files'][name], 'Captured file hash mismatch'
    for name, digest in SOURCE_HASHES.items():
        assert sha(snapshot / snapshot_name(name)) == digest, 'Frozen source contract changed'
        committed = subprocess.check_output(['git', 'show', f'{head}:{ASSEMBLY}/{name}'])
        assert committed == (snapshot / snapshot_name(name)).read_bytes(), 'Captured source differs from candidate'
    return manifest

def raw_assemblies(report):
    packages = ET.parse(report).findall('./packages/package')
    result = []
    for suffix in ('Api', 'Application', 'Domain', 'Rendering'):
        name = 'Legacy.Maliev.DocumentService.' + suffix
        inventory = {}
        for package in packages:
            if package.get('name') != name:
                continue
            for cls in package.findall('./classes/class'):
                for line in cls.findall('./lines/line'):
                    key = (cls.attrib['filename'], int(line.attrib['number']))
                    inventory[key] = inventory.get(key, False) or int(line.attrib['hits']) > 0
        covered, valid = sum(inventory.values()), len(inventory)
        if suffix == 'Application':
            assert valid == 0, 'Raw executable Application lines contradict contract-only proof'
        result.append(dict(assembly=name, covered=covered, valid=valid,
                           numericalPercent=covered * 100 / valid if valid else None,
                           numericalPassed=valid > 0 and covered * 100 >= valid * 80))
    return result

def inspect(root):
    head, run_id, attempt = (os.environ[n] for n in ('DOCUMENT_SOURCE_SHA', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'))
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == head
    snapshot = root / 'application-provenance'
    manifest = validate_capture(snapshot, head, run_id, attempt)
    with tempfile.TemporaryDirectory() as temporary:
        profile = Path(temporary) / 'surface.json'
        subprocess.run(['pwsh', '-NoProfile', '-File', 'scripts/Read-DocumentContractAssembly.ps1',
            '-DllPath', str(snapshot / (ASSEMBLY + '.dll')), '-PdbPath', str(snapshot / (ASSEMBLY + '.pdb')),
            '-ExpectedHead', head, '-OutputPath', str(profile)], check=True)
        surface = json.loads(profile.read_text(encoding='utf-8-sig'))
    assert surface['compiledHead'] == head
    assert surface['dllSha256'] == manifest['files'][ASSEMBLY + '.dll']
    assert surface['pdbSha256'] == manifest['files'][ASSEMBLY + '.pdb']
    # The existing strict reader re-parses both actual TRX files, every execution mapping,
    # all sixteen counters and the exact current focused/full inventories.
    subprocess.run([sys.executable, '-B', 'scripts/read-receipt-evidence.py', str(root)], check=True)
    observations = json.loads((root / 'receipt-evidence.json').read_text(encoding='utf-8'))
    assert observations['head_sha'] == observations['expected_head_sha'] == head
    assert observations['run_id'] == run_id and observations['errors'] == []
    runtime = [[(c['class'], c['method']) for c in observations['cases'][lane]
                if c['class'].endswith('.DocumentRuntimeHttpTests')] for lane in ('focus', 'full')]
    assert len(runtime[0]) == len(runtime[1]) == 43 and Counter(runtime[0]) == Counter(runtime[1])
    reports = list((root / 'full').rglob('coverage.cobertura.xml'))
    assert reports and len({sha(p) for p in reports}) == 1
    assert sha(reports[0]) == observations['raw_sha256']
    assemblies = raw_assemblies(reports[0])
    actual = [x for x in assemblies if x['assembly'] != ASSEMBLY]
    return dict(schemaVersion='document-contract-applicability-proposal/v1', policyActive=False,
        head=head, runId=run_id, runAttempt=attempt, rawSha256=sha(reports[0]), exclusions=[],
        application=dict(status='N/A contract-only', numericalPercent=None, numericalPassed=False, surface=surface),
        executableAssemblies=actual, executableFloorsPassed=all(x['numericalPassed'] for x in actual),
        actualHttpPassed=43, fourAssemblyNumericalAcceptance=False,
        note='Inactive reviewed proposal. N/A is separate from the three mandatory numerical floors; no deployment acceptance.')

if __name__ == '__main__':
    root = Path(sys.argv[1])
    result = inspect(root)
    output = root / 'contract-applicability-proposal.json'
    assert not output.exists(), 'Refusing to replace previous proposal observation'
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    policy_path = Path('docs/document-contract-applicability-policy.json')
    validate_policy(policy_path)
    assert subprocess.check_output(['git', 'show', f"{result['head']}:{policy_path.as_posix()}"]) == policy_path.read_bytes(), 'Policy differs from committed candidate'
    assert result['executableFloorsPassed'], 'An executable assembly remains below the mandatory generated-inclusive floor'
    controls_path = root / 'contract-negative-controls.json'
    controls = json.loads(controls_path.read_text(encoding='utf-8'))
    assert controls['head'] == result['head'] and controls['runId'] == result['runId']
    assert len(controls['controls']) == 20 and sum(c['rejected'] for c in controls['controls']) == 18
    acceptance = dict(schemaVersion='document-contract-applicability-acceptance/v1', policyActive=True,
        head=result['head'], runId=result['runId'], runAttempt=result['runAttempt'],
        policySha256=sha(policy_path), compiledProofSha256=sha(output), controlsSha256=sha(controls_path),
        applicationStatus='N/A contract-only', applicationNumericalPercent=None, applicationNumericalPassed=False,
        executableFloorsPassed=True, actualHttpPassed=43, fourAssemblyNumericalAcceptance=False,
        applicabilityAcceptance=True, exclusions=[], deployed=False)
    acceptance_path = root / 'contract-applicability-acceptance.json'
    assert not acceptance_path.exists(), 'Refusing to replace previous acceptance observation'
    acceptance_path.write_text(json.dumps(acceptance, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
