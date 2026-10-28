import unittest
import copy
from semantic_ui_actions import action_steps


class SemanticUiActionTests(unittest.TestCase):
    def test_attack_all_parts_and_single_target_preserve_selection_semantics(self):
        state={'phase':'Melee','hand':[],'enemies':[{'defeated':False,'defend':0}],
               'attacks':[{'bound':False} for _ in range(3)],'uiSelectedTargets':[],'uiReservedAttackSlots':[], 'uiActionContextNeutral':True}
        action={'Kind':'attack','Card':'','Choice':'','Targets':1,'Parts':7,'Option':0,'Input':''}
        paths=[s['buttonPath'] for s in action_steps(action,state)]
        self.assertTrue(paths[0].endswith('Enemy_0/Btn_Target'))
        self.assertTrue(any(p.endswith('AttackSelection/Btn_All') for p in paths))
        self.assertTrue(paths[-1].endswith('Inspector/Btn_Resolve'))
        for neutral in (False,None):
            with self.assertRaises(ValueError): action_steps(action,dict(state,uiActionContextNeutral=neutral))
        for changed in (dict(action,Parts=3),dict(action,Targets=3),dict(action,Input='skill=source')):
            with self.assertRaises(ValueError):
                action_steps(changed,state)
        for field in ('bound','defend'):
            changed=copy.deepcopy(state)
            if field=='bound': changed['attacks'][0]['bound']=True
            else: changed['enemies'][0]['defend']=1
            with self.assertRaises(ValueError): action_steps(action,changed)

    def test_first_occurrence_and_payment_target_pagination_fail_closed(self):
        state={'phase':'Melee','hand':['basic_card_000','basic_card_000','w']}
        action={'Kind':'sideways','Card':'basic_card_000','Choice':'','Targets':0,'Parts':0,'Option':0,'Input':''}
        steps=action_steps(action,state)
        self.assertTrue(steps[0]['buttonPath'].endswith('basic_card_000_0'))
        for changed in (dict(action,Input='skill=source'),dict(action,Targets=1),dict(action,Option=1),dict(action,Card='w')):
            with self.assertRaises(ValueError):
                action_steps(changed,state)
        with self.assertRaises(ValueError):
            action_steps(action,{'phase':'Melee','hand':['other']*7+['basic_card_000']})
        self.assertEqual(len(action_steps(action,{'phase':'Melee','hand':['other']*6+['basic_card_000']})),2)

    def test_unsupported_kind_or_phase_does_not_fall_back_to_advance(self):
        with self.assertRaises(ValueError):
            action_steps({'Kind':'attack','Targets':0}, {'phase':'Melee','hand':[]})
        with self.assertRaises(ValueError):
            action_steps({'Kind':'advance'}, {'phase':'AssignDamage','hand':[]})


if __name__=='__main__':
    unittest.main()
