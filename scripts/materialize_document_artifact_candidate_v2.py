"""Document upload53 intake bound to one reviewed shared extractor and exact policy."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import stat
import types

REPOSITORY = 'MALIEV-Co-Ltd/Legacy.Maliev.DocumentService'
BASE = 'b37c45ac92b1cdfd159a1f3fdb973f3588b45e91'
ROOT = 'D:/codex-temp/2026-10-03/legacy-code-workflows-20261003'
CANDIDATE_SEAL = 'bbaccc97550d923fb3148672de5eb4235b8ded02909b9294215b97cee76d5ef7'
DEPENDENCY_SEAL = 'edd136d82f33ae7b0450ba0bed80d8e4825d079522c51c69f46a75dfdf51834f'
CAPSULE_BLOB = 'a5af173dec91af1efa169f65ef55d7450ff10f86'
POLICY_SHA256 = 'a17b7a53fbe7b881dd6493980e753414a394a43a7b45622665fe8db05a9e7841'
SHARED_SHA256 = '44a8a5accac9da11422d606be02fe28487642215df511b5f1c4284296a453ee2'
MAX_POLICY_BYTES = 512 * 1024
MAX_SHARED_BYTES = 64 * 1024
MAX_BASE_BYTES = 64 * 1024 * 1024
MAX_BASE_FILE_BYTES = 16 * 1024 * 1024
MAX_METADATA_BYTES = 16 * 1024 * 1024
MAX_METADATA_FILES = 256


def bind_shared(policy_path, shared_path, *, policy_sha=POLICY_SHA256, shared_sha=SHARED_SHA256):
    # Compare exact reviewed bytes BEFORE importing executable helper code,
    # parsing policy addresses, network access or any target filesystem write.
    if not all(type(v) is str and re.fullmatch('[0-9a-f]{64}', v) for v in [policy_sha, shared_sha]):
        raise ValueError('Final reviewed shared extractor/policy seals pending')
    with Path(policy_path).open('rb') as stream:raw=stream.read(MAX_POLICY_BYTES+1)
    if len(raw)>MAX_POLICY_BYTES:raise ValueError('Policy exceeds bound')
    if not raw or hashlib.sha256(raw).hexdigest() != policy_sha:
        raise ValueError('Exact policy bytes differ')
    helper = Path(shared_path)
    if helper.is_symlink():
        raise ValueError('Shared helper link/size refused')
    with helper.open('rb') as stream:helper_bytes=stream.read(MAX_SHARED_BYTES+1)
    if len(helper_bytes)>MAX_SHARED_BYTES or hashlib.sha256(helper_bytes).hexdigest() != shared_sha:
        raise ValueError('Exact shared extractor bytes differ')
    shared=types.ModuleType('approved_document_source_capsule')
    shared.__file__=str(helper)
    # Execute exactly the bytes hashed above, never re-read a mutable source path.
    exec(compile(helper_bytes,str(helper),'exec'),shared.__dict__)
    policy = shared.parse_json(raw)
    validate_profile(policy, shared_sha)
    return shared, policy


def validate_profile(policy, shared_sha):
    required = {'schemaVersion', 'repository', 'acceptedBase', 'root', 'candidateWorktree',
        'sourceSealSha256', 'dependencyManifestSha256', 'sharedModuleSha256', 'executionMode',
        'bundleSha256', 'bundleBytes', 'entries', 'baseFiles', 'selectedCandidatePaths', 'helperPaths'}
    if type(policy) is not dict or set(policy) != required:
        raise ValueError('Document policy fields differ')
    exact = dict(schemaVersion=1, repository=REPOSITORY, acceptedBase=BASE, root=ROOT,
        candidateWorktree=ROOT+'/work/documentservice-artifact-upload-contract-20261008',
        sourceSealSha256=CANDIDATE_SEAL, dependencyManifestSha256=DEPENDENCY_SEAL,
        sharedModuleSha256=shared_sha, executionMode='source-intake-only')
    if type(policy['schemaVersion']) is not int or any(policy[k] != v for k,v in exact.items()):
        raise ValueError('Document owner/base/seal/mode binding differs')
    paths={'.github/workflows/_build-and-test.yml','.github/workflows/receipt-amount-evidence.yml',
        'Legacy.Maliev.DocumentService.Tests/Workflows/WorkflowContractTests.cs'}
    if type(policy['selectedCandidatePaths']) is not list or len(policy['selectedCandidatePaths']) != 3 or set(policy['selectedCandidatePaths']) != paths:
        raise ValueError('Only reviewed Document upload53 three-file overlay is allowed')
    if type(policy['baseFiles']) is not list or len(policy['baseFiles']) != 165:
        raise ValueError('Exact immutable Document baseline inventory differs')
    if type(policy['bundleBytes']) is not int or not 0 < policy['bundleBytes'] <= 1024 * 1024:
        raise ValueError('Document capsule byte bound differs')
    if type(policy['bundleSha256']) is not str or not re.fullmatch('[0-9a-f]{64}',policy['bundleSha256']):
        raise ValueError('Document capsule digest missing')
    if type(policy['entries']) is not list or not 0 < len(policy['entries']) <= 256:
        raise ValueError('Document capsule inventory bound differs')
    helpers=['run_document_artifact_upload_native_20261008_v4.py','workflows_native_admission_v3.py',
        'workflows_snapshot_census_v3.py','artifact_pin_native_result_validation_v3.py',
        'workflows_owned_command_v1.py','workflows_cleanup_supervisor_v1.py']
    if policy['helperPaths'] != helpers:
        raise ValueError('Exact reviewed Windows helper graph differs')


def read_base(checkout, policy, shared):
    checkout=Path(checkout);shared.reject_links(checkout)
    metadata=checkout/'.git';shared.reject_links(metadata)
    if not metadata.is_dir() or (metadata/'HEAD').read_text(encoding='ascii').strip() != BASE:
        raise ValueError('Exact detached Document baseline required')
    expected={}; aliases=set()
    for row in policy['baseFiles']:
        path=shared.canonical_path(row['path'])
        if path in expected or path.casefold() in aliases or type(row['checkoutBytes']) is not int or not 0 <= row['checkoutBytes'] <= MAX_BASE_FILE_BYTES:
            raise ValueError('Invalid/aliased Document baseline inventory')
        if not re.fullmatch('[0-9a-f]{64}',row['checkoutSha256']):raise ValueError('Missing baseline byte seal')
        aliases.add(path.casefold());expected[path]=row
    source={};git_files={};source_bytes=0;metadata_bytes=0
    for directory,folders,names in os.walk(checkout,followlinks=False):
        for name in [*folders,*names]:shared.reject_links(Path(directory)/name)
        for name in names:
            path=Path(directory)/name;relative=path.relative_to(checkout).as_posix()
            if not stat.S_ISREG(path.lstat().st_mode):raise ValueError('Nonregular Document checkout file')
            size=path.stat().st_size
            if relative.startswith('.git/'):
                if size > MAX_METADATA_BYTES:raise ValueError('Git metadata file exceeds bound')
                metadata_bytes+=size
                if metadata_bytes > MAX_METADATA_BYTES or len(git_files) >= MAX_METADATA_FILES:raise ValueError('Git metadata graph exceeds bound')
                with path.open('rb') as stream:raw=stream.read(size+1)
                if len(raw) != size:raise ValueError('Git metadata changed during read')
                git_files[relative]=raw
            else:
                shared.canonical_path(relative)
                if relative not in expected or size != expected[relative]['checkoutBytes']:raise ValueError('Foreign/stale Document baseline source')
                source_bytes+=size
                if source_bytes > MAX_BASE_BYTES:raise ValueError('Document baseline graph exceeds bound')
                with path.open('rb') as stream:raw=stream.read(size+1)
                if len(raw) != size or shared.digest(raw) != expected[relative]['checkoutSha256']:raise ValueError('Document baseline raw projection differs')
                source[relative]=raw
    if set(source) != set(expected):raise ValueError('Document baseline source missing')
    if any(value in git_files.get('.git/config',b'').lower() for value in [b'extraheader',b'password',b'access_token',b'credential']):
        raise ValueError('Checkout retained credential configuration')
    return source,git_files


def validate_payload(files, policy, shared):
    seal_name='outputs/consumer-artifact-pin-contract-source-20261008-v1/source-seal.json'
    deps_name='outputs/consumer-artifact-pin-native-preparation-20261008-v1/dependency-manifest.json'
    if shared.digest(files[seal_name]) != CANDIDATE_SEAL or shared.digest(files[deps_name]) != DEPENDENCY_SEAL:
        raise ValueError('Immutable nested source/dependency manifest differs')
    source=shared.parse_json(files[seal_name]);deps=shared.parse_json(files[deps_name])
    selected=[c for c in source['cohorts'] if c['application']=='Legacy.Maliev.DocumentService' and c['existingPullRequest']==53]
    if len(selected) != 1:raise ValueError('Document candidate association ambiguous')
    candidate=selected[0]
    if candidate['base'] != BASE or candidate['worktree'].replace('\\','/') != policy['candidateWorktree'] or len(candidate['files']) != 3:
        raise ValueError('Document candidate association differs')
    expected={seal_name,deps_name,*('outputs/'+p for p in policy['helperPaths'])}
    for row in candidate['files']:
        key='candidate/raw/'+shared.canonical_path(row['path'])
        if row['path'] not in policy['selectedCandidatePaths'] or shared.digest(files[key]) != row['preparedSha256']:
            raise ValueError('Document candidate raw source differs')
        expected.add(key)
    if deps['workspace'].replace('\\','/') != ROOT+'/work/artifact-pin-native-dependencies-20261008' or len(deps['dependencies']) != 2:
        raise ValueError('Pinned Document dependency root differs')
    allowed={'Legacy.Maliev.ServiceDefaults':'8f4f5f27b226ffe406c4c79b1903742e8c2e7dd3',
        'Legacy.Maliev.CompatibilityContracts':'78e48ffc4ee000df0510cba5e7c7a3c4c4d539d7'}
    if {d['repository']:d['commit'] for d in deps['dependencies']} != allowed:raise ValueError('Pinned dependency revisions differ')
    count=0
    for dependency in deps['dependencies']:
        for row in dependency['files']:
            relative=shared.canonical_path(row['path'].replace('\\','/'))
            key='dependencies/'+relative
            if shared.digest(files[key]) != row['sha256']:raise ValueError('Pinned dependency raw source differs')
            expected.add(key);count+=1
    if count != 129 or set(files) != expected:raise ValueError('Extra/missing Document capsule source')
    return candidate


def validate_git_metadata(metadata):
    if metadata.get('.git/HEAD',b'').decode('ascii').strip()!=BASE:
        raise ValueError('Exact detached metadata HEAD required')
    directories={'.git','.git/branches','.git/hooks','.git/info','.git/logs',
        '.git/logs/refs','.git/logs/refs/heads','.git/logs/refs/remotes','.git/logs/refs/remotes/origin',
        '.git/objects','.git/objects/info','.git/objects/pack','.git/refs','.git/refs/heads',
        '.git/refs/tags','.git/refs/remotes','.git/refs/remotes/origin'}
    aliases=set()
    for relative in metadata:
        parts=relative.split('/')
        if len(parts)<2 or parts[0]!='.git' or any(part in {'','..','.'} or '\\' in part or ':' in part for part in parts):
            raise ValueError('Foreign Git metadata path refused')
        if relative.casefold() in aliases:raise ValueError('Git metadata alias refused')
        aliases.add(relative.casefold())
        parent='/'.join(parts[:-1])
        if parent not in directories and not re.fullmatch(r'\.git/objects/[0-9a-f]{2}',parent):
            raise ValueError('Foreign Git metadata directory refused')
        if relative.casefold() in {directory.casefold() for directory in directories} or re.fullmatch(r'\.git/objects/[0-9a-f]{2}',relative):
            raise ValueError('Git directory cannot be a file')


def materialize(root, base_source, metadata, files, policy, shared):
    root=Path(root);shared.reject_links(root)
    if root.exists() or str(root).replace('\\','/') != ROOT:raise ValueError('Fresh fixed Document qualification root required')
    candidate=validate_payload(files,policy,shared)
    validate_git_metadata(metadata)
    candidate_root=root/'work/documentservice-artifact-upload-contract-20261008'
    # All input graphs have already been bounded/sealed; writes use the one shared
    # extractor's no-overwrite/link refusal, not an adapter-specific ZIP decoder.
    root.mkdir(parents=True)
    replacements=set(policy['selectedCandidatePaths'])
    for path,raw in base_source.items():
        if path not in replacements:shared.write_new(candidate_root,path,raw)
    for path,raw in metadata.items():
        target=candidate_root/path;shared.reject_links(target);target.parent.mkdir(parents=True,exist_ok=True)
        with target.open('xb') as stream:stream.write(raw)
        if shared.digest(target.read_bytes()) != shared.digest(raw):raise ValueError('Document Git metadata copy differs')
    # Git requires refs even when a detached checkout contains no reference files.
    refs=candidate_root/'.git/refs';shared.reject_links(refs);refs.mkdir(exist_ok=True)
    for row in candidate['files']:shared.write_new(candidate_root,row['path'],files['candidate/raw/'+row['path']])
    for path,raw in files.items():
        if path.startswith('outputs/'):shared.write_new(root,path,raw)
        elif path.startswith('dependencies/'):shared.write_new(root/'work/artifact-pin-native-dependencies-20261008',path[len('dependencies/'):],raw)
    return candidate_root


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--policy',required=True);parser.add_argument('--shared-module',required=True)
    parser.add_argument('--base-checkout',required=True);parser.add_argument('--capsule-blob',required=True)
    args=parser.parse_args()
    shared,policy=bind_shared(args.policy,args.shared_module)
    if args.capsule_blob != CAPSULE_BLOB:raise ValueError('Only the exact Document capsule Git blob is allowed')
    base,metadata=read_base(args.base_checkout,policy,shared)
    capsule=shared.fetch_git_blob(REPOSITORY,args.capsule_blob)
    files=shared.validate_zip(capsule,policy['bundleSha256'],policy['bundleBytes'],policy['entries'])
    materialize(ROOT,base,metadata,files,policy,shared)
    print('Exact Document source reconstructed; no SDK or grant was created.')


if __name__=='__main__':main()
