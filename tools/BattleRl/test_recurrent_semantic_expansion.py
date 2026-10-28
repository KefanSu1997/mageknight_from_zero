import unittest
import numpy as np
from recurrent_semantic_expansion import expand_arrays


class SemanticExpansionTests(unittest.TestCase):
    def test_sorted_catalog_insertion_preserves_history_and_kind_labels(self):
        old=[dict(Key='z',Kind='play'),dict(Key='x',Kind='unit')]
        new=[dict(Key='a',Kind='sharedUse'),old[1],old[0]]
        values={'core.encoder_w0':np.arange(70).reshape(35,2),
                'head.wp':np.array([[7,9]]),'head.bp':np.array([3,4]),
                'previous_action_embedding':np.array([[11,12],[13,14]]),
                'head.wk':np.array([[17,19]]),'head.bk':np.array([23,29]),
                'core.bn':np.array([31])}
        result=expand_arrays(values,old,new,2,5)
        np.testing.assert_array_equal(result['core.encoder_w0'][:2],values['core.encoder_w0'][:2])
        np.testing.assert_array_equal(result['core.encoder_w0'][5:],values['core.encoder_w0'][2:])
        self.assertFalse(result['core.encoder_w0'][2:5].any())
        np.testing.assert_array_equal(result['head.wp'],[[0,9,7]])
        np.testing.assert_array_equal(result['previous_action_embedding'],[[0,0],[13,14],[11,12]])
        np.testing.assert_array_equal(result['head.wk'],[[17,0,19]])
        np.testing.assert_array_equal(result['core.bn'],values['core.bn'])
        same=expand_arrays(values,old,new,2,2)
        np.testing.assert_array_equal(same['core.encoder_w0'],values['core.encoder_w0'])
        np.testing.assert_array_equal(same['head.wp'],[[0,9,7]])

    def test_complete_action_edit_cannot_bypass_key_mapping(self):
        old=[dict(Key='same',Kind='play',Parts=1)]
        with self.assertRaises(ValueError):
            expand_arrays({},old,[dict(Key='same',Kind='play',Parts=2)],2,3)


if __name__=='__main__':unittest.main()
