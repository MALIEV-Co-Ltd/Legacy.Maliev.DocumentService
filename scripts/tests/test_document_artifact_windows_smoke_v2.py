import copy
from types import SimpleNamespace
import unittest
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from smoke_document_artifact_windows_v2 import evaluate


def fixture(reason=None):
    row=dict(cleanup_verified=True,readers_settled=True,process_handle_closed=True,thread_handle_closed=True,
        job_handle_closed=True,pipe_handles_closed=True,job_caps_readback_verified=True,membership_verified_before_resume=True,
        terminal_state_verified=True,attribute_list_disposed=True,retained_unverified_handles=False,
        remaining_job_processes=0,job_memory_limit_bytes=256*1024*1024,job_cpu_rate=5000,job_active_process_limit=64,
        actual_start_filetime=123456,pid=123,executable_identity='python.exe',ports=[],persistent_data=False,stop_reason=reason)
    return SimpleNamespace(returncode=0,stdout=b'document-owned-python-smoke\r\n'),row


class Controls(unittest.TestCase):
    def test_success_requires_actual_command_and_release(self):evaluate('success',*fixture())
    def test_timeout_requires_actual_timeout_path(self):evaluate('timeout',*fixture('timeout'))
    def test_overflow_requires_actual_output_limit_path(self):evaluate('output-limit',*fixture('output-limit'))
    def test_every_missing_release_flag_refused(self):
        result,row=fixture()
        for key,value in row.items():
            if value is True:
                changed=copy.deepcopy(row);changed[key]=False
                with self.assertRaises(ValueError):evaluate('success',result,changed)
    def test_active_job_or_missing_identity_refused(self):
        result,row=fixture()
        for key,value in [('remaining_job_processes',1),('pid',None),('actual_start_filetime',None),('executable_identity','')]:
            changed=copy.deepcopy(row);changed[key]=value
            with self.assertRaises(ValueError):evaluate('success',result,changed)
    def test_wrong_caps_or_persistent_resources_refused(self):
        result,row=fixture()
        for key,value in [('job_memory_limit_bytes',0),('job_cpu_rate',10000),('job_active_process_limit',0),('persistent_data',True),('ports',[8080])]:
            changed=copy.deepcopy(row);changed[key]=value
            with self.assertRaises(ValueError):evaluate('success',result,changed)
    def test_timeout_cannot_be_console_success(self):
        with self.assertRaises(ValueError):evaluate('timeout',*fixture())
    def test_success_cannot_be_wrong_python_output(self):
        result,row=fixture();result.stdout=b'not the selected command'
        with self.assertRaises(ValueError):evaluate('success',result,row)
    def test_unknown_case_cannot_expand_execution(self):
        with self.assertRaises(ValueError):evaluate('native',*fixture())


if __name__=='__main__':unittest.main()
