"""Synthetic transport gate tests only; never UI evidence or actual scoring."""
import copy,json,tempfile,unittest
from pathlib import Path
from build_sharded_combat_review import IDS,entry,write
from verify_sharded_combat_review import facts,delivery,trace,unchanged_actual_final,image_scope,independent_final_worker
class Transport(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.p=Path(self.tmp.name)
  schema={'type':'object','required':['shard_id','read_ledger','coverage','item_facts','limitations']};write(self.p/'schema.json',schema)
  (self.p/'scope.txt').write_text('complete scope\n'+('a'*30000),encoding='utf8')
  self.task={'shard_id':'synthetic','must_read':[entry(self.p/'scope.txt')]*3+[entry(self.p/'schema.json')]+[entry(self.p/'scope.txt')],'images':[]}
  self.f={'shard_id':'synthetic','read_ledger':[dict(x,actually_read_or_viewed=True) for x in self.task['must_read']], 'coverage':[{'path':str(self.p/'scope.txt'),'item_ids':IDS}], 'item_facts':[{'id':i,'facts':[],'problems':[],'not_visible':[],'scope_limits':[],'evidence':[]} for i in IDS], 'limitations':['Synthetic transport only']}
 def tearDown(self):self.tmp.cleanup()
 def test_valid_structure(self):facts(self.f,self.task)
 def test_equivalent_absolute_separators(self):
  f=copy.deepcopy(self.f)
  for e in f['read_ledger']+f['coverage']:e['path']=Path(e['path']).as_posix()
  facts(f,self.task)
 def test_relative_ledger_rejected(self):
  f=copy.deepcopy(self.f);f['read_ledger'][0]['path']='scope.txt'
  with self.assertRaises(ValueError):facts(f,self.task)
 def test_numeric_fact_rejected(self):
  f=copy.deepcopy(self.f);f['item_facts'][0]['facts']=[{'score':5}]
  with self.assertRaises(ValueError):facts(f,self.task)
 def test_missing_item_rejected(self):
  f=copy.deepcopy(self.f);f['item_facts'].pop()
  with self.assertRaises(ValueError):facts(f,self.task)
 def test_duplicate_coverage_rejected(self):
  f=copy.deepcopy(self.f);f['coverage']*=2
  with self.assertRaises(ValueError):facts(f,self.task)
 def test_missing_scope_rejected(self):
  f=copy.deepcopy(self.f);f['read_ledger']=[]
  with self.assertRaises(ValueError):facts(f,self.task)
 def test_truncated_text_rejected(self):
  e=entry(self.p/'scope.txt')
  with self.assertRaises(ValueError):delivery(e,['complete scope'],set())
 def test_unread_png_rejected(self):
  p=self.p/'synthetic.png';p.write_bytes(b'\x89PNG\r\n\x1a\nsynthetic')
  with self.assertRaises(ValueError):delivery(entry(p),[],set(),True)
 def test_changed_bytes_rejected(self):
  e=entry(self.p/'scope.txt');Path(e['path']).write_text('changed')
  with self.assertRaises(ValueError):delivery(e,['changed'],set())
 def test_modified_final_rejected(self):
  p=self.p/'actual_final.txt';p.write_bytes(b'actual final');proof={'actual_final':entry(p)};p.write_bytes(b'fabricated final')
  with self.assertRaises(ValueError):unchanged_actual_final(proof,b'actual final')
 def test_worker_model_rejected(self):
  for worker,model in [('/wrong','gpt-6.1-sol'),('/synthetic','wrong')]:
   p=self.p/'trace.jsonl';p.write_text('\n'.join(json.dumps(x) for x in [{'type':'session_meta','payload':{'agent_path':worker}},{'type':'turn_context','payload':{'model':model}},{'payload':{'role':'assistant','phase':'final','content':[{'type':'output_text','text':'{}'}]}}]))
   with self.assertRaises(ValueError):trace(p,'/synthetic')
 def test_other_shard_image_rejected(self):
  with self.assertRaises(ValueError):image_scope([{'sha256':'assigned'}],{'assigned','another_shard'})
 def test_text_reader_cannot_silently_view_images(self):
  with self.assertRaises(ValueError):image_scope([],{'unexpected_png'})
 def test_fact_reader_cannot_be_final_judge(self):
  with self.assertRaises(ValueError):independent_final_worker('/root/reader',[{'worker':'/root/reader'}])
if __name__=='__main__':unittest.main()
