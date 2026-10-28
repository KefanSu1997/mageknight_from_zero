import json
from pathlib import Path
import tempfile
import unittest
from original_data_signature import data_signature


class DataSignatureTests(unittest.TestCase):
    def test_json_change_and_added_file_change_signature(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'card.json').write_text('{"cost":"Blue"}')
            original=data_signature(root)
            (root/'ignored.meta').write_text('editor only')
            self.assertEqual(original,data_signature(root))
            (root/'card.json').write_text('{"cost":"Red"}')
            self.assertNotEqual(original,data_signature(root))
            changed=data_signature(root)
            (root/'enemy.json').write_text('{}')
            self.assertNotEqual(changed,data_signature(root))


if __name__=='__main__':
    unittest.main()
