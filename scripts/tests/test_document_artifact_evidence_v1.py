import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import stage_document_artifact_evidence_v1 as custody


class Shared:
    def __init__(self, refused=None):self.refused=refused
    def reject_links(self,path):
        if Path(path)==self.refused or Path(path).is_symlink():raise ValueError('link refused')
    @staticmethod
    def canonical_path(path):
        if '..' in Path(path).parts or Path(path).is_absolute():raise ValueError('escape refused')
        return path
    def write_new(self,root,relative,raw):
        target=Path(root)/relative;self.reject_links(target);target.parent.mkdir(parents=True,exist_ok=True)
        with target.open('xb') as stream:stream.write(raw)


class CustodyControls(unittest.TestCase):
    def test_actual_shared_checker_refuses_linked_workspace_and_ancestor_before_writes(self):
        shared,_=custody.bind_shared(Path(__file__).resolve().parents[1]/'document-upload53-policy.json',os.environ['DOC_SHARED_MODULE'])
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);workspace,outside,_=self.fixtures(root)
            linked=root/'linked-workspace'
            linked.symlink_to(workspace,target_is_directory=True)
            try:
                for raw in [linked,linked/'nested-workspace']:
                    with self.assertRaisesRegex(ValueError,'symlink/reparse'):
                        custody.stage(raw,outside,shared)
                    self.assertFalse((workspace/'evidence-custody').exists())
                    self.assertFalse((workspace/'nested-workspace').exists())
            finally:linked.unlink()

    def test_main_checks_raw_environment_workspace_before_resolution(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);workspace,outside,_=self.fixtures(root)
            linked=root/'linked-workspace';linked.symlink_to(workspace,target_is_directory=True)
            try:
                shared,_=custody.bind_shared(Path(__file__).resolve().parents[1]/'document-upload53-policy.json',os.environ['DOC_SHARED_MODULE'])
                with patch.object(custody,'bind_shared',return_value=(shared,None)),patch.dict(os.environ,GITHUB_WORKSPACE=str(linked)),patch.object(sys,'argv',['stage','--policy','ignored','--shared-module','ignored']),patch.object(custody,'stage') as dispatch:
                    with self.assertRaisesRegex(ValueError,'symlink/reparse'):custody.main()
                    dispatch.assert_not_called()
                self.assertFalse((workspace/'evidence-custody').exists())
            finally:linked.unlink()

    def fixtures(self,root):
        workspace=root/'workspace';kernel=workspace/'evidence/windows-smoke';kernel.mkdir(parents=True)
        outside=root/'outside-owner-outputs';snapshot=outside/'snapshot-census-v3-00000000-0000-0000-0000-000000000001';snapshot.mkdir(parents=True)
        payloads={'success.log':b'document-owned-python-smoke\r\n','timeout.log':b'', 'output-limit.log':b'x'*1024,
            'receipt.json':b'{"state":"Failed","failure":"first-error"}\r\n','resource-ledger.json':b'[]\n'}
        for name,raw in payloads.items():(kernel/name).write_bytes(raw)
        (snapshot/'checkpoint.json').write_bytes(b'{"exactOwner":"retained"}\r\n')
        return workspace,outside,payloads

    def test_causal_external_root_copied_inside_workspace_without_byte_rewrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,payloads=self.fixtures(Path(temporary))
            self.assertFalse(outside.is_relative_to(workspace))
            manifest=custody.stage(workspace,outside,Shared())
            self.assertEqual(6,manifest['fileCount'])
            for row in manifest['files']:
                target=workspace/'evidence-custody'/row['path'];self.assertTrue(target.is_relative_to(workspace))
                raw=target.read_bytes();self.assertEqual(len(raw),row['bytes']);self.assertEqual(hashlib.sha256(raw).hexdigest(),row['sha256'])
            for name,raw in payloads.items():self.assertEqual(raw,(workspace/'evidence-custody/windows-smoke'/name).read_bytes())

    def test_failed_kernel_first_error_preserved_not_converted_to_success(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));custody.stage(workspace,outside,Shared())
            data=json.loads((workspace/'evidence-custody/windows-smoke/receipt.json').read_bytes())
            self.assertEqual({'state':'Failed','failure':'first-error'},data)

    def test_permit_bodies_excluded_and_only_completed_phase_metadata_retained(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));stage=workspace/'evidence/native-stage';stage.mkdir()
            (stage/'build-root-permit.json').write_bytes(b'must-not-upload')
            (stage/'completed-phases.json').write_bytes(b'{"completed":["build"]}')
            manifest=custody.stage(workspace,outside,Shared())
            self.assertFalse(manifest['RootPermitBodiesUploaded'])
            self.assertFalse(list((workspace/'evidence-custody').rglob('*root-permit*')))
            self.assertTrue((workspace/'evidence-custody/native-stage/completed-phases.json').is_file())

    def test_arbitrary_customer_log_refused_before_custody_writes(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));(workspace/'evidence/windows-smoke/customer.log').write_bytes(b'forbidden')
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())
            self.assertFalse((workspace/'evidence-custody').exists())

    def test_link_refusal_propagates_before_any_copy(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary))
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared(workspace/'evidence/windows-smoke/receipt.json'))
            self.assertFalse((workspace/'evidence-custody').exists())

    def test_file_total_and_count_bounds_refuse_without_writes(self):
        for attribute,value in [('MAX_FILES',1),('MAX_TOTAL_BYTES',1)]:
            with tempfile.TemporaryDirectory() as temporary:
                workspace,outside,_=self.fixtures(Path(temporary))
                with patch.object(custody,attribute,value),self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())
                self.assertFalse((workspace/'evidence-custody').exists())
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));(workspace/'evidence/windows-smoke/output-limit.log').write_bytes(b'x'*1025)
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())

    def test_invalid_owner_uuid_and_unreviewed_nested_native_files_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));(outside/'snapshot-census-v3-not-an-owner').mkdir()
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));native=outside/'document-artifact-upload-native-v3-build-00000000-0000-0000-0000-000000000002';native.mkdir()
            (native/'private-payload.json').write_bytes(b'forbidden')
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())

    def test_reviewed_native_receipt_trx_and_cleanup_checkpoint_bytes_retained(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));native=outside/'document-artifact-upload-native-v3-suite-00000000-0000-0000-0000-000000000002';(native/'test-results').mkdir(parents=True)
            for relative,raw in [('receipt.json',b'{"failure":"original"}'),('cleanup-1.json',b'{"retained":true}'),('test-results/suite.trx',b'<TestRun/>')]:
                (native/relative).write_bytes(raw)
            manifest=custody.stage(workspace,outside,Shared());self.assertEqual(9,manifest['fileCount'])
            self.assertEqual(b'<TestRun/>',(workspace/'evidence-custody/native-phases'/native.name/'test-results/suite.trx').read_bytes())

    def test_alias_and_preexisting_destination_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace,outside,_=self.fixtures(Path(temporary));shared=Shared()
            with patch.object(shared,'canonical_path',lambda value:'same'),self.assertRaises(ValueError):custody.stage(workspace,outside,shared)
            (workspace/'evidence-custody').mkdir()
            with self.assertRaises(ValueError):custody.stage(workspace,outside,Shared())


if __name__=='__main__':unittest.main()
