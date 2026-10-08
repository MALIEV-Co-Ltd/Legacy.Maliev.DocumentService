"""Run the reviewed five native phases only with fresh Root-issued permits."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path, PureWindowsPath
import re
import sys
import types
import uuid

from materialize_document_artifact_candidate_v2 import bind_shared, ROOT, BASE, CANDIDATE_SEAL
from smoke_document_artifact_windows_v2 import load_helpers

PHASES = ('build', 'focused', 'suite', 'format', 'audit')
MAX_BUNDLE_BYTES = 4096
REPOSITORY = 'MALIEV-Co-Ltd/Legacy.Maliev.DocumentService'


def validate_bundle(raw, consumer_head, shared, admission, now=None):
    if type(raw) is not bytes or not 0 < len(raw) <= MAX_BUNDLE_BYTES:
        raise ValueError('Root permit bundle exceeds bound')
    if type(consumer_head) is not str or not re.fullmatch('[0-9a-f]{40}', consumer_head):
        raise ValueError('Exact consumer head required')
    bundle = shared.parse_json(raw)
    if type(bundle) is not dict or set(bundle) != {'schemaVersion', 'repository', 'consumerHeadSha', 'permits'}:
        raise ValueError('Root permit bundle shape differs')
    if type(bundle['schemaVersion']) is not int or bundle['schemaVersion'] != 1 or bundle['repository'] != REPOSITORY or bundle['consumerHeadSha'] != consumer_head:
        raise ValueError('Root permit consumer association differs')
    permits = bundle['permits']
    if type(permits) is not list or len(permits) != len(PHASES):
        raise ValueError('All five ordered Root permits required')
    lease_ids = set()
    now = now or datetime.now(timezone.utc)
    for phase, permit in zip(PHASES, permits, strict=True):
        admission.validate(permit, phase, Path(ROOT)/'work/documentservice-artifact-upload-contract-20261008', BASE, CANDIDATE_SEAL, now)
        if permit['leaseId'] in lease_ids:
            raise ValueError('Phase lease IDs must be unique')
        lease_ids.add(permit['leaseId'])
    return permits


def verify_phase(phase, receipt, directory):
    if receipt.get('phase') != phase or receipt.get('baseSha') != BASE or receipt.get('candidateSha') != CANDIDATE_SEAL:
        raise ValueError('Native phase source association differs')
    run_id = receipt.get('runId')
    if type(run_id) is not str or str(uuid.UUID(run_id)) != run_id or uuid.UUID(run_id).int == 0:
        raise ValueError('Native phase run identity missing')
    if receipt.get('failure') is not None or type(receipt.get('returncode')) is not int or receipt['returncode'] != 0 or receipt.get('cleanupErrors') != [] or receipt.get('quarantineCount') != 0 or receipt.get('claimReleased') is not True:
        raise ValueError('Native phase failed or cleanup remains')
    cleanup = receipt.get('cleanupSupervision', {})
    if any(cleanup.get(key) is not True for key in ['slotReleased', 'journalClosed', 'cleanupOnly', 'cleanupVerified']) or cleanup.get('ownedLeases') != []:
        raise ValueError('Native cleanup barrier is incomplete')
    final = {};precreation=[];pending_leases=set()
    stable=['executable_identity','job_lease','log_path','job_memory_limit_bytes','job_cpu_rate','job_active_process_limit']
    for row in receipt.get('resources', []):
        if 'pid' in row:
            if row['pid'] is None:
                if any(key not in row or row[key] is not None for key in ['pid','actual_start_filetime','actual_start_utc','executable_identity']) or row.get('cleanup_verified') is not False or row.get('exit_code') is not None or not (row.get('terminal_state_verified') is None or row.get('terminal_state_verified') is False) or row.get('purpose')!='finite owned command' or row.get('ownership')!='retained process handle and private job handle':
                    raise ValueError('Inconsistent precreation ownership snapshot')
                lease=row.get('job_lease')
                if type(lease) is not str or str(uuid.UUID(lease))!=lease or uuid.UUID(lease).int==0 or lease in pending_leases:
                    raise ValueError('Duplicate or invalid precreation lease')
                pending_leases.add(lease)
                precreation.append(row)
            else:
                if type(row['pid']) is not int or row['pid']<=0 or type(row.get('actual_start_filetime')) is not int or row['actual_start_filetime']<=0 or type(row.get('executable_identity')) is not str or not row['executable_identity']:
                    raise ValueError('Actual native ownership identity incomplete')
                identity=(row['pid'],row['actual_start_filetime'])
                if identity in final and any(row.get(key)!=final[identity].get(key) for key in stable):
                    raise ValueError('Native ownership snapshot identity changed')
                final[identity] = row
        elif set(row)!={'freeMiB','sdkJobs','observedUtc'} or type(row['freeMiB']) is not int or row['freeMiB']<4096 or row['sdkJobs']!=[] or type(row['observedUtc']) is not str:
            raise ValueError('Partial or foreign native resource record')
    if len(final) != 3:
        raise ValueError('Exact HEAD, scope and native command ownership required')
    roles=[('git.exe','head.log'),('git.exe','scope.log'),('dotnet.exe','phase.log')]
    phase_root=PureWindowsPath(ROOT)/'outputs'/f'document-artifact-upload-native-v3-{phase}-{run_id}'
    actual_leases=set()
    for row,(executable,log_name) in zip(final.values(),roles,strict=True):
        lease=row.get('job_lease')
        if type(lease) is not str or str(uuid.UUID(lease))!=lease or uuid.UUID(lease).int==0 or lease in actual_leases:
            raise ValueError('Actual command leases must be canonical and unique')
        actual_leases.add(lease)
        if PureWindowsPath(row['executable_identity']).name.casefold()!=executable or type(row.get('log_path')) is not str or PureWindowsPath(row['log_path'])!=phase_root/log_name:
            raise ValueError('Ordered HEAD, scope and native executable/log roles differ')
    for pending in precreation:
        matching=[row for row in final.values() if row.get('job_lease')==pending.get('job_lease')]
        if not pending.get('job_lease') or len(matching)!=1 or any(pending.get(key)!=matching[0].get(key) for key in ['log_path','job_memory_limit_bytes','job_cpu_rate','job_active_process_limit']):
            raise ValueError('Unsettled precreation job ownership')
    for row in final.values():
        if any(row.get(key) is not True for key in ['cleanup_verified', 'terminal_state_verified', 'readers_settled', 'process_handle_closed', 'thread_handle_closed', 'job_handle_closed', 'pipe_handles_closed', 'attribute_list_disposed', 'job_caps_readback_verified', 'membership_verified_before_resume']):
            raise ValueError('Owned native resource release missing')
        if row.get('remaining_job_processes') != 0 or row.get('retained_unverified_handles') is not False or row.get('stop_reason') is not None or row.get('output_truncated') is not False:
            raise ValueError('Owned native command incomplete')
    native = [row for row in final.values() if PureWindowsPath(row['executable_identity']).name.casefold() == 'dotnet.exe']
    if len(native) != 1 or native[0]['job_memory_limit_bytes'] != 3*1024**3 or native[0]['job_cpu_rate'] != 5000 or native[0]['job_active_process_limit'] != 64:
        raise ValueError('Exact native private job envelope required')
    if phase in ('focused', 'suite'):
        result = json.loads((directory/'strict-test-result.json').read_bytes())
        if result.get('summary') != 'Completed' or result.get('passed') != result.get('total') or result.get('other') != 0 or (phase == 'suite' and result['total'] != 374):
            raise ValueError('Strict native test result missing')
    if phase == 'audit':
        result = json.loads((directory/'strict-audit-result.json').read_bytes())
        if result.get('version') != 1 or result.get('projectCount', 0) <= 0 or result.get('problems') != [] or result.get('vulnerablePackages') != 0:
            raise ValueError('Strict structured audit missing')


def sequence(permits, execute, verify, checkpoint):
    completed = []
    for phase, permit in zip(PHASES, permits, strict=True):
        result = execute(phase, permit)
        verify(phase, result)
        completed.append(phase)
        checkpoint(tuple(completed))
    return completed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--policy', required=True)
    parser.add_argument('--shared-module', required=True)
    parser.add_argument('--root-permit-bundle', required=True)
    parser.add_argument('--consumer-head', required=True)
    parser.add_argument('--evidence', required=True)
    args = parser.parse_args()
    shared, policy = bind_shared(args.policy, args.shared_module)
    modules = load_helpers(policy, shared)
    admission = modules['workflows_native_admission_v3']
    permit_path = Path(args.root_permit_bundle);shared.reject_links(permit_path)
    with permit_path.open('rb') as stream:
        raw = stream.read(MAX_BUNDLE_BYTES+1)
    permits = validate_bundle(raw, args.consumer_head, shared, admission)
    entries = {row['path']: row for row in policy['entries']}
    for name in ['artifact_pin_native_result_validation_v2.py', 'run_document_artifact_upload_native_20261008_v3.py']:
        path = Path(ROOT)/'outputs'/name;shared.reject_links(path)
        with path.open('rb') as stream:
            body = stream.read(256*1024+1)
        if len(body) > 256*1024 or hashlib.sha256(body).hexdigest() != entries['outputs/'+name]['sha256']:
            raise ValueError('Exact reviewed native module bytes differ')
        key = Path(name).stem;module = types.ModuleType(key);module.__file__ = str(path)
        sys.modules[key] = module
        exec(compile(body, str(path), 'exec'), module.__dict__)
        modules[key] = module
    runner = modules['run_document_artifact_upload_native_20261008_v3']
    evidence = Path(args.evidence);shared.reject_links(evidence);evidence.mkdir(parents=True, exist_ok=False)
    native_out = Path(ROOT)/'outputs'
    if list(native_out.glob('document-artifact-upload-native-v3-*')):
        raise ValueError('Fresh native phase history required')
    def execute(phase, permit):
        path = evidence/(phase+'-root-permit.json')
        with path.open('xb') as stream:
            stream.write((json.dumps(permit)+'\n').encode())
        before = set(native_out.glob('document-artifact-upload-native-v3-*'))
        original = sys.argv
        try:
            sys.argv = [runner.__file__, phase, str(path.resolve())]
            runner.main()
        finally:
            sys.argv = original
        created = set(native_out.glob('document-artifact-upload-native-v3-*'))-before
        if len(created) != 1:
            raise ValueError('Exactly one fresh phase receipt required')
        directory = created.pop();shared.reject_links(directory)
        return directory
    def verify(phase, directory):
        receipt = shared.parse_json((directory/'receipt.json').read_bytes())
        verify_phase(phase, receipt, directory)
        if admission.SLOT.exists():
            raise ValueError('Exclusive SDK claim remains after phase')
    def checkpoint(completed):
        (evidence/'completed-phases.json').write_bytes((json.dumps(dict(consumerHead=args.consumer_head, candidateSha=CANDIDATE_SEAL, completed=completed))+'\n').encode())
    sequence(permits, execute, verify, checkpoint)
    print('Document native build, focus, suite374, format and structured audit passed.')


if __name__ == '__main__':
    main()
