"""Native compiler fixtures are inspected as data, never loaded or executed."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from capture_document_contract_support import ASSEMBLY, sha

spec = importlib.util.spec_from_file_location('contract_gate', Path(__file__).with_name('read-document-contract-applicability.py'))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
snapshot, fixtures = map(Path, sys.argv[1:3])
head, run, attempt = (os.environ[n] for n in ('DOCUMENT_SOURCE_SHA', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'))
results = []

def rejected(name, operation, expected_reason=None):
    try:
        operation()
    except (AssertionError, FileNotFoundError, subprocess.CalledProcessError) as error:
        if expected_reason is not None:
            assert isinstance(error, subprocess.CalledProcessError) and expected_reason in error.stderr, error
        results.append(dict(control=name, rejected=True, expectedReason=expected_reason))
    else:
        raise AssertionError('Unsafe control accepted: ' + name)

def metadata(dll, pdb, expected, output):
    process = subprocess.run(['pwsh', '-NoProfile', '-File', 'scripts/Read-DocumentContractAssembly.ps1',
        '-DllPath', str(dll), '-PdbPath', str(pdb), '-ExpectedHead', expected, '-OutputPath', str(output)],
        capture_output=True, text=True)
    process.check_returncode()
    return json.loads(output.read_text(encoding='utf-8-sig'))

gate.validate_capture(snapshot, head, run, attempt)
with tempfile.TemporaryDirectory() as temporary:
    root = Path(temporary)
    dll, pdb = (snapshot / (ASSEMBLY + suffix) for suffix in ('.dll', '.pdb'))
    positive = metadata(dll, pdb, head, root / 'positive.json')
    assert positive['numericalPercent'] is None and positive['numericalPassed'] is False
    results.append(dict(control='actual pristine contract positive', rejected=False))
    reasons = dict(CONCRETE='Only the module and five-method interface',
                   DEFAULT_METHOD='Unexpected concrete, default, static or duplicate method',
                   STATIC_METHOD='Only the module and five-method interface',
                   EXTRA_TYPE='Only the module and five-method interface',
                   RESOURCE='Executable entry point, native image or resources present')
    for mode, reason in reasons.items():
        directory = fixtures / mode
        assert (directory / dll.name).is_file() and (directory / pdb.name).is_file(), 'Missing compiled fixture'
        rejected(mode, lambda d=directory, m=mode: metadata(d / dll.name, d / pdb.name, head, root / (m + '.json')), reason)
        results[-1].update(dllSha256=sha(directory / dll.name), pdbSha256=sha(directory / pdb.name))
    rejected('missing PDB', lambda: metadata(dll, root / 'missing.pdb', head, root / 'missing.json'))
    rejected('mismatched compiled PDB', lambda: metadata(dll, fixtures / 'DEFAULT_METHOD' / pdb.name, head, root / 'mismatch.json'), 'DLL and portable PDB identity mismatch')
    rejected('stale compiled candidate', lambda: metadata(dll, pdb, '0' * 40, root / 'stale.json'), 'Compiled candidate SHA mismatch')
    for name, mutation in (
        ('changed source with updated manifest hash', 'source'),
        ('missing source', 'missing'),
        ('wrong manifest candidate', 'head'),
        ('wrong run', 'run'),
        ('wrong attempt', 'attempt'),
        ('changed DLL with updated manifest hash', 'dll'),
    ):
        copy = root / mutation
        shutil.copytree(snapshot, copy)
        manifest = json.loads((copy / 'manifest.json').read_text(encoding='utf-8'))
        if mutation == 'source':
            source = copy / 'IDocumentRenderer.cs'
            source.write_bytes(source.read_bytes() + b'\n// changed\n')
            manifest['files'][source.name] = sha(source)
        elif mutation == 'missing':
            (copy / 'IDocumentRenderer.cs').unlink()
        elif mutation == 'head':
            manifest['head'] = '0' * 40
        elif mutation == 'run':
            manifest['runId'] = '0'
        elif mutation == 'attempt':
            manifest['runAttempt'] = '0'
        else:
            (copy / dll.name).write_bytes(b'invalid PE image')
            manifest['files'][dll.name] = sha(copy / dll.name)
        (copy / 'manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
        def inspect_copy(c=copy):
            gate.validate_capture(c, head, run, attempt)
            metadata(c / dll.name, c / pdb.name, head, root / 'invalid-dll.json')
        rejected(name, inspect_copy)
    for hits in (0, 1):
        report = root / f'raw-{hits}.xml'
        report.write_text(f'<coverage><packages><package name="{ASSEMBLY}"><classes><class filename="unexpected.cs"><lines><line number="1" hits="{hits}"/></lines></class></classes></package></packages></coverage>', encoding='utf-8')
        rejected(f'Application raw lines hits={hits}', lambda p=report: gate.raw_assemblies(p))

output = snapshot.parent / 'contract-negative-controls.json'
assert not output.exists()
output.write_text(json.dumps(dict(policyActive=False, head=head, runId=run, controls=results), indent=2) + '\n', encoding='utf-8')
print(f'{len(results)} contract controls passed; compiler fixture assemblies were never executed')
