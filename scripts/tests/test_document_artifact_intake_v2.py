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
        helperPaths=['run_document_artifact_upload_native_20261008_v4.py','workflows_native_admission_v3.py',
        'workflows_snapshot_census_v3.py','artifact_pin_native_result_validation_v3.py','workflows_owned_command_v1.py',
        'workflows_cleanup_supervisor_v1.py'])


class Controls(unittest.TestCase):
    def test_actual_candidate_workflow_capsule_argument_reaches_exact_cli_gate(self):
        import re
        from unittest.mock import patch
        workflow = (Path(__file__).resolve().parents[2]/'.github/workflows/document-artifact-candidate-qualification.yml').read_text()
        arguments = re.findall(r'--capsule-blob ([0-9a-f]{40})', workflow)
        self.assertEqual([adapter.CAPSULE_BLOB], arguments)
        argv = ['adapter', '--policy', 'unused', '--shared-module', 'unused', '--base-checkout', 'unused', '--capsule-blob', arguments[0]]
        with patch.object(sys, 'argv', argv), patch.object(adapter, 'bind_shared', return_value=(None, {})), patch.object(adapter, 'read_base', side_effect=RuntimeError('exact capsule gate reached')) as read:
            with self.assertRaisesRegex(RuntimeError, 'exact capsule gate reached'): adapter.main()
            read.assert_called_once()

    def test_actual_native_workflow_capsule_argument_reaches_exact_cli_gate(self):
        import re
        from unittest.mock import patch
        workflow = (Path(__file__).resolve().parents[2]/'.github/workflows/document-artifact-native-qualification.yml').read_text()
        arguments = re.findall(r'--capsule-blob ([0-9a-f]{40})', workflow)
        self.assertEqual([adapter.CAPSULE_BLOB], arguments)
        argv = ['adapter', '--policy', 'unused', '--shared-module', 'unused', '--base-checkout', 'unused', '--capsule-blob', arguments[0]]
        with patch.object(sys, 'argv', argv), patch.object(adapter, 'bind_shared', return_value=(None, {})), patch.object(adapter, 'read_base', side_effect=RuntimeError('exact native capsule gate reached')) as read:
            with self.assertRaisesRegex(RuntimeError, 'exact native capsule gate reached'): adapter.main()
            read.assert_called_once()

    def test_previous_capsule_cli_argument_refused_before_any_base_or_network_read(self):
        from unittest.mock import patch
        argv = ['adapter', '--policy', 'unused', '--shared-module', 'unused', '--base-checkout', 'unused', '--capsule-blob', '47a29480a775cf0949c5de30108605bc4d4ea3f3']
        with patch.object(sys, 'argv', argv), patch.object(adapter, 'bind_shared', return_value=(None, {})), patch.object(adapter, 'read_base') as read:
            with self.assertRaisesRegex(ValueError, 'exact Document capsule'): adapter.main()
            read.assert_not_called()

    def test_detached_metadata_head_and_empty_refs_profile_accepted(self):
        adapter.validate_git_metadata({'.git/HEAD':(adapter.BASE+'\n').encode(),'.git/config':b'', '.git/objects/pack/fixture.pack':b'raw'})
    def test_wrong_metadata_head_refused(self):
        with self.assertRaisesRegex(ValueError,'HEAD'):
            adapter.validate_git_metadata({'.git/HEAD':b'0'*40})
    def test_metadata_alias_foreign_directory_and_escape_refused(self):
        for extra in [{'.git/config':b'', '.git/CONFIG':b''},{'.git/foreign/payload':b''},{'../foreign':b''},{'.git/refs':b'file'},{'.git/Refs':b'alias-file'},{'.git/objects/ab':b'file'}]:
            with self.subTest(extra=extra),self.assertRaises(ValueError):
                adapter.validate_git_metadata({'.git/HEAD':(adapter.BASE+'\n').encode(),**extra})
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
