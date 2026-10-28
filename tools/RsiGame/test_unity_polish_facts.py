"""Offline schema/unknown-evidence gates for the original polish pack builder."""
from contextlib import ExitStack
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import unity_source_view as unity


class UnityPolishFactsTest(unittest.TestCase):
    def test_null_facts_original_pack_and_formatters(self):
        upstream = Path(__file__).resolve().parents[2] / 'AutomationOutputs/RsiGame/checkouts/RSIGame/src'
        sys.path.insert(0, str(upstream))
        import rsigame.polish.polish_evidence as evidence
        import rsigame.evidence.polish_goal as goal
        import rsigame.polish.polish_review as review
        prompt_before = review._PROMPT
        with tempfile.TemporaryDirectory() as temp, ExitStack() as stack:
            for module, name in ((evidence, 'visual_facts'), (evidence, 'render_facts'),
                                 (evidence, 'render_facts_with_peers'), (goal, '_facts_text'),
                                 (review, 'saturated')):
                stack.enter_context(patch.object(module, name, getattr(module, name)))
            stack.enter_context(patch.object(evidence, 'design_intent', return_value='Fixture intent'))
            root = Path(temp)
            for folder in ('Assets', 'ProjectSettings', 'Library', 'AutomationOutputs'):
                (root / folder).mkdir()
            (root / 'ProjectSettings/ProjectVersion.txt').write_text('fixture')
            guid = '0123456789abcdef0123456789abcdef'
            (root / 'Assets/image.png').write_bytes(b'fixture image')
            (root / 'Assets/image.png.meta').write_text('guid: ' + guid)
            (root / 'Assets/Main.unity').write_text('m_Sprite: {guid: ' + guid + '}')
            for folder in ('Library', 'AutomationOutputs'):
                (root / folder / 'not_game.png').write_bytes(b'noise')
            unity.install_polish_facts()
            # Exercise the unchanged upstream pack builder, not a replica.
            pack = evidence.build('fixture', root, {'steps': []})
            facts = pack['facts']
            self.assertEqual(facts['images_present'], 1)
            self.assertEqual(facts['images_serialized_guid_linked'], 1)
            for key in ('images_used', 'images_unused', 'unused_names', 'primitive_draws', 'texture_draws', 'authored_share'):
                self.assertIsNone(facts[key], key)
                self.assertIsNone(json.loads(json.dumps(facts))[key])
            self.assertEqual(set(pack), {'game', 'intent', 'facts', 'frames', 'interactions'})
            for text in (evidence.render_facts(facts), evidence.render_facts_with_peers(facts, {}), goal._facts_text(facts)):
                self.assertIn('UNKNOWN', text)
                self.assertIn('do not establish runtime usage', text)
                self.assertNotIn('0%', text)
                self.assertNotIn('100%', text)
                self.assertNotIn('mostly primitives', text)
                self.assertNotIn('None', text)
            rev = {dimension: {'headroom': 'medium'} for dimension in review.DIMENSIONS}
            self.assertEqual(review.saturated(rev, facts)[0], False)
            self.assertIn('unknown', review.saturated(rev, facts)[1])
            self.assertTrue(review.saturated(rev, {'images_unused': 0, 'authored_share': 1})[0])
            self.assertEqual(review._PROMPT, prompt_before)


if __name__ == '__main__':
    unittest.main()
