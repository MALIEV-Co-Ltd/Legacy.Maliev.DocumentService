import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import materialize_document_artifact_candidate_v2 as adapter


def policy(shared_sha='1'*64):
    return dict(schemaVersion=1,repository=adapter.REPOSITORY,acceptedBase=adapter.BASE,root=adapter.ROOT,
        candidateWorktree=adapter.ROOT+'/work/documentservice-artifact-upload-contract-20261008',
        sourceSealSha256=adapter.CANDIDATE_SEAL,dependencyManifestSha256=adapter.DEPENDENCY_SEAL,
        sharedModuleSha256=shared_sha,executionMode='source-intake-only',bundleSha256='2'*64,bundleBytes=1,
        entries=[{}],baseFiles=[{}]*165,selectedCandidatePaths=['.github/workflows/_build-and-test.yml',
        '.github/workflows/receipt-amount-evidence.yml','Legacy.Maliev.DocumentService.Tests/Workflows/WorkflowContractTests.cs'],
        helperPaths=['run_document_artifact_upload_native_20261008_v3.py','workflows_native_admission_v3.py',
        'workflows_snapshot_census_v3.py','artifact_pin_native_result_validation_v2.py','workflows_owned_command_v1.py',
        'workflows_cleanup_supervisor_v1.py'])


class Controls(unittest.TestCase):
    def test_final_seals_required_before_any_file_access(self):
        with self.assertRaisesRegex(ValueError,'seals pending'):
            adapter.bind_shared('does-not-exist','does-not-exist',policy_sha=None,shared_sha=None)
    def test_exact_profile_accepted(self):adapter.validate_profile(policy(),'1'*64)
    def test_wrong_policy_hash_refuses_before_helper_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'policy').write_bytes(b'{}')
            (root/'shared.py').write_text('raise AssertionError("must never execute")')
            with self.assertRaisesRegex(ValueError,'policy bytes differ'):
                adapter.bind_shared(root/'policy',root/'shared.py',policy_sha='0'*64,shared_sha='0'*64)
    def test_wrong_shared_hash_refuses_before_helper_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);raw=json.dumps(policy()).encode();(root/'policy').write_bytes(raw)
            (root/'shared.py').write_text('raise AssertionError("must never execute")')
            with self.assertRaisesRegex(ValueError,'extractor bytes differ'):
                adapter.bind_shared(root/'policy',root/'shared.py',policy_sha=hashlib.sha256(raw).hexdigest(),shared_sha='0'*64)
    def test_exact_module_and_policy_bytes_joined(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);helper=b'import json\ndef parse_json(raw):return json.loads(raw)\n'
            digest=hashlib.sha256(helper).hexdigest();raw=json.dumps(policy(digest)).encode()
            (root/'policy').write_bytes(raw);(root/'shared.py').write_bytes(helper)
            shared,accepted=adapter.bind_shared(root/'policy',root/'shared.py',policy_sha=hashlib.sha256(raw).hexdigest(),shared_sha=digest)
            self.assertEqual(adapter.BASE,accepted['acceptedBase']);self.assertTrue(callable(shared.parse_json))
    def test_each_owner_base_root_or_manifest_binding_refused(self):
        for name in ['repository','acceptedBase','root','candidateWorktree','sourceSealSha256','dependencyManifestSha256','sharedModuleSha256','executionMode']:
            with self.subTest(field=name):
                changed=policy();changed[name]='foreign'
                with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_download_candidate_cannot_be_substituted(self):
        changed=policy();changed['selectedCandidatePaths'].pop(1)
        with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_extra_candidate_path_cannot_expand_scope(self):
        changed=policy();changed['selectedCandidatePaths'].append('AppHost/Program.cs')
        with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_duplicate_candidate_path_refused(self):
        changed=policy();changed['selectedCandidatePaths'][1]=changed['selectedCandidatePaths'][0]
        with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_unknown_policy_field_refused(self):
        changed=policy();changed['networkUrl']='https://foreign.invalid'
        with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_boolean_version_or_size_refused(self):
        for name in ['schemaVersion','bundleBytes']:
            changed=policy();changed[name]=True
            with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_capsule_and_baseline_inventory_limits_refused(self):
        for name,value in [('baseFiles',[{}]*164),('entries',[{}]*257),('bundleBytes',1024*1024+1)]:
            changed=policy();changed[name]=value
            with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_foreign_native_helper_cannot_expand_graph(self):
        changed=policy();changed['helperPaths'].append('run_apphost.py')
        with self.assertRaises(ValueError):adapter.validate_profile(changed,'1'*64)
    def test_policy_size_is_bounded_before_parse(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);raw=b' '*(adapter.MAX_POLICY_BYTES+1);(root/'policy').write_bytes(raw)
            with self.assertRaisesRegex(ValueError,'Policy exceeds bound'):
                adapter.bind_shared(root/'policy',root/'absent',policy_sha=hashlib.sha256(raw).hexdigest(),shared_sha='0'*64)


if __name__=='__main__':unittest.main()
