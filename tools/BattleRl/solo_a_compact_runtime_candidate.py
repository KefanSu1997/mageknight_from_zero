"""Isolated SAB2 runtime with canonical tree reuse; not installed by import."""
import copy,hashlib,os,shutil
from collections import OrderedDict
from baseline_a_scenario_runtime import ScenarioRuntime,packed
from hierarchical_option_policy import ChoiceTree
from solo_a_decoded_cache_v3_candidate import DecodedCacheRuntimeV3,canonical
from solo_a_compact_context_candidate import encode,decode,MAGIC
class CompactRuntime(DecodedCacheRuntimeV3):
    def __init__(self,path,binding,cache_bytes=1024*2**20):
        self.decoded=OrderedDict();self.context_sizes={};self.decoded_bytes=0
        self.decoded_limit=cache_bytes;self.decoded_hits=0;self.decoded_misses=0
        if not binding:raise ValueError('Explicit binding required')
        ScenarioRuntime.__init__(self,path,dict(binding,context_codec='SAB2_exact_row_dedup'))
    def add(self,support,document):
        support=canonical(support);document=canonical(document)
        if set(document)!={'state','rules','question'} or not all(isinstance(x,str) for x in document.values()):raise ValueError('Public document schema')
        if not document['state'] or not document['question']:raise ValueError('Missing context')
        if hashlib.sha256(packed(list(support['commands']))).hexdigest()!=support['commands_sha256']:raise ValueError('Command binding mismatch')
        tree=ChoiceTree(support['operations'])
        if tree.leaves!=len(support['commands']):raise ValueError('Incomplete support')
        digest,payload=encode(support,document)
        if os.name!='nt' and shutil.disk_usage(self.path).free<3*2**30+2*len(payload):raise RuntimeError('3GiB protection')
        with self.db:
            context=self.db.execute('SELECT COALESCE(MAX(id),0)+1 FROM contexts').fetchone()[0]
            if context>=2**24:raise ValueError('Context float32 range')
            self.db.execute('INSERT INTO contexts VALUES (?,?,?)',(context,digest,payload))
        value=copy.deepcopy(dict(support=support,document=document))
        for key in ('descriptors','history_features'):value['support'][key].setflags(write=False)
        self._remember(context,value)
        self.trees[context]=(tree,value['document'])
        while len(self.trees)>1024:self.trees.popitem(last=False)
        return context
    def _read(self,context):
        context=int(context)
        if context in self.decoded:
            self.decoded_hits+=1;self.decoded.move_to_end(context);return self.decoded[context][0]
        self.decoded_misses+=1
        row=self.db.execute('SELECT sha,payload FROM contexts WHERE id=?',(context,)).fetchone()
        if row is None:raise ValueError('Missing context')
        value=decode(row[0],bytes(row[1]));value['support']['commands']=tuple(value['support']['commands'])
        return self._remember(context,value)
