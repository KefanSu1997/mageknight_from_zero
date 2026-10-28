"""Readable summary derived exclusively from the frozen public 6314 coordinates.

Original coordinate text is retained verbatim; the summary adds semantics, not
hidden state. Coordinate sources: JourneyTraining.Features, PublicEntityTokens.
"""
import re
import numpy as np
from public_entity_tokens import PublicEntityTokens, original_vocabulary

def public_features(state):
    f=np.zeros(6314,dtype=np.float32)
    pairs=re.findall(r'state\.(\d+)=([-+\d.eE]+)',state)
    if not pairs:raise ValueError('Missing public-coordinate document')
    for key,value in pairs:
        i=int(key)
        if not 0<=i<6314:raise ValueError('Public schema overflow')
        f[i]=float(value)
    if not np.isfinite(f).all():raise ValueError('Nonfinite state')
    return f

class SemanticPublicText:
    def __init__(self):self.layout=PublicEntityTokens(original_vocabulary())
    def summary(self,f):
        assert np.asarray(f).shape==(6314,)
        n=lambda x:format(round(float(x),5),'g')
        phases=['远程攻击','格挡','承受伤害','近战攻击','战斗收尾']
        phase=[name for i,name in enumerate(phases,7) if f[i]>0]
        lines=['当前战斗阶段：'+('、'.join(phase) or '战斗阶段之外')]
        for i,label,scale in [(12,'已执行规则步骤数',96),(13,'已记录行动回合数',3),(14,'手中伤牌数',16),(15,'名望（Fame）',40),(16,'英雄护甲',5),(17,'抽牌堆剩余张数',16),(206,'英雄等级',10)]:
            lines.append(label+'：'+n(f[i]*scale))
        for i,color in enumerate(['红','蓝','绿','白','金','黑']):
            lines.append(f'{color}色资源：晶体{n(f[18+2*i]*3)}，魔力标记{n(f[19+2*i]*3)}，公共源骰{n(f[30+i]*3)}')
        lines.append('持有源骰：'+('是' if f[36] else '否'))
        lines.append('昼夜：'+('夜晚' if f[156] else '白天'))
        lines.append('当前格挡贡献总量：'+n(f[137]*20)+'（是否有效须结合目标攻击）')
        for identity,coordinate,scale in self.layout.card_counts:
            if f[coordinate]>0:lines.append('手牌 '+identity+' 数量：'+n(f[coordinate]*scale))
        for i,identity in enumerate(self.layout.vocabulary['skill']):
            p=207+4*i
            if f[p]>0:lines.append(f'拥有技能 {identity}：本回合已用{n(f[p+1])}，本轮已用{n(f[p+2])}，已翻面{n(f[p+3])}')
        for i,identity in enumerate(self.layout.vocabulary['unit']):
            p=2102+16*i
            if f[p]>0:lines.append(f'拥有单位 {identity}：满足发动状态条件={"是" if f[p+1] else "否"}，伤牌数={n(f[p+2]*4)}，已摧毁={"是" if f[p+3] else "否"}（费用与目标以合法选项为准）')
        for slot in range(6):
            p=487+110*slot
            if f[p]<=0:continue
            indices=np.flatnonzero(f[p+2:p+71]>0)
            if len(indices)!=1:raise ValueError('Ambiguous public enemy')
            identity=self.layout.vocabulary['enemy'][int(indices[0])]
            lines.append(f'敌人槽{slot} {identity}：{"已击败" if f[p+1] else "尚未击败"}，印刷护甲{n(f[p+71]*20)}，当前有效护甲{n(f[1779+35*slot]*20)}')
            for j in range(4):
                q=p+94+4*j
                if f[q]:
                    element=int(round(float(f[q+3]*5)))
                    if element not in range(4):raise ValueError('Unknown attack element')
                    lines.append(f'敌人槽{slot}第{j+1}次攻击：{"已格挡" if f[q+1] else "尚未格挡"}，当前攻击力{n(f[q+2]*20)}，类型={ ["物理","火焰","寒冰","冰火"][element] }')
        return '\n'.join(lines)
    def document(self,doc):
        result=dict(doc)
        result['state']='公开局面语义摘要：\n'+self.summary(public_features(doc['state']))+'\n完整原观察（保留全部坐标与历史）：\n'+doc['state']
        return result
