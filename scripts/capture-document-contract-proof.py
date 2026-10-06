"""Capture the fresh Release assembly before Coverlet instrumentation; no assembly execution."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ASSEMBLY = 'Legacy.Maliev.DocumentService.Application'
SOURCE_HASHES = {
    'IDocumentRenderer.cs': '1405f50026f21694314101df0e3c34a710ad09d29e173b1b6e66f37837c8996f',
    ASSEMBLY + '.csproj': '63cc9bad95d614c8672f25b5f54c768458ea47d0f15e67df0b7f7ea2933578ea',
}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def snapshot_name(name):
    # An archived source project must not become a discoverable build project.
    return name + '.source' if name.endswith('.csproj') else name

def capture(destination):
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    assert head == os.environ['DOCUMENT_SOURCE_SHA'], 'Checkout SHA mismatch'
    assert os.environ['GITHUB_RUN_ID'].isdigit() and os.environ['GITHUB_RUN_ATTEMPT'].isdigit()
    project = Path(ASSEMBLY)
    inventory = subprocess.check_output(['git', 'ls-files', '--', str(project)], text=True).splitlines()
    assert set(inventory) == {f'{ASSEMBLY}/{name}' for name in SOURCE_HASHES}, 'Application source inventory changed'
    assert not destination.exists(), 'Refusing to overwrite previous proof'
    for name, digest in SOURCE_HASHES.items():
        assert sha(project / name) == digest, 'Application source changed'
        committed = subprocess.check_output(['git', 'show', f'{head}:{ASSEMBLY}/{name}'])
        assert committed == (project / name).read_bytes(), 'Source differs from candidate'
    destination.mkdir(parents=True)
    files = {}
    for name in (ASSEMBLY + '.dll', ASSEMBLY + '.pdb'):
        source = project / 'bin/Release/net10.0' / name
        assert source.is_file() and source.stat().st_size > 0
        shutil.copyfile(source, destination / name)
        files[name] = sha(destination / name)
    for name in SOURCE_HASHES:
        captured = snapshot_name(name)
        shutil.copyfile(project / name, destination / captured)
        files[captured] = sha(destination / captured)
    manifest = dict(schemaVersion='document-contract-capture/v1', head=head,
                    runId=os.environ['GITHUB_RUN_ID'], runAttempt=os.environ['GITHUB_RUN_ATTEMPT'],
                    phase='release-build-before-test-instrumentation', files=files,
                    sourceInventory=inventory, policyActive=False)
    (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')

if __name__ == '__main__':
    capture(Path(sys.argv[1]))
