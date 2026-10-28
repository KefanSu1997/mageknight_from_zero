import unittest
from pathlib import Path
from solo_printed_catalog import SoloPrintedCatalog
from solo_candidate_entities import deed_entities


class PrintedCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = SoloPrintedCatalog(Path(__file__).resolve().parents[2])

    def test_original_rule_and_cost_preserved(self):
        text = self.catalog.get('basic_card_000')['text']
        self.assertIn('移动力2。', text)
        self.assertIn('移动力4。', text)
        self.assertIn('绿', text)
        self.assertNotIn('wiki', text)
        self.assertEqual(self.catalog.get('basic_card_000'), self.catalog.get('basic_card_000'))

    def test_physical_candidate_identity_changes_text(self):
        obs = dict(Inventory=dict(Hand=[dict(Slot=0,Id='basic_card_000'),dict(Slot=1,Id='basic_card_001')]))
        left = self.catalog.describe_bindings(deed_entities(obs, dict(HandSlot=0)))
        right = self.catalog.describe_bindings(deed_entities(obs, dict(HandSlot=1)))
        self.assertNotEqual(left[0]['printed'][0]['text_sha256'], right[0]['printed'][0]['text_sha256'])
        self.assertIn('治疗', right[0]['printed'][0]['text'])

    def test_missing_identity_is_not_zero_vector(self):
        with self.assertRaises(ValueError):self.catalog.get('missing_rule')
        self.assertIn('hero_skill_pool_001:00', self.catalog.entries)
        self.assertIn('army_007', self.catalog.entries)
        self.assertIn('monster_026', self.catalog.entries)

    def test_native_wound_has_explicit_provenance(self):
        entry = self.catalog.get('w')
        self.assertEqual(entry['origin'], 'authored_summary_of_native_rules_not_printed_card_transcription')
        self.assertEqual(len(entry['native_source_sha256']), 3)
        self.assertIn('休息是弃牌，不是移除伤牌', entry['text'])
        refs = [dict(parameter=('WoundHandSlots',0), zone=('Inventory','Hand'),
                     slot=3, entity=dict(Slot=3, Id='w', Type='Wound'))]
        self.assertEqual(self.catalog.describe_bindings(refs)[0]['printed'][0], entry)


if __name__ == '__main__':unittest.main()
