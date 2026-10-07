import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import qualify_document_artifact_native_v1 as stage


class Shared:
    @staticmethod
    def parse_json(raw):
        def unique(pairs):
            result = {}
            for key, value in pairs:
                if key in result:
                    raise ValueError('duplicate')
                result[key] = value
            return result
        return json.loads(raw, object_pairs_hook=unique)


class Admission:
    def __init__(self): self.calls = []
    def validate(self, permit, phase, worktree, base, candidate, now):
        if permit['phase'] != phase or permit.get('expired'):
            raise ValueError('phase or expiry refused')
        self.calls.append((phase, str(worktree), base, candidate, now))


def bundle():
    return dict(schemaVersion=1, repository=stage.REPOSITORY, consumerHeadSha='a'*40,
        permits=[dict(phase=p, leaseId=str(i)) for i, p in enumerate(stage.PHASES)])


class NativeStageControls(unittest.TestCase):
    def test_exact_order_and_associations_sent_to_reviewed_admission(self):
        admission = Admission();now = datetime.now(timezone.utc)
        accepted = stage.validate_bundle(json.dumps(bundle()).encode(), 'a'*40, Shared, admission, now)
        self.assertEqual(5, len(accepted))
        self.assertEqual(list(stage.PHASES), [row[0] for row in admission.calls])
        self.assertTrue(all(row[2:]==(stage.BASE, stage.CANDIDATE_SEAL, now) for row in admission.calls))

    def test_foreign_head_repository_or_shape_refused_before_admission(self):
        for field, value in [('consumerHeadSha', 'b'*40), ('repository', 'foreign'), ('schemaVersion', True), ('extra', 'value')]:
            changed = bundle();changed[field] = value;admission = Admission()
            with self.subTest(field=field), self.assertRaises(ValueError):
                stage.validate_bundle(json.dumps(changed).encode(), 'a'*40, Shared, admission)
            self.assertEqual([], admission.calls)

    def test_missing_duplicate_or_reordered_phase_permits_refused(self):
        variants = []
        changed = bundle();changed['permits'].pop();variants.append(changed)
        changed = bundle();changed['permits'][1]['leaseId']=changed['permits'][0]['leaseId'];variants.append(changed)
        changed = bundle();changed['permits'].reverse();variants.append(changed)
        changed = bundle();changed['permits'][0]['expired']=True;variants.append(changed)
        for changed in variants:
            with self.assertRaises(ValueError):
                stage.validate_bundle(json.dumps(changed).encode(), 'a'*40, Shared, Admission())

    def test_oversized_duplicate_key_or_invalid_consumer_head_refused(self):
        for raw, head in [(b'x'*4097, 'a'*40), (b'{"schemaVersion":1,"schemaVersion":1}', 'a'*40), (json.dumps(bundle()).encode(), 'main')]:
            with self.assertRaises(ValueError):
                stage.validate_bundle(raw, head, Shared, Admission())

    def test_actual_orchestrator_build_first_and_barrier_before_next_execution(self):
        events=[]
        result=stage.sequence(bundle()['permits'], lambda phase,permit: events.append(('execute',phase)) or phase,
            lambda phase,result: events.append(('verified',phase)), lambda completed: events.append(('checkpoint',completed[-1])))
        self.assertEqual(list(stage.PHASES), result)
        self.assertEqual([(event,phase) for phase in stage.PHASES for event in ['execute','verified','checkpoint']], events)

    def test_failed_build_or_cleanup_barrier_stops_downstream_phases(self):
        for failure_phase in ['build','focused','suite','format','audit']:
            executed=[]
            def verify(phase,result):
                if phase==failure_phase: raise ValueError('injected cleanup/result refusal')
            with self.assertRaises(ValueError):
                stage.sequence(bundle()['permits'],lambda phase,permit: executed.append(phase),verify,lambda completed:None)
            self.assertEqual(list(stage.PHASES[:stage.PHASES.index(failure_phase)+1]),executed)

    def test_native_return_failure_stops_before_later_commands(self):
        executed=[]
        def execute(phase,permit):
            executed.append(phase)
            raise SystemExit(1)
        with self.assertRaises(SystemExit):
            stage.sequence(bundle()['permits'],execute,lambda phase,result:self.fail('verify must not run'),lambda completed:self.fail('checkpoint must not run'))
        self.assertEqual(['build'],executed)

    def test_checkpoint_failure_cannot_advance_to_next_sdk_phase(self):
        executed=[]
        def checkpoint(completed):raise OSError('injected evidence write failure')
        with self.assertRaises(OSError):
            stage.sequence(bundle()['permits'],lambda phase,permit:executed.append(phase),lambda phase,result:None,checkpoint)
        self.assertEqual(['build'],executed)

    def receipt(self, phase='build'):
        keys=['cleanup_verified','terminal_state_verified','readers_settled','process_handle_closed','thread_handle_closed','job_handle_closed','pipe_handles_closed','attribute_list_disposed','job_caps_readback_verified','membership_verified_before_resume']
        rows=[]
        for i, executable in enumerate(['git.exe','git.exe','dotnet.exe']):
            rows.append(dict(**dict.fromkeys(keys,True),pid=i+1,actual_start_filetime=i+100,remaining_job_processes=0,
                retained_unverified_handles=False,stop_reason=None,output_truncated=False,executable_identity=executable,
                job_memory_limit_bytes=3*1024**3,job_cpu_rate=5000,job_active_process_limit=64))
        return dict(phase=phase,baseSha=stage.BASE,candidateSha=stage.CANDIDATE_SEAL,runId='00000000-0000-0000-0000-000000000001',
            failure=None,returncode=0,cleanupErrors=[],quarantineCount=0,claimReleased=True,resources=rows,
            cleanupSupervision=dict(slotReleased=True,journalClosed=True,cleanupOnly=True,cleanupVerified=True,ownedLeases=[]))

    def test_complete_native_release_receipt_accepted(self):
        stage.verify_phase('build',self.receipt(),Path('unused'))

    def test_wrong_source_failed_phase_or_boolean_returncode_refused(self):
        for field,value in [('phase','suite'),('baseSha','foreign'),('candidateSha','foreign'),('returncode',False),('returncode',1),('failure','failed'),('quarantineCount',1),('claimReleased',False)]:
            changed=self.receipt();changed[field]=value
            with self.subTest(field=field),self.assertRaises(ValueError):stage.verify_phase('build',changed,Path('unused'))

    def test_unclosed_handles_live_job_and_output_overflow_refused(self):
        for field,value in [('job_handle_closed',False),('remaining_job_processes',1),('readers_settled',False),('retained_unverified_handles',True),('stop_reason','timeout'),('output_truncated',True)]:
            changed=self.receipt();changed['resources'][-1][field]=value
            with self.subTest(field=field),self.assertRaises(ValueError):stage.verify_phase('build',changed,Path('unused'))

    def test_unreleased_slot_or_unclosed_journal_refused(self):
        for field,value in [('slotReleased',False),('journalClosed',False),('cleanupVerified',False),('ownedLeases',['live'])]:
            changed=self.receipt();changed['cleanupSupervision'][field]=value
            with self.assertRaises(ValueError):stage.verify_phase('build',changed,Path('unused'))

    def test_native_job_widening_or_missing_command_refused(self):
        for field,value in [('job_memory_limit_bytes',4*1024**3),('job_cpu_rate',10000),('job_active_process_limit',128),('executable_identity','other.exe')]:
            changed=self.receipt();changed['resources'][-1][field]=value
            with self.assertRaises(ValueError):stage.verify_phase('build',changed,Path('unused'))
        changed=self.receipt();changed['resources'].pop()
        with self.assertRaises(ValueError):stage.verify_phase('build',changed,Path('unused'))

    def test_full_suite_requires374_completed_passes(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)
            for total,summary in [(373,'Completed'),(374,'Failed'),(374,'Completed')]:
                (path/'strict-test-result.json').write_text(json.dumps(dict(summary=summary,total=total,passed=total,other=0)))
                if total==374 and summary=='Completed':stage.verify_phase('suite',self.receipt('suite'),path)
                else:
                    with self.assertRaises(ValueError):stage.verify_phase('suite',self.receipt('suite'),path)

    def test_audit_requires_structured_clean_result(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)
            for data in [dict(version=1,projectCount=3,problems=[],vulnerablePackages=0),dict(version=1,projectCount=0,problems=[],vulnerablePackages=0),dict(version=1,projectCount=3,problems=['feed failure'],vulnerablePackages=0)]:
                (path/'strict-audit-result.json').write_text(json.dumps(data))
                if data['projectCount'] and not data['problems']:stage.verify_phase('audit',self.receipt('audit'),path)
                else:
                    with self.assertRaises(ValueError):stage.verify_phase('audit',self.receipt('audit'),path)


if __name__=='__main__':unittest.main()
