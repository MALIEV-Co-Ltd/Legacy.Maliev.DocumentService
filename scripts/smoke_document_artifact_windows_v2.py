"""Python-only Windows lifecycle qualification after exact Document intake."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sys
import types
from materialize_document_artifact_candidate_v2 import bind_shared, ROOT


def load_helpers(policy, shared):
    expected={row['path']:row for row in policy['entries']}
    verified={}
    for name in policy['helperPaths']:
        path=Path(ROOT)/'outputs'/name;shared.reject_links(path)
        with path.open('rb') as stream:raw=stream.read(256*1024+1)
        if len(raw)>256*1024 or shared.digest(raw)!=expected['outputs/'+name]['sha256']:
            raise ValueError('Exact reconstructed Windows helper differs')
        verified[name]=raw
    order=['workflows_cleanup_supervisor_v1.py','workflows_owned_command_v1.py',
        'workflows_snapshot_census_v3.py','workflows_native_admission_v3.py']
    modules={}
    for name in order:
        key=Path(name).stem
        module=types.ModuleType(key);module.__file__=str(Path(ROOT)/'outputs'/name)
        sys.modules[key]=module
        exec(compile(verified[name],module.__file__,'exec'),module.__dict__)
        modules[key]=module
    return modules


def evaluate(case,result,row):
    required=['cleanup_verified','readers_settled','process_handle_closed','thread_handle_closed',
        'job_handle_closed','pipe_handles_closed','job_caps_readback_verified','membership_verified_before_resume',
        'terminal_state_verified','attribute_list_disposed']
    if any(row.get(key) is not True for key in required) or row.get('remaining_job_processes')!=0:
        raise ValueError('Owned Windows lifecycle release evidence missing')
    if row.get('job_memory_limit_bytes')!=256*1024*1024 or row.get('job_cpu_rate')!=5000 or row.get('job_active_process_limit')!=64:
        raise ValueError('Actual private job envelope differs')
    if row.get('retained_unverified_handles') is not False:
        raise ValueError('Owned handles remain unverified')
    if not row.get('actual_start_filetime') or not row.get('pid') or not row.get('executable_identity') or row.get('ports')!=[] or row.get('persistent_data') is not False:
        raise ValueError('Actual ownership identity is missing or widened')
    expected={'success':None,'timeout':'timeout','output-limit':'output-limit'}
    if case not in expected or row.get('stop_reason')!=expected[case]:raise ValueError('Actual failure path was not witnessed')
    if case=='success' and (result.returncode!=0 or result.stdout.strip()!=b'document-owned-python-smoke'):
        raise ValueError('Actual successful Python command differs')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--policy',required=True)
    parser.add_argument('--shared-module',required=True);parser.add_argument('--evidence',required=True)
    args=parser.parse_args()
    shared,policy=bind_shared(args.policy,args.shared_module)
    modules=load_helpers(policy,shared)
    owned=modules['workflows_owned_command_v1'];admission=modules['workflows_native_admission_v3']
    supervisor=modules['workflows_cleanup_supervisor_v1']
    evidence=Path(args.evidence);shared.reject_links(evidence)
    evidence.mkdir(parents=True,exist_ok=False)
    rows=[];failure=None
    try:
        for case,source,timeout,limit in [('success','print("document-owned-python-smoke")',10,4096),
                ('timeout','import time;time.sleep(30)',2,4096),
                ('output-limit','print("x"*8192)',10,1024)]:
            census=admission.preflight()
            if admission.SLOT.exists():raise ValueError('Existing exclusive SDK claim prevents smoke')
            result,row=owned.run_owned([sys.executable,'-B','-c',source],timeout=timeout,output_limit=limit,
                memory_limit=256*1024*1024,cpu_rate=5000,log_path=evidence/(case+'.log'))
            rows.append(dict(case=case,admission=census,resource=row))
            (evidence/'resource-ledger.json').write_text(json.dumps(rows,indent=2))
            evaluate(case,result,row)
    except BaseException as error:
        failure=type(error).__name__+': '+str(error)
        if getattr(error,'resource_row',None) is not None:rows.append(dict(case='attention',resource=error.resource_row))
    finally:
        def checkpoint(kind,state):
            encoded=json.dumps(dict(kind=kind,owner=admission.owner_identity(),state=state)).encode()
            if len(encoded)>64*1024:raise ValueError('Cleanup checkpoint bound exceeded')
            (evidence/'cleanup-checkpoint.json').write_bytes(encoded)
        def recover(lease):
            row=owned.recover_quarantined(lease);rows.append(dict(case='recovered',resource=row))
        cleanup=supervisor.supervise(owned._quarantined,recover,None,None,checkpoint)
        (evidence/'receipt.json').write_text(json.dumps(dict(state='WindowsPythonSmokePassed' if not failure else 'Failed',
            failure=failure,resources=rows,cleanup=cleanup,SDKStarted=False,rootPermitCreated=False,
            nativeTestsExecuted=False,observedUtc=datetime.now(timezone.utc).isoformat()),indent=2))
    if failure:raise SystemExit(1)
    print('Actual bounded Windows Python smoke passed; no SDK grant or native validation.')


if __name__=='__main__':main()
