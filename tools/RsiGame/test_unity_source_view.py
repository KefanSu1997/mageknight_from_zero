import tempfile
from pathlib import Path
import unittest

from unity_source_view import build_static_view, declared_scenarios, scan


class UnitySourceViewTest(unittest.TestCase):
    def test_csharp_scene_guid_resource_facts_exclude_generated_noise(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for folder in ('Assets', 'resources/text_json', 'Library', 'AutomationOutputs'):
                (root / folder).mkdir(parents=True)
            (root / 'Assets/Main.cs').write_text(
                'public class Main {\n'
                ' // public void Invented() {}\n'
                ' private string example = "public void AlsoInvented() {}";\n'
                ' public void Boot() { }\n'
                ' public int Next() => 1;\n}\n')
            guid = '0123456789abcdef0123456789abcdef'
            script_guid = 'abcdef0123456789abcdef0123456789'
            (root / 'Assets/Main.cs.meta').write_text('guid: ' + script_guid + '\n')
            (root / 'Assets/Main.unity').write_text(
                'm_Sprite: {fileID: 1, guid: ' + guid + ', type: 3}\n'
                'm_Script: {fileID: 11500000, guid: ' + script_guid + ', type: 3}\n')
            (root / 'Assets/Card.prefab').write_text('m_Name: Card\n')
            (root / 'Assets/card.png').write_bytes(b'fixture')
            (root / 'Assets/card.png.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\n')
            (root / 'Assets/dynamic.png').write_bytes(b'fixture')
            (root / 'resources/text_json/cards.json').write_text('[{"id": 1}, {"id": 2}]')
            for generated in ('Library', 'AutomationOutputs'):
                (root / generated / 'Fake.cs').write_text('public void Noise() {}')
                (root / generated / 'noise.png').write_bytes(b'noise')
            mapping = scan(root)
            rows = mapping['files'][str(Path('Assets/Main.cs'))]['functions']
            self.assertEqual([(r['name'], r['line']) for r in rows], [('Boot', 4), ('Next', 5)])
            self.assertIn('incomplete symbol inventory', mapping['scan_scope'])
            view = build_static_view(root)
            self.assertIn('C# source files: 1', view)
            self.assertIn('scene files: 1', view)
            self.assertIn('prefab files: 1', view)
            self.assertIn('image files: 2 present; 1 with a serialized GUID link', view)
            self.assertIn('GUID-linked: Assets/card.png', view)
            self.assertIn('GUID-linked: Assets/Main.cs', view)
            self.assertIn('top-level array length 2', view)
            self.assertNotIn('Noise', view)
            self.assertNotIn('never named', view)
            self.assertIn('runtime use unknown', view)
            self.assertIn('empty list is unknown', view)
            self.assertEqual(declared_scenarios(root), [])


if __name__ == '__main__':
    unittest.main()
