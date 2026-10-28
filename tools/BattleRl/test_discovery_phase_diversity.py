import unittest
from discover_task_witnesses import search_branches, phase_diverse_frontier


class PhaseDiversityTests(unittest.TestCase):
    def test_legal_advance_survives_many_higher_ranked_card_choices(self):
        actions=[dict(Kind='play') for _ in range(8)]+[dict(Kind='advance')]
        selected=search_branches(list(range(9)),actions,'Block',4)
        self.assertEqual(selected,[0,1,2,8])
        self.assertEqual(len(set(selected)),4)
        self.assertEqual(search_branches([0,1],actions,'Block',4),[0,1])

    def test_frontier_keeps_melee_path_instead_of_only_block_pools(self):
        frontier=[(-100+i,i,(i,),'0:Block') for i in range(4)]
        frontier+=[(-10,4,(4,),'0:AssignDamage'),(5,5,(5,),'0:Melee')]
        result=phase_diverse_frontier(frontier,4)
        self.assertEqual(len(result),4)
        self.assertEqual({r[3] for r in result},{'0:Block','0:AssignDamage','0:Melee'})
        self.assertTrue(all(r in frontier for r in result))

    def test_outside_keeps_formal_rest_and_next_turn_alternatives(self):
        actions=[dict(Kind='fight')]+[dict(Kind='play') for _ in range(5)]+[dict(Kind='nextTurn'),dict(Kind='rest')]
        selected=search_branches(list(range(8)),actions,'Outside',4)
        self.assertEqual(selected,[0,1,6,7])


if __name__=='__main__':
    unittest.main()
