"""Candidate compact resource text, derived only from frozen public coordinates."""
from functools import lru_cache
from critical_public_state_text import document as critical_document
from semantic_public_text import public_features
from public_entity_tokens import PublicEntityTokens,original_vocabulary

@lru_cache(maxsize=1)
def layout():return PublicEntityTokens(original_vocabulary())

def decode(state):
 f=public_features(state);l=layout()
 n=lambda v:round(float(v),5)
 return dict(cards={identity:n(f[index]*scale) for identity,index,scale in l.card_counts if f[index]>0},
             wounds=n(f[14]*16),block=n(f[137]*20),
             crystals=[n(f[18+2*i]*3) for i in range(6)],tokens=[n(f[19+2*i]*3) for i in range(6)],
             activatable_units=[identity for i,identity in enumerate(l.vocabulary['unit']) if f[2102+16*i]>0 and f[2103+16*i]>0])

def document(original):
 r=decode(original['state']);d=critical_document(original)
 lines=['公开资源：非伤牌手牌（原规则ID×张数）='+('、'.join(f'{k}×{v:g}' for k,v in r['cards'].items()) or '无'),
        f"英雄伤牌数={r['wounds']:g}；当前格挡贡献总量={r['block']:g}（能否格挡须匹配目标）",
        '满足发动状态条件的单位='+('、'.join(r['activatable_units']) or '无')+'（费用和目标以合法候选为准）',
        '魔力颜色顺序=红蓝绿白金黑；晶体='+','.join(f'{v:g}' for v in r['crystals'])+'；标记='+','.join(f'{v:g}' for v in r['tokens'])]
 d['state']='\n'.join(lines)+'\n'+d['state']
 return d
