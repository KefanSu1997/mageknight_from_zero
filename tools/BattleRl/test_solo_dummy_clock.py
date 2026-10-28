import json
import unittest
from solo_dummy_clock import DummyClock


class DummyClockTests(unittest.TestCase):
    def make(self):
        return DummyClock([dict(id=str(i), color='red') for i in range(16)], {'red': 3}, 71)

    def test_extra_flips_not_recursive_and_exhaustion_deferred(self):
        c = self.make()
        self.assertEqual(len(c.take_turn()['revealed']), 6)
        self.assertEqual(len(c.take_turn()['revealed']), 6)
        last = c.take_turn()
        self.assertEqual(len(last['revealed']), 4)
        self.assertFalse(last['announced'])
        self.assertTrue(c.take_turn()['announced'])
        with self.assertRaises(ValueError):
            c.take_turn()

    def test_restore_preserves_next_turn_and_next_round_shuffle(self):
        a = self.make()
        a.take_turn()
        b = DummyClock.restore(json.loads(json.dumps(a.checkpoint())))
        self.assertEqual(a.take_turn(), b.take_turn())
        for c in (a, b):
            c.close_round()
            c.next_round(dict(id='advanced1', color='blue'), 'red')
        self.assertEqual(a.checkpoint(), b.checkpoint())
        self.assertEqual(a.crystals['red'], 4)
        self.assertEqual(len(a.draw), 17)

    def test_six_rounds_and_no_hidden_draw_order(self):
        c = self.make()
        for i in range(2, 7):
            c.close_round()
            c.next_round(dict(id='advanced'+str(i), color='white'), 'blue')
        c.close_round()
        with self.assertRaises(ValueError):
            c.next_round(dict(id='extra', color='white'), 'blue')
        self.assertNotIn('draw', c.observation())
        self.assertNotIn('rng', c.observation())

    def test_supply_duplication_rejected(self):
        c = self.make()
        c.close_round()
        with self.assertRaises(ValueError):
            c.next_round(dict(id='0', color='red'), 'blue')


if __name__ == '__main__':
    unittest.main()
