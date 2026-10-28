import hashlib,json,tempfile,unittest
from pathlib import Path
from solo_training_admission import verify_admission

class AdmissionTests(unittest.TestCase):
    def test_bound_evidence_and_rejection(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);files={}
            rows={
                'gate.json':dict(passed=True,complete_scenarios=7,accepted_optimizer_steps=4,reload_parameter_exact=True),
                'collection.json':dict(complete=7,episodes=[dict(status='complete',eligible_complete_episode=True)]*7),
                'review.json':dict(episodes=7,status='no_positive_public_cycle_detected',positive_public_cycle_candidates=[])}
            for name,value in rows.items():
                raw=json.dumps(value).encode();(root/name).write_bytes(raw);files[name]=hashlib.sha256(raw).hexdigest()
            record=dict(version=1,scope='experimental_solo_conquest',native_binding='bound',limitations=['provisional rules'],
                full_rules_certified=False,files=files,cuda_gate='gate.json',collection='collection.json',trajectory_review='review.json')
            (root/'training_admission.json').write_text(json.dumps(record))
            self.assertTrue(verify_admission(root,dict(binding_sha256='bound'))['sha256'])
            with self.assertRaises(ValueError):verify_admission(root,dict(binding_sha256='different'))
            (root/'gate.json').write_text('{}')
            with self.assertRaises(ValueError):verify_admission(root,dict(binding_sha256='bound'))
            files['gate.json']=hashlib.sha256(b'{}').hexdigest()
            (root/'training_admission.json').write_text(json.dumps(record))
            with self.assertRaises(ValueError):verify_admission(root,dict(binding_sha256='bound'))

if __name__=='__main__':unittest.main()
