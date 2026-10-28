"""Full delivery must survive chunk boundaries without accepting missing text."""
import unittest
from verify_reader_delivery import complete_prompt_coverage


class PromptCoverageTests(unittest.TestCase):
    def setUp(self):
        # Long single line deliberately crosses every fixed chunk boundary.
        self.text = ''.join(chr(0x4e00 + i % 2000) for i in range(25007)) + 'UNIQUE_TAIL'

    def chunks(self, text=None):
        text = self.text if text is None else text
        return [(i, 'TOOL HEADER\n' + text[i:i+12000] + '\nEND')
                for i in range(0, len(text), 12000)]

    def test_complete_slices(self):
        self.assertIn('12000_character_slice', complete_prompt_coverage(self.text, self.chunks())[1])

    def test_missing_tail_rejected(self):
        with self.assertRaises(ValueError): complete_prompt_coverage(self.text, self.chunks()[:-1])

    def test_reordered_slices_rejected(self):
        with self.assertRaises(ValueError): complete_prompt_coverage(self.text, list(reversed(self.chunks())))

    def test_changed_character_rejected(self):
        changed = self.text[:13000] + 'X' + self.text[13001:]
        with self.assertRaises(ValueError): complete_prompt_coverage(self.text, self.chunks(changed))

    def test_lines_preserved(self):
        self.assertEqual(complete_prompt_coverage('first\nsecond', [(1,'first'),(2,'second')])[1],
                         'ordered_complete_nonempty_line_coverage')

    def test_exact(self):
        self.assertEqual(complete_prompt_coverage(self.text, [(7,self.text)])[0], [7])

    def test_variable_slices(self):
        parts = [(i, 'HEADER\n' + self.text[i:i+5000] + '\nEND')
                 for i in range(0, len(self.text), 5000)]
        self.assertEqual(complete_prompt_coverage(self.text, parts)[1],
                         'ordered_complete_variable_character_slice_coverage')

    def test_variable_slices_missing_character(self):
        text = ''.join(f'{i:06d}汉字;' for i in range(5000))
        parts = [(i, text[i:i+5000]) for i in range(0, len(text), 5000)]
        parts[2] = (10000, parts[2][1][1:])
        with self.assertRaises(ValueError): complete_prompt_coverage(text, parts)

    def test_variable_slices_reordered(self):
        text = ''.join(f'{i:06d}汉字;' for i in range(5000))
        parts = [(i, text[i:i+5000]) for i in range(0, len(text), 5000)]
        with self.assertRaises(ValueError): complete_prompt_coverage(text, parts[::-1])


if __name__ == '__main__': unittest.main()
