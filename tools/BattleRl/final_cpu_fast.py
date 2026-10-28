"""Lossless CPU hot-path optimization, installed only at a saved boundary."""
import json as stdjson
import ujson
from functools import lru_cache

class Codec:
    def dumps(self,obj,**kw):
        if set(kw)-{'ensure_ascii','separators'}:return stdjson.dumps(obj,**kw)
        try:return ujson.dumps(obj,ensure_ascii=kw.get('ensure_ascii',True),escape_forward_slashes=False)
        except TypeError:return stdjson.dumps(obj,**kw)
    def loads(self,text,**kw):
        return stdjson.loads(text,**kw) if kw else ujson.loads(text)
    def __getattr__(self,key):return getattr(stdjson,key)

def install():
    import final_token_option as option
    import final_token_runtime as runtime
    from semantic_public_text import SemanticPublicText
    import train_ppo,atomic_transport_fast,atomic_action_registry,hierarchical_packed_cache
    import sb3_battle_env
    codec=Codec();saved=[]
    for module in (train_ppo,atomic_transport_fast,atomic_action_registry,hierarchical_packed_cache,sb3_battle_env):
        saved.append((module,'json',module.json));module.json=codec
    singleton=SemanticPublicText();saved.append((option,'SemanticPublicText',option.SemanticPublicText));option.SemanticPublicText=lambda:singleton
    original=runtime.node_document
    @lru_cache(maxsize=16384)
    def edge(edge,glossary):
        rules=dict(line.split('：',1) for line in glossary.splitlines() if '：' in line)
        return runtime.edge_text(edge,rules)
    def document(tree,base,node):
        current=tree.nodes[node];rules=base['rules']
        prefix='；'.join(edge(e,rules) for e in current['prefix']) or '尚未选择'
        options=[edge(e,rules) for e in current['children']] or ['唯一完整操作，直接执行']
        return dict(state=base['state']+'\n本次完整操作已锁定（不可取消或回退）：'+prefix,rules=rules,
                    question='选择当前层的一项合法后继；全部参数确定后才执行完整操作。',options=options)
    saved.append((runtime,'node_document',original));runtime.node_document=document
    def uninstall():
        for module,key,value in reversed(saved):setattr(module,key,value)
    return uninstall
