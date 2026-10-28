import json
import unittest
from solo_public_records import flatten, restore, policy_records


class PublicRecordsTests(unittest.TestCase):
    def fixture(self):
        return dict(observation=dict(Version=1, Step=3, Terminal=False, World={'Round':2},
            Inventory={'Hand':[{'Slot':i,'Id':f'card{i}'} for i in range(300)]}, Combat={}, Offers={}),
            support_sha256='fixture', commands=[json.dumps(dict(SchemaVersion=1,ExpectedStep=3,
                Deed={'HandSlot':299, 'Inputs':{'HandSlots':[7,2]}}, Clock=None))])

    def test_lossless_containers_order_and_long_arrays(self):
        value={'a':[], 'b':{}, 'c':None, 'd':[False,0,1.5,'0'], 'hand':list(range(400))}
        self.assertEqual(restore(tuple(flatten(value))),value)

    def test_complete_state_and_candidates(self):
        source=self.fixture();result=policy_records(source)
        expected={k:v for k,v in source['observation'].items() if k!='Step'}
        self.assertEqual(restore(result['state']),expected)
        self.assertEqual(restore(result['candidates'][0]),{'Deed':{'HandSlot':299,'Inputs':{'HandSlots':[7,2]}}})
        self.assertEqual(source['observation']['Step'],3)

    def test_private_fields_rejected(self):
        source=self.fixture();source['observation']['Inventory']['Seeds']={'Deck':3}
        with self.assertRaises(ValueError):policy_records(source)
        with self.assertRaises(ValueError):tuple(flatten({'x':float('nan')}))

    def test_stale_and_unknown_schema_rejected(self):
        source=self.fixture();source['observation']['Step']=4
        with self.assertRaises(ValueError):policy_records(source)
        source=self.fixture();source['observation']['HiddenDeck']=[]
        with self.assertRaises(ValueError):policy_records(source)


if __name__=='__main__':unittest.main()
