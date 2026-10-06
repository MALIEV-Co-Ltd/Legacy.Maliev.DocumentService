"""Admission controls cannot substitute for the hosted native compiled proof."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('application_proof', Path(__file__).with_name('verify-document-application-proof.py'))
proof = importlib.util.module_from_spec(spec)
spec.loader.exec_module(proof)


class ApplicationProofAdmissionTests(unittest.TestCase):
    def test_receipt_rejects_stale_identity_hashes_and_numerical_waivers(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ('contract-applicability-proposal.json', 'contract-negative-controls.json'):
                (root / name).write_text('{}')
            receipt = dict(schemaVersion='document-contract-applicability-acceptance/v1',
                           head='a' * 40, runId='123', runAttempt='1', policyActive=True,
                           applicabilityAcceptance=True, applicationStatus='N/A contract-only',
                           applicationNumericalPercent=None, applicationNumericalPassed=False,
                           fourAssemblyNumericalAcceptance=False, deployed=False,
                           executableFloorsPassed=True, actualHttpPassed=39, exclusions=[],
                           policySha256=proof.sha(Path('docs/document-contract-applicability-policy.json')),
                           compiledProofSha256=proof.sha(root / 'contract-applicability-proposal.json'),
                           controlsSha256=proof.sha(root / 'contract-negative-controls.json'))
            path = root / 'contract-applicability-acceptance.json'
            path.write_text(json.dumps(receipt))
            proof.validate_receipt(root, 'a' * 40, '123', '1')
            mutations = dict(head='b' * 40, runId='124', runAttempt='2', policyActive=False,
                             applicabilityAcceptance=False, applicationStatus='100%',
                             applicationNumericalPercent=100, applicationNumericalPassed=True,
                             fourAssemblyNumericalAcceptance=True, deployed=True,
                             executableFloorsPassed=False, actualHttpPassed=36, exclusions=['generated'],
                             policySha256='0' * 64, compiledProofSha256='0' * 64, controlsSha256='0' * 64)
            for key, value in mutations.items():
                with self.subTest(key=key):
                    path.write_text(json.dumps(dict(receipt, **{key: value})))
                    with self.assertRaises(AssertionError):
                        proof.validate_receipt(root, 'a' * 40, '123', '1')
            path.unlink()
            with self.assertRaises(FileNotFoundError):
                proof.validate_receipt(root, 'a' * 40, '123', '1')


if __name__ == '__main__':
    unittest.main()
