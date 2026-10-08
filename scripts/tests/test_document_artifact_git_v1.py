from pathlib import Path
from contextlib import redirect_stdout
import io
import json
import os
import sys
import tempfile
import types
import unittest
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import probe_document_artifact_git_v1 as probe


class GitProbeControls(unittest.TestCase):
    def permit_transport(self):
        workflow=(Path(__file__).resolve().parents[2]/'.github/workflows/document-artifact-native-qualification.yml').read_text()
        block=workflow.split('          import json,os,pathlib\n',1)[1].split("          '@ | python -B -",1)[0]
        return 'import json,os,pathlib\n'+''.join(line[10:]+'\n' for line in block.splitlines())
    def test_actual_workflow_event_transport_preserves_bytes_and_escapes_mask(self):
        code=self.permit_transport()
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);event=root/'event.json';permit=root/'root-permits.json'
            body='fixture-only % value\r\nnot an issued grant'
            event.write_text(json.dumps({'inputs':{'root-permit-bundle':body}}))
            original_path=Path
            def resolve(value):return permit if value=='root-permits.json' else original_path(value)
            output=io.StringIO()
            with patch.dict(os.environ,GITHUB_EVENT_PATH=str(event)),patch('pathlib.Path',side_effect=resolve),redirect_stdout(output):
                exec(compile(code,'actual-workflow-transport','exec'),{})
            self.assertEqual(body.encode(),permit.read_bytes())
            self.assertEqual('::add-mask::fixture-only %25 value%0D%0Anot an issued grant\n',output.getvalue())
    def test_actual_workflow_event_transport_refuses_oversized_bundle_before_write(self):
        code=self.permit_transport()
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);event=root/'event.json';permit=root/'root-permits.json'
            event.write_text(json.dumps({'inputs':{'root-permit-bundle':'x'*4097}}))
            original_path=Path
            def resolve(value):return permit if value=='root-permits.json' else original_path(value)
            with patch.dict(os.environ,GITHUB_EVENT_PATH=str(event)),patch('pathlib.Path',side_effect=resolve),self.assertRaises(AssertionError):
                exec(compile(code,'actual-workflow-transport','exec'),{})
            self.assertFalse(permit.exists())
    def row(self):
        return dict(cleanup_verified=True,readers_settled=True,process_handle_closed=True,thread_handle_closed=True,
            job_handle_closed=True,pipe_handles_closed=True,job_caps_readback_verified=True,membership_verified_before_resume=True,
            terminal_state_verified=True,attribute_list_disposed=True,remaining_job_processes=0,retained_unverified_handles=False,
            pid=1,actual_start_filetime=1,executable_identity='git.exe',job_memory_limit_bytes=256*1024*1024,
            job_cpu_rate=5000,job_active_process_limit=64,stop_reason=None,output_truncated=False)
    def result(self,body,code=0,stderr=b''):return types.SimpleNamespace(stdout=body,stderr=stderr,returncode=code)
    def test_actual_command_result_must_match_exact_head(self):
        probe.evaluate('head',self.result((probe.BASE+'\n').encode()),self.row(),[])
        with self.assertRaisesRegex(ValueError,'base'):
            probe.evaluate('head',self.result(b'0'*40),self.row(),[])
    def test_exact_scope_join_refuses_duplicates_and_foreign_paths(self):
        probe.evaluate('scope',self.result(b'a\nb\n'),self.row(),['a','b'])
        for body in [b'a\na\n',b'a\nforeign\n',b'a\n']:
            with self.assertRaises(ValueError):probe.evaluate('scope',self.result(body),self.row(),['a','b'])
    def test_plain_git_exit128_and_stderr_are_never_success(self):
        for result in [self.result(b'',128,b'fatal: not a git repository'),self.result((probe.BASE+'\n').encode(),0,b'warning')]:
            with self.assertRaisesRegex(ValueError,'failed'):probe.evaluate('head',result,self.row(),[])
    def test_unclosed_resources_or_survivors_refuse(self):
        for key,value in [('readers_settled',False),('remaining_job_processes',1),('retained_unverified_handles',True),('pid',None)]:
            row=self.row();row[key]=value
            with self.assertRaises(ValueError):probe.evaluate('head',self.result((probe.BASE+'\n').encode()),row,[])
    def test_timeout_truncation_and_wider_job_refuse(self):
        for key,value in [('stop_reason','timeout'),('output_truncated',True),('job_cpu_rate',10000)]:
            row=self.row();row[key]=value
            with self.assertRaises(ValueError):probe.evaluate('head',self.result((probe.BASE+'\n').encode()),row,[])
    def test_workflow_git_probe_precedes_file_grant_transport_without_bundle_env(self):
        workflow=(Path(__file__).resolve().parents[2]/'.github/workflows/document-artifact-native-qualification.yml').read_text()
        self.assertNotIn('DOC_ROOT_PERMIT_BUNDLE',workflow)
        self.assertLess(workflow.index('Require actual plain Git HEAD'),workflow.index('Receive and mask fresh permits'))
        self.assertLess(workflow.index('Receive and mask fresh permits'),workflow.index('Require fresh Root permits'))
        self.assertIn("os.environ['GITHUB_EVENT_PATH']",workflow)
        self.assertIn("print('::add-mask::'",workflow)


if __name__=='__main__':unittest.main()
