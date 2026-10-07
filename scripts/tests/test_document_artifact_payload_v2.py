import io
import json
import os
from pathlib import Path
import tarfile
import tempfile
import unittest
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import zipfile
import materialize_document_artifact_candidate_v2 as adapter

POLICY=Path(os.environ.get('DOC_POLICY_FILE',str(Path(__file__).resolve().parents[1]/'document-upload53-policy.json')))
SHARED=Path(os.environ['DOC_SHARED_MODULE'])



class Controls(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.shared,cls.policy=adapter.bind_shared(POLICY,SHARED)
        cls.data=Path(os.environ['DOC_CAPSULE_FILE']).read_bytes() if 'DOC_CAPSULE_FILE' in os.environ else cls.shared.fetch_git_blob(adapter.REPOSITORY,adapter.CAPSULE_BLOB)
        cls.files=cls.shared.validate_zip(cls.data,cls.policy['bundleSha256'],cls.policy['bundleBytes'],cls.policy['entries'])
    def test_actual_reviewed_shared_module_bound_to_exact_policy(self):
        self.assertEqual('44a8a5accac9da11422d606be02fe28487642215df511b5f1c4284296a453ee2',self.policy['sharedModuleSha256'])
    def test_actual_140_entry_document_graph_accepted(self):
        candidate=adapter.validate_payload(self.files,self.policy,self.shared)
        self.assertEqual(53,candidate['existingPullRequest']);self.assertEqual(3,len(candidate['files']))
        self.assertEqual(140,len(self.files))
    def test_extra_source_cannot_expand_graph(self):
        changed=dict(self.files,**{'dependencies/foreign.cs':b'not allowed'})
        with self.assertRaises(ValueError):adapter.validate_payload(changed,self.policy,self.shared)
    def test_missing_dependency_cannot_be_silently_ignored(self):
        changed=dict(self.files);del changed[next(k for k in changed if k.startswith('dependencies/'))]
        with self.assertRaises((KeyError,ValueError)):adapter.validate_payload(changed,self.policy,self.shared)
    def test_candidate_raw_byte_change_refused(self):
        changed=dict(self.files);key=next(k for k in changed if k.startswith('candidate/raw/'))
        changed[key]+=b'\n'
        with self.assertRaises(ValueError):adapter.validate_payload(changed,self.policy,self.shared)
    def test_source_and_dependency_manifest_changes_refused(self):
        for key in [k for k in self.files if k.endswith('source-seal.json') or k.endswith('dependency-manifest.json')]:
            changed=dict(self.files);changed[key]+=b' '
            with self.assertRaises(ValueError):adapter.validate_payload(changed,self.policy,self.shared)
    def test_archive_bytes_refused_by_reused_extractor(self):
        with self.assertRaises(ValueError):
            self.shared.validate_zip(self.data+b'foreign',self.policy['bundleSha256'],self.policy['bundleBytes'],self.policy['entries'])
    def test_wrong_root_refused_before_any_materialization(self):
        with self.assertRaises(ValueError):
            adapter.materialize(POLICY.parent/'must-not-be-created',{}, {},self.files,self.policy,self.shared)
        self.assertFalse((POLICY.parent/'must-not-be-created').exists())


if __name__=='__main__':unittest.main()
