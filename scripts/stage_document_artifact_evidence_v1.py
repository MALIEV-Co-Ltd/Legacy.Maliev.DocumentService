"""Preserve only bounded, reviewed owner evidence inside the upload workspace."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import uuid

from materialize_document_artifact_candidate_v2 import bind_shared, ROOT

MAX_FILES = 256
MAX_TOTAL_BYTES = 128*1024*1024
KERNEL = {'success.log':4096, 'timeout.log':4096, 'output-limit.log':1024,
    'receipt.json':256*1024, 'resource-ledger.json':256*1024}
PHASE_FILES = {'head.log':4*1024*1024, 'scope.log':4*1024*1024, 'phase.log':4*1024*1024,
    'journal.jsonl':256*1024, 'receipt.json':256*1024, 'strict-test-result.json':256*1024,
    'strict-audit-result.json':256*1024}
PHASES = {'build','focused','suite','format','audit'}


def canonical_uuid(value):
    try:
        parsed=uuid.UUID(value)
    except (ValueError, AttributeError):
        return False
    return parsed.int != 0 and str(parsed) == value


def children(directory):
    result=[]
    for child in directory.iterdir():
        if len(result)>=512:raise ValueError('Evidence directory inventory exceeds bound')
        result.append(child)
    return sorted(result)


def collect(workspace, native_outputs, shared):
    workspace=Path(workspace);native_outputs=Path(native_outputs)
    shared.reject_links(workspace);shared.reject_links(native_outputs)
    items=[];aliases=set();total=0
    def retain(source, relative, limit):
        nonlocal total
        relative=shared.canonical_path(relative)
        if relative.casefold() in aliases:raise ValueError('Evidence alias refused')
        shared.reject_links(source)
        if not stat.S_ISREG(source.stat().st_mode):raise ValueError('Evidence must be a regular file')
        with source.open('rb') as stream:raw=stream.read(limit+1)
        if len(raw)>limit:raise ValueError('Evidence file exceeds reviewed bound')
        total+=len(raw)
        if len(items)>=MAX_FILES or total>MAX_TOTAL_BYTES:raise ValueError('Evidence inventory exceeds bound')
        aliases.add(relative.casefold());items.append((relative,raw))
    kernel=workspace/'evidence/windows-smoke';shared.reject_links(kernel)
    if kernel.exists():
        for path in children(kernel):
            if path.name not in KERNEL:raise ValueError('Unreviewed kernel evidence refused')
            retain(path,'windows-smoke/'+path.name,KERNEL[path.name])
    stage=workspace/'evidence/native-stage';shared.reject_links(stage)
    if stage.exists():
        for path in children(stage):
            shared.reject_links(path)
            if path.name=='completed-phases.json':retain(path,'native-stage/completed-phases.json',256*1024)
            elif path.name not in {phase+'-root-permit.json' for phase in PHASES}:
                raise ValueError('Unreviewed stage evidence refused')
            # Actual grant bodies are never uploaded.
    if native_outputs.exists():
        for directory in children(native_outputs):
            if directory.name.startswith('snapshot-census-v3-'):
                if not canonical_uuid(directory.name.removeprefix('snapshot-census-v3-')):
                    raise ValueError('Snapshot owner identity refused')
                shared.reject_links(directory)
                for path in children(directory):
                    if path.name not in {'checkpoint.json','checkpoint.tmp'}:raise ValueError('Unreviewed snapshot evidence refused')
                    retain(path,'snapshot-census/'+directory.name+'/'+path.name,64*1024)
            elif directory.name.startswith('document-artifact-upload-native-v3-'):
                match=re.fullmatch(r'document-artifact-upload-native-v3-(build|focused|suite|format|audit)-(.+)',directory.name)
                if not match or not canonical_uuid(match[2]):raise ValueError('Native phase identity refused')
                shared.reject_links(directory)
                for current,folders,names in os.walk(directory,followlinks=False):
                    if len(folders)+len(names)>512:raise ValueError('Native evidence directory inventory exceeds bound')
                    for folder in folders:
                        target=Path(current)/folder;shared.reject_links(target)
                        if target.relative_to(directory).as_posix()!='test-results':raise ValueError('Unreviewed phase directory refused')
                    for name in sorted(names):
                        path=Path(current)/name;relative=path.relative_to(directory).as_posix()
                        if relative in PHASE_FILES:limit=PHASE_FILES[relative]
                        elif re.fullmatch(r'cleanup-[1-9][0-9]*\.json',relative):limit=256*1024
                        elif match[1] in {'focused','suite'} and relative=='test-results/'+match[1]+'.trx':limit=16*1024*1024
                        else:raise ValueError('Unreviewed native evidence refused')
                        retain(path,'native-phases/'+directory.name+'/'+relative,limit)
    return items


def stage(workspace, native_outputs, shared):
    workspace=Path(workspace)
    shared.reject_links(workspace)
    workspace=workspace.resolve();destination=workspace/'evidence-custody'
    shared.reject_links(destination)
    if destination.exists():raise ValueError('Fresh evidence custody directory required')
    items=collect(workspace,native_outputs,shared)
    destination.mkdir()
    manifest=[]
    for relative,raw in items:
        shared.write_new(destination,relative,raw)
        copied=destination/relative
        with copied.open('rb') as stream:readback=stream.read(len(raw)+1)
        if readback!=raw:raise ValueError('Exact evidence custody readback differs')
        manifest.append(dict(path=relative,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
    body=dict(schemaVersion=1,files=manifest,fileCount=len(items),totalBytes=sum(len(raw) for _,raw in items),
        originalEvidenceRewritten=False,RootPermitBodiesUploaded=False)
    shared.write_new(destination,'staging-manifest.json',(json.dumps(body,indent=2)+'\n').encode())
    return body


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--policy',required=True);parser.add_argument('--shared-module',required=True)
    args=parser.parse_args();shared,_=bind_shared(args.policy,args.shared_module)
    workspace=Path(os.environ['GITHUB_WORKSPACE'])
    shared.reject_links(workspace)
    stage(workspace,Path(ROOT)/'outputs',shared)
    print('Allowlisted owner evidence copied byte-for-byte into workspace custody.')


if __name__=='__main__':main()
