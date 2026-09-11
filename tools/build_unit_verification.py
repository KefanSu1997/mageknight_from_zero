"""Author independent unit expectations from the inspected original card faces."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# kind, value, element, color, wounds. AB options are 0 attack / 1 block.
UNITS = {
    'army_003': (8, 'Keep', 3, 6, [('AB', 5, 'Physical', '', 0), ('ranged', 6, 'Fire', 'Red', 0)]),
    'army_2_001': (7, 'MageTower', 2, 3, [('AB', 2, 'Physical', '', 0), ('block', 4, 'Fire', 'Red', 0), ('block', 4, 'Ice', 'Blue', 0)]),
    'army_2_005': (7, 'Monastery', 2, 4, [('AB', 3, 'Physical', '', 0), ('AB', 4, 'Ice', 'Blue', 0)]),
    'army_2_006': (4, 'Village', 1, 3, [('AB', 2, 'Physical', '', 0), ('influence', 2, 'Physical', '', 0), ('move', 2, 'Physical', '', 0)]),
    'army_2_007': (7, 'Monastery', 2, 4, [('AB', 3, 'Physical', '', 0), ('AB', 4, 'Fire', 'Red', 0)]),
    'army_2_008': (7, 'Monastery', 2, 4, [('AB', 3, 'Physical', '', 0), ('siege', 4, 'Physical', 'Green', 0)]),
    'army_2_012': (6, 'Keep', 2, 4, [('AB', 3, 'Physical', '', 0), ('ranged', 2, 'Physical', '', 0)]),
    'army_2_014': (6, 'Keep', 2, 4, [('AB', 3, 'Physical', '', 0), ('AB', 6, 'Physical', '', 1)]),
}


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def build():
    cases = []

    def step(kind, label, ability=0, option=0, **expected):
        return dict(kind=kind, label=label, ability=ability, option=option,
                    expected=[dict(key=k, value=str(v)) for k, v in expected.items()])

    def add(card, title, steps, **fields):
        cases.append(dict(id=f'unit_{len(cases):03}', cardId=card, title=title, location=UNITS[card][1], steps=steps, **fields))

    for card, (cost, place, level, armor, abilities) in UNITS.items():
        for i, (kind, value, element, color, wound) in enumerate(abilities):
            for option in ((0, 1) if kind == 'AB' else (0,)):
                actual_kind = ('melee' if option == 0 else 'block') if kind == 'AB' else kind
                expected = {actual_kind: value + (20 - cost if kind == 'influence' else 0), 'ready': 'False', 'wounds': wound, 'exception': ''}
                if color: expected[color] = 1
                if actual_kind in ('melee', 'ranged', 'siege'): expected['element'] = element
                if actual_kind == 'block': expected['blockElement'] = element
                steps = [step('recruit', '支付原费用招募', units=1, offer=0, influence=20-cost, ready='True', armor=armor, level=level,
                              physicalResist='True' if card == 'army_2_001' else 'False', outcome='True')]
                if kind not in ('move', 'influence'): steps.append(step('battle', '进入交战并选择敌人', exception=''))
                steps.append(step('use', '发动原版能力', i, option, **expected))
                if actual_kind in ('melee', 'ranged', 'siege'):
                    steps.append(step('resolve', '结算攻击与真实击杀', effective=value, required=3, kills=int(value>=3), **{actual_kind: 0}, exception=''))
                elif actual_kind == 'block':
                    steps.append(step('resolve', '结算格挡与英雄伤牌', effective=value, required=3, heroWounds=0 if value>=3 else 2, block=0, exception=''))
                elif kind == 'move': steps.append(step('move', '支付平原费用移动', move=0, position=1, outcome='True', exception=''))
                add(card, f'能力{i}·选项{option}·招募到结算', steps)

        # Shared lifecycle tested once per unit, across both current and legacy turn entry points.
        add(card, '同轮不能重复使用，下一轮才重整', [step('prepare', '持有就绪原部队', units=1, ready='True'),
            step('battle', '进入交战', exception=''), step('use', '第一次发动', ready='False', exception=''),
            step('use', '重复发动应被拒绝', ready='False', exception='部队必须就绪、未受伤且未销毁'),
            step('nextTurn', '下一回合仍然横置', ready='False'), step('legacyTurn', '旧回合入口同样不重整', ready='False'),
            step('nextRound', '新一轮恢复指挥标记', ready='True', canActivate='True'), step('battle', '新一轮重新进入交战', exception=''),
            step('use', '新一轮允许发动', ready='False', exception='')])
        for state in ('unowned', 'wounds', 'exhausted', 'destroyed'):
            fields = {state: 1 if state == 'wounds' else True}
            add(card, state + '状态禁止发动', [step('prepare', '设置部队状态', exception=''), step('battle', '进入交战', exception=''),
                step('use', '尝试非法发动', exception='只能发动自有部队' if state == 'unowned' else '部队必须就绪、未受伤且未销毁',
                     melee=0, block=0, Red=2, Blue=2, Green=2)], **fields)
        for ability, option in ((99, 0), (0, 2)):
            add(card, '非法能力或选项不得横置', [step('prepare', '持有就绪部队', ready='True'), step('battle', '进入交战', exception=''),
                step('use', '拒绝无效选择', ability, option, exception='部队能力尚未接入或选择无效' if ability==99 else '部队能力选项无效', ready='True', melee=0, block=0),
                step('use', '随后正常发动', exception='', ready='False')])
        add(card, '招募资源不足保持供应与单位不变', [step('recruit', '尝试低于原费用招募', outcome='False', units=0, offer=1, influence=cost-1)], influence=cost-1)
        invalid_place = 'Monastery' if card == 'army_003' else 'City'
        cases.append(dict(id=f'unit_{len(cases):03}', cardId=card, title='原图没有此招募地点', location=invalid_place,
                          steps=[step('recruit', '尝试错误地点招募', outcome='False', units=0, offer=1, influence=20)]))
        for i, (_, _, _, color, _) in enumerate(abilities):
            if color:
                add(card, '魔力不足不支付不横置', [step('prepare', '持有部队但没有魔力', ready='True'), step('battle', '进入交战', exception=''),
                    step('use', '拒绝无法支付的能力', i, exception='部队能力所需魔力不足', ready='True', melee=0, block=0, ranged=0, siege=0,
                         Red=0, Blue=0, Green=0)], mana=0)

    add('army_003', '物品栏魔晶可以支付部队能力', [step('prepare', '仅有物品栏魔晶', redCrystal=1), step('battle', '进入交战', exception=''),
        step('use', '以红色魔晶发动火焰远程', 1, redCrystal=0, Red=0, ranged=6, element='Fire', ready='False', exception='')], mana=0, crystals=1)
    add('army_2_006', '部队影响力继续支付第二支原版部队', [step('recruit', '招募第一支农民', influence=2, units=1, offer=1),
        step('use', '农民产生影响力2', 1, influence=4, ready='False', exception=''),
        step('recruit', '支付影响力招募另一支农民', influence=0, units=2, offer=0, outcome='True')], influence=6, extraSlots=1, offerCount=2)
    add('army_2_014', '自伤后重整不能代替治疗', [step('prepare', '持有乌顿剑士', ready='True'), step('battle', '进入交战', exception=''),
        step('use', '自伤攻击6', 1, melee=6, wounds=1, ready='False', exception=''),
        step('nextRound', '下一轮重整但伤牌保留', ready='True', wounds=1, canActivate='False'),
        step('use', '受伤仍然不能发动', exception='部队必须就绪、未受伤且未销毁'),
        step('heal', '治疗后才允许发动', wounds=0, ready='True', canActivate='True'),
        step('battle', '重新进入交战', exception=''), step('use', '发动基础能力', melee=3, ready='False', exception='')])

    suite = ROOT/'Assets/Resources/CardVerification/unit_cases.json'; write(suite,dict(cases=cases))
    sha = hashlib.sha256(suite.read_bytes()).hexdigest()
    config = dict(scenePath='Assets/Scenes/Verification/units.unity', useRunSubfolder=False, captureInitialView=True,
                  initialDelaySeconds=1.5, defaultWaitAfterSeconds=0.05, maxRunSeconds=900, steps=[])
    def checks(d): return [dict(key=k,expected=str(v),rule='原版部队独立预期：费用、状态、操作与结算') for k,v in d.items()]
    for i,c in enumerate(cases):
        if i: config['steps'].append(dict(label=c['id']+'_next', buttonPath='Canvas/UIRoot/Btn_Next',skipScreenshot=True,after=checks(dict(caseId=c['id'],step=0))))
        config['steps'].append(dict(label=c['id']+'_select',buttonPath='Canvas/UIRoot/Btn_Select',skipScreenshot=True,
                                    after=checks(dict(selected='True',art=c['cardId'],suiteSha256=sha))))
        for j,s in enumerate(c['steps']):
            config['steps'].append(dict(label=f"{c['id']}_{j:02}_{s['kind']}",buttonPath='Canvas/UIRoot/Btn_Execute',
                skipScreenshot=s['kind'] in ('prepare','battle','nextTurn','legacyTurn','heal'),before=checks(dict(caseId=c['id'],step=j)),
                after=checks(dict(step=j+1,**{v['key']:v['value'] for v in s['expected']}))))
    write(ROOT/'AutomationConfigs/AllOriginalCards/units.json',config)
    print(json.dumps(dict(cases=len(cases),operations=len(config['steps']),captures=1+sum(not s['skipScreenshot'] for s in config['steps']))))


if __name__=='__main__': build()
