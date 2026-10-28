"""New input contract gates, not original card acceptance tests."""
import unittest
import numpy as np
from public_entity_tokens import PublicEntityTokens, original_vocabulary


class PublicEntityTokensTests(unittest.TestCase):
    def setUp(self):
        self.layout = PublicEntityTokens(original_vocabulary())
        self.features = np.zeros((2, 6314))

    def test_rejects_unknown_schema_size_and_unmapped_raw_card(self):
        with self.assertRaises(ValueError):
            PublicEntityTokens(original_vocabulary(), feature_schema='unknown')
        with self.assertRaises(ValueError):
            self.layout.pack(np.zeros((2, 6313)))
        self.features[0, 202] = 0.5
        with self.assertRaisesRegex(ValueError, 'advanced044-047'):
            self.layout.pack(self.features)

    def test_original_card_count_and_skill_marker_public_coordinates(self):
        self.features[0, 37] = 1.
        self.features[1, 207:211] = [1., 1., 0., 0.]
        tokens, present, identities = self.layout.pack(self.features)
        self.assertEqual(identities[0][1], ('basic_card_000', 'card', None))
        self.assertEqual(tokens[0, 1, self.layout.numeric_start], 2.)
        self.assertEqual(identities[1][1], ('hero_skill_pool_000:00', 'skill', None))
        np.testing.assert_array_equal(tokens[1, 1, self.layout.numeric_start:self.layout.numeric_start + 4], [1., 1., 0., 0.])
        self.assertEqual(present.sum(), 4)

    def test_nonfinite_public_input_and_ambiguous_enemy_or_unit_rejected(self):
        self.features[0, -1] = np.nan
        with self.assertRaises(ValueError):
            self.layout.pack(self.features)
        self.features[:] = 0.
        self.features[0, 487] = 1.
        with self.assertRaisesRegex(ValueError, 'enemy'):
            self.layout.pack(self.features)
        self.features[:] = 0.
        self.features[0, 2102] = 1.
        with self.assertRaisesRegex(ValueError, 'public slot'):
            self.layout.pack(self.features)

    def test_fourth_owned_unit_keeps_public_fields_without_fabricated_slot(self):
        for i in range(4):
            self.features[0, 2102+16*i] = 1.
        for slot in range(3):
            self.features[0, 2847+30*slot+slot] = 1.
        tokens, _, identities = self.layout.pack(self.features)
        fourth = (self.layout.vocabulary['unit'][3], 'unit', None)
        index = identities[0].index(fourth)
        self.assertEqual(tokens[0, index, self.layout.numeric_start], 1.)
        slot_start = len(self.layout.ids)+5
        self.assertEqual(tokens[0,index,slot_start:slot_start+6].sum(), 0.)
        self.features[0,2847] = 0.
        with self.assertRaisesRegex(ValueError, 'public slot'):
            self.layout.pack(self.features)

    def test_numeric_gradient_scatter_matches_public_coordinates(self):
        self.features[0, 37] = .5
        self.features[0, 207] = 1.
        self.features[0, 2102] = 1.
        self.features[0, 2847] = 1.
        self.features[0, 487] = 1.
        self.features[0, 489] = 1.
        tokens, _, identities = self.layout.pack(self.features)
        gradient = np.random.default_rng(51).normal(size=tokens.shape)
        dx = self.layout.scatter_numeric_gradient(gradient, identities)
        for coordinate in (0, 37, 209, 2104, 558, 1779, 5704):
            original = self.features[0, coordinate]
            self.features[0, coordinate] = original + 1e-6
            plus = float((self.layout.pack(self.features)[0] * gradient).sum())
            self.features[0, coordinate] = original - 1e-6
            minus = float((self.layout.pack(self.features)[0] * gradient).sum())
            self.features[0, coordinate] = original
            self.assertAlmostEqual((plus - minus) / 2e-6, dx[0, coordinate], places=7)


if __name__ == '__main__':
    unittest.main()
