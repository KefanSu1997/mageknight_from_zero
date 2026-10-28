"""Public semantic context and actual printed glossary for A's option reader.

This is a readable text companion to the complete structured branch, not a
replacement for it. All complete options remain in the immutable choice tree.
"""
import json
from solo_policy_inputs import state_entities


def compact(value):return json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False)


class ScenarioText:
    def __init__(self,schema,text_assets):self.schema=schema;self.assets=text_assets
    def render(self,observation,support):
        self.schema.validate(observation)
        world=observation['World'];inv=observation['Inventory'];combat=observation['Combat'];offers=observation['Offers']
        identities=set()
        for binding in self.assets.catalog.describe_bindings(state_entities(observation)):
            identities.update(p['id'] for p in binding['printed'])
        for candidate in support['rule_bindings']:
            for binding in candidate:identities.update(binding['identities'])
        lines=[
            '任务：单人征服；遵循规则规定的昼夜和虚拟玩家时限，争取最终声望与任务计分。',
            '以下是公开状态文字摘要；模型同时接收完整结构化公开状态，未描述字段不表示为零。',
            f"英雄={inv['Hero']}；轮次={world['Round']}；昼夜={world['DayPart']}；位置=({world['PlayerQ']},{world['PlayerR']})；战斗阶段={combat['Phase']}",
            '自身资源：'+compact({k:inv[k] for k in ('Fame','Level','Reputation','Armor','CommandSlots','DrawCount','TacticAsideCount','Movement','Influence','Healing','GrailHealing','Mana')}),
            '手牌（Slot为当前手牌槽，Id对应下方原文）：'+compact(inv['Hand']),
            '已打出：'+compact(inv['Played']),
            '弃牌与轮外置牌：'+compact(dict(Discard=inv['Discard'],RoundAside=inv['RoundAside'])),
            '单位：'+compact([{k:v for k,v in u.items() if k!='Actions'} for u in inv['Units']]),
            '技能：'+compact(inv['Skills']),
            '当前敌人：'+compact(combat['Enemies']),
            '地图已知地点：'+compact([dict(q=c['Q'],r=c['R'],site=c['Site']) for c in world['Cells'] if c['Site'] is not None]),
            '公开轮次状态：'+compact({k:world[k] for k in ('ActiveTurn','EndingTurn','PendingTacticInput','PlayerAnnounced','PlayerTactic','DummyTactic','Dummy')}),
            '公开供应与待处理奖励：'+compact(offers),
        ]
        return dict(state='\n'.join(lines),rules='\n'.join(i+'：'+self.assets.catalog.get(i)['text'] for i in sorted(identities)),
                    question='从当前层全部合法选项选择一项，锁定参数后不可反悔；只有完整操作确定后才执行游戏规则。')
