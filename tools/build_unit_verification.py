"""Author independent unit expectations from the inspected original card faces."""
import hashlib
import json
import os
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
    content = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    if path.exists() and path.read_bytes() == content:
        return
    temporary = path.with_name(path.name + '.tmp')
    temporary.write_bytes(content)
    os.replace(temporary, path)


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

    # Rulebook pages 9-10; these expectations do not call the production damage calculator.
    def hit(damage, targets=None, block=0, **expected):
        value = step('suffer', f'确认敌人攻击{damage}的伤害分配', enemyAttack=damage, **expected)
        value.update(damage=damage, unitTargets=[0] if targets is None else targets, block=block)
        return value

    def damage_case(card, title, steps, **fields):
        add(card, title, [step('prepare', '选择原部队作为承伤目标', exception=''),
                          step('battle', '进入同一场战斗', exception='')] + steps, **fields)

    for card, table in [('army_2_006', [(1,1,0),(3,1,0),(4,1,1),(7,1,2)]),
                        ('army_2_001', [(1,0,0),(3,0,0),(4,1,0),(6,1,0),(7,1,1)])]:
        for damage,wound,hero in table:
            damage_case(card, '护甲边界与溢出继续交给英雄', [hit(damage, wounds=wound, heroWounds=hero, heroDiscardWounds=0,
                damageAssigned=1, ready='True', canActivate='True' if wound==0 else 'False', exception='')])
    for element in ('Fire','Ice','ColdFire'):
        damage_case('army_2_001', element+'攻击不受物理抗性保护', [hit(4, wounds=1, heroWounds=1, damageAssigned=1, exception='')], enemyElement=element)
    for card,table in [('army_2_006',[(1,2,0),(7,2,2)]),('army_2_001',[(3,0,0),(4,2,0),(7,2,1)])]:
        for damage,wound,hero in table:
            damage_case(card, '剧毒只把实际部队伤牌替换为两张', [hit(damage,wounds=wound,heroWounds=hero,heroDiscardWounds=hero,
                destroyed='False',damageAssigned=1,exception='')],enemyTraits='Poison')
    for card,damage,destroyed,hero in [('army_2_006',1,True,0),('army_2_001',3,False,0),('army_2_001',4,True,0),('army_2_001',7,True,1)]:
        damage_case(card, '麻痹只在实际受伤时消灭部队并腾出指挥槽', [hit(damage,wounds=0,heroWounds=hero,
            destroyed=str(destroyed),units=0 if destroyed else 1,freeSlots=1 if destroyed else 0,exception='')],enemyTraits='Paralyze')
    damage_case('army_2_006','剧毒与麻痹同在，部队消灭而英雄仍收剧毒弃牌伤',[
        hit(4,wounds=0,destroyed='True',units=0,heroWounds=1,heroDiscardWounds=1,exception='')],enemyTraits='Poison,Paralyze')
    damage_case('army_2_006','横置不妨碍未受伤部队承伤',[
        hit(3,wounds=1,ready='False',heroWounds=0,exception='')],exhausted=True)
    for fields,reason in [({'wounds':1},'受伤或已销毁部队不能承伤'),({'destroyed':True},'受伤或已销毁部队不能承伤'),
                          ({'unowned':True},'只能让自有部队承伤'),({'noUnitDamage':True},'当前效果禁止部队承伤')]:
        damage_case('army_2_006','非法承伤选择必须先拒绝，不能改变英雄伤牌',[
            hit(4,wounds=fields.get('wounds',0),heroWounds=0,damageAssigned=0,exception=reason)],**fields)
    damage_case('army_2_001','抗性无伤吸收也不能同场复用，下一场才重置',[
        hit(3,wounds=0,heroWounds=0,damageAssigned=1,exception=''),
        hit(3,wounds=0,heroWounds=0,damageAssigned=1,exception='同场战斗不能重复向该部队分配伤害'),
        step('endBattle','结束战斗恢复下场承伤资格',damageAssigned=0,wounds=0,ready='True'),
        step('battle','开始下一场战斗',exception=''),hit(3,wounds=0,heroWounds=0,damageAssigned=1,exception=''),
        step('use','无伤且就绪的魔像还能发动攻击',melee=2,ready='False',exception='')])
    damage_case('army_2_001','同次攻击按魔像、农民、英雄顺序继续分配',[
        hit(10,[0,1],wounds=1,secondWounds=1,heroWounds=1,damageAssigned=2,units=2,exception='')],extraUnitId='army_2_006',extraSlots=1)
    damage_case('army_2_001','前一部队吸收完时不消耗后续部队资格',[
        hit(2,[0,1],wounds=0,secondWounds=0,heroWounds=0,damageAssigned=1,exception=''),
        hit(3,[1],wounds=0,secondWounds=1,heroWounds=0,damageAssigned=2,exception='')],extraUnitId='army_2_006',extraSlots=1)
    damage_case('army_2_006','重复目标拒绝在任何伤害发生之前',[
        hit(7,[0,0],wounds=0,heroWounds=0,damageAssigned=0,exception='同场战斗不能重复向该部队分配伤害')])
    damage_case('army_2_006','不存在的部队索引不得造成伤害',[
        hit(7,[99],wounds=0,heroWounds=0,damageAssigned=0,exception='承伤部队索引无效')])
    damage_case('army_2_006','可以选择全部伤害交给英雄',[
        hit(5,[],wounds=0,heroWounds=3,damageAssigned=0,exception='')])
    damage_case('army_2_006','禁用部队承伤时仍可分配给英雄',[
        hit(3,[],wounds=0,heroWounds=2,damageAssigned=0,exception='')],noUnitDamage=True)
    damage_case('army_2_001','攻击元素转换后按最终元素判断抗性',[
        hit(4,wounds=1,heroWounds=1,damageAssigned=1,exception='')],changedElement='Fire')
    for trait in ['Poison','Paralyze']:
        damage_case('army_2_006','被忽略的'+trait+'不再追加伤牌或消灭部队',[
            hit(3,wounds=1,heroWounds=0,destroyed='False',units=1,exception='')],enemyTraits=trait,ignoredAbility=trait)
    damage_case('army_2_006','残暴先翻倍再分配给部队与英雄',[
        hit(2,wounds=1,heroWounds=1,exception='')],enemyTraits='Brutal')
    damage_case('army_2_001','残暴先翻倍再应用抗性吸收',[
        hit(2,wounds=1,heroWounds=0,exception='')],enemyTraits='Brutal')
    damage_case('army_2_001','迅捷只影响格挡要求，不翻倍伤害',[
        hit(3,wounds=0,heroWounds=0,exception='')],enemyTraits='Swift')
    damage_case('army_2_001','部队护甲加成参与抗性两次吸收',[
        hit(11,wounds=1,heroWounds=1,exception='')],unitArmorBonus=2)
    damage_case('army_2_001','完全格挡不消耗部队承伤资格',[
        hit(3,block=3,wounds=0,heroWounds=0,damageAssigned=0,exception=''),
        hit(3,wounds=0,heroWounds=0,damageAssigned=1,exception='')])
    damage_case('army_2_006','不完全格挡不能减少交给部队的原伤害',[
        hit(5,block=2,wounds=1,heroWounds=1,damageAssigned=1,exception='')])
    damage_case('army_2_006','临时物理抗性效果连接承伤并在战后失效',[
        step('natureResistance','执行既有自然之力抗性效果模块',physicalResistTemp='True'),
        hit(3,wounds=0,heroWounds=0,damageAssigned=1,exception=''),
        step('endBattle','战后清除临时抗性',physicalResistTemp='False',damageAssigned=0),
        step('battle','下一场战斗',exception=''),hit(4,wounds=1,heroWounds=1,exception='')])
    damage_case('army_2_006','剧毒两张伤牌必须分两次治疗才能发动',[
        hit(1,wounds=2,heroWounds=0,ready='True',exception=''),
        step('endBattle','战后伤牌仍保留',wounds=2,ready='True'),step('heal','第一次治疗一张',wounds=1,canActivate='False'),
        step('battle','受伤的部队参与下一场战斗',exception=''),step('use','伤牌未清除前不能发动',exception='部队必须就绪、未受伤且未销毁'),
        step('endBattle','结束战斗后继续治疗',wounds=1),step('heal','第二次治疗才移除最后一张',wounds=0,canActivate='True'),
        step('battle','再次交战',exception=''),step('use','未横置的部队此时能发动原攻击2',melee=2,ready='False',exception='')],enemyTraits='Poison')
    damage_case('army_2_006','麻痹消灭后治疗和重整不能复活部队',[
        hit(1,destroyed='True',wounds=0,units=0,exception=''),step('endBattle','清理战斗标记',damageAssigned=0,destroyed='True'),
        step('heal','治疗不能复活',wounds=0,canActivate='False'),step('nextRound','新一轮也不能复活',ready='False',units=0),
        step('use','不能发动已不属于玩家的消灭部队',exception='只能发动自有部队')],enemyTraits='Paralyze')

    suite = ROOT/'Assets/Resources/CardVerification/unit_cases.json'; write(suite,dict(cases=cases))
    sha = hashlib.sha256(suite.read_bytes()).hexdigest()
    config = dict(scenePath='Assets/Scenes/Verification/units.unity', useRunSubfolder=False, captureInitialView=True,
                  initialDelaySeconds=1.5, defaultWaitAfterSeconds=0.05, maxRunSeconds=900, steps=[])
    def checks(d):
        d = dict(d, **{'ui:ruleFits':'True','ui:valuesFits':'True'})
        return [dict(key=k,expected=str(v),rule='原版部队独立预期：费用、状态、操作、结算及完整可读的文字') for k,v in d.items()]
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
