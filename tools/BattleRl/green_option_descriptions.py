"""Bind each hierarchical alternative to exact original rules, preserving IDs."""
import json,os
from pathlib import Path
import hierarchical_option_policy as h

def install():
    rules=json.loads((Path(os.environ['BATTLE_RL_TEXT_ASSETS'])/'texts.json').read_text())['entities']
    original=h.ChoiceTree.document
    labels={'Kind':'操作类型','Card':'使用的牌或技能','Choice':'发动模式','material':'作为材料使用','spellId':'法术','skill':'技能','advanced':'高级行动','target':'目标','color':'颜色'}
    def describe(edge):
        key,value=edge;field=key.split('/')[-1]
        text=f'{key}：{labels.get(field,field)}选择为 {value}'
        if value in rules:text+='。该实体原始规则：'+rules[value]
        return text
    def document(self,base,node):
        doc=original(self,base,node)
        doc['options']=[describe(edge) for edge in self.nodes[node]['children']]
        doc['state']+='\n已锁定选择的具体含义：'+'；'.join(describe(e) for e in self.nodes[node]['prefix'])
        doc['question']+='选项中的原始规则不等于本次已产生的效果；必须结合模式、付款、目标和当前公开局面。'
        return doc
    h.ChoiceTree.document=document
