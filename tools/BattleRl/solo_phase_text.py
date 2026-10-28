"""Separate phase documents with all current candidate-referenced rules."""
import json
from solo_phase_routing import split_public_text_state


class PhaseText:
    def __init__(self, schema, assets):
        self.schema, self.assets = schema, assets

    def render(self, observation, support):
        self.schema.validate(observation)
        expert, state = split_public_text_state(observation)
        identities = set()
        for candidate in support['rule_bindings']:
            for binding in candidate:
                identities.update(binding['identities'])
        # Current actionable/hostile entities remain explained even if no current
        # candidate directly targets them. Unknown printed identities fail below.
        inv = observation['Inventory']
        for zone in ('Hand', 'Units', 'Skills'):
            identities.update(x['Id'] for x in inv[zone] if x['Id'] != 'w')
        if expert == 'combat':
            identities.update(x['Id'] for x in observation['Combat']['Enemies'])
        return dict(
            state=('战斗执行策略' if expert == 'combat' else '全局规划策略')
                  + '；目标为整局最终声望与任务计分，伤牌及消耗会影响后续游戏。\n'
                  + json.dumps(state, ensure_ascii=False, separators=(',', ':'), allow_nan=False),
            rules='\n'.join(i+'：'+self.assets.catalog.get(i)['text'] for i in sorted(identities)),
            question='从当前层全部合法选项选择一项；参数锁定后不可反悔，完整参数确定后才执行。')
