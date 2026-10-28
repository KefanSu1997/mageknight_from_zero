import unittest
from discover_policy_witnesses import candidate_rows


class CandidateQueueTests(unittest.TestCase):
    def test_selected_deficit_order_overrides_cheapest_training_order(self):
        document=dict(candidates=[dict(task_id='cheap',split='train',estimated_cost=1),
                                  dict(task_id='deficit',split='train',estimated_cost=2)],selected_train_drafts=['deficit'])
        self.assertEqual(candidate_rows(document,'train',1,True)[0]['task_id'],'deficit')
        self.assertEqual(candidate_rows(document,'train',1,False)[0]['task_id'],'cheap')

    def test_selected_train_queue_never_becomes_withheld_tasks(self):
        document=dict(candidates=[dict(task_id='withheld',split='validation',estimated_cost=1)],selected_train_drafts=['withheld'])
        with self.assertRaises(ValueError):
            candidate_rows(document,'validation',1,True)
        with self.assertRaises(ValueError):
            candidate_rows(document,'train',1,True)


if __name__=='__main__':
    unittest.main()
