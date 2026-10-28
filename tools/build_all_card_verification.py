"""Independent printed-rule fixtures. Never import engine output to set expectations.

This first audit separates immediate numerical checks from unverified downstream
rules. A partial case is never promoted to a fully verified card.
"""
from pathlib import Path
import json
import os

ROOT = Path(__file__).resolve().parents[1]
COLORS = ['Red', 'Blue', 'Green', 'White', 'Gold', 'Black']
CN = {'红': 'Red', '蓝': 'Blue', '绿': 'Green', '白': 'White', '金': 'Gold', '黑': 'Black'}
CARDS = {}
for category in ['basic_card', 'advanced_card', 'magic', 'items']:
    for card in json.loads((ROOT / f'resources/text_json/{category}.json').read_text(encoding='utf-8-sig')):
        CARDS[card['id']] = card
CASES = []


def values(mapping):
    return [{'key': k, 'value': str(v) if not isinstance(v, bool) else str(v)} for k, v in mapping.items()]


def case(card_id, enhanced, title, expected, option=0, setup=None, limits=(), followup='', effect_choice='', error=''):
    card = CARDS[card_id]
    result = dict(MovementPool=0, InfluencePool=0, MeleePool=0, RangedPool=0, BlockPool=0)
    if card_id.startswith(('basic', 'advanced')) and enhanced:
        for color in CN:
            if color in (card.get('required_crystals') or ''):
                result['token:' + CN[color]] = 1
    if card_id.startswith('magic'):
        # Independent spell rule: the basic color is paid even for the top half;
        # a strong spell needs black mana at night in addition to its base color.
        color = ['Green', 'Red', 'Blue', 'White'][int(card_id.split('_')[-1]) // 6]
        result['token:' + color] = 1
        if enhanced:
            result['token:Black'] = 1
    result.update(expected)
    sequence = sum(c['cardId'] == card_id for c in CASES)
    CASES.append(dict(id=f'{card_id}_{sequence:02}', cardId=card_id, batch=card['set'], enhanced=enhanced,
                      option=option, effectChoice=effect_choice, title=('强化 / ' if enhanced else '基础 / ') + title,
                      followup=followup, followupEnhanced=False, setup=values(setup or {}),
                      expected=values(result), limitations=list(limits)))
    if error:
        CASES[-1]['expectedException'] = error
        # Rejection must preserve every mana color, not just the missing one.
        unchanged = {f'token:{color}': (setup or {}).get(f'token:{color}', 2) for color in COLORS}
        unchanged.update({f'crystal:{color}': (setup or {}).get(f'crystal:{color}', int(color in COLORS[:4])) for color in COLORS})
        unchanged.update(hand=6, discard=2, Wounds=3, handWounds=3, Fame=0, Reputation=0,
                         unitReady=False, unitWounds=0, ManaCurseTriggered=False)
        result.update(unchanged)
        CASES[-1]['expected'] = values(result)


def b(n, enhanced, title, expected, **kw):
    case(f'basic_card_{n:03}', enhanced, title, expected, **kw)


for strong in [False, True]:
    move = 4 if strong else 2
    for n in [0, 14]: b(n, strong, '移动', {'MovementPool': move})
    b(1, strong, '治疗手牌中的创伤', {'Wounds': 1 if strong else 2, 'handWounds': 1 if strong else 2})
    CASES[-1]['operations']=[dict(kind='HealHand',option=2 if strong else 1)]
    b(1, strong, '抽牌', {'hand': 8 if strong else 7}, option=1)
    for n, bonus in [(2, 2), (6, 3)]:
        if strong:
            b(n, True, '同时打出狂怒，免费强化并加值', {'MeleePool': 4 + bonus, 'token:Red': 2}, followup='basic_card_021')
        else:
            for opt, color in enumerate(['Blue', 'White', 'Red']):
                b(n, False, '获得' + color + '魔力', {'token:' + color: 3}, option=opt)
            if n == 6: b(n, False, '选择绿色魔晶', {'crystal:Green': 2, 'token:Blue': 2}, option=3)
    b(3, strong, '移动不能凭空产生格挡', {'MovementPool': move}, limits=['移动逐格弃牌、同色魔晶与触发上限尚未接入结算；不能判整卡通过'])
    b(4, strong, '选择治疗，不同时获得移动', {'Wounds': 1 if strong else 2, 'handWounds': 1 if strong else 2}, option=0)
    CASES[-1]['operations']=[dict(kind='HealHand',option=2 if strong else 1)]
    extra = {'BlockPool': 3 if strong else 2}
    if strong: extra['blockElement'] = 'Fire'
    b(4, strong, '选择格挡，不同时获得移动', extra, option=1)
    for opt, title, result in [(0, '治疗', {'Wounds': 1 if strong else 2, 'handWounds': 1 if strong else 2}),
                               (1, '抽牌', {'hand': 8 if strong else 7}),
                               (2, '绿色魔晶' if strong else '绿色魔力', {'crystal:Green': 2} if strong else {'token:Green': 3}),
                               (3, '重整耗竭的2级部队', {'unitReady': True})]:
        b(5, strong, title, result, option=opt)
        if opt == 0: CASES[-1]['operations']=[dict(kind='HealHand',option=2 if strong else 1)]
    b(7, strong, '远程攻击' if strong else '移动', {'RangedPool': 3} if strong else {'MovementPool': 2})
    b(8, strong, '影响力', {'InfluencePool': 4 if strong else 2})
    for n in [9, 13]:
        if not strong:
            result = {'ExtraManaDice': 1}
            if n == 13: result['BlackManaWild'] = True
            b(n, strong, '增加魔力源取骰额度', result, limits=['还须验证实际取骰、归还不重掷和非法颜色；仅设置额度不等于结算完成'])
        else:
            b(n, strong, '选择红色骰', {'token:Red': 4}, option=0,
              limits=['现有实现直接发魔力；未验证魔力源移除、保留骰面和归还'])
    for opt, title, result in [(0, '移动', {'MovementPool': move}), (1, '远程攻击', {'RangedPool': 3 if strong else 1}),
                               (2, '降低敌人一次攻击', {'AttackReduction': 2 if strong else 1})]:
        b(10, strong, title, result, option=opt, limits=['减攻分支需与敌人实际攻击结算联验'] if opt == 2 else [])
    b(11, strong, '远程攻击' if strong else '移动', {'RangedPool': 3} if strong else {'MovementPool': 2},
      limits=['远程击杀奖励需对照原卡面核对名望/声誉并实际击杀'] if strong else [])
    if not strong: b(11, False, '远程攻击', {'RangedPool': 1}, option=1)
    for negotiation in [False, True]:
        b(12, strong, '交涉中' if negotiation else '交涉外', {'InfluencePool': 4 if strong else 2, 'Fame': int(negotiation),
          'Reputation': int(negotiation and strong)}, setup={'InNegotiation': negotiation})
    for n in [15, 19]:
        for opt, color in enumerate(COLORS[:4]):
            result = {'crystal:' + color: 2}
            if not strong: result['token:' + color] = 1
            b(n, strong, color + '魔晶', result, option=opt,
              limits=['回合末弃另一张牌并收回本卡的流程未覆盖'] if n == 19 else [])
    b(16, strong, '格挡' if strong else '攻击', {'BlockPool': 5} if strong else {'MeleePool': 2})
    if not strong: b(16, False, '格挡', {'BlockPool': 2}, option=1)
    b(17, strong, '随后打出行进，检验额外移动', {'MovementPool': move + 3}, followup='basic_card_000', limits=['本例未覆盖横置移动及强化多张后续牌'])
    b(18, strong, '森林费用降至2', {'MovementPool': move, 'move:selectedCost': 2}, limits=['单格/整类地形范围还需移动场景结算'])
    b(20, strong, '寒冰格挡' if strong else '攻击', {'BlockPool': 5, 'blockElement': 'Ice'} if strong else {'MeleePool': 2},
      limits=['强化额外格挡需敌人能力、攻击颜色和奥术免疫夹具'] if strong else [])
    if not strong: b(20, False, '寒冰格挡', {'BlockPool': 3, 'blockElement': 'Ice'}, option=1)
    b(21, strong, '攻击', {'MeleePool': 4 if strong else 2})
    if not strong: b(21, False, '格挡', {'BlockPool': 2}, option=1)
    b(22, strong, '影响力与声誉', {'InfluencePool': 5 if strong else 2, 'Reputation': -1 if strong else 0})
    for opt, pool in enumerate(['MovementPool', 'InfluencePool', 'MeleePool', 'BlockPool']):
        b(23, strong, '弃一张牌换取' + pool, {pool: 5 if strong else 3, 'hand': 5, 'discard': 3}, option=opt)
    options = [{'MeleePool': 2}, {'BlockPool': 2}, {'RangedPool': 1}] if not strong else [
        {'MeleePool': 4}, {'BlockPool': 4}, {'MeleePool': 3, 'attackElement': 'Fire'},
        {'BlockPool': 3, 'blockElement': 'Fire'}, {'RangedPool': 3}, {'SiegePool': 2}]
    for opt, result in enumerate(options): b(24, strong, '印刷效果分支' + str(opt + 1), result, option=opt)
    b(25, strong, '攻击和受伤加成额度', {'MeleePool': 4 if strong else 2, 'AttackBonusPerWound': 1, 'AttackBonusLimit': 4 if strong else 1},
      limits=['还须实际承受创伤并检验攻击增加及上限'])
    b(26, strong, '影响力与部队重整' if strong else '影响力与招募折扣',
      {'InfluencePool': 6, 'Reputation': -1, 'ReadyInfluencePerLevel': 2} if strong else {'InfluencePool': 2, 'RecruitDiscount': 2},
      limits=['实际招募和重整的消费及条件尚未联验'])
    # Read directly from the existing basic_card_027.png: 本能, four choices 2/4, red power.
    for opt, pool in enumerate(['MovementPool', 'InfluencePool', 'MeleePool', 'BlockPool']):
        result = {pool: 4 if strong else 2}
        if strong: result['token:Red'] = 1
        b(27, strong, '本能原卡面：' + pool, result, option=opt,
          limits=['本能四选一的即时分支；地图、交涉、战斗及卡区完整生命周期仍须联验'])

# Advanced actions: primary values and explicit resource/choice branches.
# Entries come from the printed Chinese rules, not the effect implementation.
ADV = {
    0: ({'crystal:Red': 2}, {'RangedPool': 3, 'attackElement': 'Fire'}),
    1: ({'crystal:Blue': 2}, {'RangedPool': 3, 'attackElement': 'Ice'}),
    2: ({'crystal:White': 2}, {'RangedPool': 4}),
    3: ({'crystal:Green': 2}, {'SiegePool': 3}),
    4: ({'MeleePool': 2}, {'MeleePool': 4}),
    5: ({'BlockPool': 3, 'blockElement': 'Ice'}, {'BlockPool': 3, 'blockElement': 'Ice'}),
    6: ({'MovementPool': 2, 'MoveCostAttack': 1}, {'MovementPool': 4, 'MoveCostAttack': 1, 'MoveCostRanged': 2}),
    7: ({'MovementPool': 2, 'Wounds': 2, 'handWounds': 2}, {'MovementPool': 4, 'Wounds': 1, 'handWounds': 1}),
    8: ({'InfluencePool': 4, 'Reputation': -1}, {'InfluencePool': 8, 'Reputation': -2}),
    9: ({'MovementPool': 2, 'TerrainCostOverride:Swamp': 1}, {'MovementPool': 4, 'TerrainCostOverride:Swamp': 1, 'TerrainCostOverride:Lake': 1}),
    10: ({'MovementPool': 2}, {'MovementPool': 2, 'RequireBlueForLake': True}),
    11: ({'MovementPool': 2}, {'MovementPool': 4, 'TerrainCostOverride:Forest': 2}),
    12: ({'Wounds': 4, 'handWounds': 4, 'crystal:Red': 2, 'token:Red': 3}, {'Wounds': 4, 'handWounds': 4, 'token:Red': 3, 'crystal:Red': 2}),
    13: ({'MeleePool': 4, 'token:Red': 1}, {'MeleePool': 7}),
    14: ({'InfluencePool': 3, 'ReputationPerRecruit': 1}, {'InfluencePool': 6, 'ReputationPerRecruit': 1, 'FamePerRecruit': 1}),
    15: ({'Wounds': 2, 'handWounds': 2, 'unitReady': True}, {'Wounds': 1, 'handWounds': 1, 'unitReady': True}),
    16: ({'UnitAttackBonus': 2, 'UnitBlockBonus': 2, 'NoUnitDamage': True}, {'UnitAttackBonus': 3, 'UnitBlockBonus': 3, 'NoUnitDamage': True}),
    17: ({'MovementPool': 2}, {'MovementPool': 4}),
    18: ({'InfluencePool': 2, 'InfluenceAsBlock': True}, {'InfluencePool': 4, 'InfluenceAsBlock': True}),
    19: ({'InfluencePool': 6}, {'InfluencePool': 11}),
    20: ({'crystal:Red': 3, 'hand': 5}, {'crystal:Blue': 2, 'crystal:Green': 2, 'crystal:White': 2, 'hand': 5}),
    21: ({'crystal:Red': 2}, {'ReturnSpentCrystals': True}),
    22: ({}, {'ExtraManaDice': 3, 'GoldManaWild': True, 'BlackManaWild': True}),
    23: ({'MovementPool': 2, 'AmbushAttackBonus': 1, 'AmbushBlockBonus': 2}, {'MovementPool': 4, 'AmbushAttackBonus': 2, 'AmbushBlockBonus': 4}),
    24: ({'MeleePool': 6, 'hand': 5}, {'MeleePool': 8, 'hand': 5}),
    25: ({'hand': 5, 'discard': 3, 'SpellColors:count': 1}, {'spellOffer': 2, 'discard': 3, 'hand': 6, 'token:Red': 1}),
    26: ({'InfluencePool': 2}, {'InfluencePool': 4}),
    27: ({'hand': 5, 'discard': 3, 'advancedOffer': 2}, {'hand': 6, 'advancedOffer': 2}),
    28: ({'MeleePool': 2, 'AttackBonusPerBlock': 2}, {'MeleePool': 4, 'AttackBonusPerBlock': 3}),
    29: ({'MeleePool': 5, 'hand': 5}, {'MeleePool': 6, 'attackElement': 'Fire', 'hand': 5}),
    30: ({'Wounds': 4, 'handWounds': 4}, {'Wounds': 4, 'handWounds': 4}),
    31: ({'BlockPool': 3, 'SwiftBlockBonus': 3}, {'BlockPool': 5, 'SwiftBlockBonus': 5}),
    32: ({'TeleportRange': 1, 'NextDrawBonus': 1}, {'TeleportRange': 2, 'NextDrawBonus': 1}),
    33: ({'crystal:Red': 2}, {'crystal:Red': 2, 'crystal:Blue': 2}),
    34: ({'MeleePool': 3}, {'MeleePool': 6}),
    35: ({'InfluencePool': 3, 'HealInfluenceRate': 2, 'PeacefulMomentAvailable': True}, {'InfluencePool': 6, 'HealInfluenceRate': 2, 'PeacefulMomentAvailable': True, 'PeacefulMomentReady': True}),
    36: ({'AttackReduction': 2, 'AttackIfNoWound': 1}, {'AttackReduction': 4, 'AttackIfNoWound': 2}),
    37: ({'MovementPool': 2}, {'MovementPool': 3}),
    38: ({'unitPhysicalResist': True}, {'SiegePool': 3}),
    39: ({'MovementPool': 3}, {'MovementPool': 5, 'MountainSafe': True}),
    40: ({'crystal:Red': 1}, {'MovementPool': 6}),
    41: ({'Wounds': 4, 'handWounds': 4, 'crystal:Red': 2, 'crystal:White': 2}, {'RangedPool': 3, 'ArmorReducePerKill': 1}),
    42: ({'WoundDrawRemaining': 3}, {'WoundDrawRemaining': 3, 'FirstWoundIgnored': True}),
    43: ({'InfluencePool': 3}, {'InfluencePool': 5}),
}
for n in range(48):
    for strong in [False, True]:
        expected = ADV.get(n, ({}, {}))[int(strong)]
        simple = n in [0, 1, 2, 3, 19]
        limitations = [] if simple else ['首轮检验本分支的即时输出；目标范围、额外选择及回合/战斗后续结算须补专用案例']
        if n >= 44: limitations = ['原卡定义缺少名称与效果文本，不能把占位逻辑判为通过']
        kwargs = {}
        if n == 24: kwargs['option'] = 0  # Option of the actual Fury material, never an effect enum.
        if n == 30 and strong:
            kwargs['option'] = 0  # Borrow actual first Advanced Action offer; never inject an effect enum.
            expected = {**expected, 'RangedPool': 3, 'attackElement': 'Fire'}
        if n == 33 and strong: kwargs['option'] = 256  # Red + Blue, two distinct offered spells.
        if n == 40 and not strong: kwargs['setup'] = {'crystal:Red': 0}
        if n == 25 and strong: kwargs['setup'] = {'CardToDiscard': '<none>'}
        case(f'advanced_card_{n:03}', strong, '主要印刷效果', expected, limits=limitations, **kwargs)
        if n == 4:
            case(f'advanced_card_{n:03}', strong, '主动承受创伤增强攻击', {'MeleePool': 9 if strong else 5, 'Wounds': 4, 'handWounds': 4}, option=1)
        if n == 7:
            CASES[-1]['operations']=[dict(kind='HealHand',option=2 if strong else 1)]
            case(f'advanced_card_{n:03}', strong, '战斗中不治疗', {'MovementPool': 4 if strong else 2, 'Wounds': 3, 'handWounds': 3}, setup={'InBattle': True})
        if n == 8:
            case(f'advanced_card_{n:03}', strong, '选择攻击', {'MeleePool': 7 if strong else 3, 'Reputation': -2 if strong else -1}, option=1)

SPELL = {
    0: ({'unitReady': True}, {'unitReady': True, 'unitWounds': 0}),
    1: ({'Wounds': 1, 'handWounds': 1, 'hand': 6}, {'BlockedArmorOne': True}),
    2: ({'discard': 0, 'NextDrawBonus': 2}, {'discard': 0, 'NextDrawBonus': 2}),
    3: ({'ArmorReduction:0': 3}, {'ArmorReduction:0': 3}),
    4: ({'position:q':1,'TeleportRange':0}, {'position:q':1,'IgnoreSiteFortifications':True,'IgnoreFortified':False,'TunnelAssaultActive':True}),
    5: ({'Wounds': 0, 'handWounds': 0}, {'Wounds': 0, 'handWounds': 0, 'unitReady': True}),
    6: ({'Wounds': 3}, {'Wounds': 4, 'handWounds': 4, 'crystal:Red': 3}),
    7: ({'IgnoreSiteFortifications': True, 'IgnoreFortified': False, 'ArmorReduction:0': 1},
        {'KillEnemyIndex': 0, 'DefeatedEnemyIndices:count':1, 'Fame':3, 'IgnoreSiteFortifications':False}),
    8: ({'BlockPool': 4, 'blockElement': 'Fire', 'AttackAfterBlock': 4}, {'BlockPool': 4, 'blockElement': 'Fire', 'KillBlockedEnemy': True}),
    9: ({'RangedPool': 5, 'attackElement': 'Fire'}, {'SiegePool': 8, 'attackElement': 'Fire', 'Wounds': 4, 'handWounds': 4}),
    10: ({'MeleePool': 5, 'attackElement': 'Fire'}, {'MeleePool': 7, 'attackElement': 'Fire'}),
    11: ({'crystal:Red': 3, 'crystal:Blue': 2, 'hand': 4}, {'SiegePool': 4, 'attackElement': 'Fire', 'token:Red': 2, 'token:Green': 3, 'crystal:Red': 0, 'crystal:Green': 0}),
    12: ({'token:Red': 5}, {'token:Red': 5}),
    13: ({'SkipAttackIndices:0': True}, {'SkipAttackIndices:0': True, 'ArmorReduction:0': 4}),
    14: ({'MovementPool': 4, 'TerrainCostOverride:Forest': 2}, {'UnitsResistAll': True, 'IgnoreNextWound': True}),
    15: ({'RangedPool': 5, 'attackElement': 'Ice'}, {'SiegePool': 8, 'attackElement': 'Ice', 'Wounds': 4, 'handWounds': 4}),
    16: ({'IgnoreRampaging': True}, {'ExtraTurn': True, 'SkipDraw': True}),
    17: ({'MeleePool': 7, 'attackElement': 'ColdFire', 'token:Red': 1}, {'MeleePool': 10, 'attackElement': 'ColdFire', 'token:Red': 1}),
    18: ({'crystal:Red': 2}, {'crystal:Red': 2}),
    19: ({'RangedPool': 2}, {'RangedPool': 3}),
    20: ({'NoUnitDamage': True}, {'FreeRecruit': True}),
    21: ({'InfluencePool': 4}, {'SkipAttackIndices:0': True, 'MeleePool': 4}),
    22: ({'SkipAttackIndices:0': True}, {'KillEnemyIndex': 0,'DefeatedEnemyIndices:count':1,'Fame':3}),
    23: ({'MovementPool': 2, 'position:q':1, 'TeleportRange':0}, {'SkipAttackIndices:0': True}),
}
for n in range(24):
    for strong in [False, True]:
        kwargs = {'option': 1} if n == 23 else {}
        if n == 23 and not strong: kwargs['setup'] = {'MovementPool': 3}
        if n == 23 and strong: kwargs['setup'] = {'InBattle':True}
        if n == 1 and strong: kwargs['setup'] = {'InBattle':True}
        if n == 13: kwargs['setup'] = {'InBattle':True}
        if n == 19: kwargs['setup'] = {'InBattle':True}
        if n == 3: kwargs['setup'] = {'InBattle':True}
        if n == 7: kwargs['setup'] = {'InBattle':True,'CombatPhase':'Melee' if strong else 'Ranged'}
        if n == 22: kwargs['setup'] = {'InBattle':True,'CombatPhase':'Melee' if strong else 'Block'}
        if n == 11 and strong: kwargs['option'] = 2  # Green + Red: siege fire, one crystal pair.
        case(f'magic_{n:03}', strong, '印刷效果及施法费用', SPELL[n][int(strong)], **kwargs,
             limits=['法术通过旧 ActionSystem 映射执行；所有目标选择、日夜合法性和后续结算尚未完整接通，不能判整卡通过'])
        if n == 23 and not strong: CASES[-1]['travelPath']=[dict(q=1)]
        if n == 4:
            CASES[-1]['travelPath']=[dict(q=1)]
            if strong: CASES[-1]['mapPlaces']=[dict(q=1,type='Keep',enemy=True)]
        if n == 1 and not strong: CASES[-1]['operations']=[dict(kind='HealHand',option=2)]

ITEM = {
    0: ({'UnitArmorBonus': 1, 'UnitAttackBonus': 1, 'UnitBlockBonus': 1}, {'UnitArmorBonus': 1, 'UnitAttackBonus': 1, 'UnitBlockBonus': 1}),
    1: ({}, {'SkipAttackIndices:0': True}),
    2: ({'UnitArmorBonus': 1}, {}),
    3: ({'unitReady': True}, {'unitReady': True}),
    8: ({'MeleePool': 6, 'hand': 4, 'discard': 4}, {'DoublePhysicalAttack': True}),
    9: ({'SiegePool': 5}, {'SiegePool': 5}),
    10: ({'InfluencePool': 4, 'Fame': 2}, {'InfluencePool': 9, 'Fame': 3}),
    11: ({}, {'token:Red': 3, 'token:Blue': 3, 'token:Green': 3, 'token:White': 3, 'token:Gold': 3}),
    12: ({'Wounds': 1, 'handWounds': 1, 'Fame': 2}, {'Wounds': 0, 'handWounds': 0, 'hand': 6}),
    13: ({'hand': 6, 'advancedOffer': 2}, {'hand': 6, 'spellOffer': 2, 'crystal:Red': 2}),
    14: ({'token:Gold': 3}, {'token:Gold': 5}),
    15: ({'token:Red': 3}, {'token:Red': 5}),
    16: ({'RangedPool': 4, 'hand': 4, 'discard': 4}, {'StarBowActive': True}),
    17: ({'InfluencePool': 4}, {'Fame': 2, 'FreeRecruit': True}),
    18: ({}, {'unitWounds': 0}),
    19: ({'MeleePool': 3, 'CrystalsPerKill': 1}, {'MeleePool': 8, 'CrystalsPerKill': 1}),
    20: ({'BlockPool': 6}, {'BlockPool': 8, 'blockElement': 'ColdFire'}),
    21: ({'hand': 5, 'unitReady': True}, {'unitReady': True, 'Wounds': 0, 'handWounds': 0}),
    22: ({'MovementPool': 2}, {'Skills:count': 1, 'skillOffer': 2}),
    23: ({'hand': 5, 'RangedPool': 5, 'attackElement': 'Fire'}, {'hand': 6, 'SiegePool': 8, 'attackElement': 'Fire', 'Wounds': 4, 'handWounds': 4}),
    24: ({}, {'Fame': 1}),
}
for n, color in zip(range(4, 8), ['Red', 'Blue', 'White', 'Green']):
    ITEM[n] = ({'token:' + color: 3, 'crystal:' + color: 2, 'Fame': 1}, {'InfiniteMana:' + color: True, 'InfiniteMana:Black': True})
for n in range(25):
    for strong in [False, True]:
        kwargs = {'option': 2 + (3 << 16) if strong else 2} if n == 21 else {}
        case(f'items_{n:03}', strong, '印刷效果', ITEM[n][int(strong)], **kwargs,
             limits=['复用已有CardEffectFactory模块；尚无按ItemCardData出牌的完整服务。分配、强化移除、限次、持续及随机效果保持待联验'])
        if n == 12:
            # Preserve legacy numerical expectations, now reached by explicit healing consumption.
            CASES[-1]['operations']=[dict(kind='HealHand',option=3 if strong else 2)]

# Healing boundaries use the same real hand as the Wounds property. A heal is
# capped by available wound cards and never draws for a nonexistent wound.
for strong in [False, True]:
    b(1, strong, '没有伤牌时保留治疗点，不自动抽牌',
      {'Wounds': 0, 'handWounds': 0, 'hand': 3, 'discard': 2,'HealingPool':2 if strong else 1},
      setup={'player:Wounds': 0, 'DrawPerHeal': 1})
    b(1, strong, '治疗上限与每治疗一伤抽一牌',
      {'Wounds': 0, 'handWounds': 0, 'hand': 4, 'discard': 2},
      setup={'player:Wounds': 1, 'DrawPerHeal': 1})
    CASES[-1]['operations']=[dict(kind='HealHand',option=1)]


# Resource rejection cases assert both the rejection and unchanged real state.
# There are no wildcard tokens in these fixtures, so they cannot cover missing costs.
no_red = {'token:Red': 0, 'crystal:Red': 0, 'token:Gold': 0, 'token:Black': 0}
b(21, True, '红色费用不足不能降级为基础攻击',
  {'token:Red': 0, 'crystal:Red': 0, 'hand': 6, 'discard': 2}, setup=no_red,
  error='InvalidOperationException: 法力不足，不能支付所选效果')
for n in [0, 6, 12, 18]:
    color = ['Green', 'Red', 'Blue', 'White'][n // 6]
    # A full base-color token is available but black is missing; neither is spent.
    case(f'magic_{n:03}', True, '缺黑色费用，保留基础色与全部牌区',
         {'token:' + color: 2, 'token:Black': 0, 'hand': 6, 'discard': 2, 'unitReady': False},
         setup={'token:Black': 0, 'crystal:Black': 0, 'token:Gold': 0},
         error='InvalidOperationException: 法力不足，不能支付所选效果')
    case(f'magic_{n:03}', True, '白昼强效被拒绝且资源不变',
         {'token:' + color: 2, 'token:Black': 2, 'hand': 6, 'discard': 2, 'unitReady': False},
         setup={'DayPart': 'Day'}, error='InvalidOperationException: 白昼不能施放强效法术')


# Original card -> typed power -> real combat consumer. These are branch tests,
# not a replacement for the unresolved full turn / target lifecycle audit.
def enemy(armor, attack=0, element='Physical', abilities=(), fame=3):
    return dict(id='规则目标', armor=armor, attack=attack, element=element, abilities=list(abilities), fame=fame)


def combat(card_id, strong, title, phase, targets, printed, effective, required,
           kills=0, wounds=0, option=0, setup=None, followup='', followup_strong=False, extra=None, error=''):
    expected = {'combat:printed': printed, 'combat:effective': effective, 'combat:required': required,
                'combat:kills': kills, 'combat:fame': sum(e['fame'] for e in targets) if kills else 0,
                'combat:wounds': wounds, 'Fame': sum(e['fame'] for e in targets) if kills else 0,
                'Wounds': 3 + wounds + int(strong and card_id in ['magic_009', 'magic_015']),
                'handWounds': 3 + wounds + int(strong and card_id in ['magic_009', 'magic_015']), 'SiegePool': 0}
    expected.update(extra or {})
    if error:
        expected = extra or {}
    case(card_id, strong, title, expected, option=option, setup=setup, followup=followup,
         limits=['此例覆盖出牌、战斗资源及目标结算；整卡回合生命周期与其余条件仍按总表验收'])
    CASES[-1].update(combatPhase=phase, enemies=targets, followupEnhanced=followup_strong)
    if error: CASES[-1]['expectedException'] = error


for n, element, resistance, opposite in [(9, 'Fire', 'FireResist', 'IceResist'), (15, 'Ice', 'IceResist', 'FireResist')]:
    card = f'magic_{n:03}'
    for trait, armor, effective, kills, title in [([], 5, 5, 1, '无抗性'), ([opposite], 6, 5, 0, '相对元素不产生双倍'),
        ([resistance], 3, 2, 0, '同元素抗性取整'), (['MagicResist'], 5, 5, 1, '奥术免疫不抵抗攻击')]:
        combat(card, False, title, 'Ranged', [enemy(armor, abilities=trait)], 5, effective, armor, kills,
               extra={'played:RangedPool': 5, 'played:attackElement': element})
    combat(card, True, '攻城击破单重城防', 'Ranged', [enemy(8, abilities=['Fortified'])], 8, 8, 8, 1,
           extra={'played:SiegePool': 8, 'played:attackElement': element})
    combat(card, True, '双重城防在攻击阶段不限制攻城攻击', 'Melee', [enemy(8, abilities=['Fortified'])], 8, 8, 8, 1,
           setup={'FortifiedSite': True}, extra={'played:SiegePool': 8})
    combat(card, False, '远程不能攻击城防目标，已产生资源保留', 'Ranged', [enemy(3, abilities=['Fortified'])], 0, 0, 0,
           extra={'RangedPool': 5, 'Fame': 0, 'Wounds': 3, 'played:RangedPool': 5},
           error='InvalidOperationException: 城防限制：当前攻击不能指定这些目标')
    combat(card, True, '双重城防拒绝攻城，伤牌与攻城资源保留', 'Ranged', [enemy(8, abilities=['Fortified'])], 0, 0, 0,
           setup={'FortifiedSite': True}, extra={'SiegePool': 8, 'Fame': 0, 'Wounds': 4, 'handWounds': 4},
           error='InvalidOperationException: 城防限制：当前攻击不能指定这些目标')

combat('advanced_card_000', True, '两张原卡低效贡献合计后减半', 'Ranged',
       [enemy(3, abilities=['FireResist', 'IceResist'])], 6, 3, 3, 1,
       followup='advanced_card_001', followup_strong=True,
       extra={'token:Blue': 1, 'played:attackElement': 'Fire+Ice', 'played:RangedPool': 6})
combat('advanced_card_003', True, '碎裂之矢以攻城方式击破城防', 'Ranged', [enemy(3, abilities=['Fortified'])], 3, 3, 3, 1,
       extra={'played:SiegePool': 3})
combat('basic_card_021', True, '物理抗性将狂怒攻击4减为2', 'Melee', [enemy(3, abilities=['PhysicalResist'])], 4, 2, 3,
       extra={'played:MeleePool': 4})
combat('basic_card_024', True, '火焰格挡对寒冰攻击全效', 'Block', [enemy(5, attack=3, element='Ice')], 3, 3, 3,
       option=3, extra={'played:blockElement': 'Fire'})
combat('basic_card_024', True, '火焰格挡对火焰攻击低效，不足不能减伤', 'Block', [enemy(5, attack=3, element='Fire')], 3, 1, 3,
       wounds=2, option=3, extra={'played:blockElement': 'Fire'})
combat('basic_card_024', True, '低效格挡3加5合计后除2成功阻挡冰火4', 'Block', [enemy(5, attack=4, element='ColdFire')], 8, 4, 4,
       option=3, followup='basic_card_016', followup_strong=True,
       extra={'token:Blue': 1, 'played:BlockPool': 8, 'played:blockElement': 'Physical+Fire'})


combat('basic_card_002', True, '凝结接火焰箭矢，加值保持火焰元素', 'Ranged', [enemy(3, abilities=['FireResist'])], 5, 2, 3,
       followup='advanced_card_000', extra={'played:RangedPool': 5, 'played:attackElement': 'Fire', 'token:Red': 2})
combat('basic_card_002', True, '凝结接碎裂之矢，加值保持攻城方式', 'Ranged', [enemy(5, abilities=['Fortified'])], 5, 5, 5, 1,
       followup='advanced_card_003', extra={'played:SiegePool': 5, 'token:Green': 1})


combat('basic_card_002', True, '凝结接狂怒，实际攻击6击破护甲6', 'Melee', [enemy(6)], 6, 6, 6, 1,
       followup='basic_card_021', extra={'played:MeleePool': 6, 'token:Red': 2})


# Original basic-card branch repairs. Expected numbers come from the existing faces.
for strong in [False, True]:
    b(4, strong, '只选择移动，不同时治疗或格挡', {'MovementPool': 4 if strong else 2, 'Wounds': 3, 'handWounds': 3}, option=2)
    for option in [-1, 4]:
        b(27, strong, '非法本能选项保持费用与所有资源', {}, option=option,
          error='InvalidOperationException: 本能必须选择移动、影响、攻击或格挡')
    b(4, strong, '战斗中禁止治疗且不扣绿色费用', {}, setup={'InBattle': True},
      error='InvalidOperationException: 战斗中不能使用治疗效果')
    b(4, strong, '非法大地之子选项不得同时产生移动', {}, option=3,
      error='InvalidOperationException: 大地之子必须选择治疗、格挡或移动')
    combat('basic_card_027', strong, '本能攻击分支实际击败目标', 'Melee', [enemy(4 if strong else 2)],
           4 if strong else 2, 4 if strong else 2, 4 if strong else 2, 1, option=2)
    combat('basic_card_027', strong, '本能格挡分支实际阻挡敌人', 'Block', [enemy(5, attack=4 if strong else 2)],
           4 if strong else 2, 4 if strong else 2, 4 if strong else 2, option=3)

for terrain, time, value, element in [('Forest', 'Day', 3, 'Fire'), ('Forest', 'Night', 5, 'Ice'),
    ('Desert', 'Day', 5, 'Fire'), ('Desert', 'Night', 3, 'Ice'), ('Mountain', 'Day', 5, 'Fire'), ('Lake', 'Night', 2, 'Ice')]:
    # Select the opposite elemental attack, so this card's block operates at full strength.
    attack_element = 'Ice' if element == 'Fire' else 'Fire'
    combat('basic_card_004', True, '未修正地形费用与昼夜格挡：' + terrain + '/' + time,
           'Block', [enemy(5, attack=value, element=attack_element)], value, value, value, option=1,
           setup={'CurrentTerrain': terrain, 'DayPart': time, 'terrainCost:' + terrain: 2},
           extra={'played:blockElement': element, 'played:BlockPool': value})

combat('basic_card_020', False, '基础寒冰格挡3实际阻挡火焰攻击', 'Block', [enemy(5, attack=3, element='Fire')], 3, 3, 3, option=1,
       extra={'played:blockElement': 'Ice'})
for traits, attack_element, attack, printed, effective, required, wounds, title in [
    (['Swift'], 'Fire', 3, 7, 7, 6, 0, '迅捷能力加火焰颜色，共增加2'),
    ([], 'ColdFire', 3, 7, 3, 3, 0, '冰火两色增加2，冰格挡低效合计取整'),
    (['FireResist', 'IceResist'], 'Physical', 7, 7, 7, 7, 0, '两种抗性能力增加2'),
    (['MagicResist', 'Swift'], 'Fire', 3, 5, 5, 6, 2, '奥术免疫取消额外加成，格挡不足承受完整伤害')]:
    combat('basic_card_020', True, title, 'Block', [enemy(5, attack=attack, element=attack_element, abilities=traits)],
           printed, effective, required, wounds=wounds,
           extra={'played:BlockPool': 5, 'played:blockElement': 'Ice'})
b(20, True, '非法强化选项不得支付蓝色费用', {}, option=1,
  error='InvalidOperationException: 寒冰护体的效果选项无效')
b(6, False, '全神贯注绿色魔晶选项不额外产生蓝色魔力', {'crystal:Green': 3, 'token:Blue': 2},
  option=3, setup={'crystal:Green': 2}, limits=['魔晶存量上限、实际使用及回合生命周期仍须覆盖'])
b(6, False, '全神贯注非法选择不得退回蓝色魔力', {}, option=4,
  error='InvalidOperationException: 全神贯注的魔力选项无效')


b(6, False, '绿色魔晶满3后转成绿色魔力标记', {'crystal:Green': 3, 'token:Green': 3, 'token:Blue': 2},
  option=3, setup={'crystal:Green': 3}, limits=['回合内使用与最终卡区生命周期仍须联验'])
for n, color in [(0, 'Red'), (1, 'Blue'), (2, 'White'), (3, 'Green')]:
    case(f'advanced_card_{n:03}', False, '满库存的原卡魔晶奖励转为同色标记',
         {'crystal:' + color: 3, 'token:' + color: 3}, setup={'crystal:' + color: 3},
         limits=['此例覆盖魔晶奖励边界；实际使用和卡区生命周期仍须联验'])



# Printed Druidic Paths is a reduction for a selected hex/type, not a global cost override.
# The two legacy cases retain the expected numeric cost 2 and now observe the actual cost API.
def operation_case(card_id, strong, title, expected, operations, setup=None, **fields):
    case(card_id, strong, title, expected, setup=setup,
         limits=['已联验本例的实际操作与资源；完整卡区、其他组合及回合行动限制仍待联验'])
    CASES[-1].update(operations=operations, **fields)


def move(q, r=0): return dict(kind='Move', q=q, r=r)
def ready(index=0): return dict(kind='Ready', targetIndex=index)

for strong in [False, True]:
    # Identical Forest hexes expose base-only-one-hex versus strong-whole-terrain scope.
    operation_case('basic_card_018', strong, '连续移动区分选中格与另一森林',
        {'MovementPool': 0 if strong else 2, 'position:q': 2 if strong else 1,
         'move:selectedCost': 2, 'move:otherCost': 2 if strong else 3,
         'operation:0:success': True, 'operation:0:cost': 2,
         'operation:1:success': strong, 'operation:1:cost': 2 if strong else 3},
        [move(1), move(2)], setup={'MovementPool': 0 if strong else 2})
    for terrain, time, cost in [('Forest','Night',4), ('Desert','Day',4), ('Desert','Night',2),
                                ('Plains','Day',2), ('Mountain','Day',2147483647)]:
        valid=cost<2147483647
        operation_case('basic_card_018', strong, terrain+'/'+time+'实际移动与最低费用',
            {'MovementPool': (8 if strong else 6)-(cost if valid else 0), 'position:q': 1 if valid else 0,
             'operation:0:success':valid,'operation:0:cost':cost}, [move(1)],
            setup={'MovementPool':4,'DayPart':time}, destinationTerrain=terrain)
    operation_case('basic_card_018', strong, '非相邻格拒绝不消耗资源',
        {'MovementPool':4 if strong else 2,'position:q':0,'operation:0:success':False,
         'operation:0:cost':2 if strong else 3}, [move(2)])
    operation_case('basic_card_018', strong, '回合结束后减费与移动力过期',
        {'MovementPool':0,'InfluencePool':0,'move:selectedCost':3,'move:otherCost':3,
         'operation:0:success':True, **{'token:'+c:0 for c in COLORS}}, [dict(kind='EndTurn')])
    b(18,strong,'缺少移动目标时在支付前拒绝',{},
      error='InvalidOperationException: 德鲁伊之道必须选择有效六角格或地形')
    CASES[-1]['omitMovementTarget']=True

for level in [1,2,3,4]:
    operation_case('basic_card_026',True,'重整等级'+str(level)+'部队的上限与费用',
        {'InfluencePool':6-2*level if level<=2 else 6,'Reputation':-1,'unitReady':level<=2,
         'operation:0:success':level<=2,'unitLevel':level},[ready()],unitLevels=[level,2])
operation_case('basic_card_026',True,'依次重整1级和2级部队刚好支付6影响力',
    {'InfluencePool':0,'Reputation':-1,'readyUnits':2,'operation:0:success':True,'operation:1:success':True},
    [ready(0),ready(1)],unitLevels=[1,2])
operation_case('basic_card_026',True,'第二支部队影响力不足时保留剩余资源',
    {'InfluencePool':2,'Reputation':-1,'readyUnits':1,'operation:0:success':True,'operation:1:success':False},[ready(0),ready(1)])
for index,title in [(-1,'无目标'),(2,'非自有目标')]:
    operation_case('basic_card_026',True,title+'不得扣影响力',
        {'InfluencePool':6,'Reputation':-1,'unitReady':False,'operation:0:success':False},[ready(index)])
for fields,title in [({'unitInitiallyReady':True},'已就绪目标'),({'unitDestroyed':True},'已摧毁目标')]:
    operation_case('basic_card_026',True,title+'拒绝重复重整',
        {'InfluencePool':6,'Reputation':-1,'operation:0:success':False},[ready()],**fields)
operation_case('basic_card_026',True,'重整受伤部队保留伤牌且仍不能发动',
    {'InfluencePool':2,'Reputation':-1,'unitReady':True,'unitWounds':2,'unitCanActivate':False,
     'operation:0:success':True},[ready()],unitWounds=2)
operation_case('basic_card_026',True,'回合结束重整许可过期',
    {'InfluencePool':0,'Reputation':-1,'ReadyInfluencePerLevel':0,'ReadyUnitMaxLevel':0,
     'unitReady':False,'operation:0:success':True,'operation:1:success':False,**{'token:'+c:0 for c in COLORS}},
    [dict(kind='EndTurn'),ready()])



# Elemental spell costs and block contributions must survive into actual combat.
for strong in [False, True]:
    for color, option, value, element, attack_type in [
        ('Blue',1,8,'Ice','Melee'),('Red',0,7,'ColdFire','Melee'),
        ('White',3,6,'Ice','Ranged'),('Green',2,5,'Ice','Siege')]:
        power=value+(3 if strong else 0)
        phase='Melee' if attack_type=='Melee' else 'Ranged'
        combat('magic_017',strong,color+'额外魔力决定攻击类型并实际杀敌',phase,
               [enemy(power,abilities=['PhysicalResist'])],power,power,power,option=option,kills=1,
               extra={'token:Blue':0 if color=='Blue' else 1,'token:'+color:0 if color=='Blue' else 1,
                      'played:attackElement':element,'played:'+('SiegePool' if attack_type=='Siege' else 'RangedPool' if attack_type=='Ranged' else 'MeleePool'):power})
    for option,setup in [(1,{'token:Blue':1,'crystal:Blue':0}), (0,{'token:Red':0,'crystal:Red':0})]:
        case('magic_017',strong,'额外费用不足时施法费用也不得先扣',{},option=option,setup=setup,
             error='InvalidOperationException: 法力不足，不能支付所选效果')
    case('magic_017',strong,'黑色不能作为箭矢的四选一颜色',{},option=5,
         error='InvalidOperationException: 魔力箭矢必须选择红蓝绿白之一')
    power=7 if strong else 5
    combat('magic_010',strong,'火焰攻击对火抗合计减半','Melee',[enemy(power//2,abilities=['FireResist'])],
           power,power//2,power//2,kills=1)
    combat('magic_010',strong,'火焰攻击穿过物理抗性','Melee',[enemy(power,abilities=['PhysicalResist'])],power,power,power,kills=1)
    for element,required,wounds in [('Ice',9 if strong else 7,0),('Fire',5 if strong else 4,3 if strong else 2)]:
        block=9 if strong else 7
        combat('magic_010',strong,'火焰格挡对'+element+'攻击的效率','Block',[enemy(5,attack=required,element=element)],
               block,block if element=='Ice' else block//2,required,option=1,wounds=wounds)
    case('magic_010',strong,'非法攻击格挡选项不得支付',{},option=2,
         error='InvalidOperationException: 烈焰之墙必须选择攻击或格挡')


def combat_operation(kind,index=0): return dict(kind=kind,targetIndex=index)
def operation_result(index,printed,effective,required,kills=0,wounds=0):
    return {f'operation:{index}:'+k:v for k,v in dict(printed=printed,effective=effective,required=required,kills=kills,wounds=wounds).items()}

for traits,attack,index,title in [([],3,0,'成功格挡后实际减甲并击杀'),(['IceResist'],3,0,'寒冰抗性取消减甲'),
                                 (['MagicResist'],3,0,'奥术免疫取消减甲'),([],4,0,'格挡失败不得提前减甲'),
                                 ([],3,1,'选择第二个敌人只改变该敌人的护甲')]:
    reduced=not traits and attack==3
    expected={'MeleePool':0,'BlockPool':0,'BlockArmorReduction':0,'Wounds':3 if attack==3 else 5,
              'Fame':3 if reduced else 0, **operation_result(0,3,3,attack,wounds=0 if attack==3 else 2),
              **operation_result(1,2,2,2 if reduced else 5,kills=1 if reduced else 0),
              'operation:0:armor0':3 if reduced and index==0 else 0,'operation:0:armor1':3 if reduced and index==1 else 0}
    operation_case('advanced_card_005',True,title,expected,[combat_operation('Block',index),combat_operation('Attack',index)],
                   setup={'MeleePool':2},enemies=([enemy(9)] if index else [])+[enemy(5,attack=attack,element='Fire',abilities=traits)])
combat('advanced_card_005',False,'基础寒冰格挡实际对抗火焰攻击','Block',[enemy(5,attack=3,element='Fire')],3,3,3)

operation_case('magic_008',False,'成功格挡后火焰攻击可用于另一个物抗敌人',
    {'MeleePool':0,'BlockPool':0,'AttackAfterBlock':0,'Fame':3,'Wounds':3,
     **operation_result(0,4,4,4),**operation_result(1,4,4,4,kills=1),'operation:0:MeleePool':4},
    [combat_operation('Block'),combat_operation('Attack',1)],
    enemies=[enemy(9,attack=4,element='Ice'),enemy(4,abilities=['PhysicalResist'])])
operation_case('magic_008',False,'失败格挡没有追加攻击',
    {'MeleePool':0,'BlockPool':0,'AttackAfterBlock':0,'Fame':0,'Wounds':6,**operation_result(0,4,4,5,wounds=3)},
    [combat_operation('Block')],enemies=[enemy(9,attack=5,element='Ice')])
for traits in [[],['FireResist'],['MagicResist']]:
    operation_case('magic_008',True,'爆破护盾消灭条件与抗性'+str(traits),
        {'MeleePool':0,'BlockPool':0,'KillBlockedEnemy':False,'Fame':0 if traits else 3,'Wounds':3,
         **operation_result(0,4,4,4,kills=0 if traits else 1)},[combat_operation('Block')],
        enemies=[enemy(9,attack=4,element='Ice',abilities=traits)])
operation_case('magic_008',True,'消灭只绑定本次格挡不得泄漏到下个敌人',
    {'MeleePool':0,'BlockPool':0,'KillBlockedEnemy':False,'Fame':3,'Wounds':5,
     **operation_result(0,4,4,4,kills=1),**operation_result(1,0,0,3,wounds=2)},
    [combat_operation('Block'),combat_operation('Block',1)],
    enemies=[enemy(9,attack=4,element='Ice'),enemy(9,attack=3,element='Ice')])


# Siege dice and split blocks: original faces, actual rolls, separate enemy attacks.
for time in ['Day', 'Night']:
    for face,color in enumerate(COLORS):
        hurt=int(color in ['Red','Black'])
        combat('items_009',False,time+'掷出'+color+'后攻城攻击城防敌人','Ranged',
               [enemy(5,abilities=['Fortified'])],5,5,5,kills=1,setup={'DayPart':time},
               extra={'Wounds':3+hurt,'handWounds':3+hurt,'dice:faces':color,'dice:count':1,'played:SiegePool':5})
        CASES[-1]['manaRolls']=[face]
    for bonus in [0,1,3,5]:
        faces=[0,5,1,4,0][:bonus]
        hurt=sum(f in [0,5] for f in faces)
        combat('items_009',True,time+'选择额外攻城'+str(bonus)+'逐颗掷骰','Ranged',
               [enemy(5+bonus,abilities=['Fortified'])],5+bonus,5+bonus,5+bonus,kills=1,option=bonus,
               setup={'DayPart':time},extra={'Wounds':3+hurt,'handWounds':3+hurt,
                'dice:faces':','.join(COLORS[f] for f in faces),'dice:count':bonus,'played:SiegePool':5+bonus})
        CASES[-1]['manaRolls']=faces
for option in [-1,6]:
    case('items_009',True,'拒绝超范围加攻且不掷骰',{'SiegePool':0,'dice:count':0},option=option,
         error='InvalidOperationException: 愤怒号角额外攻击必须在允许范围内')
    CASES[-1]['manaRolls']=[]

for traits,armor,effective in [(['Fortified'],3,3),(['PhysicalResist'],1,1)]:
    combat('advanced_card_038',True,'攻城3对'+str(traits),'Ranged',[enemy(armor,abilities=traits)],3,effective,armor,kills=1,
           extra={'played:SiegePool':3})
for element,attack,effective,wounds in [('Physical',6,6,0),('Fire',4,3,2)]:
    combat('advanced_card_038',True,'选择格挡6对'+element,'Block',[enemy(5,attack=attack,element=element)],6,effective,attack,option=1,wounds=wounds)
case('advanced_card_038',True,'无效选项不扣绿色费用',{},option=2,error='InvalidOperationException: 自然之力选项无效')

for strong in [False,True]:
    power=8 if strong else 6
    for element in ['Physical','Fire','Ice','ColdFire']:
        effective=power if strong or element=='Physical' else power//2
        combat('items_020',strong,'单次格挡对'+element,'Block',[enemy(5,attack=effective,element=element)],power,effective,effective,
               extra={'played:blockElement':'ColdFire' if strong else 'Physical'})
    count=3 if strong else 2
    expected={'BlockPool':0,'Wounds':3,'handWounds':3,'Fame':0}
    for index in range(count):
        expected.update(operation_result(index,4,4,4))
        expected['operation:'+str(index)+':BlockPool']=(count-index-1)*4
    operation_case('items_020',strong,'分次格挡必须分别消耗在不同攻击上',expected,
                   [combat_operation('Block',i) for i in range(count)],enemies=[enemy(9,attack=4) for _ in range(count)])
    CASES[-1]['option']=1
    expected={'BlockPool':(count-2)*4,'Wounds':6,'handWounds':6,
              **operation_result(0,4,4,5,wounds=3),**operation_result(2,4,4,4),
              'operation:1:success':False,'operation:1:exception':'分次格挡必须选择尚未使用该效果的攻击',
              'operation:1:BlockPool':(count-1)*4}
    operation_case('items_020',strong,'格挡5失败不合并分段且拒绝重复同次攻击',expected,
                   [combat_operation('Block',0),combat_operation('Block',0),combat_operation('Block',1)],
                   enemies=[enemy(9,attack=5),enemy(9,attack=4)])
    CASES[-1]['option']=1
    case('items_020',strong,'拒绝单次与分次之外的选项',{},option=2,error='InvalidOperationException: 亡君之盾必须选择单次或分次格挡')


# Original magic_014: independently authored downstream expectations, appended
# without changing any of the previous 490 definitions.
def mist_damage(title, enemies, operations, expected):
    operation_case('magic_014', True, title, expected, operations,
                   enemies=enemies, unitIds=['army_2_006', 'army_2_001'])


def damage(target=0, units=()):
    return dict(kind='Damage', targetIndex=target, unitTargets=list(units))


def mist_state(index, hero=3, discard=0, wounds='0,0', ignored=True, resistance=True,
               destroyed='False,False', success=True):
    return {f'operation:{index}:{k}': v for k, v in dict(heroWounds=hero, discardWounds=discard,
            unitWounds=wounds, ignoreWound=ignored, allResist=resistance, destroyed=destroyed, success=success).items()}


for element in ['Physical', 'Fire', 'Ice', 'ColdFire']:
    for attack, wounds, hero, ignored in [(3,'0,0',3,True),(4,'1,0',3,True),(6,'1,0',3,True),(7,'1,0',3,False),(9,'1,0',4,False)]:
        mist_damage(element+'全抗性承伤'+str(attack), [enemy(5,attack,element)], [damage(units=[0])],
                    {'Wounds':hero, 'unitWounds':int(wounds[0]), 'IgnoreNextWound':ignored,
                     **mist_state(0,hero=hero,wounds=wounds,ignored=ignored)})
mist_damage('所有原部队分别吸收且不消费英雄首伤', [enemy(5,3,'ColdFire'),enemy(5,3,'Fire')],
            [damage(0,[0]),damage(1,[1])], {**mist_state(0),**mist_state(1), 'DamageAssignedUnits:count':2})
mist_damage('同回合另一场战斗继续获得抗性', [enemy(5,3,'ColdFire')],
            [damage(0,[0]),dict(kind='EndBattle'),damage(0,[0])],
            {**mist_state(0),**mist_state(1),**mist_state(2)})
mist_damage('本场重复分配被拒绝且资源不变', [enemy(5,3,'Ice')],
            [damage(0,[0]),damage(0,[0])], {**mist_state(0),**mist_state(1,success=False),
             'operation:1:exception':'同场战斗不能重复向该部队分配伤害'})
mist_damage('跨战斗仍生效而结束回合后失效', [enemy(5,3,'ColdFire')],
            [damage(0,[0]),dict(kind='EndBattle'),dict(kind='EndTurn'),damage(0,[0])],
            {**mist_state(0),**mist_state(1),**mist_state(2,ignored=False,resistance=False),
             **mist_state(3,wounds='1,0',ignored=False,resistance=False),
             'token:Blue':0,'token:Black':0,'UnitsResistAll':False,'IgnoreNextWound':False})
for trait in ['Poison','Paralyze']:
    mist_damage('英雄第一伤忽略'+trait+'随后再次承伤', [enemy(5,2,abilities=[trait])],
                [damage(),damage()], {**mist_state(0,ignored=False),
                 **mist_state(1,hero=4,discard=1 if trait=='Poison' else 0,ignored=False),
                 'Wounds':4,'hand':7 if trait=='Poison' else 4,'discard':3 if trait=='Poison' else 5})
    mist_damage('同一攻击剩余伤牌仍触发'+trait, [enemy(5,4,abilities=[trait])], [damage()],
                {**mist_state(0,hero=4,discard=1 if trait=='Poison' else 0,ignored=False),
                 'Wounds':4,'hand':7 if trait=='Poison' else 4,'discard':3 if trait=='Poison' else 5})
    mist_damage('全抗性避免部队伤牌及'+trait, [enemy(5,3,'ColdFire',[trait])], [damage(0,[0])], mist_state(0))
    mist_damage('抗性超额部队仍受'+trait, [enemy(5,4,'ColdFire',[trait])], [damage(0,[0])],
                mist_state(0,wounds='2,0' if trait=='Poison' else '0,0',destroyed='True,False' if trait=='Paralyze' else 'False,False'))
mist_damage('非敌人创伤不消费首伤保护', [enemy(5,2,abilities=['Poison'])],
            [dict(kind='SelfWound'),damage()], {**mist_state(0,hero=4),**mist_state(1,hero=4,ignored=False),'Wounds':4})

for time in ['Day','Night']:
    for terrain in ['Plains','Forest','Desert','Swamp','Wasteland','Lake','Hills','Mountain']:
        allowed = terrain not in ['Hills','Mountain']
        operation_case('magic_014',False,time+terrain+'两格移动后不得透支',
            {'MovementPool':0 if allowed else 4,'position:q':2 if allowed else 0,
             **{f'operation:{i}:success':allowed and i<2 for i in range(3)},
             **{f'operation:{i}:cost':2 if allowed else 2147483647 for i in range(3)}},
            [move(1),move(2),move(1)],setup={'DayPart':time},destinationTerrain=terrain)
for terrain,cost in [('Lake',2147483647),('Hills',3),('Mountain',2147483647),('Forest',5)]:
    operation_case('magic_014',False,'结束回合恢复'+terrain+'通行规则',
        {'MovementPool':0,'MoveForbidden:count':0,
         'move:selectedCost':cost,'operation:0:success':True,'operation:1:success':False,
         'operation:1:cost':cost,'token:Blue':0},
        [dict(kind='EndTurn'),move(1)],setup={'DayPart':'Night'},destinationTerrain=terrain)
for strong in [False,True]:
    case('magic_014',strong,'无效选项在支付前拒绝',{'UnitsResistAll':False,'IgnoreNextWound':False},option=1,
         error='InvalidOperationException: 迷雾形态没有可选分支')
    case('magic_014',strong,'缺蓝色费用原子拒绝',{'UnitsResistAll':False,'IgnoreNextWound':False},
         setup={'token:Blue':0,'crystal:Blue':0},error='InvalidOperationException: 法力不足，不能支付所选效果')
case('magic_014',True,'缺黑色费用不先扣蓝色',{'UnitsResistAll':False,'IgnoreNextWound':False},
     setup={'token:Black':0},error='InvalidOperationException: 法力不足，不能支付所选效果')
case('magic_014',True,'白昼拒绝强效法术',{'UnitsResistAll':False,'IgnoreNextWound':False},
     setup={'DayPart':'Day'},error='InvalidOperationException: 白昼不能施放强效法术')
operation_case('magic_014',True,'蓝晶加黑标记支付后实际忽略毒素首伤',
    {'token:Blue':0,'crystal:Blue':0,'token:Black':1,**mist_state(0,ignored=False)},
    [damage()],setup={'token:Blue':0},enemies=[enemy(5,2,abilities=['Poison'])],
    unitIds=['army_2_006','army_2_001'])


# magic_011: printed discard rewards and crystal pairs, using original instances.
def sacrifice_discard(title, targets, rewards, extra=(), option=0, setup=None, error='', foreign=False):
    initial=['magic_011','basic_card_021','basic_card_014','wound_0','wound_1','wound_2',*extra]
    expected={'hand':len(initial)-(0 if error else len(targets)), 'discard':2+(0 if error else len(targets)),
              'handWounds':3,'Wounds':3,'CardsToDiscard:count':len(targets) if error else 0,
              'hand:ids':','.join(c for i,c in enumerate(initial) if error or i not in targets),
              'discard:ids':','.join(([] if error else [initial[i] for i in reversed(targets)])+['basic_card_016','basic_card_008'])}
    if error:
        expected.update({f'token:{c}':(setup or {}).get(f'token:{c}',2) for c in COLORS})
        expected.update({f'crystal:{c}':(setup or {}).get(f'crystal:{c}',int(c in COLORS[:4])) for c in COLORS})
    expected.update(rewards)
    case('magic_011',False,title,expected,option=option,setup=setup,
         limits=['实际弃牌及奖励已测；正在施放卡的实例身份和完整出牌区生命周期仍需联验'])
    CASES[-1].update(extraHandCards=list(extra),discardIndices=list(targets),foreignDiscard=foreign,expectedException=error)


sacrifice_discard('不弃牌仍获得一红晶',[],{'crystal:Red':2})
sacrifice_discard('弃原红牌获得红晶',[1],{'crystal:Red':3})
sacrifice_discard('弃原蓝牌获得蓝晶',[2],{'crystal:Red':2,'crystal:Blue':2})
sacrifice_discard('两张不同颜色分别进入弃牌堆',[1,2],{'crystal:Red':3,'crystal:Blue':2})
sacrifice_discard('最多三张不同原色',[1,2,6],{'crystal:Red':3,'crystal:Blue':2,'crystal:Green':2},extra=['basic_card_000'])
sacrifice_discard('同ID不同实例均可弃掉',[2,6],{'crystal:Red':2,'crystal:Blue':3},extra=['basic_card_014'])
sacrifice_discard('只弃所选同ID实例',[6],{'crystal:Red':2,'crystal:Blue':2},extra=['basic_card_014'])
sacrifice_discard('法术按原蓝色奖励',[6],{'crystal:Red':2,'crystal:Blue':2},extra=['magic_014'])
for color in range(4):
    sacrifice_discard('弃神器选择'+COLORS[color],[6],{'crystal:Red':3 if color==0 else 2,
                      **({} if color==0 else {'crystal:'+COLORS[color]:2})},extra=['items_004'],option=color)
sacrifice_discard('三件神器分别选择红蓝绿',[6,7,8],{'crystal:Red':3,'crystal:Blue':2,'crystal:Green':2},
                  extra=['items_004','items_005','items_006'],option=0|(1<<2)|(2<<4))
sacrifice_discard('同ID神器实例可选不同颜色',[6,7],{'crystal:Red':2,'crystal:Blue':2,'crystal:Green':2},
                  extra=['items_004','items_004'],option=1|(2<<2))
sacrifice_discard('红晶满三上限转为红标记',[1],{'crystal:Red':3,'token:Red':3},setup={'crystal:Red':3})
sacrifice_discard('两张蓝牌溢出蓝晶上限',[2,6],{'crystal:Red':2,'crystal:Blue':3,'token:Blue':4},
                  setup={'crystal:Blue':3},extra=['basic_card_014'])
sacrifice_discard('拒绝重复选择同一实例',[1,1],{},error='InvalidOperationException: 献祭不能重复选择同一张手牌')
sacrifice_discard('拒绝夹杂伤牌且不先弃合法牌',[1,3],{},error='InvalidOperationException: 献祭不能弃掉创伤或非契约卡牌')
sacrifice_discard('同ID外来对象不能冒充持有手牌',[1],{},foreign=True,error='InvalidOperationException: 献祭必须选择实际持有的手牌实例')
sacrifice_discard('拒绝超过三张',[1,2,6,7],{},extra=['basic_card_000','items_004'],error='InvalidOperationException: 献祭最多弃掉三张非创伤手牌')
sacrifice_discard('无神器时拒绝额外选色编码',[1],{},option=1,error='InvalidOperationException: 献祭神器魔晶颜色选择无效')
sacrifice_discard('拒绝超出单神器颜色范围',[6],{},extra=['items_004'],option=4,error='InvalidOperationException: 献祭神器魔晶颜色选择无效')
sacrifice_discard('不能用尚未获得的红晶支付本卡',[1],{},setup={'token:Red':0,'crystal:Red':0},error='InvalidOperationException: 法力不足，不能支付所选效果')


def sacrifice_pair_state(first, second, first_count, second_count):
    pairs=min(first_count,second_count)
    tokens={c:2 for c in COLORS};tokens['Red']-=1;tokens['Black']-=1
    crystals={c:1 for c in COLORS[:4]};crystals[first]=first_count;crystals[second]=second_count
    for c in [first,second]:tokens[c]+=pairs;crystals[c]-=pairs
    return {**{'token:'+c:n for c,n in tokens.items()},**{'crystal:'+c:n for c,n in crystals.items()}}


for first in ['Green','White']:
    for second,element,resist in [('Red','Fire','FireResist'),('Blue','Ice','IceResist')]:
        option=COLORS.index(first)|(COLORS.index(second)<<8)
        for count in [0,1,2,3]:
            power=count*(4 if first=='Green' else 6)
            expected=sacrifice_pair_state(first,second,count,3)
            setup={'crystal:'+first:count,'crystal:'+second:3}
            if count==0:
                combat('magic_011',True,first+second+'零对不能攻击','Ranged',[enemy(1)],0,0,1,
                       option=option,setup=setup,extra={'SiegePool':0,'RangedPool':0,**expected},
                       error='InvalidOperationException: 没有可用于此阶段的攻击')
            else:
                combat('magic_011',True,first+second+str(count)+'对实际消灭目标','Ranged',
                       [enemy(power,abilities=['Fortified'] if first=='Green' else [])],power,power,power,kills=1,
                       option=option,setup=setup,extra={'played:attackElement':element,
                       'played:SiegePool':power if first=='Green' else 0,'played:RangedPool':power if first=='White' else 0,**expected})
        power=4 if first=='Green' else 6
        combat('magic_011',True,first+second+'对应元素抗性减半','Ranged',[enemy(power//2,abilities=[resist])],
               power,power//2,power//2,kills=1,option=option,extra=sacrifice_pair_state(first,second,1,1))
        if first=='White':
            combat('magic_011',True,'远程'+element+'仍被城防拒绝','Ranged',[enemy(3,abilities=['Fortified'])],0,0,3,
                   option=option,extra={'RangedPool':6,'attackElement':element,**sacrifice_pair_state(first,second,1,1)},
                   error='InvalidOperationException: 城防限制：当前攻击不能指定这些目标')
for option in [-1,0,1,4,514,65538]:
    case('magic_011',True,'非法配对选择不消费魔力或魔晶',{'SiegePool':0},option=option,
         error='InvalidOperationException: 牺牲必须选择绿或白，再选择红或蓝')
for setup in [{'token:Black':0},{'token:Red':0,'crystal:Red':0},{'DayPart':'Day'}]:
    case('magic_011',True,'施法条件不满足时不转化任何晶体',{'SiegePool':0},option=2,setup=setup,
         error='InvalidOperationException: '+('白昼不能施放强效法术' if 'DayPart' in setup else '法力不足，不能支付所选效果'))
combat('magic_011',True,'支付红晶后已无红绿配对不能攻击','Ranged',[enemy(1)],0,0,1,option=2,
       setup={'token:Red':0},extra={'SiegePool':0,'crystal:Red':0,'crystal:Green':1,'token:Red':0,'token:Green':2},
       error='InvalidOperationException: 没有可用于此阶段的攻击')
combat('magic_011',True,'配对所得蓝魔力继续施放原迷雾形态','Ranged',[enemy(4,abilities=['Fortified'])],4,4,4,kills=1,
       option=258,setup={'token:Blue':0},followup='magic_014',extra={'MovementPool':4,'token:Blue':0,'crystal:Blue':0,
       'token:Green':3,'crystal:Green':0,'played:attackElement':'Ice'})


# magic_021: possession preserves printed power, element and source exclusion.
for element, traits in [('Physical',['PhysicalResist']),('Fire',['FireResist']),('Ice',['IceResist']),('ColdFire',['FireResist','IceResist'])]:
    for resisted in [False,True]:
        effective=3 if resisted else 7
        operation_case('magic_021',True,element+'附身攻击其他敌人'+('并结算抗性' if resisted else ''),
            {**operation_result(0,7,effective,effective,kills=1),'Fame':3,'Wounds':3,
             'played:MeleePool':7,'played:attackElement':element,'SkipAttackIndices:0':True},
            [combat_operation('Attack',1)],enemies=[enemy(5,7,element,['Brutal','Swift','Poison']),enemy(effective,abilities=traits if resisted else [])])
operation_case('magic_021',True,'附身第二个敌人并用其攻击第一个敌人',
    {**operation_result(0,7,7,7,kills=1),'Fame':3,'SkipAttackIndices:1':True,'played:attackElement':'Ice'},
    [combat_operation('Attack',0)],option=1,enemies=[enemy(7),enemy(5,7,'Ice')])
operation_case('magic_021',True,'禁止攻击附身来源且保留攻击供另一目标使用',
    {'operation:0:success':False,'operation:0:exception':'没有可用于此阶段的攻击','operation:0:MeleePool':4,
     **operation_result(1,4,4,4,kills=1),'Fame':3},
    [combat_operation('Attack',0),combat_operation('Attack',1)],enemies=[enemy(4,4),enemy(4)])
operation_case('magic_021',True,'普通攻击击杀附身者后仍可使用借来的攻击',
    {**operation_result(0,4,4,4,kills=1),'operation:0:MeleePool':4,
     **operation_result(1,4,4,4,kills=1),'Fame':6},
    [combat_operation('Attack',0),combat_operation('Attack',1)],setup={'MeleePool':4},enemies=[enemy(4,4),enemy(4)])
operation_case('magic_021',True,'第二个敌人被附身不造成毒素伤牌且可用于攻击',
    {**operation_result(0,0,0,8,wounds=0),**operation_result(1,8,8,8,kills=1),'Wounds':3,'handWounds':3,'DiscardWounds':0,'Fame':3},
    [combat_operation('Block',1),combat_operation('Attack',0)],option=1,
    enemies=[enemy(8),enemy(5,8,'Fire',['Poison'])])
operation_case('magic_021',True,'零攻击敌人不凭空产生攻击力',
    {'operation:0:success':False,'operation:0:exception':'没有可用于此阶段的攻击','SkipAttackIndices:0':True},
    [combat_operation('Attack',1)],enemies=[enemy(5,0),enemy(4)])
for target in [-1,1]:
    case('magic_021',True,'非法敌人索引在费用支付前拒绝',{'SkipAttackIndices:count':0,'PossessedEnemyIndices:count':0},
         option=target,error='InvalidOperationException: 附身必须选择当前战斗中的敌人')
for setup in [{'token:White':0,'crystal:White':0},{'token:Black':0},{'DayPart':'Day'}]:
    case('magic_021',True,'附身费用或昼夜条件不满足时原子拒绝',{'SkipAttackIndices:count':0},setup=setup,
         error='InvalidOperationException: '+('白昼不能施放强效法术' if 'DayPart' in setup else '法力不足，不能支付所选效果'))
combat('magic_021',True,'附身不能借转远程效果提前攻击','Ranged',[enemy(5,4),enemy(4)],0,0,9,
       setup={'NextMeleeAsRanged':True},extra={'MeleePool':4,'NextMeleeAsRanged':True},
       error='InvalidOperationException: 没有可用于此阶段的攻击')
operation_case('magic_021',True,'结束战斗后借来的攻击与跳过攻击标记过期',
    {'SkipAttackIndices:count':0,'PossessedEnemyIndices:count':0,
     'operation:1:success':False,'operation:1:exception':'没有可用于此阶段的攻击'},
    [dict(kind='EndBattle'),combat_operation('Attack',1)],enemies=[enemy(5,4),enemy(4)])

# These pre-existing possession fixtures describe an active battle, including
# the fee/day and invalid-index cases. Make that precondition explicit so the
# new guard cannot mask the particular rejection each case is intended to test.
for existing in CASES:
    if existing['cardId']=='magic_021' and existing['enhanced']:
        existing['setup'].append(dict(key='InBattle',value='True'))


# magic_021 top: conditional rewards and one successful original-unit recruitment.
for color in range(4):
    name=COLORS[color]
    case('magic_021',False,'交涉中选择'+name+'魔晶',{'InfluencePool':4,'crystal:'+name:2,'CharmRecruitDiscount':0},option=color,setup={'InNegotiation':True},limits=['已测交涉奖励；完整出牌区生命周期仍需联验'])
    case('magic_021',False,name+'魔晶满额转为同色魔力',{'InfluencePool':4,'crystal:'+name:3,'token:'+name:2 if name=='White' else 3,'CharmRecruitDiscount':0},
         option=color,setup={'InNegotiation':True,'crystal:'+name:3},limits=['已测魔晶溢出；完整出牌区生命周期仍需联验'])
for option in [0,256]:
    case('magic_021',False,'非交涉只获得影响力',{'InfluencePool':4,'CharmRecruitDiscount':0,**{'crystal:'+c:1 for c in COLORS[:4]}},option=option,
         limits=['已测交涉条件；完整出牌区生命周期仍需联验'])
for option in [-1,4,5,257,512,65536]:
    case('magic_021',False,'非法魔晶或混合奖励编码在支付前拒绝',{'InfluencePool':0,'CharmRecruitDiscount':0},option=option,setup={'InNegotiation':True},
         error='InvalidOperationException: 魅惑必须选择红蓝绿白魔晶或一次招募减费')
case('magic_021',False,'支付已有白晶后获得新的白晶',{'InfluencePool':4,'token:White':0,'crystal:White':1,'SpentCrystals:White':1},option=3,
     setup={'InNegotiation':True,'token:White':0},limits=['费用先于奖励；完整出牌区生命周期仍需联验'])
case('magic_021',False,'不能用本次未来白晶支付当前法术',{'InfluencePool':0,'CharmRecruitDiscount':0},option=3,
     setup={'InNegotiation':True,'token:White':0,'crystal:White':0},error='InvalidOperationException: 法力不足，不能支付所选效果')
case('magic_021',False,'本次白晶供后续原魅惑支付',{'InfluencePool':8,'token:White':0,'crystal:White':0,'crystal:Red':2,'SpentCrystals:White':2},option=3,
     setup={'InNegotiation':True,'token:White':0},followup='magic_021',limits=['已测奖励支付后续法术；完整出牌区生命周期仍需联验'])


def recruit(index=0,location='Village'):return dict(kind='Recruit',targetIndex=index,recruitLocation=location)
def recruit_result(index,cost,success,influence,discount,offer,units):
    return {f'operation:{index}:'+k:v for k,v in dict(cost=cost,success=success,influence=influence,discount=discount,offer=','.join(offer),units=units).items()}
farmer='army_2_006';monk='army_2_005'
def charm_recruit(title,expected,ops,offer=None,setup=None,option=256):
    offer=offer or [farmer]
    operation_case('magic_021',False,title,expected,ops,setup={'InNegotiation':True,'player:ExtraCommandSlots':2,**(setup or {})},
                   option=option,recruitOffer=offer,unitIds=[farmer])
charm_recruit('一次减费招募原版农民，供给转入队伍',
    {'InfluencePool':3,'CharmRecruitDiscount':0,'recruit:offer':'','recruit:owned':farmer+','+farmer,'readyUnits':1,
     'recruit:art':farmer,**recruit_result(0,1,True,3,0,[],2)},[recruit()])
charm_recruit('第二次招募恢复原价，不重复减三',
    {'InfluencePool':1,'CharmRecruitDiscount':0,'recruit:offer':'','recruit:owned':','.join([farmer]*3),'readyUnits':2,
     **recruit_result(0,1,True,5,0,[farmer],2),**recruit_result(1,4,True,1,0,[],3)},
    [recruit(),recruit()],offer=[farmer,farmer],setup={'InfluencePool':2})
charm_recruit('折扣不能因第一次成功而让资源不足的第二次通过',
    {'InfluencePool':3,'CharmRecruitDiscount':0,'recruit:offer':farmer,'Units:count':2,
     **recruit_result(0,1,True,3,0,[farmer],2),**recruit_result(1,4,False,3,0,[farmer],2)},
    [recruit(),recruit()],offer=[farmer,farmer])
charm_recruit('地点拒绝后改选农民，仍可用同一次折扣',
    {'InfluencePool':3,'CharmRecruitDiscount':0,'recruit:offer':monk,'recruit:owned':farmer+','+farmer,
     **recruit_result(0,4,False,4,3,[monk,farmer],1),**recruit_result(1,1,True,3,0,[monk],2)},
    [recruit(),recruit(1)],offer=[monk,farmer])
charm_recruit('修道院合法招募原版寒冰僧侣',
    {'InfluencePool':0,'CharmRecruitDiscount':0,'recruit:offer':'','recruit:owned':farmer+','+monk,'recruit:art':monk,
     **recruit_result(0,4,True,0,0,[],2)},[recruit(location='Monastery')],offer=[monk])
charm_recruit('没有指挥槽时保留折扣、供给与影响力',
    {'InfluencePool':4,'CharmRecruitDiscount':3,'recruit:offer':farmer,'Units:count':1,
     **recruit_result(0,1,False,4,3,[farmer],1)},[recruit()],setup={'player:ExtraCommandSlots':0})
charm_recruit('费用不足时保留一次折扣',
    {'InfluencePool':4,'CharmRecruitDiscount':3,'recruit:offer':monk,'Units:count':1,'Reputation':-1,
     **recruit_result(0,5,False,4,3,[monk],1)},[recruit(location='Monastery')],offer=[monk],setup={'player:Reputation':-1})
charm_recruit('无效供给索引不消费折扣或影响力',
    {'InfluencePool':4,'CharmRecruitDiscount':3,'recruit:offer':farmer,
     **recruit_result(0,-1,False,4,3,[farmer],1)},[recruit(2)])
charm_recruit('魔晶奖励与减费互斥，农民仍须原价',
    {'InfluencePool':0,'crystal:Red':2,'CharmRecruitDiscount':0,'recruit:offer':'',
     **recruit_result(0,4,True,0,0,[],2)},[recruit()],option=0)
charm_recruit('保留其他折扣，魅惑只消费自己的三点且费用最低零',
    {'InfluencePool':2,'RecruitDiscount':2,'CharmRecruitDiscount':0,'recruit:offer':'',
     **recruit_result(0,0,True,4,0,[farmer],2),**recruit_result(1,2,True,2,0,[],3)},
    [recruit(),recruit()],offer=[farmer,farmer],setup={'RecruitDiscount':2})
charm_recruit('与无情威压折扣叠加，招募成功只扣一次声望',
    {'InfluencePool':4,'RecruitDiscount':0,'IntimidateUsed':False,'Reputation':-1,'CharmRecruitDiscount':0,
     **recruit_result(0,0,True,4,0,[],2)},[recruit()],setup={'RecruitDiscount':2,'IntimidateUsed':True})
charm_recruit('未用的一次招募折扣在回合结束过期',
    # Turn resources expire; independently check that playing first granted influence 4.
    {'played:InfluencePool':4,'InfluencePool':0,'CharmRecruitDiscount':0,'recruit:offer':farmer,'Units:count':1,'operation:0:success':True,
     **{'token:'+c:0 for c in COLORS},**{'crystal:'+c:1 for c in COLORS[:4]}},[dict(kind='EndTurn')])


# Backend-first Ritual Attack acceptance: color/type/payment permutations are
# checked by RitualAttackStateTests before running these UI/target paths.
for strong in [False, True]:
    for material, element, kind, amount in [
        ('basic_card_021', 'Fire' if strong else 'Physical', 'Melee', 6 if strong else 5),
        ('basic_card_016', 'ColdFire' if strong else 'Ice', 'Melee', 4 if strong else 3),
        ('basic_card_008', 'Fire' if strong else 'Physical', 'Ranged', 4 if strong else 3),
        ('basic_card_000', 'Fire' if strong else 'Physical', 'Siege', 3 if strong else 2)]:
        combat('advanced_card_029', strong, '去除原卡'+material+'后实际'+kind+'击杀',
               'Melee' if kind == 'Melee' else 'Ranged', [enemy(amount)], amount, amount, amount, 1, option=999,
               extra={'played:'+kind+'Pool':amount, 'played:attackElement':element, 'hand':6, 'discard':2})
        CASES[-1].update(extraHandCards=[material],removeIndex=6)
for material, title, traits, armor, effective, kills in [
    ('basic_card_021','火抗使火焰攻击六变三',['FireResist'],4,3,0),
    ('basic_card_016','单一火抗不减冰火四',['FireResist'],4,4,1),
    ('basic_card_000','攻城火焰三击破单重城防',['Fortified'],2,3,1)]:
    combat('advanced_card_029',True,title,'Ranged' if material=='basic_card_000' else 'Melee',
           [enemy(armor,abilities=traits)],6 if material=='basic_card_021' else 4 if material=='basic_card_016' else 3,
           effective,armor,kills,extra={'hand':6,'discard':2})
    CASES[-1].update(extraHandCards=[material],removeIndex=6)
combat('advanced_card_029',True,'双重城防拒绝攻城，已出牌资源保留','Ranged',[enemy(2,abilities=['Fortified'])],0,0,0,
       setup={'FortifiedSite':True},extra={'SiegePool':3,'Fame':0,'hand':6,'discard':2,'played:SiegePool':3},
       error='InvalidOperationException: 城防限制：当前攻击不能指定这些目标')
CASES[-1].update(extraHandCards=['basic_card_000'],removeIndex=6)
case('advanced_card_029',True,'伪造同ID手牌不能付费或去除',{'hand':6,'discard':2,'token:Red':2},
     error='InvalidOperationException: 仪式攻击必须选择实际持有的行动卡实例')
CASES[-1]['foreignRemove']=True
case('advanced_card_029',True,'创伤不是行动卡，拒绝付费和去除',{'hand':6,'discard':2,'token:Red':2},
     error='InvalidOperationException: 仪式攻击只能去除有颜色定义的行动卡')
CASES[-1]['removeIndex']=3


# Magic Talent: CLI verifies payment/color permutations first; scenes exercise
# the offer selection, normal casting costs, single-use permission and expiry.
for color, spell in [(1, 'magic_015'), (2, 'magic_005'), (3, 'magic_021')]:
    case('advanced_card_025', True, '额外支付'+COLORS[color]+'魔力获取原供给法术，无需弃手牌',
         {'hand':6, 'discard':3, 'spellOffer':2, 'SpellColors:count':0,
          'token:'+COLORS[color]:0 if color==1 else 1,
          'discard:ids':spell+',basic_card_016,basic_card_008'}, option=color,
         setup={'CardToDiscard':'<none>'}, limits=['已测原供给获取及整笔费用；完整出牌区生命周期仍待联验'])
    CASES[-1]['spellOffer']=[spell, 'magic_009', 'magic_003']

def cast_offer(index=0, strong=False): return dict(kind='CastOffer', targetIndex=index, enhanced=strong)
def talent_cast(title, ops, expected=None, setup=None):
    operation_case('advanced_card_025', False, title,
        {'hand':5, 'discard':3, 'spellOffer':3, 'SpellColors:count':0, 'RangedPool':5,
         'token:Red':1, 'spell:offer':'magic_009,magic_015,magic_005', **(expected or {})}, ops, setup=setup)

talent_cast('弃狂怒后正常付红色魔力施放供给火球，供给原牌保留', [cast_offer()],
            {'operation:0:success':True, 'operation:0:permissions':0, 'spell:art':'magic_009'})
talent_cast('一次许可不能重复施放供给火球', [cast_offer(),cast_offer()],
            {'operation:0:success':True, 'operation:1:success':False, 'operation:1:permissions':0})
talent_cast('错误颜色拒绝后仍可施放同色火球', [cast_offer(1),cast_offer()],
            {'operation:0:success':False, 'operation:0:permissions':1, 'operation:1:success':True, 'operation:1:permissions':0})
talent_cast('供给索引错误后仍保留许可和费用', [cast_offer(9),cast_offer()],
            {'operation:0:success':False, 'operation:0:permissions':1, 'operation:1:success':True})
talent_cast('正常法术费用不足时不消费许可', [cast_offer()],
            {'operation:0:success':False, 'operation:0:permissions':1, 'SpellColors:count':1,
             'RangedPool':0, 'token:Red':0, 'crystal:Red':0}, setup={'token:Red':0,'crystal:Red':0})
talent_cast('白昼不能借供给许可施放强化火球', [cast_offer(strong=True)],
            {'operation:0:success':False, 'operation:0:permissions':1, 'SpellColors:count':1,
             'RangedPool':0, 'token:Red':2, 'token:Black':2}, setup={'DayPart':'Day'})
talent_cast('夜晚支付红黑魔力施放强化火球并获得真实伤牌', [cast_offer(strong=True)],
            {'operation:0:success':True, 'operation:0:permissions':0, 'RangedPool':0, 'SiegePool':8,
             'token:Black':1, 'hand':6, 'Wounds':4, 'handWounds':4}, setup={'DayPart':'Night'})
talent_cast('未使用供给许可在回合结束失效', [dict(kind='EndTurn')],
            {'operation:0:success':True, 'RangedPool':0, **{'token:'+c:0 for c in COLORS}})
case('advanced_card_025',True,'额外红色魔力不足不提前支付蓝色费用',{'spellOffer':3,'SpellColors:count':0},
     setup={'token:Red':0,'crystal:Red':0}, error='InvalidOperationException: 法力不足，不能支付所选效果')
case('advanced_card_025',True,'法术颜色不匹配时不消费蓝色费用',{'spellOffer':3,'SpellColors:count':0},option=1,
     error='InvalidOperationException: 魔法天赋额外魔力必须与所获法术同色')


def portal(q): return dict(kind='Teleport', q=q)
def finish_draw(refill=True): return dict(kind='EndTurn', refill=refill)
def portal_scene(title, strong, option, ops, expected, **fields):
    operation_case('advanced_card_032', strong, title, expected, ops, option=option, initialHandCount=2, **fields)
    CASES[-1]['limitations']=['本例已验证落点与真实补牌；完整出牌区、回合行动互斥及地点全类型仍待联验']

for strong, option, distance, bonus in [(False,0,1,1),(True,0,2,1),(True,1,1,2)]:
    portal_scene('移动'+str(distance)+'格后真实补牌上限加'+str(bonus),strong,option,[portal(distance),finish_draw()],
        {'operation:0:success':True,'operation:0:q':distance,'operation:0:range':0,
         'operation:0:blue':1 if strong else 2,'operation:1:hand':5+bonus,
         'hand':5+bonus,'draw':7-bonus,'NextDrawBonus':0,'TeleportRange':0,
         **{'token:'+c:0 for c in COLORS}})
portal_scene('不传送也获得强化补牌奖励',True,1,[finish_draw()],
    {'position:q':0,'hand':7,'draw':5,'NextDrawBonus':0,'TeleportRange':0,'operation:0:success':True,**{'token:'+c:0 for c in COLORS}})
portal_scene('跳过补牌后许可失效，奖励保留至下次实际补牌',True,1,[finish_draw(False),portal(1),finish_draw()],
    {'operation:0:hand':2,'operation:0:bonus':2,'operation:1:success':False,'operation:2:hand':7,
     'hand':7,'draw':5,'NextDrawBonus':0,'TeleportRange':0,**{'token:'+c:0 for c in COLORS}})
portal_scene('过远落点拒绝后可选择相邻格，一次传送不能重复',False,0,[portal(2),portal(1),portal(0)],
    {'operation:0:success':False,'operation:0:range':1,'operation:1:success':True,'operation:2:success':False,
     'position:q':1,'hand':2,'draw':10,'NextDrawBonus':1,'TeleportRange':0})
for terrain in ['Lake','Mountain']:
    portal_scene('拒绝未解锁的'+terrain+'安全落点',False,0,[portal(1)],
        {'operation:0:success':False,'position:q':0,'TeleportRange':1,'NextDrawBonus':1,'hand':2},destinationTerrain=terrain)
for kind, accepted in [('Keep',False),('Dungeon',True)]:
    portal_scene(kind+'驻军落点按强制战斗规则判断',False,0,[portal(1)],
        {'operation:0:success':accepted,'position:q':1 if accepted else 0,'TeleportRange':0 if accepted else 1,'hand':2,'NextDrawBonus':1},
        mapPlaces=[dict(q=1,type=kind,enemy=True)])
portal_scene('跨过肆虐敌人所在格不触发战斗',True,0,[portal(2)],
    {'operation:0:success':True,'position:q':2,'TeleportRange':0,'NextDrawBonus':1,'hand':2,'Wounds':0,'Fame':0},
    mapPlaces=[dict(q=1,type='OrcRampaging',enemy=True)])
portal_scene('夜晚和负声望不减少英雄手牌上限',False,0,[finish_draw()],
    {'hand':6,'draw':6,'NextDrawBonus':0,'TeleportRange':0,**{'token:'+c:0 for c in COLORS}},
    setup={'DayPart':'Night','player:Reputation':-7})
portal_scene('牌库只剩两张时不补造卡牌、不重洗弃牌',True,1,[finish_draw()],
    {'hand':4,'draw':0,'discard':2,'NextDrawBonus':0,'TeleportRange':0,**{'token:'+c:0 for c in COLORS}},initialDrawCount=2)
case('advanced_card_032',True,'非法双重强化选项付费前拒绝',{'TeleportRange':0,'NextDrawBonus':0},option=2,
     error='InvalidOperationException: 时光传送必须选择距离或手牌上限，不能同时强化')


# Full hand/played/discard pipeline, added after the scoped state/scene pass.
for strong, option, distance, bonus in [(False,0,1,1),(True,0,2,1),(True,1,1,2)]:
    portal_scene('真实出牌、传送、回合弃牌及补牌全流程',strong,option,[portal(distance),finish_draw()],
        {'operation:0:success':True,'operation:0:hand':1,'operation:0:played':1,
         'operation:1:hand':5+bonus,'operation:1:played':0,'operation:1:discard':3,
         'hand':5+bonus,'draw':6-bonus,'cards:total':14,'cards:played':0,'discard':3,
         'discard:ids':'advanced_card_032,basic_card_016,basic_card_008','NextDrawBonus':0,
         **{'token:'+c:0 for c in COLORS}},playFromHand=True)
    CASES[-1]['limitations']=['本例已验证真实出牌及完整结算；整卡还须汇总行动互斥、地点及组合边界']
portal_scene('传送后不能使用剩余移动力或发动攻击',False,0,[portal(1),move(2),combat_operation('Attack',0)],
    {'operation:0:success':True,'operation:1:success':False,'operation:2:success':False,
     'position:q':1,'MovementPool':9,'MeleePool':5,'hand':1,'cards:played':1,'cards:total':14},
    setup={'MovementPool':9,'MeleePool':5},playFromHand=True)
portal_scene('另一张同名卡不能提供本回合第二次行动',False,0,[portal(1),dict(kind='PlayHand')],
    {'operation:0:success':True,'operation:1:success':False,'operation:1:exception':'时光传送必须作为本回合唯一行动',
     'hand':6,'cards:played':1,'cards:total':19},playFromHand=True,extraHandCards=['advanced_card_032'])
CASES[-1]['initialHandCount']=-1
portal_scene('传送行动之后无法招募部队且影响力与供给保留',False,0,[portal(1),recruit()],
    {'operation:0:success':True,'operation:1:success':False,'InfluencePool':10,'hand':1,'cards:played':1,
     'recruit:offer':farmer,'Units:count':1,'operation:1:influence':10},
    setup={'InfluencePool':10,'player:ExtraCommandSlots':2},playFromHand=True,recruitOffer=[farmer],unitIds=[farmer])


for distance in [2,5]:
    case('magic_023',False,'沿已揭示路径实际飞行'+str(distance)+'格',
         {'MovementPool':6-distance,'position:q':distance,'TeleportRange':0,'cards:played':1,'hand':5},option=distance,
         setup={'MovementPool':6},limits=['本例路径费用及真实出牌已验；落点全类型与其他组合仍待汇总'])
    CASES[-1].update(playFromHand=True,revealedDistance=5,travelPath=[dict(q=q) for q in range(1,distance+1)])
case('magic_023',False,'路径中未揭示的格不能飞越，白色费用与移动力保留',{'MovementPool':3,'position:q':0},option=3,
     setup={'MovementPool':3},error='InvalidOperationException: 清风之翼路径必须逐格相邻且已揭示')
CASES[-1]['travelPath']=[dict(q=1),dict(q=2),dict(q=3)]
case('magic_023',False,'不能落在未征服要塞',{'MovementPool':3,'position:q':0},option=1,
     setup={'MovementPool':3},error='InvalidOperationException: 清风之翼必须落在安全格')
CASES[-1].update(travelPath=[dict(q=1)],mapPlaces=[dict(q=1,type='Keep',enemy=True)])
case('magic_023',False,'飞过肆虐敌人不触发攻击',{'MovementPool':1,'position:q':2,'Wounds':3,'Fame':0},option=2,
     setup={'MovementPool':3},limits=['本例真实路径与不激怒已验；全回合组合仍待联验'])
CASES[-1].update(travelPath=[dict(q=1),dict(q=2)],mapPlaces=[dict(q=1,type='OrcRampaging',enemy=True)])
for count, cost in [(1,0),(2,1),(3,3)]:
    operation_case('magic_023',True,'选择'+str(count)+'名敌人，扣'+str(cost)+'移动力并实际结算不攻击',
        {'MovementPool':4-cost,'Wounds':3,'handWounds':3,'SkipAttackIndices:count':count,
         **{f'operation:{i}:wounds':0 for i in range(count)}},
        [combat_operation('Block',i) for i in range(count)],setup={'InBattle':True,'MovementPool':4},option=count,
        spellTargets=list(reversed(range(count))),enemies=[enemy(4,4) for _ in range(count)])
operation_case('magic_023',True,'战斗结束后不攻击效果失效，新战斗正常承伤',
    {'operation:0:wounds':0,'operation:2:wounds':2,'Wounds':5,'SkipAttackIndices:count':0},
    [combat_operation('Block',0),dict(kind='EndBattle'),combat_operation('Block',0)],setup={'InBattle':True},option=1,enemies=[enemy(4,4)])
case('magic_023',True,'第三个敌人需要总计三点移动力，不足时整笔拒绝',{'MovementPool':2,'SkipAttackIndices:count':0},option=3,
     setup={'InBattle':True,'MovementPool':2},error='InvalidOperationException: 黑夜之翼额外目标需要递增移动力费用')
CASES[-1]['enemies']=[enemy(4,4) for _ in range(3)]
case('magic_023',True,'奥术免疫目标在魔力扣除前拒绝',{'MovementPool':0,'SkipAttackIndices:count':0},option=1,
     setup={'InBattle':True},error='InvalidOperationException: 奥术免疫敌人不能成为黑夜之翼目标')
CASES[-1]['enemies']=[enemy(4,4,abilities=['MagicResist'])]


# Original weapon bases: real hand identities, discard conservation and kill rewards.
for card_id, phase, power, reward in [('items_008','Melee',3,'SwordFamePerKill'),('items_016','Ranged',2,'BowFamePerKill')]:
    for count in [0,2]:
        operation_case(card_id,False,'实际弃'+str(count)+'张牌，武器回合末进入弃牌堆',
            {'hand':5-count,'discard':3+count,'cards:played':0,'cards:total':18,
             'MeleePool':count*power if phase=='Melee' else 0,'RangedPool':count*power if phase=='Ranged' else 0,
             'SwordFamePerKill':0,'BowFamePerKill':0},[dict(kind='EndTurn')],
            setup={'InBattle':True,'CombatPhase':phase},discardIndices=list(range(1,count+1)),playFromHand=True)
    combat(card_id,False,'两张弃牌形成一笔攻击，击败两敌各加一名望',phase,
        [enemy(2),enemy(2)],2*power,2*power,4,2,
        setup={'InBattle':True,'CombatPhase':phase},extra={'hand':3,'discard':4,'cards:played':1,
        'cards:total':18,'combat:fame':8,'Fame':8,reward:1})
    CASES[-1].update(playFromHand=True,discardIndices=[1,2])
    case(card_id,False,'同ID不同实例只弃所选一张',
        {'hand':5,'discard':3,'cards:played':1,'cards:total':19,reward:1,
         'MeleePool':power if phase=='Melee' else 0,'RangedPool':power if phase=='Ranged' else 0,
         'hand:ids':'basic_card_021,basic_card_014,wound_0,wound_1,wound_2'},
        setup={'InBattle':True,'CombatPhase':phase},limits=['基础真实卡区已验；焚毁模式和整卡其他组合待补'])
    CASES[-1].update(playFromHand=True,extraHandCards=['basic_card_014'],discardIndices=[6])
    for title,targets,foreign,message in [
        ('重复选择同一实例整笔拒绝',[1,1],False,'武器不能重复选择同一张手牌'),
        ('外来同ID实例整笔拒绝',[1],True,'武器必须弃掉实际持有的手牌实例'),
        ('不能弃创伤，先前合法目标也保留',[1,3],False,'武器不能弃掉创伤或非契约卡牌'),
        ('不能弃武器自身',[1,0],False,'正在使用的武器不能弃掉自身')]:
        case(card_id,False,title,{'cards:played':0,'cards:total':18,'SwordFamePerKill':0,'BowFamePerKill':0},
            setup={'InBattle':True,'CombatPhase':phase},error='InvalidOperationException: '+message)
        CASES[-1].update(playFromHand=True,discardIndices=targets,foreignDiscard=foreign)
    operation_case(card_id,False,'结束战斗区分本回合与本阶段奖励',
        {'hand':5,'discard':2,'cards:played':1,'cards:total':18,
         'SwordFamePerKill':int(phase=='Melee'),'BowFamePerKill':0},[dict(kind='EndBattle')],
        setup={'InBattle':True,'CombatPhase':phase},discardIndices=[],playFromHand=True)
operation_case('items_016',False,'远程阶段使用弓后进入格挡与攻击阶段，后续狂怒击杀不再额外加名望',
    {'Fame':3,'BowFamePerKill':0,'hand':5,'discard':2,'cards:played':1,
     'operation:1:kills':1,'operation:1:fame':3},[dict(kind='Block'),dict(kind='Attack')],
    setup={'InBattle':True,'CombatPhase':'Ranged'},discardIndices=[],playFromHand=True,
    followup='basic_card_021',enemies=[enemy(2)])


# Weapon burn modes: real removal, attack phase/type/element and per-contribution choice.
for phase,followup,strong,traits,printed,effective,armor,kills in [
    ('Melee','basic_card_021',False,['PhysicalResist'],2,4,4,1),
    ('Ranged','basic_card_011',True,['PhysicalResist'],3,3,4,0),
    ('Melee','magic_009',False,['FireResist'],5,2,3,0)]:
    combat('items_008',True,'焚毁：'+phase+'阶段原卡'+followup+'对抗'+','.join(traits),phase,
        [enemy(armor,abilities=traits)],printed,effective,armor,kills,
        followup=followup,followup_strong=strong,setup={'InBattle':True,'CombatPhase':phase},
        extra={'hand':5,'discard':2,'cards:played':0,'cards:removed':1,'cards:total':17,
               'removed:ids':'items_008','combat:fame':4*kills,'Fame':4*kills,'IgnoreResist':False,'IgnorePhysicalResist':True})
    CASES[-1]['playFromHand']=True
for card_id in ['items_008','items_016']:
    operation_case(card_id,True,'焚毁武器不进弃牌，结束回合持续效果失效',
        {'hand':5,'discard':2,'cards:played':0,'cards:removed':1,'cards:total':17,
         'removed:ids':card_id,'DoublePhysicalAttack':False,'IgnorePhysicalResist':False,'StarBowActive':False},
        [dict(kind='EndBattle'),dict(kind='EndTurn')],playFromHand=True)
combat('items_016',True,'焚毁后火球远程翻倍，再按火抗合计减半','Ranged',
    [enemy(5,abilities=['FireResist'])],5,5,5,1,followup='magic_009',
    extra={'hand':5,'discard':2,'cards:removed':1,'cards:total':17,'StarBowActive':True})
CASES[-1]['playFromHand']=True
for spell,strong,choice,armor,effective,fortified,wounds in [
    ('magic_009',False,2,5,5,True,3),
    ('magic_009',True,1,16,16,False,4),
    ('magic_009',True,0,8,8,True,4)]:
    operation_case('items_016',True,'逐笔选择：'+('攻城翻倍变远程' if choice==1 else '远程转攻城' if choice==2 else '攻城保持原值'),
        {'hand':5+int(strong),'discard':2,'cards:removed':1,'StarBowActive':True,
         'operation:0:success':True,'operation:1:effective':effective,'operation:1:kills':1,'operation:1:fame':3,
         'Fame':3,'Wounds':wounds,'handWounds':wounds,'SiegePool':0},
        [dict(kind='ChooseStarBow',option=choice),dict(kind='RangedAttack')],
        setup={'DayPart':'Night','InBattle':True,'CombatPhase':'Ranged'},
        followup=spell,followupEnhanced=strong,playFromHand=True,enemies=[enemy(armor,abilities=['Fortified'] if fortified else [])])
operation_case('items_016',True,'攻城翻倍变远程后不得打城防，拒绝保留原攻击',
    {'hand':6,'discard':2,'cards:removed':1,'StarBowActive':True,'SiegePool':8,'Fame':0,'Wounds':4,'handWounds':4,
     'operation:0:success':True,'operation:1:success':False,
     'operation:1:exception':'城防限制：当前攻击不能指定这些目标'},
    [dict(kind='ChooseStarBow',option=1),dict(kind='RangedAttack')],
    setup={'DayPart':'Night','InBattle':True,'CombatPhase':'Ranged'},
    followup='magic_009',followupEnhanced=True,playFromHand=True,enemies=[enemy(8,abilities=['Fortified'])])
operation_case('items_016',True,'两张真实手牌各产生一笔攻击，分别选择攻城与翻倍',
    {'hand':5,'discard':2,'cards:played':2,'cards:removed':1,'cards:total':19,
     'operation:0:success':True,'operation:1:success':True,'operation:2:success':True,
     'operation:3:effective':15,'operation:3:kills':1,'operation:3:fame':3,'Fame':3,
     'token:Red':1,'token:Blue':1,'StarBowActive':True},
    [dict(kind='PlayCard',cardId='magic_009'),dict(kind='PlayCard',cardId='magic_015'),
     dict(kind='ChooseStarBow',option=2),dict(kind='RangedAttack')],
    setup={'InBattle':True,'CombatPhase':'Ranged'},playFromHand=True,discardIndices=[],
    extraHandCards=['magic_009','magic_015'],enemies=[enemy(15)])


# Tome uses matching original colored material; only the printed spell mana is free.
empty_mana={**{'token:'+c:0 for c in COLORS},**{'crystal:'+c:0 for c in COLORS}}
for spell,index,extras,reward in [
    ('magic_009',1,[],{'RangedPool':5,'attackElement':'Fire','hand':4}),
    ('magic_015',2,[],{'RangedPool':5,'attackElement':'Ice','hand':4}),
    ('magic_001',6,['basic_card_000'],{'Wounds':1,'handWounds':1,'hand':5,'draw':8}),
    ('magic_021',6,['basic_card_008'],{'InfluencePool':4,'hand':5})]:
    case('items_023',False,'弃同色原卡，免费使用供给'+spell,
        {'discard':3,'cards:played':1,'cards:removed':0,'spell:offer':spell,'spell:art':spell,**empty_mana,**reward},
        setup=empty_mana,limits=['实际同色费用与供给保留已验；其他目标法术及完整组合仍须按覆盖表补齐'])
    CASES[-1].update(playFromHand=True,discardIndex=index,extraHandCards=extras,spellOffer=[spell])
    if spell=='magic_001': CASES[-1]['operations']=[dict(kind='HealHand',option=2)]
for spell,index in [('magic_009',1),('magic_015',2)]:
    for part in ['Day','Night']:
        combat('items_023',True,part+'焚毁宝典，零魔力施放强效并实际击败城防敌人','Ranged',
            [enemy(8,abilities=['Fortified'])],8,8,8,1,
            setup={**empty_mana,'DayPart':part,'InBattle':True},extra={**empty_mana,
            'hand':5,'discard':3,'cards:played':0,'cards:removed':1,'removed:ids':'items_023',
            'Wounds':4,'handWounds':4,'spell:offer':spell,'spell:art':spell})
        CASES[-1].update(playFromHand=True,discardIndex=index,spellOffer=[spell])
for title,index,offer,option,foreign,message in [
    ('外来同ID弃牌实例不能支付',1,'magic_009',0,True,'法术宝典必须弃掉实际持有的有色手牌实例'),
    ('创伤没有可用原色',3,'magic_009',0,False,'法术宝典弃牌必须具有原版颜色'),
    ('宝典自身不能作为有色费用',0,'magic_009',0,False,'法术宝典弃牌必须具有原版颜色'),
    ('红色手牌不能施放蓝色供给',1,'magic_015',0,False,'法术宝典供给法术必须与弃牌同色'),
    ('负索引不能自动改成首张',1,'magic_009',-1,False,'法术宝典必须选择当前供给中的法术'),
    ('越界不能自动改成末张',1,'magic_009',1,False,'法术宝典必须选择当前供给中的法术')]:
    case('items_023',True,title,{'cards:played':0,'cards:removed':0,'spell:offer':offer},
        option=option,setup=empty_mana,error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,discardIndex=index,spellOffer=[offer],foreignDiscard=foreign)
for spell_option,message in [(9,'魔力箭矢必须选择红蓝绿白之一'),(0,'法力不足，不能支付所选效果')]:
    case('items_023',True,'法术选项及额外费用在弃牌和焚毁前检查',
        {'cards:played':0,'cards:removed':0,'spell:offer':'magic_017'},
        setup={**empty_mana,'TomeSpellOption':spell_option},error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,discardIndex=2,spellOffer=['magic_017'])
combat('items_023',True,'印刷蓝黑费用免除，魔力雷矢额外红色仍付一颗并实际攻击','Melee',
    [enemy(10)],10,10,10,1,setup={**empty_mana,'token:Red':1},extra={**empty_mana,
        'hand':4,'discard':3,'cards:removed':1,'cards:played':0,'spell:offer':'magic_017','spell:art':'magic_017'})
CASES[-1].update(playFromHand=True,discardIndex=2,spellOffer=['magic_017'])
operation_case('items_023',False,'实际出牌后回合末宝典进入弃牌，供给法术仍在原位',
    {**empty_mana,'hand':4,'discard':4,'cards:played':0,'cards:removed':0,'RangedPool':5,
     'spell:offer':'magic_009','spell:art':'magic_009'},[dict(kind='EndTurn')],
    setup=empty_mana,playFromHand=True,spellOffer=['magic_009'])


# Original four rings: actual artifact -> paid original spells -> turn expiry.
for ring, color, spell in [('items_004','Red','magic_009'), ('items_005','Blue','magic_015'),
                            ('items_006','White','magic_018'), ('items_007','Green','magic_001')]:
    operation_case(ring,False,'原戒指基础效果，结束回合只保留魔晶与名望',
        {**empty_mana,'crystal:'+color:1,'Fame':1,'hand':5,'discard':3,'cards:played':0,'cards:removed':0,
         'operation:0:success':True,'operation:0:fame':1,'InfiniteMana:count':0},
        [dict(kind='EndTurn')],setup=empty_mana,playFromHand=True,initialDrawCount=0)
    CASES[-1]['limitations']=[]
    case(ring,False,'同色魔晶已满三颗，多出的魔晶成为本回合魔力',
        {**empty_mana,'crystal:'+color:3,'token:'+color:2,'Fame':1,'hand':5,'cards:played':1,'cards:removed':0},
        setup={**empty_mana,'crystal:'+color:3})
    CASES[-1].update(playFromHand=True,initialDrawCount=0)
    extra = {'RangedPool':5,'SiegePool':8,'Wounds':4,'hand':6} if color in ('Red','Blue') else \
        {'crystal:Red':2,'hand':5} if color=='White' else {'Wounds':1,'hand':3}
    operation_case(ring,True,'夜间连续施放同色原法术，基础与强化各奖励一次名望',
        {**empty_mana,**extra,'Fame':2,'cards:played':2,'cards:removed':1,'removed:ids':ring,'discard':2,
         'InfiniteMana:count':2,'operation:0:success':True,'operation:0:fame':1,'operation:1:success':True,'operation:1:fame':2},
        [dict(kind='PlayCard',cardId=spell),dict(kind='PlayCard',cardId=spell,enhanced=True)],
        setup={**empty_mana,'DayPart':'Night'},playFromHand=True,extraHandCards=[spell,spell],initialDrawCount=0)
    operation_case(ring,True,'白昼即使无尽黑魔力，也不能施放强效法术',
        {**empty_mana,'Fame':0,'hand':6,'cards:played':0,'cards:removed':1,'operation:0:success':False,
         'operation:0:exception':'白昼不能施放强效法术','operation:0:fame':0},
        [dict(kind='PlayCard',cardId=spell,enhanced=True)],setup={**empty_mana,'DayPart':'Day'},
        playFromHand=True,extraHandCards=[spell],initialDrawCount=0)
    CASES[-1]['limitations']=[]
    operation_case(ring,True,'回合结束后无尽魔力和施法奖励清除，零费用再施法应拒绝',
        {**empty_mana,'Fame':0,'hand':6,'cards:played':0,'cards:removed':1,'InfiniteMana:count':0,
         'operation:0:success':True,'operation:0:infinite':0,'operation:0:rewards':0,
         'operation:1:success':False,'operation:1:exception':'法力不足，不能支付所选效果','operation:1:fame':0},
        [dict(kind='EndTurn'),dict(kind='PlayCard',cardId=spell)],setup=empty_mana,
        playFromHand=True,extraHandCards=[spell],initialDrawCount=0)
    CASES[-1]['limitations']=[]
operation_case('items_004',True,'无尽红魔力可强化狂怒，但行动牌不奖励法术名望',
    {**empty_mana,'Fame':0,'MeleePool':4,'hand':4,'cards:played':1,'cards:removed':1,
     'operation:0:success':True,'operation:0:fame':0},[dict(kind='PlayCard',cardId='basic_card_021',enhanced=True)],
    setup=empty_mana,playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],initialDrawCount=0)
for blue in [0,1]:
    operation_case('items_004',True,'异色法术必须支付蓝色，黑色可由戒指提供：蓝魔力'+str(blue),
        {**empty_mana,'Fame':0,'SiegePool':8 if blue else 0,'Wounds':4 if blue else 3,
         'hand':6,'cards:played':blue,'cards:removed':1,'operation:0:success':bool(blue),'operation:0:fame':0,
         'operation:0:exception':'' if blue else '法力不足，不能支付所选效果'},
        [dict(kind='PlayCard',cardId='magic_015',enhanced=True)],setup={**empty_mana,'DayPart':'Night','token:Blue':blue},
        playFromHand=True,extraHandCards=['magic_015'],initialDrawCount=0)
operation_case('items_004',False,'实际戒指离开手牌后不得再次打出获得奖励',
    {**empty_mana,'token:Red':1,'crystal:Red':1,'Fame':1,'hand':5,'cards:played':1,'cards:removed':0,
     'operation:0:success':False,'operation:0:fame':1,'operation:0:exception':'武器必须选择实际持有且与原定义一致的神器实例'},
    [dict(kind='PlayCard',cardId='items_004')],setup=empty_mana,playFromHand=True,initialDrawCount=0)
CASES[-1]['limitations']=[]


# Endless Gem Pouch uses the real six-sided die, regardless of day/night.
for part in ['Day', 'Night']:
    for face, color in enumerate(COLORS):
        expected = {**empty_mana, 'Fame':2 if color=='Black' else 0,
                    'hand':5, 'cards:played':1, 'cards:removed':0, 'dice:faces':color+','+color, 'dice:count':2}
        if color!='Black': expected['crystal:'+('White' if color=='Gold' else color)]=2
        case('items_011',False,part+'两次掷出'+color+'，完整六面骰结果',expected,
             setup={**empty_mana,'DayPart':part},option=3|(3<<8))
        CASES[-1].update(playFromHand=True,initialDrawCount=0,manaRolls=[face,face])
case('items_011',False,'黑夜两次金色分别选择红绿，红晶已满转为临时魔力',
     {**empty_mana,'crystal:Red':3,'crystal:Green':1,'token:Red':1,'Fame':0,'hand':5,
      'cards:played':1,'cards:removed':0,'dice:count':2,'dice:faces':'Gold,Gold'},
     setup={**empty_mana,'DayPart':'Night','crystal:Red':3},option=2<<8)
CASES[-1].update(playFromHand=True,initialDrawCount=0,manaRolls=[4,4])
operation_case('items_011',False,'蓝色加黑色结果，结束回合保留魔晶名望并弃掉真实宝石袋',
    {**empty_mana,'crystal:Blue':1,'Fame':1,'hand':5,'discard':3,'cards:played':0,'cards:removed':0,
     'dice:count':2,'dice:faces':'Blue,Black','operation:0:success':True},
    [dict(kind='EndTurn')],setup=empty_mana,playFromHand=True,initialDrawCount=0,manaRolls=[1,5])
CASES[-1]['limitations']=[]
for part, extra in [('Day','Gold'),('Night','Black')]:
    tokens={**empty_mana,**{'token:'+c:1 for c in COLORS[:4]+[extra]}}
    case('items_011',True,part+'焚毁：四色加'+extra+'，不掷骰',
         {**tokens,'Fame':0,'hand':5,'cards:played':0,'cards:removed':1,'removed:ids':'items_011','dice:count':0},
         setup={**empty_mana,'DayPart':part})
    CASES[-1].update(playFromHand=True,initialDrawCount=0,manaRolls=[])
    operation_case('items_011',True,part+'焚毁后结束回合：临时魔力归零，神器永久移除',
         {**empty_mana,'Fame':0,'hand':5,'discard':2,'cards:played':0,'cards:removed':1,
          'removed:ids':'items_011','operation:0:success':True},[dict(kind='EndTurn')],
         setup={**empty_mana,'DayPart':part},playFromHand=True,initialDrawCount=0,manaRolls=[])
    CASES[-1]['limitations']=[]
    follow='basic_card_021' if part=='Day' else 'magic_015'
    tokens['token:'+('Red' if part=='Day' else 'Blue')]=0
    if part=='Night': tokens['token:Black']=0
    operation_case('items_011',True,part+'所得魔力实际支付原卡强化费用：'+follow,
         {**tokens,'Fame':0,'MeleePool':4 if part=='Day' else 0,'SiegePool':8 if part=='Night' else 0,
          'Wounds':3 if part=='Day' else 4,'hand':4 if part=='Day' else 6,'cards:played':1,'cards:removed':1,
          'operation:0:success':True},[dict(kind='PlayCard',cardId=follow,enhanced=True)],
         setup={**empty_mana,'DayPart':part},playFromHand=True,initialDrawCount=0,manaRolls=[],
         extraHandCards=[] if part=='Day' else [follow],discardIndex=-1,removeIndex=-1,discardIndices=[])
for option in [-1,4,4<<8,1<<16]:
    case('items_011',False,'非法金色选色编码'+str(option)+'必须在掷骰和出牌前拒绝',
         {'dice:count':0,'cards:played':0,'cards:removed':0},option=option,setup=empty_mana,
         error='InvalidOperationException: 无尽宝石袋必须为每次金色骰选择一种基本色')
    CASES[-1].update(playFromHand=True,initialDrawCount=0,manaRolls=[4,4])


def grail(strong,title,expected,ops=(),setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('items_012',strong,title,expected,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=3,operations=list(ops),**fields)

grail(False,'获得治疗2，等待选择目标，不自动治疗英雄或预付名望',
      {'HealingPool':2,'GrailHealing':2,'Wounds':3,'Fame':0,'hand':5,'cards:played':1,'cards:removed':0})
grail(False,'选择原版二级魔像，治疗一伤消耗2点并获得名望2，不重整',
      {'HealingPool':0,'GrailHealing':0,'Wounds':3,'Fame':2,'unitWounds':0,'unitReady':False,'hand':5},
      [dict(kind='HealUnit',option=1)],unitIds=['army_2_001'],unitWounds=1)
grail(False,'两点分别治疗一张手牌伤牌和原版一级农民',
      {'HealingPool':0,'Wounds':2,'Fame':2,'unitWounds':0,'unitReady':False,'hand':4,
       'operation:0:healing':1,'operation:0:fame':1,'operation:1:healing':0,'operation:1:fame':2},
      [dict(kind='HealHand',option=1),dict(kind='HealUnit',option=1)],unitIds=['army_2_006'],unitWounds=1)
grail(True,'先治疗二级魔像两张毒伤花费4点不抽牌，再治手牌一伤抽一张',
      {'HealingPool':1,'Wounds':2,'Fame':0,'unitWounds':0,'unitReady':False,'hand':5,'draw':2,'cards:removed':1,
       'operation:0:healing':2,'operation:0:handWounds':3,'operation:1:healing':1},
      [dict(kind='HealUnit',option=2),dict(kind='HealHand',option=1)],unitIds=['army_2_001'],unitWounds=2)
grail(True,'治疗三张实际手牌伤牌，逐张获得抽牌，保留剩余治疗3',
      {'HealingPool':3,'Wounds':0,'Fame':0,'hand':5,'draw':0,'cards:played':0,'cards:removed':1},
      [dict(kind='HealHand',option=1)]*3)
grail(True,'没有手牌伤牌时拒绝消费与抽牌',
      {'HealingPool':6,'Wounds':0,'Fame':0,'hand':2,'draw':3,'operation:0:success':False,
       'operation:0:exception':'目标没有足够伤牌'},[dict(kind='HealHand',option=1)],setup={'player:Wounds':0})
for title,op,fields,error in [
    ('两张二级部队伤牌费用不足',dict(kind='HealUnit',option=2),dict(unitWounds=2),'治疗点不足或消费数量无效'),
    ('已销毁部队不能治疗',dict(kind='HealUnit',option=1),dict(unitWounds=1,unitDestroyed=True),'只能治疗未销毁的自有部队'),
    ('目标不属于当前部队',dict(kind='HealUnit',option=1,targetIndex=99),dict(unitWounds=1),'只能治疗未销毁的自有部队'),
    ('零张伤牌不是有效操作',dict(kind='HealHand',option=0),dict(unitWounds=1),'必须选择正数伤牌')]:
    grail(False,title+'，拒绝后治疗点和名望不变',
          {'HealingPool':2,'GrailHealing':2,'Fame':0,'Wounds':3,'unitWounds':fields['unitWounds'],
           'operation:0:success':False,'operation:0:exception':error},[op],unitIds=['army_2_001'],**fields)
for strong in [False,True]:
    case('items_012',strong,'战斗中禁止圣杯治疗，手牌不得弃掉或焚毁',
         {'HealingPool':0,'DrawPerHeal':0,'cards:played':0,'cards:removed':0},setup={'InBattle':True},
         error='InvalidOperationException: 战斗中不能使用治疗效果')
    CASES[-1].update(playFromHand=True)
grail(True,'进入战斗时治疗点消失，战后本回合其他原卡治疗仍抽牌',
      {'HealingPool':0,'Wounds':2,'Fame':0,'hand':5,'draw':2,'DrawPerHeal':1,'cards:played':1,'cards:removed':1},
      [dict(kind='BeginBattle'),dict(kind='EndBattle'),dict(kind='PlayCard',cardId='basic_card_001'),dict(kind='HealHand',option=1)],
      extraHandCards=['basic_card_001'])
grail(True,'结束回合后其他原卡治疗不再触发旧圣杯抽牌',
      {'HealingPool':0,'Wounds':2,'Fame':0,'hand':4,'draw':3,'DrawPerHeal':0,'cards:played':1,'cards:removed':1},
      [dict(kind='EndTurn'),dict(kind='PlayCard',cardId='basic_card_001'),dict(kind='HealHand',option=1)],extraHandCards=['basic_card_001'])
grail(False,'未使用的圣杯治疗回合末归零，原神器进弃牌，名望不凭空增加',
      {'HealingPool':0,'GrailHealing':0,'Wounds':3,'Fame':0,'hand':5,'discard':3,'cards:played':0,'cards:removed':0},
      [dict(kind='EndTurn')])

grail(True,'治疗抽到的新伤牌可再次选择治疗，逐次扣点并抽牌',
      {'HealingPool':4,'Wounds':0,'Fame':0,'hand':3,'draw':0,'cards:removed':1,
       'operation:0:handWounds':1,'operation:0:healing':5,'operation:1:handWounds':0,'operation:1:healing':4},
      [dict(kind='HealHand',option=1),dict(kind='HealHand',option=1)],setup={'player:Wounds':1},initialDrawCards=['w','basic_card_000'])
CASES[-1]['initialDrawCount']=0

# Tranquility: actual hand, exclusive choice, costs, shared healing, and lifecycle.
def tranquility(strong,title,expected,ops=(),option=0,setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('basic_card_001',strong,title,expected,option=option,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=3,operations=list(ops),**fields)

for strong in [False,True]:
    tranquility(strong,'真实宁静先提供治疗点，等待选择目标',
        {'HealingPool':2 if strong else 1,'Wounds':3,'Fame':0,'hand':5,'cards:played':1})
    tranquility(strong,'原卡选择抽牌，剩余牌库只有1张不洗弃牌也不治疗',
        {'HealingPool':0,'Wounds':3,'Fame':0,'hand':6,'draw':0,'discard':2,'cards:played':1},option=1)
    CASES[-1]['initialDrawCount']=1
    tranquility(strong,'回合末治疗点归零，实际宁静进入弃牌区',
        {'HealingPool':0,'Wounds':3,'Fame':0,'hand':5,'discard':3,'cards:played':0,'token:Green':0},[dict(kind='EndTurn')])
    for option in [0,1]:
        case('basic_card_001',strong,'治疗图标禁止战斗中选择'+('治疗' if option==0 else '抽牌'),
             {'HealingPool':0,'draw':3,'cards:played':0},option=option,setup={'InBattle':True},
             error='InvalidOperationException: 战斗中不能使用宁静的治疗或抽牌效果')
        CASES[-1].update(playFromHand=True,initialDrawCount=3)
for option in [-1,2]:
    case('basic_card_001',True,'拒绝非法选择'+str(option)+'且不扣绿色费用',
         {'HealingPool':0,'draw':3,'cards:played':0},option=option,
         error='InvalidOperationException: 宁静必须选择治疗或抽牌')
    CASES[-1].update(playFromHand=True,initialDrawCount=3)
for option in [0,1]:
    case('basic_card_001',True,'缺绿色费用拒绝'+('治疗' if option==0 else '抽牌')+'，保留原卡',
         {'HealingPool':0,'draw':3,'cards:played':0},option=option,setup=empty_mana,
         error='InvalidOperationException: 法力不足，不能支付所选效果')
    CASES[-1].update(playFromHand=True,initialDrawCount=3)
tranquility(True,'强化治疗2选择原二级魔像，支付绿色，治疗不自动重整',
    {'HealingPool':0,'Wounds':3,'Fame':0,'unitWounds':0,'unitReady':False,'hand':5},
    [dict(kind='HealUnit',option=1)],unitIds=['army_2_001'],unitWounds=1)
tranquility(True,'治疗点分给手牌与一级农民，分别消耗且不增加名望',
    {'HealingPool':0,'Wounds':2,'Fame':0,'unitWounds':0,'unitReady':False,'hand':4,
     'operation:0:healing':1,'operation:1:healing':0},
    [dict(kind='HealHand',option=1),dict(kind='HealUnit',option=1)],unitIds=['army_2_006'],unitWounds=1)
tranquility(False,'宁静1与原圣杯2合用治疗三级烈焰枪手，只有圣杯2点奖励名望',
    {'HealingPool':0,'GrailHealing':0,'Wounds':3,'Fame':2,'unitWounds':0,'unitReady':False,'hand':5,'cards:played':2},
    [dict(kind='PlayCard',cardId='items_012'),dict(kind='HealUnit',option=1)],
    extraHandCards=['items_012'],unitIds=['army_003'],unitWounds=1)


# Earth Strength: original three-way action, targeted healing and actual card lifetime.
def earth(strong,title,expected,ops=(),option=0,setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('basic_card_004',strong,title,expected,option=option,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=3,operations=list(ops),**fields)

for strong in [False,True]:
    earth(strong,'大地之子提供治疗点，等待选择目标，不同时移动或格挡',
          {'HealingPool':2 if strong else 1,'Wounds':3,'hand':5,'cards:played':1,'Fame':0})
    earth(strong,'选择原版部队，按等级消耗治疗点，不重整或治疗英雄',
          {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'Fame':0,'unitWounds':0,'unitReady':False},
          [dict(kind='HealUnit',option=1)],unitIds=['army_2_001' if strong else 'army_2_006'],unitWounds=1)
    earth(strong,'未用治疗点回合末失效，实际原卡进入弃牌区',
          {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':0,'discard':3,'token:Green':0},[dict(kind='EndTurn')])
    case('basic_card_004',strong,'战斗中拒绝治疗，保留原卡及绿色费用',
         {'HealingPool':0,'draw':3,'cards:played':0},setup={'InBattle':True},
         error='InvalidOperationException: 战斗中不能使用治疗效果')
    CASES[-1].update(playFromHand=True,initialDrawCount=3)
for option in [0,1,2]:
    case('basic_card_004',True,'缺绿色费用拒绝三选一分支'+str(option)+'，保留原卡',
         {'HealingPool':0,'draw':3,'cards:played':0},option=option,setup=empty_mana,
         error='InvalidOperationException: 法力不足，不能支付所选效果')
    CASES[-1].update(playFromHand=True,initialDrawCount=3)


# Refreshing: four original choices, hand-icon timing and owned ready targets.
def refreshing(strong,title,expected,ops=(),option=0,setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('basic_card_005',strong,title,expected,option=option,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=1,operations=list(ops),**fields)

for strong in [False,True]:
    refreshing(strong,'治疗点选择原版部队，按等级消费，英雄伤牌保留',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'unitWounds':0,'unitReady':False,'Fame':0},
        [dict(kind='HealUnit',option=1)],unitIds=['army_2_001' if strong else 'army_2_006'],unitWounds=1)
    refreshing(strong,'抽取实际剩余1张，不凭空补牌也不治疗',
        {'HealingPool':0,'Wounds':3,'hand':6,'draw':0,'cards:played':1,'discard':2},option=1)
    refreshing(strong,'绿色资源原类型，魔晶满3时溢出为魔力',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'crystal:Green':3,'token:Green':1 if strong else 2},
        option=2,setup={'token:Green':1,'crystal:Green':3})
    refreshing(strong,'重整原等级上限部队，伤牌保留且仍不能发动',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'unitLevel':3 if strong else 2,
         'unitWounds':1,'unitReady':True,'unitCanActivate':False},option=3,
        unitIds=['army_003' if strong else 'army_2_001'],unitWounds=1)
refreshing(False,'未用治疗回合末失效，实际原卡进入弃牌区',
    {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':0,'discard':3,'token:Green':0},[dict(kind='EndTurn')])
for option in range(4):
    for battle in [True,False]:
        case('basic_card_005',True,('战斗中禁止所有效果' if battle else '缺绿色费用不得执行')+'分支'+str(option),
             {'HealingPool':0,'draw':1,'cards:played':0},option=option,setup={'InBattle':True} if battle else empty_mana,
             error='InvalidOperationException: '+('战斗中不能使用活力焕发的任何效果' if battle else '法力不足，不能支付所选效果'))
        CASES[-1].update(playFromHand=True,initialDrawCount=1,unitIds=['army_2_001'])
for option in [-1,4]:
    case('basic_card_005',True,'非法选项'+str(option)+'在扣费前拒绝',{'HealingPool':0,'cards:played':0},option=option,
         error='InvalidOperationException: 活力焕发必须选择治疗、抽牌、绿色资源或重整')
    CASES[-1].update(playFromHand=True)
for problem in ['missing','foreign','destroyed','overlevel']:
    case('basic_card_005',problem!='overlevel','非法重整目标 '+problem+'，不扣费不出牌',
         {'HealingPool':0,'cards:played':0},option=3,
         error='InvalidOperationException: 活力焕发必须选择等级范围内未销毁的自有部队')
    CASES[-1].update(playFromHand=True,unitIds=['army_003' if problem=='overlevel' else 'army_2_001'],
                    omitUnitTarget=problem=='missing',foreignUnitTarget=problem=='foreign',unitDestroyed=problem=='destroyed')


# Refreshing Walk combines two independent resource consumers, with a combat exception.
def refreshing_walk(strong,title,expected,ops=(),setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('advanced_card_007',strong,title,expected,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=1,destinationTerrain='Plains',operations=list(ops),**fields)

for strong in [False,True]:
    refreshing_walk(strong,'先移动实际平原，再消费治疗手牌，两个资源互不挪用',
        {'MovementPool':2 if strong else 0,'HealingPool':0,'Wounds':1 if strong else 2,
         'hand':3 if strong else 4,'cards:played':1,'position:q':1,'position:r':0,
         'operation:0:healing':2 if strong else 1,'operation:0:handWounds':3},
        [dict(kind='Move',q=1),dict(kind='HealHand',option=2 if strong else 1)])
    refreshing_walk(strong,'治疗原一级或二级部队，全部移动点保留且不重整',
        {'MovementPool':4 if strong else 2,'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'unitWounds':0,'unitReady':False},
        [dict(kind='HealUnit',option=1)],unitIds=['army_2_001' if strong else 'army_2_006'],unitWounds=1)
    refreshing_walk(strong,'战斗中只有移动点；拒绝地图移动与治疗，不扣资源',
        {'MovementPool':4 if strong else 2,'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'position:q':0,
         'operation:0:success':False,'operation:1:success':False,'operation:1:exception':'战斗中不能使用治疗效果'},
        [dict(kind='Move',q=1),dict(kind='HealHand',option=1)],setup={'InBattle':True})
    refreshing_walk(strong,'进入战斗失去未用治疗，战斗结束也不恢复',
        {'MovementPool':4 if strong else 2,'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,
         'operation:0:healing':0,'operation:2:success':False,'operation:2:exception':'治疗点不足或消费数量无效'},
        [dict(kind='BeginBattle'),dict(kind='EndBattle'),dict(kind='HealHand',option=1)])
    refreshing_walk(strong,'回合末两个资源清空，实际原卡进入弃牌',
        {'MovementPool':0,'HealingPool':0,'Wounds':3,'hand':5,'cards:played':0,'discard':3,'token:Green':0},[dict(kind='EndTurn')])
refreshing_walk(False,'行动结束后可治疗，但不能恢复地图移动',
    {'MovementPool':2,'HealingPool':0,'Wounds':2,'hand':4,'cards:played':1,'position:q':0,'operation:1:success':False},
    [dict(kind='HealHand',option=1),dict(kind='Move',q=1)],setup={'TurnActionCommitted':True})
case('advanced_card_007',True,'缺绿色费用，移动治疗均不生效且保留原卡',
    {'HealingPool':0,'cards:played':0},setup=empty_mana,error='InvalidOperationException: 法力不足，不能支付所选效果')
CASES[-1].update(playFromHand=True)


# Cure resolves actual healing throughout this turn, including earlier healing.
def cure(title,expected,ops=(),setup=None,**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('magic_001',False,title,expected,setup=setup)
    CASES[-1].update(playFromHand=True,initialDrawCount=4,operations=list(ops),**fields)

cure('治疗2等待目标，未治疗不抽牌',{'HealingPool':2,'Wounds':3,'hand':5,'draw':4,'DrawPerHeal':1,'cards:played':1})
cure('分两次治疗实际手牌，每次抽1，消耗绿色费用',
     {'HealingPool':0,'Wounds':1,'hand':5,'draw':2,'DrawPerHeal':1,'HealedHandWoundsThisTurn':2,'cards:played':1,
      'operation:0:healing':1,'operation:0:handWounds':2},[dict(kind='HealHand',option=1),dict(kind='HealHand',option=1)])
cure('原二级魔像治疗消费2并重整，部队治疗不抽手牌',
     {'HealingPool':0,'Wounds':3,'hand':5,'draw':4,'unitWounds':0,'unitReady':True,'unitCanActivate':True,'cards:played':1},
     [dict(kind='HealUnit',option=1)],unitIds=['army_2_001'],unitWounds=1)
cure('原一级农民治疗一张毒伤后仍有伤牌，重整但不能发动',
     {'HealingPool':1,'Wounds':3,'hand':5,'draw':4,'unitWounds':1,'unitReady':True,'unitCanActivate':False,'cards:played':1},
     [dict(kind='HealUnit',option=1)],unitIds=['army_2_006'],unitWounds=2)
cure('无伤不能凭空抽牌，失败消费保留治疗点',
     {'HealingPool':2,'Wounds':0,'hand':2,'draw':4,'cards:played':1,'operation:0:success':False,'operation:0:exception':'目标没有足够伤牌'},
     [dict(kind='HealHand',option=1)],setup={'player:Wounds':0})
cure('痊愈后焚毁原圣杯，两种奖励每治手牌1伤合计抽2',
     {'HealingPool':7,'Wounds':2,'hand':6,'draw':2,'DrawPerHeal':2,'cards:played':1,'cards:removed':1},
     [dict(kind='PlayCard',cardId='items_012',enhanced=True),dict(kind='HealHand',option=1)],extraHandCards=['items_012'])
cure('治疗抽到新伤牌后再次治疗，伤牌与牌库守恒',
     {'HealingPool':0,'Wounds':0,'hand':3,'draw':0,'cards:played':1,'HealedHandWoundsThisTurn':2,
      'operation:0:handWounds':1,'operation:1:handWounds':0},
     [dict(kind='HealHand',option=1),dict(kind='HealHand',option=1)],setup={'player:Wounds':1},initialDrawCards=['w','basic_card_000'])
CASES[-1]['initialDrawCount']=0
cure('已治疗后结束回合，治疗池、抽牌奖励和历史一并清空',
     {'HealingPool':0,'Wounds':2,'hand':5,'draw':3,'cards:played':0,'discard':3,'DrawPerHeal':0,
      'HealedHandWoundsThisTurn':0,'ReadyUnitOnHeal':False,'token:Green':0},
     [dict(kind='HealHand',option=1),dict(kind='EndTurn')])
for battle in [True,False]:
    case('magic_001',False,'战斗中拒绝痊愈' if battle else '缺绿色魔力拒绝痊愈',
         {'HealingPool':0,'DrawPerHeal':0,'cards:played':0},setup={'InBattle':True} if battle else empty_mana,
         error='InvalidOperationException: '+('战斗中不能施放痊愈' if battle else '法力不足，不能支付所选效果'))
    CASES[-1].update(playFromHand=True)

tranquility(False,'先宁静治疗手牌，再施放原痊愈，为此前治疗补抽1',
    {'HealingPool':2,'Wounds':2,'hand':5,'draw':2,'cards:played':2,'DrawPerHeal':1,'HealedHandWoundsThisTurn':1,'token:Green':1},
    [dict(kind='HealHand',option=1),dict(kind='PlayCard',cardId='magic_001')],extraHandCards=['magic_001'])
tranquility(False,'先宁静治疗原农民，再施放痊愈，追溯重整已治疗部队但不抽牌',
    {'HealingPool':2,'Wounds':3,'hand':5,'draw':3,'cards:played':2,'unitWounds':0,'unitReady':True,'DrawPerHeal':1,'token:Green':1},
    [dict(kind='HealUnit',option=1),dict(kind='PlayCard',cardId='magic_001')],extraHandCards=['magic_001'],unitIds=['army_2_006'],unitWounds=1)
grail(True,'先焚毁圣杯再施放痊愈，两种抽牌奖励不会互相覆盖',
    {'HealingPool':7,'Wounds':2,'hand':6,'draw':1,'DrawPerHeal':2,'cards:played':1,'cards:removed':1,'token:Green':1},
    [dict(kind='PlayCard',cardId='magic_001'),dict(kind='HealHand',option=1)],extraHandCards=['magic_001'])
# Preserve existing emerald-ring mana/fame/wound expectations with explicit healing.
next(c for c in CASES if c['id']=='items_007_04')['operations'].append(dict(kind='HealHand',option=2))

# Disease is combat-only. The emerald ring still earns the same two spell rewards.
emerald=next(c for c in CASES if c['id']=='items_007_04')
emerald['operations']=[dict(kind='PlayCard',cardId='magic_001'),dict(kind='HealHand',option=2),
                       dict(kind='BeginBattle'),dict(kind='PlayCard',cardId='magic_001',enhanced=True)]
for expectation in emerald['expected']:
    if expectation['key']=='operation:1:fame': expectation['key']='operation:3:fame'

def disease(title,expected,ops,targets=None,setup=None):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('magic_001',True,title,expected,setup={'InBattle':True,'DayPart':'Night','CardToDiscard':'<none>','CardToRemove':'<none>',**(setup or {})})
    CASES[-1].update(playFromHand=True,operations=ops,enemies=targets or [enemy(5,attack=2)],extraHandCards=['basic_card_016'],discardIndices=[])

for abilities,attack,target,title,required,kills in [
    ([],2,0,'完全格挡后真实攻击2击杀护甲1敌人',1,1),
    (['MagicResist'],2,0,'奥术免疫仍可格挡，但不受感染减甲',5,0),
    (['FireResist'],2,0,'火抗性不抵抗绿色感染',1,1),
    (['IceResist'],2,0,'冰抗性不抵抗绿色感染',1,1),
    ([],3,0,'格挡不足仍受伤，感染不能减甲',5,0),
    ([],2,1,'相同属性的未格挡敌人不能借用另一实例减甲',5,0)]:
    targets=[enemy(5,attack=attack,abilities=abilities)]
    if target==1: targets.append(enemy(5,attack=2))
    disease(title,{'BlockedArmorOne':True,'HealingPool':0,'Wounds':5 if attack==3 else 3,'hand':6 if attack==3 else 4,
        'cards:played':3,'Fame':3*kills,'enemyArmor:'+str(target):required,
        'operation:1:wounds':2 if attack==3 else 0,'operation:3:required':required,'operation:3:kills':kills},
        [dict(kind='PlayCard',cardId='basic_card_021',option=1),dict(kind='Block',targetIndex=0),
         dict(kind='PlayCard',cardId='basic_card_016'),dict(kind='Attack',targetIndex=target)],targets)
disease('战斗结束感染与格挡历史清空，保留另一个普通减甲2',
    {'BlockedArmorOne':False,'enemyArmor:0':3,'Wounds':3,'hand':4,'cards:played':3,'Fame':0,'operation:5:required':3,'operation:5:kills':0},
    [dict(kind='PlayCard',cardId='basic_card_021',option=1),dict(kind='Block'),dict(kind='EndBattle'),dict(kind='BeginBattle'),
     dict(kind='PlayCard',cardId='basic_card_016'),dict(kind='Attack')],setup={'armorReduction:0':2})
disease('回合结束清除感染，已打出原法术进入弃牌',
    {'BlockedArmorOne':False,'enemyArmor:0':5,'Wounds':3,'hand':6,'cards:played':0,'discard':3,'token:Green':0,'token:Black':0},
    [dict(kind='EndTurn')])
for problem in ['green','black','day','outside']:
    setup={'InBattle':True,'DayPart':'Night'}
    message='法力不足，不能支付所选效果'
    if problem in ['green','black']:
        color='Green' if problem=='green' else 'Black'
        setup.update({'token:'+color:0,'crystal:'+color:0,'token:Gold':0})
    elif problem=='day':setup['DayPart']='Day';message='白昼不能施放强效法术'
    else:setup['InBattle']=False;message='感染只能在战斗中施放'
    case('magic_001',True,'拒绝感染 '+problem+'，两色费用与原卡均保留',{'BlockedArmorOne':False,'cards:played':0},
         setup=setup,error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True)
case('basic_card_021',False,'先实际格挡，再用感染追溯该敌人并攻击击杀',
    {'BlockedArmorOne':True,'enemyArmor:0':1,'Wounds':3,'hand':5,'Fame':3,'cards:played':3,'token:Green':1,'token:Black':1,
     'operation:0:success':True,'operation:1:success':True,'operation:2:success':True,'operation:3:success':True,
     'operation:3:required':1,'operation:3:kills':1},option=1,setup={'InBattle':True,'DayPart':'Night'})
CASES[-1].update(playFromHand=True,extraHandCards=['magic_001','basic_card_016'],enemies=[enemy(5,attack=2)],
    operations=[dict(kind='Block'),dict(kind='PlayCard',cardId='magic_001',enhanced=True),
                dict(kind='PlayCard',cardId='basic_card_016'),dict(kind='Attack')])


# Regeneration: healing points and readying are independent printed effects.
for previous in [c for c in CASES if c['cardId']=='advanced_card_015']:
    previous['operations']=[dict(kind='HealHand',option=2 if previous['enhanced'] else 1)]

def regeneration(strong,title,expected,ops=(),**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('advanced_card_015',strong,title,expected)
    CASES[-1].update(playFromHand=True,operations=list(ops),**fields)

for strong in [False,True]:
    regeneration(strong,'重整等级上限原部队，治疗另用于手牌，部队伤牌仍阻止发动',
        {'HealingPool':0,'Wounds':1 if strong else 2,'hand':3 if strong else 4,'cards:played':1,
         'unitLevel':3 if strong else 2,'unitReady':True,'unitWounds':1,'unitCanActivate':False},
        [dict(kind='HealHand',option=2 if strong else 1)],unitIds=['army_003' if strong else 'army_2_001'],unitWounds=1)
    regeneration(strong,'不选重整目标也可治疗，原卡只打出一次',
        {'HealingPool':0,'Wounds':1 if strong else 2,'hand':3 if strong else 4,'cards:played':1,'unitReady':False},
        [dict(kind='HealHand',option=2 if strong else 1)],omitUnitTarget=True,unitIds=['army_2_001'])
    regeneration(strong,'原农民重整不自动治伤，治疗后进入战斗发动攻击2',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'unitReady':False,'unitWounds':0,'MeleePool':2,
         'operation:0:success':False,'operation:0:exception':'部队必须就绪、未受伤且未销毁',
         'operation:1:healing':1 if strong else 0},
        [dict(kind='UnitUse'),dict(kind='HealUnit',option=1),dict(kind='BeginBattle'),dict(kind='UnitUse')],
        unitIds=['army_2_006'],unitWounds=1)
    regeneration(strong,'未用治疗回合末失效，原再生进入弃牌区',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':0,'discard':3,'unitReady':True,'token:Green':0},
        [dict(kind='EndTurn')],unitIds=['army_2_001'])
    case('advanced_card_015',strong,'战斗中不得治疗或重整',{'HealingPool':0,'cards:played':0},
         setup={'InBattle':True},error='InvalidOperationException: 战斗中不能使用再生')
    CASES[-1].update(playFromHand=True,unitIds=['army_2_001'])
for problem in ['foreign','destroyed','baseOverlevel','strongOverlevel']:
    case('advanced_card_015',problem!='baseOverlevel','拒绝非法重整目标 '+problem,{'HealingPool':0,'cards:played':0},
         error='InvalidOperationException: 再生只能重整等级范围内未销毁的自有部队')
    CASES[-1].update(playFromHand=True,foreignUnitTarget=problem=='foreign',unitDestroyed=problem=='destroyed')
    if problem=='strongOverlevel': CASES[-1]['unitLevels']=[4] # explicit invalid boundary fixture, not original artwork evidence
    else: CASES[-1]['unitIds']=['army_003' if problem=='baseOverlevel' else 'army_2_001']
for option in [-1,1]:
    case('advanced_card_015',True,'非法互斥选项在费用前拒绝 '+str(option),{'HealingPool':0,'cards:played':0},option=option,
         error='InvalidOperationException: 再生没有互斥选项')
    CASES[-1].update(playFromHand=True,unitIds=['army_2_001'])
case('advanced_card_015',True,'缺绿色费用保持伤牌和部队不变',{'HealingPool':0,'cards:played':0},setup=empty_mana,
     error='InvalidOperationException: 法力不足，不能支付所选效果')
CASES[-1].update(playFromHand=True,unitIds=['army_2_001'])

# Restoration/Rebirth: terrain budget, separate healing, and multiple real unit targets.
for previous in [c for c in CASES if c['cardId']=='magic_005']:
    previous['operations']=[dict(kind='HealHand',option=3)]

def restoration(strong,forest,title,expected,ops=(),**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('magic_005',strong,title,expected,setup={'CurrentTerrain':'Forest' if forest else 'Plains','DayPart':'Night'})
    CASES[-1].update(playFromHand=True,operations=list(ops),**fields)

for forest in [False,True]:
    for strong in [False,True]:
        restoration(strong,forest,'地形治疗点先保留，再实际治疗手牌3；无需重整目标',
            {'HealingPool':2 if forest else 0,'Wounds':0,'hand':2,'cards:played':1,'unitReady':False},
            [dict(kind='HealHand',option=3)],readyUnitTargets=[],unitIds=['army_2_001'])
    ids=['army_2_006','army_2_001']+(['army_2_005'] if forest else [])+['army_003']
    selected=list(range(len(ids)-1))
    restoration(True,forest,'多支原部队按总等级重整，分别治疗后实际发动攻击合计4',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'MeleePool':4,
         'units:ready':'False,False,True,False' if forest else 'False,False,False',
         'units:wounds':','.join('0' for _ in ids),'operation:1:healing':2 if forest else 0},
        [dict(kind='HealUnit',targetIndex=0,option=1),dict(kind='HealUnit',targetIndex=1,option=1),
         dict(kind='BeginBattle'),dict(kind='UnitUse',targetIndex=0),dict(kind='UnitUse',targetIndex=1)],
        unitIds=ids,initialUnitWounds=[1,1],readyUnitTargets=selected)
    restoration(False,forest,'基础只产生治疗，不重整指定的原部队',
        {'HealingPool':5 if forest else 3,'Wounds':3,'hand':5,'cards:played':1,'units:ready':'False'},
        unitIds=['army_2_001'],readyUnitTargets=[0])
    restoration(True,forest,'未用治疗在回合末失效，实际原法术进入弃牌',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':0,'discard':3,'token:Green':0,'token:Black':0,'unitReady':True},
        [dict(kind='EndTurn')],unitIds=['army_2_001'])

for problem in ['over3','over5','duplicate','foreign','destroyed']:
    ids=['army_2_001','army_003','army_2_006'] if problem=='over5' else ['army_2_001','army_2_001']
    if problem in ['foreign','destroyed']: ids=['army_2_001','army_2_006']
    targets=[0,0] if problem=='duplicate' else list(range(len(ids)))
    message='重生所选部队总等级超过地形上限' if problem.startswith('over') else '重生必须选择不重复且未销毁的自有部队'
    case('magic_005',True,'非法多部队选择整笔拒绝 '+problem,{'HealingPool':0,'cards:played':0,'units:ready':','.join('False' for _ in ids)},
         setup={'CurrentTerrain':'Forest' if problem=='over5' else 'Plains'},error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,unitIds=ids,readyUnitTargets=targets,
                    foreignUnitTarget=problem=='foreign',unitDestroyed=problem=='destroyed')
for strong in [False,True]:
    case('magic_005',strong,'战斗中不能用治疗或重整部分',{'HealingPool':0,'cards:played':0},setup={'InBattle':True},
         error='InvalidOperationException: 战斗中不能使用复原或重生')
    CASES[-1].update(playFromHand=True,unitIds=['army_2_001'],readyUnitTargets=[0])
for problem in ['green','black','day']:
    setup={'DayPart':'Night'};message='法力不足，不能支付所选效果'
    if problem=='day':setup['DayPart']='Day';message='白昼不能施放强效法术'
    else:
        color='Green' if problem=='green' else 'Black';setup.update({'token:'+color:0,'crystal:'+color:0,'token:Gold':0})
    case('magic_005',True,'费用或昼夜拒绝 '+problem,{'HealingPool':0,'cards:played':0},setup=setup,error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,unitIds=['army_2_001'],readyUnitTargets=[0])
for option in [-1,1]:
    case('magic_005',True,'非法选项在费用前拒绝 '+str(option),{'HealingPool':0,'cards:played':0},option=option,
         error='InvalidOperationException: 复原和重生没有互斥选项')
    CASES[-1].update(playFromHand=True,unitIds=['army_2_001'])

for strong in [False,True]:
    regeneration(strong,'重整第一支原部队，治疗另一支原部队，两者伤牌和就绪独立',
        {'HealingPool':0,'Wounds':3,'hand':5,'cards:played':1,'unitWounds':1,'unitReady':True,'unitCanActivate':False,
         'units:ready':'True,False','units:wounds':'1,0'},
        [dict(kind='HealUnit',targetIndex=1,option=1)],
        unitIds=['army_003' if strong else 'army_2_001','army_2_001' if strong else 'army_2_006'],
        unitWounds=1,initialUnitWounds=[0,1])

def energy_others(strong=False):
    return [dict(unitIds=['army_2_006','army_003' if strong else 'army_2_001']) for _ in range(2)]

def energy(strong,title,expected,ops=(),**fields):
    expected=dict(expected)
    for i in range(len(ops)): expected.setdefault('operation:'+str(i)+':success',True)
    case('magic_000',strong,title,expected,setup={'DayPart':'Night'})
    CASES[-1].update(playFromHand=True,unitIds=['army_003'],operations=list(ops),otherPlayers=energy_others(strong),**fields)

for strong in [False,True]:
    energy(strong,'放弃可选耗尽，其他玩家所有原部队保持就绪',
        {'Wounds':3,'hand':5,'cards:played':1,'unitReady':True,'unitWounds':0 if strong else 1,
         'otherUnits:ready':'True,True;True,True','HealedUnitsThisTurn:count':1 if strong else 0},unitWounds=1)
    energy(strong,'分别选每位其他玩家第二支原部队，第一支保持就绪',
        {'Wounds':3,'hand':5,'cards:played':1,'unitReady':True,'otherUnits:ready':'True,False;True,False'},
        exhaustTargets=[dict(playerIndex=0,unitIndex=1),dict(playerIndex=1,unitIndex=1)])
    energy(strong,'被耗尽的其他玩家原部队不能发动，回合末原法术弃牌',
        {'Wounds':3,'hand':5,'cards:played':0,'discard':3,'unitReady':True,'otherUnits:ready':'True,False;True,True',
         'operation:0:success':False,'operation:0:exception':'部队必须就绪、未受伤且未销毁','operation:0:attack':0,
         'token:Green':0,'token:Black':0,'HealedUnitsThisTurn:count':0},
        [dict(kind='OtherUnitUse',q=0,targetIndex=1),dict(kind='EndTurn')],exhaustTargets=[dict(playerIndex=0,unitIndex=1)])
energy(True,'强化治疗两张部队伤牌；实际击杀后痊愈追溯治疗记录再次重整',
    {'Wounds':3,'hand':5,'draw':10,'cards:played':2,'unitReady':True,'unitWounds':0,'HealedUnitsThisTurn:count':1,
     'HealingPool':2,'token:Green':0,'token:Black':1,'otherUnits:ready':'True,True;True,True','Fame':3,
     'operation:2:required':5,'operation:2:kills':1},
    [dict(kind='BeginBattle'),dict(kind='UnitUse'),dict(kind='Attack'),dict(kind='EndBattle'),dict(kind='PlayCard',cardId='magic_001')],
    unitWounds=2,extraHandCards=['magic_001'],enemies=[enemy(5,attack=0)])

for problem in ['missing','foreign','destroyed','alreadyReady']:
    case('magic_000',True,'未实际重整自有部队不得触发后续 '+problem,{'cards:played':0,'otherUnits:ready':'True,True;True,True'},
         error='InvalidOperationException: 能量法术必须实际重整一支耗尽且未销毁的自有部队')
    CASES[-1].update(playFromHand=True,unitIds=['army_003'],otherPlayers=energy_others(),
        omitUnitTarget=problem=='missing',foreignUnitTarget=problem=='foreign',unitDestroyed=problem=='destroyed',
        unitInitiallyReady=problem=='alreadyReady',exhaustTargets=[dict(playerIndex=0,unitIndex=1)])
    if problem=='alreadyReady':
        for item in CASES[-1]['expected']:
            if item['key']=='unitReady':item['value']='True'

for problem in ['twoSameOwner','duplicate','overlevel','unlistedOwner','destroyed','ownUnit','alreadySpent']:
    other=energy_others();targets=[dict(playerIndex=0,unitIndex=1)]
    if problem=='twoSameOwner':targets.append(dict(playerIndex=0,unitIndex=0))
    if problem=='duplicate':targets.append(dict(playerIndex=0,unitIndex=1))
    if problem=='overlevel':other[0]['unitIds'][1]='army_003'
    if problem=='unlistedOwner':targets[0]['foreign']=True
    if problem=='destroyed':other[0]['destroyedIndices']=[1]
    if problem=='alreadySpent':other[0]['exhaustedIndices']=[1]
    if problem=='ownUnit':targets=[dict(playerIndex=-1,unitIndex=0)]
    states='True,False;True,True' if problem in ['destroyed','alreadySpent'] else 'True,True;True,True'
    case('magic_000',problem!='overlevel','可选耗尽目标不合法整笔拒绝 '+problem,{'cards:played':0,'otherUnits:ready':states},
         error='InvalidOperationException: 耗尽目标须为每位其他玩家至多一支等级范围内就绪且未销毁的部队')
    CASES[-1].update(playFromHand=True,unitIds=['army_003'],otherPlayers=other,exhaustTargets=targets)

for problem in ['green','black','day','battle','battleBase']:
    setup={'DayPart':'Night'};message='法力不足，不能支付所选效果'
    if problem=='day':setup['DayPart']='Day';message='白昼不能施放强效法术'
    elif problem.startswith('battle'):setup['InBattle']=True;message='战斗中不能施放能量流转或能量窃取'
    else:
        color='Green' if problem=='green' else 'Black';setup.update({'token:'+color:0,'crystal:'+color:0,'token:Gold':0})
    case('magic_000',problem!='battleBase','费用时机拒绝 '+problem,{'cards:played':0,'otherUnits:ready':'True,True;True,True'},
         setup=setup,error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,unitIds=['army_003'],otherPlayers=energy_others(),exhaustTargets=[dict(playerIndex=0,unitIndex=1)])
for option in [-1,1]:
    case('magic_000',True,'非法互斥选项拒绝 '+str(option),{'cards:played':0},option=option,
         error='InvalidOperationException: 能量流转和能量窃取没有互斥选项')
    CASES[-1].update(playFromHand=True,unitIds=['army_003'])

# Rulebook p9: unused combat power cannot be carried into the next battle.
for card_id, phase in [('items_008','Melee'),('items_016','Ranged')]:
    operation_case(card_id,False,'未消费原武器攻击在战斗结束失效，下场不能借用',
        {'hand':4,'discard':3,'cards:played':1,'cards:total':18,'Fame':0,
         'SwordFamePerKill':int(phase=='Melee'),'BowFamePerKill':0,
         'operation:2:success':False,'operation:2:exception':'没有可用于此阶段的攻击'},
        [dict(kind='EndBattle'),dict(kind='BeginBattle'),dict(kind='Attack' if phase=='Melee' else 'RangedAttack')],
        setup={'InBattle':True,'CombatPhase':phase},discardIndices=[1],playFromHand=True,enemies=[enemy(2)])
for burn in [False,True]:
    operation_case('items_020',burn,'分次格挡剩余不能跨战斗，下一场实际承伤并检查卡牌去向',
        {'hand':6,'discard':2 if burn else 3,'cards:played':0,'cards:removed':int(burn),'Wounds':4,'handWounds':4,
         'operation:0:printed':4,'operation:0:wounds':0,'operation:0:BlockPool':8 if burn else 4,
         'operation:3:printed':0,'operation:3:wounds':1,'operation:3:success':True},
        [dict(kind='Block'),dict(kind='EndBattle'),dict(kind='BeginBattle'),dict(kind='Block'),dict(kind='EndTurn')],
        setup={'InBattle':True},option=1,playFromHand=True,enemies=[enemy(5,attack=2)])

# Full production TurnMachine closes ranged power, then damage/melee and combat.
for strong, armor, skip in [(False,9,False),(True,9,False),(False,5,False),(False,5,True)]:
    killed=int(not skip and armor<=5)
    operation_case('magic_009',strong,'正式阶段机：'+('放弃远程' if skip else '远程击杀' if killed else '远程不足')+'后收尾，禁止重复结算与跨场借用',
        {'hand':6 if strong else 5,'cards:played':1,'Wounds':4 if strong else 3,'handWounds':4 if strong else 3,
         'token:Red':1,'token:Black':1 if strong else 2,'Fame':3*killed,'GamePhase':'Ranged','CombatPhase':'Ranged','InBattle':True,
         'operation:0:kills':killed,'operation:0:phase':'Block','operation:0:rangedRemaining':0,
         'operation:1:phase':'End','operation:1:inBattle':False,'operation:1:wounds':0,
         'operation:2:success':False,'operation:2:exception':'必须在 AssignDamage 阶段调用，当前：End',
         'operation:3:phase':'Ranged','operation:4:success':False,'operation:4:exception':'没有可用于此阶段的攻击'},
        [dict(kind='TurnRanged',option=int(skip)),dict(kind='TurnResolve'),dict(kind='TurnResolve'),dict(kind='TurnBegin'),dict(kind='RangedAttack')],
        turnMachineStartPhase='Ranged',playFromHand=True,enemies=[enemy(armor)])
for burn in [False,True]:
    operation_case('items_020',burn,'正式阶段机连续两场：首场原盾免伤，下场无卡承伤，回合末检查去向',
        {'hand':6,'discard':2 if burn else 3,'cards:played':0,'cards:removed':int(burn),'Wounds':4,'handWounds':4,
         'GamePhase':'End','InBattle':False,'operation:0:blockBefore':8 if burn else 6,
         'operation:0:phase':'End','operation:0:wounds':0,'operation:0:inBattle':False,
         'operation:1:phase':'Ranged','operation:2:phase':'Block','operation:3:phase':'End','operation:3:wounds':1},
        [dict(kind='TurnResolve'),dict(kind='TurnBegin'),dict(kind='TurnRanged'),dict(kind='TurnResolve'),dict(kind='EndTurn')],
        turnMachineStartPhase='Block',playFromHand=True,enemies=[enemy(5,attack=2)])

# Burning Shield: defense reward must remain available for a later target choice.
for trait,effective,kills in [('PhysicalResist',4,1),('FireResist',2,0),('MagicResist',4,1)]:
    operation_case('magic_008',False,'正式分阶段：成功格挡后火焰攻击另一个'+trait+'目标',
        {'hand':5,'discard':3,'cards:played':0,'Wounds':3,'handWounds':3,'Fame':3*kills,
         'token:Red':0,'token:Black':0,'GamePhase':'End','InBattle':False,
         'operation:0:phase':'Melee','operation:0:wounds':0,'operation:0:meleeRemaining':4,'operation:0:red':1,'operation:0:black':2,
         'operation:1:effective':effective,'operation:1:kills':kills,'operation:1:meleeRemaining':0,
         'operation:2:success':False,'operation:2:exception':'目标已经被击败' if kills else '没有可用于此阶段的攻击'},
        [dict(kind='TurnDefense'),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        turnMachineStartPhase='Block',playFromHand=True,enemies=[enemy(9,attack=4,abilities=['MagicResist']),enemy(4,abilities=[trait])])
operation_case('magic_008',False,'正式分阶段：失败格挡承受完整伤害且没有攻击奖励',
    {'hand':8,'discard':3,'cards:played':0,'Wounds':6,'handWounds':6,'token:Red':0,'token:Black':0,'Fame':0,
     'GamePhase':'End','InBattle':False,'operation:0:wounds':3,'operation:0:meleeRemaining':0,
     'operation:1:success':False,'operation:1:exception':'没有可用于此阶段的攻击'},
    [dict(kind='TurnDefense'),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Block',playFromHand=True,enemies=[enemy(9,attack=5),enemy(4)])
operation_case('magic_008',False,'正式分阶段：玩家放弃奖励攻击，结束战斗后不留存',
    {'hand':5,'discard':3,'cards:played':0,'Wounds':3,'handWounds':3,'token:Red':0,'token:Black':0,'Fame':0,
     'GamePhase':'End','InBattle':False,'operation:0:meleeRemaining':4,'operation:1:meleeRemaining':0},
    [dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Block',playFromHand=True,enemies=[enemy(9,attack=4)])
for trait,killed in [('PhysicalResist',True),('FireResist',False),('MagicResist',False)]:
    operations=[dict(kind='TurnDefense')]
    expected={'hand':4 if killed else 5,'discard':4 if killed else 3,'cards:played':0,'Wounds':3,'handWounds':3,
              'token:Red':0,'token:Black':0,'Fame':6 if killed else 0,'GamePhase':'End','InBattle':False,
              'operation:0:kills':int(killed),'operation:0:meleeRemaining':0,'operation:0:red':1,'operation:0:black':1}
    if killed:
        operations += [dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack'),dict(kind='TurnAttack',targetIndex=1)]
        expected.update({'operation:2:success':False,'operation:2:exception':'目标已经被击败','operation:2:meleeRemaining':2,
                         'operation:3:effective':2,'operation:3:kills':1})
    else:
        operations += [dict(kind='TurnAttack')]
        expected.update({'operation:1:success':False,'operation:1:exception':'没有可用于此阶段的攻击'})
    operations += [dict(kind='TurnFinish'),dict(kind='EndTurn')]
    operation_case('magic_008',True,'正式分阶段：爆破护盾对'+trait+'的消灭、后续目标及真实卡区',expected,operations,
        turnMachineStartPhase='Block',playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
        enemies=[enemy(9,attack=4,abilities=[trait]),enemy(2)])

# Printed attack rewards belong to the contributing card in this attack.
for strong in [False, True]:
    for option in [0, 1]:
        power = (4 if option else 6) if strong else (2 if option else 3)
        operation_case('advanced_card_034',strong,'骑士精神：印刷攻击选择、双目标奖励与真实弃牌',
            {'hand':5,'discard':3,'cards:played':0,'Fame':8 if strong and option else 6,'Reputation':2 if option else 0,
             'token:White':0,'played:token:White':1 if strong else 2,'GamePhase':'End','InBattle':False,
             'operation:0:effective':power,'operation:0:kills':2,'operation:0:meleeRemaining':0},
            [dict(kind='TurnAttack',attackTargets=[0,1]),dict(kind='TurnFinish'),dict(kind='EndTurn')],
            option=option,turnMachineStartPhase='Melee',playFromHand=True,enemies=[enemy(1),enemy(1)])
    operation_case('advanced_card_034',strong,'两张骑士精神：分别付费、分别奖励同一次攻击的每个击杀',
        {'hand':5,'discard':4,'cards:played':0,'Fame':10 if strong else 6,'Reputation':4,'token:White':0,
         'GamePhase':'End','InBattle':False,'operation:1:effective':8 if strong else 4,'operation:1:kills':2,
         'operation:1:white':0 if strong else 2,'operation:1:meleeRemaining':0},
        [dict(kind='PlayCard',cardId='advanced_card_034',enhanced=strong,option=1),dict(kind='TurnAttack',attackTargets=[0,1]),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=1,turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['advanced_card_034'],enemies=[enemy(1),enemy(1)])
    case('advanced_card_034',strong,'非法攻击分支在付款和出牌前拒绝',{},option=2 if strong else -1,
         error='InvalidOperationException: 骑士精神必须选择普通攻击或附带奖励的攻击')
    CASES[-1]['playFromHand']=True

for phase in ['Ranged','Melee']:
    operations = [dict(kind='TurnRanged',attackTargets=[0,1]),dict(kind='TurnResolve')] if phase == 'Ranged' else [dict(kind='TurnAttack',attackTargets=[0,1]),dict(kind='TurnFinish')]
    operation_case('basic_card_011',True,'利斧投掷：'+phase+'攻击击败两敌也只额外获得名望1',
        {'hand':5,'discard':3,'cards:played':0,'Fame':7,'Reputation':0,'token:Blue':0,'played:token:Blue':1,
         'GamePhase':'End','InBattle':False,'operation:0:kills':2,'operation:0:fame':7,'operation:0:rangedRemaining':0},
        operations+[dict(kind='EndTurn')],turnMachineStartPhase=phase,playFromHand=True,enemies=[enemy(1),enemy(1)])
operation_case('basic_card_011',True,'两张强化利斧：实际分别付款、同次击杀分别奖励',
    {'hand':5,'discard':4,'cards:played':0,'Fame':8,'Reputation':0,'token:Blue':0,'GamePhase':'End','InBattle':False,
     'operation:1:kills':2,'operation:1:fame':8,'operation:1:blue':0,'operation:1:rangedRemaining':0},
    [dict(kind='PlayCard',cardId='basic_card_011',enhanced=True),dict(kind='TurnRanged',attackTargets=[0,1]),dict(kind='TurnResolve'),dict(kind='EndTurn')],
    turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['basic_card_011'],enemies=[enemy(1),enemy(1)])
operation_case('basic_card_011',True,'强化利斧攻击失败后，普通利斧击杀不继承奖励',
    {'hand':5,'discard':4,'cards:played':0,'Fame':3,'Reputation':0,'token:Blue':0,'GamePhase':'End','InBattle':False,
     'operation:0:kills':0,'operation:0:effective':3,'operation:0:rangedRemaining':0,'operation:2:kills':1,'operation:2:fame':3},
    [dict(kind='TurnAttack'),dict(kind='PlayCard',cardId='basic_card_011',option=1),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['basic_card_011'],enemies=[enemy(4),enemy(1)])
operation_case('basic_card_011',True,'结束战斗使未用利斧奖励失效，下场普通攻击只有敌人名望',
    {'hand':5,'discard':4,'cards:played':0,'Fame':3,'Reputation':0,'token:Blue':0,'GamePhase':'End','InBattle':False,
     'operation:0:rangedRemaining':0,'operation:3:kills':1,'operation:3:fame':3},
    [dict(kind='TurnFinish'),dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='basic_card_011',option=1),dict(kind='TurnRanged'),dict(kind='TurnResolve'),dict(kind='EndTurn')],
    turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['basic_card_011'],enemies=[enemy(1)])
for strong,option in [(False,2),(True,1)]:
    case('basic_card_011',strong,'非法选项在付款和出牌前拒绝',{},option=option,error='InvalidOperationException: 利斧投掷选项无效')
    CASES[-1]['playFromHand']=True

# Chill remains attached to its original enemy after ranged casualties compact the list.
for strong in [False, True]:
    for removed in [0, 2]:
        target = 0 if removed == 0 else 1
        armor = 2 if strong else 5
        operation_case('magic_013',strong,'冰冻目标存活：远程击杀'+str(removed+1)+'号后效果仍属于原目标',
            {'hand':4 if strong else 5,'discard':5,'cards:played':0,'Wounds':3,'handWounds':3,'Fame':6,
             'token:Blue':0,'token:Black':0,'token:Red':0,'played:token:Blue':1,'played:token:Black':1 if strong else 2,
             'GamePhase':'End','InBattle':False,'operation:1:kills':1,'operation:1:skip':str(target),
             'operation:1:enemyArmor':f'{armor},1' if target==0 else f'1,{armor}',
             'operation:2:wounds':0,'operation:4:effective':2 if strong else 5,'operation:4:kills':1},
            [dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged',targetIndex=removed),dict(kind='TurnDefense'),
             dict(kind='PlayCard',cardId='basic_card_021' if strong else 'magic_009'),dict(kind='TurnAttack',targetIndex=target),
             dict(kind='TurnFinish'),dict(kind='EndTurn')],
            option=1,turnMachineStartPhase='Ranged',playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
            extraHandCards=['magic_009'] if strong else ['magic_009','magic_009'],
            enemies=[enemy(1),enemy(6 if strong else 5,attack=4,abilities=['FireResist']),enemy(1)])
    operation_case('magic_013',strong,'冰冻目标已击败：后一敌人照常攻击且保留火抗和护甲',
        {'hand':7,'discard':5,'cards:played':0,'Wounds':5,'handWounds':5,'Fame':3,
         'token:Blue':0,'token:Black':0,'token:Red':0,'GamePhase':'End','InBattle':False,
         'operation:1:kills':1,'operation:1:skip':'','operation:1:enemyArmor':'5',
         'operation:2:wounds':2,'operation:4:effective':2,'operation:4:kills':0},
        [dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),
         dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_009','magic_009'],
        enemies=[enemy(1),enemy(5,attack=4,abilities=['FireResist'])])
    for failure in [-1,1,2,3]:
        reason='奥术免疫敌人不能成为冰冻目标' if failure==2 else '冰冻必须选择本场战斗中的有效敌人'
        case('magic_013',strong,'无效、免疫或非战斗目标拒绝且不支付费用 '+str(failure),
            {'SkipAttackIndices:count':0,'FireResistRemoved:count':0},option=failure if failure<2 else 0,
            setup={'InBattle':failure!=3},error='InvalidOperationException: '+reason)
        CASES[-1].update(playFromHand=True,enemies=[enemy(5,attack=4,abilities=['MagicResist'] if failure==2 else ['FireResist'])])

for strong in [False, True]:
    for refill in [False, True]:
        operations=[dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn',refill=refill),
                    dict(kind='StartTurn'),dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense')]
        if not strong:
            operations += [dict(kind='PlayCard',cardId='basic_card_002',option=2),dict(kind='PlayCard',cardId='magic_009')]
        else:
            operations += [dict(kind='PlayCard',cardId='basic_card_021')]
        attack_index=len(operations)
        operations += [dict(kind='TurnAttack'),dict(kind='TurnFinish')]
        operation_case('magic_013',strong,'冰冻期限：结束战斗停攻失效，'+('正常补牌' if refill else '跳过补牌')+'后下一回合恢复原敌人',
            {'hand':6 if strong else 7,'discard':3,'cards:played':1 if strong else 2,'Wounds':5,'handWounds':5,'Fame':0,
             'token:Blue':0,'token:Black':0,'token:Red':0,'ArmorReduction:count':0,'FireResistRemoved:count':0,'SkipAttackIndices:count':0,
             'GamePhase':'End','InBattle':False,'operation:0:wounds':0,
             'operation:1:skip':'','operation:1:enemyArmor':'2' if strong else '6','operation:1:fireRemoved':0 if strong else 1,
             'operation:2:enemyArmor':'6','operation:2:fireRemoved':0,'operation:2:blue':0,'operation:2:black':0,
             'operation:2:played':0,'operation:2:discard':3,'operation:6:wounds':2,
             f'operation:{attack_index}:effective':2,f'operation:{attack_index}:kills':0},operations,
            turnMachineStartPhase='Block',playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
            extraHandCards=[] if strong else ['basic_card_002','magic_009'],enemies=[enemy(6,attack=4,abilities=['FireResist'])])

for killed in [0,2]:
    source=0 if killed==0 else 1
    operation_case('magic_021',True,'附身来源移位：仍禁打原来源，普通狂怒可用，借来攻击用于另一敌人',
        {'hand':4,'discard':5,'cards:played':0,'Wounds':3,'handWounds':3,'Fame':6,
         'token:White':0,'token:Black':0,'token:Red':0,'GamePhase':'End','InBattle':False,
         'played:MeleePool':4,'played:attackElement':'Fire','played:token:White':1,'played:token:Black':1,
         'operation:1:kills':1,'operation:1:skip':str(source),'operation:2:wounds':0,
         'operation:3:success':False,'operation:3:exception':'没有可用于此阶段的攻击','operation:3:meleeRemaining':4,
         'operation:5:effective':2,'operation:5:kills':0,'operation:5:meleeRemaining':4,
         'operation:6:effective':4,'operation:6:kills':1,'operation:6:meleeRemaining':0},
        [dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged',targetIndex=killed),dict(kind='TurnDefense'),
         dict(kind='TurnAttack',targetIndex=source),dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack',targetIndex=source),
         dict(kind='TurnAttack',targetIndex=1-source),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=1,turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_009'],discardIndex=-1,removeIndex=-1,discardIndices=[],
        enemies=[enemy(1),enemy(6,attack=4,element='Fire'),enemy(1)])
for original_source in [1,2]:
    source=original_source-1
    operation_case('magic_021',True,'两只相同牌面敌人：列表压缩后仍按实际附身来源限制目标 '+str(original_source+1),
        {'hand':7,'discard':4,'cards:played':0,'Wounds':5,'handWounds':5,'Fame':6,
         'token:White':0,'token:Black':0,'token:Red':0,'GamePhase':'End','InBattle':False,
         'operation:1:kills':1,'operation:1:skip':str(source),'operation:2:wounds':2,
         'operation:3:success':False,'operation:3:exception':'没有可用于此阶段的攻击','operation:3:meleeRemaining':4,
         'operation:4:effective':4,'operation:4:kills':1},
        [dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),
         dict(kind='TurnAttack',targetIndex=source),dict(kind='TurnAttack',targetIndex=1-source),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=original_source,turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_009'],
        enemies=[enemy(1),enemy(4,attack=4,element='Fire'),enemy(4,attack=4,element='Fire')])
operation_case('magic_021',True,'附身来源先被火球击败：借来的攻击不能把无关存活敌人误当来源',
    {'hand':5,'discard':4,'cards:played':0,'Wounds':3,'Fame':6,'token:White':0,'token:Black':0,'token:Red':0,
     'GamePhase':'End','InBattle':False,'operation:1:kills':1,'operation:1:meleeRemaining':4,'operation:1:skip':'',
     'operation:2:wounds':0,'operation:3:effective':4,'operation:3:kills':1},
    [dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_009'],enemies=[enemy(1,attack=4,element='Fire'),enemy(4)])

for immune in [False, True]:
    case('magic_021',True,'奥术免疫目标拒绝' if immune else '战斗外即使残留敌人也拒绝附身',
         {'SkipAttackIndices:count':0,'PossessedEnemyIndices:count':0,'cards:played':0},
         setup={'InBattle':immune},error='InvalidOperationException: '+('奥术免疫敌人不能成为附身目标' if immune else '附身必须选择当前战斗中的敌人'),
         limits=['已验目标拒绝及真实卡区费用守恒；其他整卡分支仍按历史范围保留'])
    CASES[-1].update(playFromHand=True,enemies=[enemy(5,attack=4,abilities=['MagicResist'] if immune else [])])

operation_case('magic_021',True,'白色附身可选元素抗性敌人，借来的攻击可击败另一奥术免疫敌人',
    {'hand':5,'discard':3,'cards:played':0,'Fame':3,'Wounds':3,'handWounds':3,
     'token:White':0,'token:Black':0,'GamePhase':'End','InBattle':False,
     'operation:0:wounds':0,'operation:1:kills':1,'operation:1:effective':4},
    [dict(kind='TurnDefense'),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Block',playFromHand=True,
    enemies=[enemy(6,4,'Fire',['FireResist','IceResist','PhysicalResist']),enemy(4,abilities=['MagicResist'])])

# Whirlwind/Tornado: real defense, actual defeat/rewards, and original-card zones.
for day in ['Day','Night']:
    operation_case('magic_022',False,'旋风只阻止所选敌人；结束回合后下一场照常承伤 '+day,
        {'hand':9,'discard':3,'cards:played':0,'Wounds':7,'handWounds':7,'Fame':0,
         'token:White':0,'token:Black':0,'GamePhase':'End','InBattle':False,
         'operation:0:wounds':1,'operation:0:skip':'0','operation:1:skip':'',
         'operation:2:played':0,'operation:6:wounds':3,'DefeatedEnemyIndices:count':0},
        [dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn'),dict(kind='StartTurn'),
         dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish')],
        setup={'DayPart':day},turnMachineStartPhase='Block',playFromHand=True,
        enemies=[enemy(5,4,abilities=['FireResist','IceResist']),enemy(5,2)])

operation_case('magic_022',True,'飓风实际消灭高护甲目标；重复施法和攻击拒绝，狂怒攻击另一目标',
    {'hand':7,'discard':4,'cards:played':0,'Fame':6,'Wounds':5,'handWounds':5,
     'token:White':0,'token:Black':0,'GamePhase':'End','InBattle':False,'DefeatedEnemyIndices:count':2,
     'operation:0:defeated':'0','operation:0:totalFame':3,
     'operation:1:success':False,'operation:1:exception':'目标已经被击败','operation:1:meleeRemaining':2,
     'operation:2:success':False,'operation:2:exception':'目标已经被击败','operation:2:white':1,'operation:2:black':1,
     'operation:3:kills':1,'operation:3:defeated':'0,1','operation:3:totalFame':6},
    [dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack'),
     dict(kind='PlayCard',cardId='magic_022',enhanced=True),dict(kind='TurnAttack',targetIndex=1),
     dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['magic_022'],
    discardIndex=-1,removeIndex=-1,discardIndices=[],
    enemies=[enemy(99,4,abilities=['Fortified','FireResist','IceResist']),enemy(2)])

for strong in [False,True]:
    for failure in [-1,1,2,3]:
        reason='奥术免疫敌人不能成为旋风目标' if failure==2 else '旋风必须选择本场战斗中的有效敌人'
        case('magic_022',strong,'目标越界、免疫或战斗外原子拒绝 '+str(failure),
            {'cards:played':0,'DefeatedEnemyIndices:count':0,'SkipAttackIndices:count':0},
            option=failure if failure<2 else 0,setup={'InBattle':failure!=3,'CombatPhase':'Melee'},
            error='InvalidOperationException: '+reason)
        CASES[-1].update(playFromHand=True,enemies=[enemy(5,abilities=['MagicResist'] if failure==2 else [])])
for phase in ['None','Ranged','Block','AssignDamage','End']:
    case('magic_022',True,'攻击阶段以外不能打出飓风 '+phase,
        {'cards:played':0,'DefeatedEnemyIndices:count':0},setup={'InBattle':True,'CombatPhase':phase},
        error='InvalidOperationException: 飓风只能在攻击阶段打出')
    CASES[-1]['playFromHand']=True
for strong, failure in [(False,0),(True,0),(True,1),(True,2)]:
    resources=[{'token:White':0,'crystal:White':0},{'token:Black':0},{'DayPart':'Day'}][failure]
    case('magic_022',strong,'合法目标仍须支付原费用并遵守昼夜 '+str(failure),
        {'cards:played':0,'DefeatedEnemyIndices:count':0,'SkipAttackIndices:count':0},
        setup={'InBattle':True,'CombatPhase':'Melee',**resources},
        error='InvalidOperationException: '+('白昼不能施放强效法术' if failure==2 else '法力不足，不能支付所选效果'))
    CASES[-1]['playFromHand']=True

operation_case('magic_022',False,'爆裂护盾已消灭的目标不能再施放飓风；拒绝保留费用和真实手牌',
    {'hand':6,'discard':4,'cards:played':0,'Fame':3,'Wounds':3,'handWounds':3,
     'token:White':0,'token:Black':0,'GamePhase':'End','InBattle':False,'DefeatedEnemyIndices:count':1,
     'operation:1:kills':1,'operation:1:defeated':'0',
     'operation:0:success':True,
     'operation:2:success':False,'operation:2:exception':'目标已经被击败','operation:2:white':1,'operation:2:black':1,
     'operation:2:totalFame':3},
    [dict(kind='PlayCard',cardId='magic_008',enhanced=True),dict(kind='TurnDefense'),
     dict(kind='PlayCard',cardId='magic_022',enhanced=True),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    setup={'DayPart':'Night'},turnMachineStartPhase='Block',playFromHand=True,extraHandCards=['magic_008','magic_022'],option=1,
    enemies=[enemy(9,2),enemy(4)])

operation_case('magic_022',False,'旋风后焚毁公正之剑：飓风和普通狂怒各击败一敌、名望各加一次',
    {'hand':4,'discard':5,'cards:played':0,'cards:removed':1,'Fame':8,'Wounds':3,'handWounds':3,
     'token:White':0,'token:Black':0,'GamePhase':'End','InBattle':False,'DefeatedEnemyIndices:count':2,
     'operation:1:wounds':0,'operation:2:meleeRemaining':2,'operation:3:meleeRemaining':2,
     'operation:3:defeated':'0','operation:3:totalFame':4,
     'operation:0:success':True,'operation:2:success':True,'operation:3:success':True,
     'operation:4:kills':1,'operation:4:defeated':'0,1','operation:4:totalFame':8},
    [dict(kind='PlayCard',cardId='items_008',enhanced=True),dict(kind='TurnDefense'),
     dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='PlayCard',cardId='magic_022',enhanced=True),
     dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    setup={'DayPart':'Night'},turnMachineStartPhase='Block',playFromHand=True,extraHandCards=['items_008','magic_022'],
    discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(99,4),enemy(4)])

# Demolish ignores only site fortification. Disintegrate is a separate attack-phase effect.
for mode in range(3):
    attack_card='advanced_card_003' if mode==1 else 'basic_card_007'
    expected={'hand':5,'discard':4,'cards:played':0,'Fame':0 if mode==2 else 3,
              'token:Red':0,'IgnoreFortified':False,'IgnoreSiteFortifications':False,
              'DefeatedEnemyIndices:count':0 if mode==2 else 1,
              'operation:0:siteIgnored':True,'operation:0:enemyArmor':'3' if mode==0 else '2',
              'operation:1:success':mode!=2,'operation:5:siteIgnored':False}
    if mode==2:
        expected.update({'operation:1:exception':'城防限制：当前攻击不能指定这些目标','operation:1:rangedRemaining':3})
    else:
        expected.update({'operation:1:kills':1,'operation:1:required':3 if mode==0 else 2})
    operation_case('magic_007',False,'地点城防消失但怪物城防保留；原卡远程/攻城 '+str(mode),expected,
        [dict(kind='PlayCard',cardId=attack_card,enhanced=True),dict(kind='RangedAttack'),
         dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'FortifiedSite':True},turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=[attack_card],
        discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(3,abilities=['FireResist'] if mode==0 else ['Fortified'])])

for day in ['Day','Night']:
    operation_case('magic_007',False,'两次拆毁叠加、护甲最低1、红色免疫与回合结束恢复 '+day,
        {'hand':5,'discard':4,'cards:played':0,'Fame':0,'token:Red':0,'IgnoreSiteFortifications':False,
         'ArmorReduction:count':0,'operation:0:enemyArmor':'1,1,5,5,3','operation:0:red':0,
         'operation:3:enemyArmor':'1,1,5,5,3','operation:3:siteIgnored':True,
         'operation:4:enemyArmor':'3,1,5,5,5','operation:4:siteIgnored':False},
        [dict(kind='PlayCard',cardId='magic_007'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),
         dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'DayPart':day,'FortifiedSite':True},turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_007'],
        enemies=[enemy(3),enemy(1),enemy(5,abilities=['FireResist']),enemy(5,abilities=['MagicResist']),enemy(5,abilities=['IceResist'])])

operation_case('magic_007',True,'解离实际消灭并减弱其他敌人，狂怒完成击杀，重复目标拒绝且不再扣费',
    {'hand':7,'discard':4,'cards:played':0,'Fame':6,'Wounds':5,'handWounds':5,'token:Red':0,'token:Black':0,
     'IgnoreFortified':False,'IgnoreSiteFortifications':False,'DefeatedEnemyIndices:count':2,
     'operation:0:enemyArmor':'99,2,3,3','operation:0:defeated':'0','operation:0:totalFame':3,
     'operation:1:success':False,'operation:1:exception':'目标已经被击败','operation:1:red':1,'operation:1:black':1,
     'operation:2:kills':1,'operation:2:effective':2,'operation:2:totalFame':6},
    [dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='PlayCard',cardId='magic_007',enhanced=True),
     dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    setup={'FortifiedSite':True},turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['magic_007'],
    discardIndex=-1,removeIndex=-1,discardIndices=[],
    enemies=[enemy(99,4,abilities=['Fortified','IceResist']),enemy(3),enemy(3,abilities=['FireResist']),enemy(3,abilities=['MagicResist'])])

for failure in [-1,1,2,3,4]:
    reason='火焰抗性或奥术免疫敌人不能成为解离目标' if failure in [2,3] else '解离必须选择本场战斗中的有效敌人'
    case('magic_007',True,'非法目标不能消灭、减甲或支付费用 '+str(failure),
        {'cards:played':0,'DefeatedEnemyIndices:count':0,'ArmorReduction:count':0,'IgnoreSiteFortifications':False},
        option=failure if failure<2 else 0,setup={'InBattle':failure!=4,'CombatPhase':'Melee'},
        error='InvalidOperationException: '+reason)
    CASES[-1].update(playFromHand=True,enemies=[enemy(5,abilities=['MagicResist'] if failure==2 else ['FireResist'] if failure==3 else [])])

for phase in ['None','Ranged','Block','AssignDamage','End']:
    case('magic_007',True,'解离不能在攻击阶段以外打出 '+phase,
        {'cards:played':0,'DefeatedEnemyIndices:count':0,'ArmorReduction:count':0,'IgnoreSiteFortifications':False},
        setup={'InBattle':True,'CombatPhase':phase},error='InvalidOperationException: 解离只能在攻击阶段打出')
    CASES[-1].update(playFromHand=True,enemies=[enemy(5)])

for strong,failure in [(False,0),(True,0),(True,1),(True,2)]:
    setup={'InBattle':True,'CombatPhase':'Melee','DayPart':'Day' if failure==2 else 'Night'}
    if failure<2: setup['token:Red' if failure==0 else 'token:Black']=0
    if failure==0: setup['crystal:Red']=0
    if failure==1: setup['token:Red']=0  # available red crystal must survive missing black mana
    case('magic_007',strong,'合法目标仍须原费用与昼夜限制 '+str(failure),
        {'cards:played':0,'DefeatedEnemyIndices:count':0,'ArmorReduction:count':0,'IgnoreSiteFortifications':False},
        setup=setup,error='InvalidOperationException: '+('白昼不能施放强效法术' if failure==2 else '法力不足，不能支付所选效果'))
    CASES[-1].update(playFromHand=True,enemies=[enemy(5)])

operation_case('magic_007',False,'原版冰冻去除火抗后才允许解离；另一火抗敌人不减甲',
    {'hand':5,'discard':5,'cards:played':0,'Fame':3,'token:Red':0,'token:Blue':0,'token:Black':0,
     'DefeatedEnemyIndices:count':1,'IgnoreSiteFortifications':False,
     'operation:0:fireRemoved':1,'operation:0:enemyArmor':'9,3',
     'operation:1:success':True,'operation:1:defeated':'0','operation:1:enemyArmor':'9,3',
     'operation:1:red':0,'operation:1:black':1,'operation:1:totalFame':3},
    [dict(kind='PlayCard',cardId='magic_013'),dict(kind='PlayCard',cardId='magic_007',enhanced=True),
     dict(kind='TurnFinish'),dict(kind='EndTurn')],
    setup={'DayPart':'Night'},turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['magic_013','magic_007'],
    enemies=[enemy(9,abilities=['FireResist']),enemy(3,abilities=['FireResist'])])

for strong in [False,True]:
    operation_case('magic_007',strong,'没有红魔力时可以合法消耗一颗红魔晶；整笔支付后才生效',
        {'hand':5,'discard':3,'cards:played':0,'Fame':3 if strong else 0,'token:Red':0,'token:Black':0,'crystal:Red':0,
         'DefeatedEnemyIndices:count':1 if strong else 0,
         'operation:0:enemyArmor':'5' if strong else '4','operation:0:red':0,'operation:0:black':1 if strong else 2,
         'operation:0:totalFame':3 if strong else 0},
        [dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'token:Red':0},turnMachineStartPhase='Melee',playFromHand=True,enemies=[enemy(5)])

for strong in [False,True]:
    for fire_too in [False,True]:
        case('magic_013',strong,'冰抗阻止蓝色法术，复合火冰抗亦同 '+str(fire_too),
            {'cards:played':0,'SkipAttackIndices:count':0,'FireResistRemoved:count':0,'ArmorReduction:count':0},
            setup={'InBattle':True},error='InvalidOperationException: 冰霜抗性敌人不能成为冰冻目标')
        CASES[-1].update(playFromHand=True,enemies=[enemy(6,4,abilities=['IceResist','FireResist'] if fire_too else ['IceResist'])])
    for destroyed in [False,True]:
        operations=[dict(kind='PlayCard',cardId='magic_022' if destroyed else 'basic_card_021',enhanced=destroyed)]
        if not destroyed: operations.append(dict(kind='TurnAttack'))
        reject=len(operations)
        operations += [dict(kind='PlayCard',cardId='magic_013',enhanced=strong),dict(kind='TurnFinish'),dict(kind='EndTurn')]
        operation_case('magic_013',strong,'已由'+('飓风消灭' if destroyed else '狂怒击败')+'的敌人不能再冰冻；另一目标效果保留',
            {'hand':6 if destroyed else 5,'discard':4,'cards:played':0,'Fame':3,'Wounds':3,'token:Blue':0,'token:Black':0,
             'DefeatedEnemyIndices:count':1,'ArmorReduction:count':0,'FireResistRemoved:count':0,
             f'operation:{reject}:success':False,f'operation:{reject}:exception':'目标已经被击败',
             f'operation:{reject}:blue':1,f'operation:{reject}:black':2-int(strong)-int(destroyed),
             f'operation:{reject}:defeated':'0',f'operation:{reject}:skip':'1',
             f'operation:{reject}:enemyArmor':'2,2' if strong else '2,6',f'operation:{reject}:fireRemoved':0 if strong else 1},
            operations,setup={'DayPart':'Night'},option=1,turnMachineStartPhase='Melee',playFromHand=True,
            extraHandCards=['magic_013']+(['magic_022'] if destroyed else []),discardIndex=-1,removeIndex=-1,discardIndices=[],
            enemies=[enemy(2),enemy(6,abilities=['FireResist'])])
    operation_case('magic_013',strong,'伤害结算后仍可施法；不撤销伤牌，原攻击完成击杀',
        {'hand':6 if strong else 7,'discard':4,'cards:played':0,'Fame':3,'Wounds':5,'handWounds':5,
         'token:Blue':0,'token:Black':0,'DefeatedEnemyIndices:count':1,
         'operation:0:fireRemoved':0 if strong else 1,'operation:0:enemyArmor':'2' if strong else '5',
         'operation:1:kills':1,'operation:1:effective':2 if strong else 5,'operation:1:totalFame':3},
        [dict(kind='PlayCard',cardId='basic_card_021' if strong else 'magic_009'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=[] if strong else ['magic_009'],
        discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(6 if strong else 5,4,abilities=['FireResist'])])

operation_case('magic_013',True,'两次致命冰冻减甲叠加至最低1，保持火抗并阻止本场攻击',
    {'hand':4,'discard':5,'cards:played':0,'Fame':3,'Wounds':3,'token:Blue':0,'token:Black':0,'ArmorReduction:count':0,
     'operation:0:enemyArmor':'1','operation:0:blue':0,'operation:0:black':0,'operation:0:fireRemoved':0,
     'operation:1:wounds':0,'operation:3:kills':1,'operation:3:effective':2},
    [dict(kind='PlayCard',cardId='magic_013',enhanced=True),dict(kind='TurnDefense'),
     dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    turnMachineStartPhase='Block',playFromHand=True,extraHandCards=['magic_013'],discardIndex=-1,removeIndex=-1,discardIndices=[],
    enemies=[enemy(6,4,abilities=['FireResist'])])

for strong,mode in [(False,0),(True,0),(True,1),(True,2)]:
    setup={'InBattle':True,'DayPart':'Day' if mode==2 else 'Night'}
    if mode!=2: setup['token:Blue']=0
    if mode==0: setup['crystal:Blue']=0
    if mode==1: setup['token:Black']=0
    case('magic_013',strong,'蓝色法术费用和昼夜条件逐项拒绝 '+str(mode),
        {'cards:played':0,'SkipAttackIndices:count':0,'FireResistRemoved:count':0,'ArmorReduction:count':0},
        setup=setup,error='InvalidOperationException: '+('白昼不能施放强效法术' if mode==2 else '法力不足，不能支付所选效果'))
    CASES[-1].update(playFromHand=True,enemies=[enemy(6)])

for strong in [False,True]:
    operation_case('magic_013',strong,'合法蓝魔晶支付，基础与强化不同效果和黑魔力费用',
        {'hand':5,'discard':3,'cards:played':0,'Fame':0,'Wounds':3,'token:Blue':0,'token:Black':0,'crystal:Blue':0,
         'operation:0:wounds':0,'operation:0:enemyArmor':'2' if strong else '6','operation:0:blue':0,
         'operation:0:black':1 if strong else 2,'operation:0:fireRemoved':0 if strong else 1},
        [dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'token:Blue':0},turnMachineStartPhase='Block',playFromHand=True,enemies=[enemy(6,4,abilities=['FireResist'])])

# Expose: per-enemy changes, mutually exclusive mass choices, combat lifetime.
for target in [0,1]:
    operation_case('magic_019',False,'暴露只改变所选敌人；先攻击另一敌人被拒绝，再击杀合法目标 '+str(target),
        {'hand':5,'discard':3,'cards:played':0,'Fame':3,'token:White':0,'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,
         'IgnoreFortified':False,'IgnoreResist':False,'operation:0:success':False,'operation:0:exception':'城防限制：当前攻击不能指定这些目标',
         'operation:0:rangedRemaining':2,'operation:0:fortRemoved':str(target),'operation:0:resRemoved':str(target),
         'operation:1:kills':1,'operation:1:effective':2,'operation:1:totalFame':3},
        [dict(kind='RangedAttack',targetIndex=1-target),dict(kind='RangedAttack',targetIndex=target),
         dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'FortifiedSite':True},option=target,turnMachineStartPhase='Ranged',playFromHand=True,
        enemies=[enemy(2,abilities=['Fortified','PhysicalResist']),enemy(2,abilities=['Fortified','PhysicalResist'])])

for option in [0,1]:
    expected={'hand':4,'discard':4,'cards:played':0,'Fame':3 if option==1 else 0,'token:White':0,'token:Black':0,
              'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,'IgnoreFortified':False,'IgnoreResist':False,
              'operation:0:fortRemoved':'0' if option==0 else '', 'operation:0:resRemoved':'0' if option==1 else '',
              'operation:4:kills':1 if option==1 else 0,'operation:4:effective':2 if option==1 else 1}
    if option==0: expected.update({'operation:0:kills':0,'operation:0:effective':1})
    else: expected.update({'operation:0:success':False,'operation:0:exception':'城防限制：当前攻击不能指定这些目标','operation:0:rangedRemaining':3})
    operation_case('magic_019',True,'群体暴露二选一且奥术免疫保持防护 '+('去城防' if option==0 else '去抗性'),expected,
        [dict(kind='RangedAttack'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='PlayCard',cardId='basic_card_021'),
         dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'FortifiedSite':True},option=option,turnMachineStartPhase='Ranged',playFromHand=True,
        discardIndex=-1,removeIndex=-1,discardIndices=[],
        enemies=[enemy(2,abilities=['Fortified','PhysicalResist']),enemy(2,abilities=['Fortified','PhysicalResist','MagicResist'])])

for fortified in [False,True]:
    expected={'hand':5,'discard':3,'cards:played':0,'Fame':0 if fortified else 3,'token:White':0,
              'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,'operation:0:fortRemoved':'','operation:0:resRemoved':''}
    if fortified: expected.update({'operation:0:success':False,'operation:0:exception':'城防限制：当前攻击不能指定这些目标','operation:0:rangedRemaining':2})
    else: expected.update({'operation:0:effective':1,'operation:0:kills':1})
    operation_case('magic_019',False,'奥术免疫阻挡敌人变化，但独立远程2仍可使用；城防 '+str(fortified),expected,
        [dict(kind='RangedAttack'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        turnMachineStartPhase='Ranged',playFromHand=True,
        enemies=[enemy(1,abilities=['MagicResist','PhysicalResist']+(['Fortified'] if fortified else []))])

for strong in [False,True]:
    operation_case('magic_019',strong,'暴露去火冰抗后，原版冰冻与解离可依法生效',
        {'hand':5,'discard':5,'cards:played':0,'Fame':3,'token:White':0,'token:Black':0,'token:Blue':0,'token:Red':0,
         'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,'DefeatedEnemyIndices:count':1,
         'operation:2:success':True,'operation:2:resRemoved':'0','operation:2:fireRemoved':1,
         'operation:3:success':True,'operation:3:defeated':'0','operation:3:black':0 if strong else 1,'operation:3:totalFame':3,
         'operation:4:resRemoved':''},
        [dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='PlayCard',cardId='magic_013'),
         dict(kind='PlayCard',cardId='magic_007',enhanced=True),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'DayPart':'Night'},option=1 if strong else 0,turnMachineStartPhase='Ranged',playFromHand=True,
        extraHandCards=['magic_013','magic_007'],enemies=[enemy(9,abilities=['FireResist','IceResist'])])

for killed in [0,2]:
    target=0 if killed==0 else 1
    operation_case('magic_019',False,'远程杀敌重排后，暴露仍属于原敌人 '+str(killed),
        {'hand':4,'discard':4,'cards:played':0,'Fame':6,'token:White':0,
         'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,'operation:0:kills':1,
         'operation:0:fortRemoved':str(target),'operation:0:resRemoved':str(target),'operation:3:kills':1,'operation:3:effective':2},
        [dict(kind='TurnRanged',targetIndex=killed),dict(kind='TurnDefense'),dict(kind='PlayCard',cardId='basic_card_021'),
         dict(kind='TurnAttack',targetIndex=target),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=1,turnMachineStartPhase='Ranged',playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
        enemies=[enemy(1),enemy(2,abilities=['Fortified','PhysicalResist']),enemy(1)])

for mode in [-1,0,1]:
    operation_case('magic_019',mode>=0,'本场结束即恢复城防与抗性；下一场冰冻和迅捷远攻均受阻 '+str(mode),
        {'hand':6,'discard':4,'cards:played':0,'Fame':0,'token:White':0,'token:Black':0,
         'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,'operation:2:fortRemoved':'','operation:2:resRemoved':'',
         'operation:4:success':False,'operation:4:exception':'冰霜抗性敌人不能成为冰冻目标','operation:4:blue':2,
         'operation:6:success':False,'operation:6:exception':'城防限制：当前攻击不能指定这些目标','operation:6:rangedRemaining':3},
        [dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='TurnBegin'),
         dict(kind='PlayCard',cardId='magic_013'),dict(kind='PlayCard',cardId='basic_card_007',enhanced=True),dict(kind='RangedAttack'),
         dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'FortifiedSite':True},option=max(0,mode),turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_013','basic_card_007'],
        enemies=[enemy(9,abilities=['Fortified','PhysicalResist','FireResist','IceResist'])])

for strong,option in [(False,-1),(False,1),(True,-1),(True,2)]:
    case('magic_019',strong,'非法目标或强化选择拒绝，不给远程也不支付费用 '+str(option),
        {'cards:played':0,'FortificationsRemoved:count':0,'ResistancesRemoved:count':0},option=option,
        setup={'InBattle':True},error='InvalidOperationException: '+('群体暴露必须选择去城防或去抗性' if strong else '暴露必须选择本场战斗中的有效敌人'))
    CASES[-1].update(playFromHand=True,enemies=[enemy(3)])

operation_case('magic_019',False,'正式击败的目标不能再次暴露，另一个敌人的暴露状态保留',
    {'hand':5,'discard':4,'cards:played':0,'Fame':3,'token:White':0,'FortificationsRemoved:count':0,'ResistancesRemoved:count':0,
     'operation:4:success':False,'operation:4:exception':'目标已经被击败','operation:4:white':1,'operation:4:rangedRemaining':0,
     'operation:4:resRemoved':'1','operation:4:fortRemoved':'1'},
    [dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='PlayCard',cardId='basic_card_021'),
     dict(kind='TurnAttack'),dict(kind='PlayCard',cardId='magic_019'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    option=1,turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_019'],discardIndex=-1,removeIndex=-1,discardIndices=[],
    enemies=[enemy(2),enemy(4,abilities=['PhysicalResist'])])

for strong in [False,True]:
    case('magic_019',strong,'战斗外不能执行战斗法术',{'cards:played':0,'FortificationsRemoved:count':0,'ResistancesRemoved:count':0},
        setup={'InBattle':False},error='InvalidOperationException: 暴露必须在战斗中施放')
    CASES[-1].update(playFromHand=True,enemies=[enemy(3)])

for strong in [False,True]:
    for all_targets in [False,True]:
        armor=(6 if all_targets else 8) if strong else (4 if all_targets else 5)
        other=4 if strong else 6
        operation_case('magic_003',strong,'地点城防与单体/全体减甲，原版狂怒实际击杀 '+str(all_targets),
            {'hand':4,'discard':4,'cards:played':0,'Fame':3,'token:Green':0,'token:Black':0,'ArmorReduction:count':0,
             'operation:0:enemyArmor':f'2,{other if all_targets else 8},8','operation:1:kills':1,'operation:1:effective':2,
             'operation:2:enemyArmor':f'2,{other if all_targets else 8},8','operation:3:enemyArmor':f'{armor},8,8'},
            [dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
            setup={'FortifiedSite':True},option=-1 if all_targets else 0,turnMachineStartPhase='Melee',playFromHand=True,
            discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(armor),enemy(8,abilities=['Fortified']),enemy(8,abilities=['MagicResist'])])
    operation_case('magic_003',strong,'全体模式逐敌判断：自身城防、普通、火冰抗和奥术免疫',
        {'hand':4,'discard':4,'cards:played':0,'Fame':3 if strong else 0,'token:Green':0,'token:Black':0,'token:Red':0,
         'operation:0:enemyArmor':f'6,{4 if strong else 6},6,8','operation:1:kills':1 if strong else 0,'operation:1:effective':4},
        [dict(kind='PlayCard',cardId='basic_card_021',enhanced=True),dict(kind='TurnAttack',targetIndex=1),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=-1,turnMachineStartPhase='Melee',playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
        enemies=[enemy(8),enemy(8,abilities=['Fortified']),enemy(8,abilities=['FireResist','IceResist']),enemy(8,abilities=['MagicResist'])])
    operation_case('magic_003',strong,'两次震颤累加到护甲最低1，费用各付一次并实际击杀',
        {'hand':4,'discard':5,'cards:played':0,'Fame':3,'token:Green':0,'token:Black':0,
         'operation:0:enemyArmor':'1','operation:0:green':0,'operation:0:black':0 if strong else 2,
         'operation:2:kills':1,'operation:2:effective':2},
        [dict(kind='PlayCard',cardId='magic_003',enhanced=strong),dict(kind='PlayCard',cardId='basic_card_021'),
         dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['magic_003'],discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(5)])

for demolish_first in [False,True]:
    for intrinsic in [False,True]:
        armor=6 if demolish_first and not intrinsic else 3
        operation_case('magic_007' if demolish_first else 'magic_003',not demolish_first,
            '拆毁与地震出牌顺序，怪物自身城防 '+str(intrinsic)+'；拆毁先打 '+str(demolish_first),
            {'hand':4,'discard':5,'cards:played':0,'Fame':3 if armor==3 else 0,'token:Green':0,'token:Black':0,'token:Red':0,
             'operation:0:enemyArmor':str(armor),'operation:0:green':1,'operation:0:red':1,'operation:0:black':1,
             'operation:2:kills':1 if armor==3 else 0,'operation:2:effective':4},
            [dict(kind='PlayCard',cardId='magic_003' if demolish_first else 'magic_007',enhanced=demolish_first),
             dict(kind='PlayCard',cardId='basic_card_021',enhanced=True),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
            setup={'FortifiedSite':True,'DayPart':'Night'},turnMachineStartPhase='Melee',playFromHand=True,
            extraHandCards=['magic_003' if demolish_first else 'magic_007'],discardIndex=-1,removeIndex=-1,discardIndices=[],
            enemies=[enemy(10,abilities=['Fortified'] if intrinsic else [])])

for all_targets in [False,True]:
    operation_case('magic_019',False,'先暴露只去一敌城防，地震按现有防护减甲，火球实际结算 '+str(all_targets),
        {'hand':5,'discard':5,'cards:played':0,'Fame':0 if all_targets else 3,
         'token:White':0,'token:Green':0,'token:Black':0,'token:Red':0,'ArmorReduction:count':0,
         'operation:2:enemyArmor':'6,4' if all_targets else '5,8','operation:4:kills':0 if all_targets else 1,'operation:4:effective':5},
        [dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='PlayCard',cardId='magic_003',enhanced=True,option=-1 if all_targets else 0),
         dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnAttack'),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        setup={'FortifiedSite':True,'DayPart':'Night'},turnMachineStartPhase='Ranged',playFromHand=True,extraHandCards=['magic_003','magic_009'],
        enemies=[enemy(8,abilities=['Fortified']),enemy(8,abilities=['Fortified'])])

for strong in [False,True]:
    operation_case('magic_003',strong,'全体模式跳过已败目标，单体再次指定已败目标拒绝且保留费用',
        {'hand':5,'discard':5,'cards:played':0,'Fame':3,'token:Green':0,'token:Black':0,'ArmorReduction:count':0,
         'operation:2:enemyArmor':'2,1','operation:2:defeated':'0','operation:3:success':False,
         'operation:3:exception':'目标已经被击败','operation:3:green':0,'operation:3:black':0 if strong else 2},
        [dict(kind='PlayCard',cardId='basic_card_021'),dict(kind='TurnAttack'),dict(kind='PlayCard',cardId='magic_003',enhanced=strong,option=-1),
         dict(kind='PlayCard',cardId='magic_003',enhanced=strong),dict(kind='TurnFinish'),dict(kind='EndTurn')],
        option=1,turnMachineStartPhase='Melee',playFromHand=True,extraHandCards=['magic_003','magic_003'],
        discardIndex=-1,removeIndex=-1,discardIndices=[],enemies=[enemy(2),enemy(6)])
    for failure in [-2,1,2,3]:
        reason='震颤必须在战斗中施放' if failure==3 else '奥术免疫敌人不能成为震颤目标' if failure==2 else '震颤必须选择有效敌人或全体模式'
        case('magic_003',strong,'非法目标、全体编码或战斗条件拒绝 '+str(failure),
            {'cards:played':0,'ArmorReduction:count':0},option=failure if failure<2 else 0,
            setup={'InBattle':failure!=3},error='InvalidOperationException: '+reason)
        CASES[-1].update(playFromHand=True,enemies=[enemy(8,abilities=['MagicResist'] if failure==2 else [])])

for strong,mode in [(False,0),(True,0),(True,1),(True,2)]:
    setup={'InBattle':True,'DayPart':'Day' if mode==2 else 'Night'}
    if mode!=2: setup['token:Green']=0
    if mode==0: setup['crystal:Green']=0
    if mode==1: setup['token:Black']=0
    case('magic_003',strong,'绿色法术费用与昼夜条件守恒 '+str(mode),{'cards:played':0,'ArmorReduction:count':0},
        setup=setup,error='InvalidOperationException: '+('白昼不能施放强效法术' if mode==2 else '法力不足，不能支付所选效果'))
    CASES[-1].update(playFromHand=True,enemies=[enemy(5)])

for strong in [False,True]:
    operation_case('magic_003',strong,'合法绿魔晶支付仍遵守原减甲数值及强化黑魔力费用',
        {'hand':5,'discard':3,'cards:played':0,'Fame':0,'token:Green':0,'token:Black':0,'crystal:Green':0,'ArmorReduction:count':0,
         'operation:0:enemyArmor':'2','operation:0:green':0,'operation:0:black':1 if strong else 2},
        [dict(kind='TurnFinish'),dict(kind='EndTurn')],setup={'token:Green':0},turnMachineStartPhase='Melee',playFromHand=True,enemies=[enemy(5)])

def write_json(path, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    if path.exists() and path.read_bytes() == data:
        return
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes(data)
    os.replace(temporary, path)


# Meditation/Trance: exact discard instances, destination order, real refill and one-use bonus.
meditation_start = len(CASES)
def meditation(strong,title,expected,ops=(),option=0,**fields):
    operation_case('magic_002',strong,title,expected,list(ops),option=option,playFromHand=True,**fields)
    CASES[-1]['limitations']=['本例回收、费用、卡区与补牌已联验；其他卡组合与完整游戏仍待整体验收']

for strong in [False,True]:
    for bottom in [False,True]:
        old=['basic_card_000','basic_card_014','basic_card_021','basic_card_001','basic_card_004','basic_card_005','basic_card_007','basic_card_008']
        picked=['basic_card_008','w']
        order=old+picked if bottom else picked+old
        # Two unplayed hand cards remain; the first refill draws five, leaving five.
        first=order[:5]; remaining=order[5:]; wounds=first.count('w')
        second=remaining[5-wounds:]
        meditation(strong,'置'+('底' if bottom else '顶')+'不改原牌序，真实补到7后仅再补到正常5',
            {'played:draw:ids':','.join(order),'played:NextDrawBonus':2,'played:cards:played':1,
             'played:token:Green':1,'played:token:Black':1 if strong else 2,'played:discard:ids':'basic_card_016',
             'operation:0:hand':7,'operation:0:draw':5,'operation:0:bonus':0,'operation:0:discard':2,
             'operation:1:hand':wounds,'operation:1:discard':9-wounds,
             'operation:2:hand':5,'operation:2:bonus':0,'operation:2:draw':wounds,
             'hand':5,'draw':wounds,'draw:ids':','.join(second),'NextDrawBonus':0,'cards:played':0,
             'cards:total':14,'cards:uniqueInstances':14,'token:Green':0,**({'token:Black':0} if strong else {})},
            [dict(kind='EndTurn',refill=True),dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
            option=int(bottom),initialHandCount=3,initialDrawCount=0,initialDrawCards=old,
            initialDiscardCards=['basic_card_008','w','basic_card_016'],recoverDiscardIndices=[2,1],manaRolls=[2,1])
    for count in [0,1]:
        meditation(strong,'弃牌不足'+str(count)+'张仍保留一次加成，跳过补牌后再实际补牌',
            {'operation:0:bonus':2,'operation:0:hand':0,'operation:0:discard':1,
             'operation:1:bonus':0,'operation:1:hand':3+count,'hand':3+count,'draw':0,'draw:ids':'',
             'discard':1,'discard:ids':'magic_002','cards:played':0,'NextDrawBonus':0,
             'cards:total':4+count,'cards:uniqueInstances':4+count,'token:Green':0,**({'token:Black':0} if strong else {})},
            [dict(kind='EndTurn'),dict(kind='EndTurn',refill=True)],option=1,initialHandCount=1,initialDrawCount=0,
            initialDrawCards=['basic_card_000','basic_card_014','basic_card_021'],
            initialDiscardCards=['basic_card_008'][:count],manaRolls=[0])
    for option in [-1,2]:
        case('magic_002',strong,'非法牌库目的地在支付前拒绝 '+str(option),
            {'NextDrawBonus':0,'draw':10,'cards:played':0,'cards:total':18,'cards:uniqueInstances':18},option=option,
            error='InvalidOperationException: 冥想必须选择牌库顶或牌库底')
        CASES[-1]['playFromHand']=True

for indices,foreign,message in [([0,1],True,'神游必须选择不同的实际弃牌实例'),
                               ([0,0],False,'神游必须选择不同的实际弃牌实例'),
                               ([0],False,'神游必须选择两张弃牌，不足时选择全部弃牌'),
                               ([0,1,0],False,'神游必须选择两张弃牌，不足时选择全部弃牌'),
                               ([-1,1],False,'神游必须选择不同的实际弃牌实例')]:
    case('magic_002',True,'拒绝外来/重复/不足/过多/手牌冒充弃牌 '+str(indices)+' 外来'+str(foreign),
         {'NextDrawBonus':0,'draw':10,'cards:played':0,'cards:total':18,'cards:uniqueInstances':18},error='InvalidOperationException: '+message)
    CASES[-1].update(playFromHand=True,recoverDiscardIndices=indices,foreignRecovery=foreign)

meditation(True,'同ID弃牌只回收所选实例，不复制或替换另一张',
    {'played:draw:ids':'basic_card_008,basic_card_016,basic_card_000,basic_card_014,basic_card_021',
     'played:discard:ids':'basic_card_008','played:cards:uniqueInstances':9,'cards:uniqueInstances':9,'cards:total':9,
     'discard:ids':'magic_002,basic_card_008','draw':5,'hand':2,'cards:played':0,'NextDrawBonus':2,'token:Green':0,'token:Black':0},
    [dict(kind='EndTurn')],initialHandCount=3,initialDrawCount=0,
    initialDrawCards=['basic_card_000','basic_card_014','basic_card_021'],
    initialDiscardCards=['basic_card_008','basic_card_008','basic_card_016'],recoverDiscardIndices=[2,0])

for strong,mode in [(False,0),(True,0),(True,1),(True,2),(False,3),(True,3)]:
    setup={'token:Green':0,'crystal:Green':int(mode in [1,3])}
    if mode==1: setup['token:Black']=0
    if mode==2: setup.update({'token:Green':2,'DayPart':'Day'})
    if mode==3:
        meditation(strong,'绿魔晶支付并回收，真实回合结束只消费一次加成',
            {'hand':7,'draw':10,'discard':1,'cards:played':0,'NextDrawBonus':0,'crystal:Green':0,
             'token:Green':0,**({'token:Black':0} if strong else {}),'cards:total':18,'cards:uniqueInstances':18,
             'operation:0:hand':7,'operation:0:bonus':0},[dict(kind='EndTurn',refill=True)],setup=setup,manaRolls=[0,0])
    else:
        case('magic_002',strong,'缺色/缺黑/白昼拒绝，原弃牌与费用均保持 '+str(mode),
             {'NextDrawBonus':0,'draw':10,'cards:played':0,'cards:total':18,'cards:uniqueInstances':18},setup=setup,
             error='InvalidOperationException: '+('白昼不能施放强效法术' if mode==2 else '法力不足，不能支付所选效果'))
        CASES[-1]['playFromHand']=True

# Underground Travel/Attack: real revealed path, assault engine and return to the recorded origin.
def tunnel(strong,title,expected,ops=(),path=None,setup=None,**fields):
    operation_case('magic_004',strong,title,expected,list(ops),setup={'MovementPool':5,'DayPart':'Night',**(setup or {})},
                   playFromHand=True,revealedDistance=4,travelPath=path if path is not None else [dict(q=q) for q in [1,2,3]],**fields)
    CASES[-1]['limitations']=['本例地道与单守军地点结算；多守军/合作城市、玩家对战、败退后重新抽守军及完整冒险整合仍待联验']

for distance in range(4):
    tunnel(False,'免费地道'+str(distance)+'格后仍可正常步行，夜间森林实际支付5',
           {'played:position:q':distance,'played:MovementPool':5,'played:token:Green':1,'position:q':distance+1,
            'operation:0:success':True,'operation:0:q':distance+1,'MovementPool':0,'hand':5,'discard':3,'cards:played':0,
            'token:Green':0,'TeleportRange':0,'IgnoreRampaging':False,'Wounds':3},
           [dict(kind='Move',q=distance+1),dict(kind='EndTurn')],path=[dict(q=q) for q in range(1,distance+1)],
           mapTerrains=[dict(q=1,terrain='Mountain')] if distance==3 else [],
           mapPlaces=[dict(q=2,type='OrcRampaging',enemy=True)] if distance==3 else [])

for strong in [False,True]:
    for reason in range(5):
        path=[dict(q=q) for q in [1,2,3]]
        terrain=[];revealed=4
        if reason<2: terrain=[dict(q=2,terrain='Swamp' if reason==0 else 'Lake')]
        if reason==2:path=[dict(q=2),dict(q=3)]
        if reason==3:revealed=1
        if reason==4:path.append(dict(q=4))
        message='地道不能进入或经过沼泽与湖泊' if reason<2 else '地道穿行必须选择至多三格路径' if reason==4 else '地道路径必须逐格相邻且已揭示'
        case('magic_004',strong,'非法路径拒绝并保留所有资源 '+str(reason),
             {'MovementPool':5,'position:q':0,'cards:played':0,'TunnelAssaultActive':False},setup={'MovementPool':5},error='InvalidOperationException: '+message)
        CASES[-1].update(playFromHand=True,revealedDistance=revealed,travelPath=path,mapTerrains=terrain,
                        mapPlaces=[dict(q=3,type='Keep',enemy=True)] if strong else [])

for kind in ['Keep','City','MageTower']:
    case('magic_004',False,'基础不得落在仍有守军的 '+kind,{'position:q':0,'cards:played':0},error='InvalidOperationException: 地道穿行必须结束在安全格')
    CASES[-1].update(playFromHand=True,revealedDistance=3,travelPath=[dict(q=q) for q in [1,2,3]],mapPlaces=[dict(q=3,type=kind,enemy=True)])

for kind,intrinsic in [('Keep',False),('Keep',True),('City',False),('MageTower',False)]:
    expected={'played:position:q':3,'played:MovementPool':5,'played:IgnoreSiteFortifications':True,'played:IgnoreFortified':False,
              'played:token:Green':1,'played:token:Black':1,
              'operation:0:success':False,'operation:3:success':not intrinsic,
              'operation:5:handWounds':5 if intrinsic else 3,
              'operation:7:success':True,'operation:7:q':0 if intrinsic else 3,'operation:7:siteIgnored':False,
              'operation:8:success':False,'position:q':0 if intrinsic else 3,'Fame':0 if intrinsic else 3,
              'Wounds':5 if intrinsic else 3,'hand':7 if intrinsic else 5,'discard':4,'cards:played':0,
              'MovementPool':0,'token:Green':0,'token:Black':0,'token:Red':0,'TunnelAssaultActive':False,'ReturnAfterRetreat':False,'IgnoreSiteFortifications':False}
    if not intrinsic:expected['operation:3:kills']=1
    tunnel(True,'真实突袭'+kind+'，自身城防'+str(intrinsic)+'；败退原点/击杀留位后结束回合',expected,
           [dict(kind='FinishTunnel',option=1),dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='RangedAttack'),
            dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=int(intrinsic)),
            dict(kind='Move',q=1 if intrinsic else 4),dict(kind='EndTurn')],
           extraHandCards=['magic_009'],mapTerrains=[dict(q=1,terrain='Mountain')],
           mapPlaces=[dict(q=2,type='OrcRampaging',enemy=True),dict(q=3,type=kind,enemyDefinition=enemy(5,attack=4,abilities=['Fortified'] if intrinsic else []))])
    expected.update({'world:enemy':False,'world:conquered':not intrinsic,'world:owner':'none' if intrinsic else '1',
                     'world:replaceGuard':intrinsic,'world:shields':int(kind=='City'),'Reputation':0 if kind=='City' else -1})
    if kind=='MageTower':
        CASES[-1]['operations'].insert(-1,dict(kind='SelectSpellReward',option=1))
        CASES[-1]['spellOffer']=['magic_009','magic_015','magic_005','magic_002']
        expected.update({'draw':11,'world:drawTop':'magic_015','world:pendingReward':False,'world:offer':'magic_009,magic_005,magic_002'})
    CASES[-1]['expected']=values({**{e['key']:e['value'] for e in CASES[-1]['expected']},**expected})

for strong in [False,True]:
    if strong:
        tunnel(True,'玩家占格进入玩家战斗路由，后续对战尚未验收',
               {'position:q':3,'MovementPool':5,'InBattle':True,'TurnActionCommitted':True,'TunnelAssaultActive':True,
                'tunnel:playerTarget':True,'hand':5,'cards:played':1},otherPlayers=[dict(q=3,unitIds=[])])
    else:
        case('magic_004',False,'基础不能停在其他玩家占格',{'position:q':0,'cards:played':0},error='InvalidOperationException: 地道穿行必须结束在安全格')
        CASES[-1].update(playFromHand=True,revealedDistance=3,travelPath=[dict(q=q) for q in [1,2,3]],otherPlayers=[dict(q=3,unitIds=[])])

for strong,mode in [(False,0),(True,0),(True,1),(True,2),(False,3),(True,3)]:
    setup={'token:Green':0,'crystal:Green':int(mode in [1,3])}
    if mode==1:setup['token:Black']=0
    if mode==2:setup.update({'DayPart':'Day','token:Green':2})
    if mode==3:
        tunnel(strong,'绿晶支付后执行真实地道移动',{'position:q':3,'MovementPool':5,'crystal:Green':0,'token:Green':0,'cards:played':1,'hand':5},
               setup=setup,mapPlaces=[dict(q=3,type='Keep',enemy=True)] if strong else [])
    else:
        case('magic_004',strong,'费用或白昼失败不移动 '+str(mode),{'position:q':0,'cards:played':0,'TunnelAssaultActive':False},setup=setup,
             error='InvalidOperationException: '+('白昼不能施放强效法术' if mode==2 else '法力不足，不能支付所选效果'))
        CASES[-1].update(playFromHand=True,revealedDistance=3,travelPath=[dict(q=q) for q in [1,2,3]],mapPlaces=[dict(q=3,type='Keep',enemy=True)] if strong else [])

# Real single-defender world settlement: no duplicate scene checks of unrelated travel cases.
for kind in ['City','MageTower']:
    tunnel(True,'败退保留城市守军/弃掉法师塔守军 '+kind,
        {'MovementPool':0,'position:q':0,'hand':7,'discard':3,'cards:played':0,'Wounds':5,'Fame':0,
         'Reputation':0 if kind=='City' else -1,'token:Green':0,'token:Black':0,
         'world:enemy':kind=='City','world:replaceGuard':kind!='City','world:conquered':False,'world:owner':'none','world:pendingReward':False,
         'operation:4:success':True,'operation:4:q':0},
        [dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=1),dict(kind='EndTurn')],
        mapPlaces=[dict(q=3,type=kind,enemyDefinition=enemy(5,attack=4))])

for kind,limit in [('Keep',6),('City',7)]:
    tunnel(True,'征服地点后真实补牌到'+str(limit)+'张 '+kind,
        {'MovementPool':0,'hand':limit,'draw':15-limit,'discard':4,'cards:played':0,'Fame':3,'Wounds':3,
         'Reputation':0 if kind=='City' else -1,'token:Green':0,'token:Black':0,'token:Red':0,
         'world:conquered':True,'world:owner':'1','world:locationBonus':limit-5,
         'operation:5:conquered':True,'operation:6:hand':limit,'operation:6:success':True,'operation:7:success':False},
        [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),dict(kind='TurnFinish'),
         dict(kind='FinishTunnel'),dict(kind='EndTurn',refill=True),dict(kind='FinishTunnel')],
        extraHandCards=['magic_009'],mapPlaces=[dict(q=3,type=kind,enemyDefinition=enemy(5,attack=4))])

for refill in [False,True]:
    tunnel(True,'法师塔选择真实供给，结束回合领取；补牌'+str(refill),
        {'MovementPool':0,'hand':5,'draw':11,'discard':4,'cards:played':0,'Reputation':-1,'Fame':3,'Wounds':3,
         'token:Green':0,'token:Black':0,'token:Red':0,'world:conquered':True,'world:owner':'1','world:pendingReward':False,
         'world:drawTop':'magic_015','world:offer':'magic_009,magic_005,magic_002',
         'operation:6:success':False,'operation:6:pendingReward':True,'operation:7:success':False,
         'operation:8:success':True,'operation:8:draw':10,'operation:8:pendingReward':True,
         'operation:9:success':True,'operation:9:drawTop':'magic_015','operation:9:pendingReward':False},
        [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),dict(kind='TurnFinish'),
         dict(kind='FinishTunnel'),dict(kind='TryEndTurn',refill=refill),dict(kind='SelectSpellReward',option=8),
         dict(kind='SelectSpellReward',option=1),dict(kind='EndTurn',refill=refill)],
        extraHandCards=['magic_009'],spellOffer=['magic_009','magic_015','magic_005','magic_002'],
        mapPlaces=[dict(q=3,type='MageTower',enemyDefinition=enemy(5,attack=4))])
    if refill:
        current=CASES[-1]
        current['operations'].insert(9,dict(kind='DiscardNonWoundHand'))
        expected={e['key']:e['value'] for e in current['expected']}
        expected={k.replace('operation:9:','operation:10:'):v for k,v in expected.items()}
        expected.update({'draw':9,'discard':6,'world:drawTop':'basic_card_000','world:handSpells':'magic_015',
                         'operation:10:drawTop':'basic_card_000','operation:10:hand':5})
        current['expected']=values(expected)


# Original enemy tokens: failed assault -> real next turn -> replacement from the same supply.
for kind,stock,first,next_guard,wounds,left in [
    ('Keep',['monster_025','monster_027','monster_028','monster_002'],'monster_025','monster_027',2,2),
    ('MageTower',['monster_025','monster_027','monster_028','monster_002'],'monster_028','monster_002',3,2),
    ('Keep',['monster_027'],'monster_027','monster_027',3,0)]:
    ops=[dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=1),
         dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True),dict(kind='StartTurn'),dict(kind='PlayCard',cardId='magic_004',enhanced=True),
         dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged'),dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel')]
    if kind=='MageTower':ops.append(dict(kind='SelectSpellReward',option=1))
    ops.append(dict(kind='EndTurn',refill=True))
    expected={'MovementPool':0,'position:q':3,'Fame':4,'Reputation':-2,'Wounds':wounds,'hand':5 if kind=='MageTower' else wounds+2,'discard':7,'cards:played':0,
        'token:Green':0,'token:Black':0,'token:Red':0,'crystal:Green':0,'crystal:Red':0,
        'draw':1 if kind=='MageTower' else 0,
        'world:guard':next_guard,'world:guardArt':True,'world:guardArmor':5,'world:conquered':True,'world:pendingSites':0,
        'world:enemyDraw':left,'world:enemyInUse':0,'world:enemyDiscard':first+','+next_guard if len(stock)>1 else next_guard,
        'operation:4:enemyDiscard':first,'operation:4:pendingSites':1,'operation:6:hand':5,
        'operation:8:guard':next_guard,'operation:8:guardArmor':5,'operation:8:success':True,
        'operation:11:kills':1,'operation:14:conquered':True,'world:pendingReward':False}
    tunnel(True,'原守军败退→回合补牌→再次突袭 '+kind+('（弃置标记洗回）' if len(stock)==1 else ''),expected,ops,
        initialHandCount=3,enemySupplyIds=stock,ongoingBlackMana=1,initialDrawCount=0,
        initialDrawCards=['magic_004','magic_009','basic_card_000','basic_card_014'],
        mapPlaces=[dict(q=3,type=kind,guardFromSupply=True)],spellOffer=['magic_009','magic_015','magic_005','magic_002'])
    CASES[-1]['limitations']=['真实原守军供给和两回合地道已联验；初始状态含既有魔力宣夺每回合黑魔力；全怪物数据/标记数量、多守军城市和PVP仍待验收']

for missing_mana in [False,True]:
    case('magic_004',True,'待抽守军时缺少'+('费用' if missing_mana else '对应标记')+'必须原子拒绝',
        {'MovementPool':5,'position:q':0,'cards:played':0,'world:pendingSites':1,'world:enemyInUse':0,'world:enemyDraw':4 if missing_mana else 0},
        setup={'MovementPool':5,**({'token:Green':0,'crystal:Green':0} if missing_mana else {})},
        error='InvalidOperationException: '+('法力不足，不能支付所选效果' if missing_mana else '缺少可用于再次突袭的实际守军标记'))
    CASES[-1].update(playFromHand=True,revealedDistance=3,travelPath=[dict(q=q) for q in [1,2,3]],
        enemySupplyIds=['monster_025','monster_027','monster_028','monster_002'] if missing_mana else [],
        mapPlaces=[dict(q=3,type='Keep',guardNeedsReplacement=True)])


# City guards are actual supply tokens. Each kill counts once, even on a failed assault.
for kill_second in [False,True]:
    survivor='monster_027' if kill_second else 'monster_002'
    killed='monster_002' if kill_second else 'monster_027'
    tunnel(True,'城市两守军：击败第'+str(2 if kill_second else 1)+'名后必须撤退，幸存者与盾徽保留',
        {'position:q':0,'Fame':4,'Reputation':0,'Wounds':6,'hand':6,'discard':6 if kill_second else 9,'draw':10,'cards:played':0,
         'world:guards':survivor,'world:guardCount':1,'world:conquered':False,'world:owner':'none','world:shieldOrder':'1','world:locationBonus':0,
         'world:guardArt':True,'world:secondGuardArt':True,'world:enemyInUse':1,'world:enemyDiscard':killed,
         'operation:2:kills':1,'operation:5:success':False,'operation:5:shields':0,'operation:6:success':True,
         'operation:6:guards':survivor,'operation:6:shields':1,'operation:7:success':False,
         'token:Green':0,'token:Black':0,'token:Red':0,'TunnelAssaultActive':False},
        [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='TurnRanged',targetIndex=int(kill_second)),
         dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel'),dict(kind='FinishTunnel',option=1),
         dict(kind='FinishTunnel',option=1),dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
        extraHandCards=['magic_009'],enemySupplyIds=['monster_027','monster_002'],
        mapPlaces=[dict(q=3,type='City',guardColors=['Gray','Purple'])])
    CASES[-1]['limitations']=['固定两名原标记的城市部分击杀已验；各色城市增益/等级守军配置、合作城市与完整冒险仍待联验']

for earlier_leader in [False,True]:
    shields=[9,1,9,9] if earlier_leader else [9]
    tunnel(True,'同ID两实体守军全部击败；'+('平票按首枚盾徽决定领袖' if earlier_leader else '按盾徽数量成为领袖'),
        {'position:q':3,'Fame':8,'Reputation':0,'Wounds':3,'hand':6 if earlier_leader else 7,'draw':7 if earlier_leader else 6,
         'discard':7,'cards:played':0,'world:guardCount':0,'world:guards':'','world:conquered':True,'world:owner':'9' if earlier_leader else '1',
         'world:shieldOrder':','.join(map(str,shields+[1,1])),'world:locationBonus':1 if earlier_leader else 2,
         'world:guardArt':True,'world:secondGuardArt':True,'world:enemyInUse':0,'world:enemyDiscard':'monster_027,monster_027',
         'operation:2:kills':1,'operation:4:kills':1,'operation:8:success':False,'operation:9:success':True,
         'operation:9:conquered':True,'operation:9:shields':len(shields)+2,'token:Green':0,'token:Black':0,'token:Red':0,'TunnelAssaultActive':False},
        [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='RangedAttack',targetIndex=0),
         dict(kind='PlayCard',cardId='magic_009'),dict(kind='RangedAttack',targetIndex=1),dict(kind='TurnRanged',option=1),
         dict(kind='TurnDefense'),dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=1),dict(kind='FinishTunnel'),
         dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
        extraHandCards=['magic_009','magic_009'],enemySupplyIds=['monster_027','monster_027'],
        mapPlaces=[dict(q=3,type='City',guardColors=['Gray','Gray'],cityShields=shields)])
    CASES[-1]['limitations']=['固定两实体原标记的征服/领袖/补牌已验；各色城市增益/等级守军配置、合作城市与完整冒险仍待联验']

# Rulebook p10: city bonuses change live combat, never the printed token or other enemies.
for color,guard,token_color,armor,attack,printed,abilities,wounds,poison in [
    ('Red','monster_027','Gray',5,6,6,'Brutal',6,0),
    ('Green','monster_027','Gray',5,6,6,'Poison',3,3),
    ('Blue','monster_028','Purple',6,7,5,'IceResist',4,0),
    ('Blue','monster_030','Purple',5,8,6,'FireResist',4,0),
    ('Blue','monster_027','Gray',5,6,6,'',3,0),
    ('Red','monster_028','Purple',6,5,5,'IceResist',3,0),
    ('Green','monster_028','Purple',6,5,5,'IceResist',3,0),
    ('Green','monster_002','Purple',5,5,5,'Poison',3,3)]:
    tunnel(True,'城市加成 '+color+' / '+guard+'：真实承伤、败退与正常补牌',
        {'operation:0:effectiveArmor':armor,'operation:0:effectiveAttack':attack,'operation:0:cityAbilities':abilities,
         'operation:2:handWounds':wounds,'operation:3:cityBonusActive':False,'operation:4:success':True,
         'world:cityColor':color,'world:cityBonusActive':False,'world:effectiveAttack':printed,'world:guardArmor':armor,
         'world:guardArt':True,'world:enemyInUse':1,'world:enemyDiscard':'','world:conquered':False,'world:guards':guard,
         'position:q':0,'Fame':0,'Reputation':0,'Wounds':wounds,'hand':max(5,wounds),'discard':5+poison,
         'draw':10-max(0,5-wounds),'cards:played':0,'token:Green':0,'token:Black':0,'TunnelAssaultActive':False},
        [dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),dict(kind='TurnFinish'),
         dict(kind='FinishTunnel',option=1),dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
        initialHandCount=3,enemySupplyIds=[guard],mapPlaces=[dict(q=3,type='City',cityColor=color,guardColors=[token_color])])
    CASES[-1]['limitations']=['对应城市颜色和原标记分支已验；蓝城冰火攻击缺已审计原标记场景，等级配军/合作/PVP/完整冒险仍待联验']

tunnel(True,'白城护甲+1：火焰魔球5不足，实际追加火焰风暴8后征服',
    {'operation:0:effectiveArmor':6,'operation:2:required':6,'operation:2:effective':5,'operation:2:kills':0,
     'operation:4:kills':1,'operation:6:cityBonusActive':False,'operation:7:conquered':True,
     'world:cityColor':'White','world:guardArmor':5,'world:effectiveArmor':5,'world:cityBonusActive':False,'world:guardArt':True,
     'world:enemyDiscard':'monster_027','world:enemyInUse':0,'world:conquered':True,'world:shields':1,'world:owner':'1',
     'position:q':3,'Fame':4,'Reputation':0,'Wounds':4,'hand':7,'discard':7,'draw':7,'cards:played':0,
     'token:Red':0,'token:Green':0,'token:Black':0,'TunnelAssaultActive':False},
    [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009'),dict(kind='RangedAttack'),
     dict(kind='PlayCard',cardId='magic_009',enhanced=True),dict(kind='TurnRanged'),dict(kind='TurnDefense'),dict(kind='TurnFinish'),
     dict(kind='FinishTunnel'),dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
    extraHandCards=['magic_009','magic_009'],enemySupplyIds=['monster_027'],
    mapPlaces=[dict(q=3,type='City',cityColor='White',guardColors=['Gray'])])
CASES[-1]['limitations']=['白城实际护甲、两种原攻击与征服已验；等级配军/合作/PVP/完整冒险仍待联验']

# Audited original white token 008: Cold Fire 4, physical resistance, brutal and poison.
for color,add_fury,wounds in [('None',False,0),('Blue',False,5),('Blue',True,0)]:
    ops=[dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='PlayCard',cardId='magic_010',enhanced=True,option=1)]
    if add_fury:ops.append(dict(kind='PlayCard',cardId='basic_card_021',option=1))
    defense=len(ops);ops.append(dict(kind='TurnDefense'))
    if add_fury:ops.extend([dict(kind='PlayCard',cardId='basic_card_021',enhanced=True),dict(kind='TurnAttack')])
    ops.extend([dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=1)])
    if not wounds:ops.append(dict(kind='DiscardNonWoundHand'))
    ops.append(dict(kind='EndTurn',refill=True))
    expected={'operation:0:effectiveAttack':5 if color=='Blue' else 4,'operation:0:cityAbilities':'PhysicalResist,Brutal,Poison',
        f'operation:{defense}:blockPrinted':11 if add_fury else 9,f'operation:{defense}:blockEffective':5 if add_fury else 4,
        f'operation:{defense}:blockRequired':5 if color=='Blue' else 4,f'operation:{defense}:wounds':wounds,
        f'operation:{defense}:black':0,f'operation:{defense}:red':1,
        f'operation:{defense}:battleWounds':wounds,f'operation:{defense}:knockedOut':wounds>=5,
        'position:q':0,'Fame':0,'Reputation':0,'Wounds':3+wounds,'hand':max(5,3+wounds),'discard':7 if add_fury else 6+wounds,
        'draw':8 if wounds==0 else 10,'cards:played':0,'token:Red':0,'token:Green':0,'token:Black':0,
        'world:guard':'monster_008','world:guardArmor':8,'world:guardArt':True,'world:effectiveAttack':4,'world:enemyInUse':1,
        'world:enemyDiscard':'','world:conquered':False,'world:cityBonusActive':False,'world:shieldOrder':''}
    if add_fury:expected.update({'operation:3:success':True,'operation:5:success':True,'operation:6:effective':2,'operation:6:kills':0,'operation:6:red':0})
    if wounds:expected[f'operation:{defense}:nonWoundHand']=0
    tunnel(True,'原冰火守军 / '+color+'：烈焰浪涌9'+('与狂怒2合计格挡；再验证物抗' if add_fury else '实际格挡门槛'),expected,ops,
        extraHandCards=['magic_010']+(['basic_card_021'] if add_fury else []),enemySupplyIds=['monster_008'],
        mapPlaces=[dict(q=3,type='City',cityColor=color,guardColors=['White'])])
    if add_fury:CASES[-1].update(discardIndices=[],discardIndex=-1,removeIndex=-1)
    CASES[-1]['limitations']=['本例原冰火/物抗/残暴/剧毒与蓝城分支已验；等级配军/合作/PVP/完整冒险仍待联验']

tunnel(True,'原火焰风暴攻城8击败物抗守军，阻止冰火残暴剧毒攻击',
    {'operation:0:effectiveAttack':5,'operation:2:kills':1,'operation:3:wounds':0,'operation:5:conquered':True,
     'position:q':3,'Fame':8,'Reputation':0,'Wounds':4,'hand':7,'discard':6,'draw':7,'cards:played':0,
     'token:Red':0,'token:Green':0,'token:Black':0,'world:guardArt':True,'world:guardArmor':8,
     'world:enemyInUse':0,'world:enemyDiscard':'monster_008','world:conquered':True,'world:shields':1,'world:owner':'1',
     'world:cityBonusActive':False,'world:effectiveAttack':4},
    [dict(kind='TurnBegin'),dict(kind='PlayCard',cardId='magic_009',enhanced=True),dict(kind='TurnRanged'),dict(kind='TurnDefense'),
     dict(kind='TurnFinish'),dict(kind='FinishTunnel'),dict(kind='DiscardNonWoundHand'),dict(kind='EndTurn',refill=True)],
    extraHandCards=['magic_009'],enemySupplyIds=['monster_008'],mapPlaces=[dict(q=3,type='City',cityColor='Blue',guardColors=['White'])])
CASES[-1]['limitations']=['本例原冰火守军、火焰攻城和征服已验；等级配军/合作/PVP/完整冒险仍待联验']

tunnel(True,'原火焰风暴自伤达到昏迷门槛：自动弃手牌，已产生的攻击仍可征服',
    {'operation:2:wounds':4,'operation:2:battleWounds':4,'operation:2:knockedOut':False,
     'operation:2:hand':10,'operation:2:nonWoundHand':3,
     'operation:3:battleWounds':5,'operation:3:knockedOut':True,'operation:3:hand':8,'operation:3:nonWoundHand':0,
     'operation:4:kills':1,'operation:4:effective':8,
     'position:q':3,'Fame':8,'Reputation':0,'Wounds':8,'hand':8,'discard':10,'draw':10,'cards:played':0,
     'token:Red':0,'token:Green':0,'token:Black':0,'world:guardArt':True,'world:guardArmor':8,
     'world:enemyInUse':0,'world:enemyDiscard':'monster_008','world:conquered':True,'world:shields':1,'world:owner':'1',
     'world:cityBonusActive':False,'world:battleWounds':5,'world:knockedOut':True},
    [dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),
     dict(kind='PlayCard',cardId='magic_009',enhanced=True),dict(kind='TurnAttack'),dict(kind='TurnFinish'),
     dict(kind='FinishTunnel'),dict(kind='EndTurn',refill=True)],
    extraHandCards=['magic_009'],enemySupplyIds=['monster_008'],mapPlaces=[dict(q=3,type='City',cityColor='None',guardColors=['White'])])
CASES[-1]['limitations']=['本例实战与自伤累积昏迷及原卡结算已验；昏迷后技能/部队、等级配军/合作/PVP/完整冒险仍待联验']

# A knocked-out hero retains owned units and skills; original resistance still applies.
for skill_option in [0,1]:
    won=skill_option==1
    tunnel(True,'昏迷后原烈焰枪手与炽热剑术'+('火焰合击8征服' if won else '物理合击有效7不足'),
        {'operation:2:battleWounds':5,'operation:2:knockedOut':True,'operation:2:nonWoundHand':0,
         'operation:3:success':True,'operation:3:red':1,'operation:3:unitsReady':'False','operation:3:art':'army_003',
         'operation:3:printedRule':'埃莫提普烈焰枪手：远程火焰攻击6。',
         'operation:4:success':False,'operation:4:red':1,'operation:4:rangedRemaining':6,
         'operation:5:success':True,'operation:5:skillsUsed':'True','operation:5:meleeRemaining':2,'operation:5:art':'skill_000',
         'operation:5:printedRule':'炽热剑术：每回合一次：攻击2或火焰攻击2。',
         'operation:6:success':False,'operation:6:meleeRemaining':2,
         'operation:7:effective':8 if won else 7,'operation:7:kills':1 if won else 0,
         'operation:11:skillsUsed':'False','operation:11:unitsReady':'False',
         'position:q':3 if won else 0,'Fame':8 if won else 0,'Reputation':0,'Wounds':8,'hand':8,'discard':10,'draw':10,'cards:played':0,
         'token:Red':0,'token:Green':0,'token:Black':0,'world:guardArt':True,'world:enemyInUse':0 if won else 1,
         'world:enemyDiscard':'monster_008' if won else '','world:conquered':won,'world:shields':1 if won else 0,
         'world:cityBonusActive':False,'world:battleWounds':5,'world:knockedOut':True,'unitWounds':0},
        [dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense'),
         dict(kind='UnitUse',option=1),dict(kind='UnitUse',option=1),
         dict(kind='SkillUse',option=skill_option),dict(kind='SkillUse',option=skill_option),dict(kind='TurnAttack'),
         dict(kind='TurnFinish'),dict(kind='FinishTunnel',option=0 if won else 1),dict(kind='EndTurn',refill=True),dict(kind='StartTurn')],
        unitIds=['army_003'],unitInitiallyReady=True,skillPoolId='hero_skill_pool_000',skillIndices=[2],
        enemySupplyIds=['monster_008'],mapPlaces=[dict(q=3,type='City',cityColor='Blue',guardColors=['White'])])
    CASES[-1]['limitations']=['本例昏迷后原部队/自有技能及重复限制已验；其他技能/部队、等级配军/合作/PVP/完整冒险仍待联验']
    if not won:CASES[-1]['captureAfterOperations']=[2,4]

# Free acquisition must move the original offer and the played card, not leave a permission flag.
for free_id in ('magic_020','items_017'):
    banner = free_id.startswith('items')
    base_expected = {'InfluencePool':2,'Fame':2 if banner else 0,'Reputation':-7,'hand':0,'Wounds':0,'handWounds':0,
        'cards:played':0 if banner else 1,'cards:removed':1 if banner else 0,'discard':2,
        'token:White':2 if banner else 1,'token:Black':2 if banner else 1,'FreeRecruit':False,
        'CharmRecruitDiscount':3,'recruit:offer':'army_003','recruit:owned':'army_2_006,army_2_011',
        'recruit:ready':'False,True','recruit:freeSlots':0,'recruit:disbanded':False,'recruit:interactionReversed':False}
    fields=dict(playFromHand=True,initialHandCount=1,unitIds=['army_2_006'],recruitOffer=['army_2_011','army_003'])
    setup={'InfluencePool':2,'player:Reputation':-7,'player:ExtraCommandSlots':1,'CharmRecruitDiscount':3}
    old=next(c for c in CASES if c['id']==free_id+'_01')
    old.update(fields,setup=values(setup),expected=values(dict(MovementPool=0,MeleePool=0,RangedPool=0,BlockPool=0,**base_expected)))
    old['title']='强化 / 原卡实际免费取得暴徒，不应用声誉X限制或反转'
    limits=['本例真实免费获取与卡区已验；基础能力、仅可交涉部队限制、其他组合与完整冒险仍待联验']
    old['limitations']=limits

    # The selected second offer is a real elite unit requiring a different recruitment location.
    case(free_id,True,'选择供应第二张原精英，地点与印刷费用不参与免费获取',
        dict(base_expected,**{'recruit:offer':'army_2_011','recruit:owned':'army_2_006,army_003','recruit:art':'army_003'}),
        setup=setup,option=1,limits=limits)
    CASES[-1].update(fields)

    operation_case(free_id,True,'满编真实解散、立即发动新部队、重复出牌拒绝及回合卡区',
        dict(base_expected,InfluencePool=0,CharmRecruitDiscount=0,**{'cards:played':0,'discard':2 if banner else 3,
             'token:White':0,'token:Black':0,'recruit:owned':'army_2_011','recruit:ready':'False','recruit:disbanded':True,
             'operation:0:success':True,'operation:0:influence':6,'operation:0:owned':'army_2_011',
             'operation:1:success':False,'operation:1:influence':6,'operation:1:offer':'army_003',
             'operation:2:success':True}),
        [dict(kind='UnitUse',targetIndex=0,option=2),dict(kind='PlayCard',cardId=free_id,enhanced=True),dict(kind='EndTurn')],
        setup=dict(setup,**{'player:ExtraCommandSlots':0}),**fields,disbandUnitIndex=0)
    CASES[-1]['limitations']=limits

    for invalid in ('full','foreign','index','empty','unneeded'):
        bad_fields=dict(fields)
        bad_setup=dict(setup)
        option=99 if invalid=='index' else 0
        if invalid in ('full','foreign'):bad_setup['player:ExtraCommandSlots']=0
        if invalid in ('foreign','unneeded'):bad_fields['foreignDisband']=True
        if invalid=='empty':bad_fields['recruitOffer']=[]
        reason='指挥槽已满，必须选择解散一支自有部队' if invalid in ('full','foreign') else '有空闲指挥槽时不能借招募解散部队' if invalid=='unneeded' else '必须选择真实部队供应中的部队'
        rejected=dict(base_expected,Fame=0,hand=1,**{'cards:played':0,'cards:removed':0,'token:White':2,'token:Black':2,
            'recruit:offer':'' if invalid=='empty' else 'army_2_011,army_003','recruit:owned':'army_2_006',
            'recruit:freeSlots':0 if invalid in ('full','foreign') else 1,'recruit:ready':'False'})
        case(free_id,True,'非法免费招募 '+invalid+' 必须在消费前拒绝',rejected,option=option,setup=bad_setup,limits=limits)
        CASES[-1].update(bad_fields,expectedException='InvalidOperationException: '+reason)

for reason,changed in [('白昼不能施放强效法术',{'DayPart':'Day'}),('法力不足，不能支付所选效果',{'token:Black':0})]:
    case('magic_020',True,'施法限制不得先解散部队或扣白色魔力',
        {'InfluencePool':2,'Fame':0,'Reputation':-7,'hand':1,'Wounds':0,'handWounds':0,'cards:played':0,'cards:removed':0,
         'token:White':2,'token:Black':changed.get('token:Black',2),'recruit:owned':'army_2_006',
         'recruit:offer':'army_2_011,army_003','recruit:disbanded':False},
        setup=dict(setup,**{'player:ExtraCommandSlots':0},**changed),limits=limits)
    CASES[-1].update(fields,disbandUnitIndex=0,expectedException='InvalidOperationException: '+reason)

# Battle Summons base: a turn-scoped permission, then a real selected offer ability.
summon_limits=['已验列明供应能力/费用/卡区；尚未接入部队、部队增益组合和特殊地点限制仍须联验，不代表整卡完成']
def summon(title, expected, ops, offer=None, setup=None, **fields):
    common = {'OfferedUnitUses':0,'TempCommandSlots':0,'NoUnitDamage':False,'HasRecruited':False,
              'recruit:owned':'army_2_006','recruit:freeSlots':0,'recruit:ready':'False',
              'recruit:offer':','.join(offer if offer is not None else ['army_2_006']),
              'hand':0,'cards:played':1,'cards:removed':0,'Wounds':0,'handWounds':0}
    common.update(expected)
    operation_case('magic_020',False,title,common,ops,
                   setup=dict({'DayPart':'Day','InBattle':False,'SkipDraw':True},**(setup or {})),
                   recruitOffer=offer if offer is not None else ['army_2_006'],unitIds=['army_2_006'],
                   playFromHand=True,initialHandCount=1,**fields)
    CASES[-1]['limitations']=summon_limits

summon('先施法后使用供应农民移动；重复发动拒绝，回合弃牌',
       {'MovementPool':0,'cards:played':0,'discard':3,'token:White':0,
        'operation:0:success':True,'operation:0:move':2,'operation:0:permissions':0,
        'operation:1:success':False,'operation:1:move':2,'operation:1:permissions':0,
        'operation:2:success':True},
       [dict(kind='OfferedUnitUse',option=2),dict(kind='OfferedUnitUse',option=1),dict(kind='EndTurn')])
# Replace obsolete flag-only case without discarding its historical identity.
new_base=CASES.pop();old_base=next(c for c in CASES if c['id']=='magic_020_00')
new_base['id']='magic_020_00';old_base.clear();old_base.update(new_base)

summon('供应暴徒影响力及声誉副作用，供应与队伍不变',
       {'InfluencePool':4,'Reputation':-1,'operation:0:success':True,'operation:0:art':'army_2_011'},
       [dict(kind='OfferedUnitUse',option=2)],offer=['army_2_011'])
for choice in (0,1):
    action='Block' if choice else 'Attack'
    expected={'token:Blue':1,'Fame':0 if choice else 3,'operation:0:success':True,
              'operation:0:attack':0 if choice else 4,'operation:0:block':4 if choice else 0,
              'operation:0:blue':1,'operation:0:art':'army_2_005',
              'operation:1:success':True,'operation:1:printed':4,'operation:1:effective':4,
              'operation:1:required':4,'operation:1:kills':0 if choice else 1}
    summon('原北地僧侣蓝魔力寒冰'+('格挡' if choice else '攻击')+'进入真实结算',expected,
           [dict(kind='OfferedUnitUse',option=1,abilityChoice=choice),dict(kind=action)],
           offer=['army_2_005'],setup={'InBattle':True},enemies=[enemy(4,4)])

for invalid in ('missingBlue','unitIndex','abilityIndex','choice','outsideCombat'):
    blue=0 if invalid=='missingBlue' else 2
    bad_setup={'InBattle':invalid!='outsideCombat'}
    if invalid=='missingBlue':bad_setup.update({'token:Blue':0,'crystal:Blue':0,'token:Gold':0})
    first=dict(kind='OfferedUnitUse',targetIndex=99 if invalid=='unitIndex' else 0,
               option=99 if invalid=='abilityIndex' else 1,abilityChoice=2 if invalid=='choice' else 0)
    summon('非法供应能力 '+invalid+' 不消费许可，改选合法农民移动',
           {'MovementPool':2,'token:Blue':blue,'operation:0:success':False,'operation:0:permissions':1,
            'operation:0:blue':blue,'operation:0:attack':0,'operation:0:block':0,
            'operation:1:success':True,'operation:1:art':'army_2_006','operation:1:permissions':0},
           [first,dict(kind='OfferedUnitUse',targetIndex=1,option=2)],
           offer=['army_2_005','army_2_006'],setup=bad_setup)

summon('未使用许可结束战斗仍在，结束回合过期，再次发动被拒绝',
       {'cards:played':0,'discard':3,'token:White':0,'operation:0:permissions':1,
        'operation:1:permissions':0,'operation:2:success':False,'operation:2:move':0},
       [dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='OfferedUnitUse',option=2)],setup={'InBattle':True})
summon('禁止供应部队承伤，自有农民正常承伤，仍可使用供应能力',
       {'InfluencePool':4,'Reputation':-1,'unitWounds':1,
        'operation:0:success':False,'operation:0:unitWounds':'0','operation:0:permissions':1,
        'operation:1:success':True,'operation:1:unitWounds':'1','operation:1:heroWounds':0,
        'operation:2:success':True},
       [dict(kind='OfferedUnitDamage'),dict(kind='Damage',unitTargets=[0]),dict(kind='OfferedUnitUse',option=2)],
       offer=['army_2_011'],setup={'InBattle':True},enemies=[enemy(5,3)])
summon('选择第二张原部队，付红魔力并使用远程火焰攻击击杀原怪物',
       {'token:Red':1,'Fame':4,'operation:0:success':True,'operation:0:ranged':6,'operation:0:art':'army_003',
        'operation:1:success':True,'operation:1:printed':6,'operation:1:effective':6,'operation:1:required':5,
        'operation:1:kills':1,'operation:1:fame':4},
       [dict(kind='OfferedUnitUse',targetIndex=1,option=1),dict(kind='RangedAttack')],
       offer=['army_2_006','army_003'],setup={'InBattle':True},enemies=[dict(id='monster_027',originalDefinition=True)])
summon('供应为空时不能发动，不消费本回合许可',
       {'OfferedUnitUses':1,'operation:0:success':False,'operation:0:permissions':1},
       [dict(kind='OfferedUnitUse',option=2)],offer=[])

charge_limits=['列明原部队、费用、攻防和期限已联验；未接入部队/其他旗帜技能组合及完整冒险仍待验']
def charge(strong,title,expected,ops=(),unit='army_2_006',setup=None,**fields):
    if fields.get('extraHandCards'):
        fields.update(discardIndex=-1,removeIndex=-1,discardIndices=[])
    defaults={'ChargeUnitBonus':3 if strong else 2,'NoUnitDamage':True,'hand':0,'cards:played':1,
              'cards:removed':0,'Wounds':0,'handWounds':0}
    defaults.update(expected)
    operation_case('advanced_card_016',strong,title,defaults,ops,
                   setup=dict({'InBattle':True,'CombatPhase':'Ranged','SkipDraw':True},**(setup or {})),
                   unitIds=[unit],unitInitiallyReady=True,playFromHand=True,initialHandCount=1,**fields)
    CASES[-1]['limitations']=charge_limits

for strong in (False,True):
    value=5 if strong else 4
    charge(strong,'原农民攻击2加本场加值后击杀所选目标',
           {'Fame':3,'operation:0:success':True,'operation:0:attack':value,'operation:0:unitArt':'army_2_006',
            'operation:1:success':True,'operation:1:printed':value,'operation:1:required':value,'operation:1:kills':1},
           [dict(kind='UnitUse'),dict(kind='Attack')],enemies=[enemy(value)])
    replacement=CASES.pop();legacy=next(c for c in CASES if c['id']==f'advanced_card_016_{int(strong):02}')
    replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)

for choice in (0,1):
    charge(True,'原北地僧侣寒冰能力4+3，只增强所选攻击或格挡',
           {'token:Blue':1,'Fame':0 if choice else 3,
            'operation:0:success':True,'operation:0:attack':0 if choice else 7,'operation:0:block':7 if choice else 0,
            'operation:1:success':True,'operation:1:printed':7,'operation:1:required':7,'operation:1:kills':0 if choice else 1},
           [dict(kind='UnitUse',option=1,abilityChoice=choice),dict(kind='Block' if choice else 'Attack')],
           unit='army_2_005',enemies=[enemy(7,7)])

for unit,kind,power,color in [('army_003','ranged',8,'Red'),('army_2_008','siege',6,'Green')]:
    charge(False,'原部队'+kind+'实际增加2并击杀原人类战士',
           {'token:'+color:1,'Fame':4,'operation:0:success':True,'operation:0:'+kind:power,'operation:0:unitArt':unit,
            'operation:1:success':True,'operation:1:printed':power,'operation:1:required':5,'operation:1:kills':1},
           [dict(kind='UnitUse',option=1),dict(kind='RangedAttack')],unit=unit,enemies=[dict(id='monster_027',originalDefinition=True)])

charge(False,'先冲锋再战斗号召，供应北地僧侣寒冰攻击同样获得加值',
       {'hand':0,'cards:played':2,'token:White':1,'token:Blue':1,'Fame':3,'OfferedUnitUses':0,
        'recruit:offer':'army_2_005','recruit:owned':'army_2_006','unitReady':True,
        'operation:0:success':True,'operation:1:success':True,'operation:1:attack':6,
        'operation:2:success':True,'operation:2:printed':6,'operation:2:required':6,'operation:2:kills':1},
       [dict(kind='PlayCard',cardId='magic_020'),dict(kind='OfferedUnitUse',option=1),dict(kind='Attack')],
       recruitOffer=['army_2_005'],extraHandCards=['magic_020'],enemies=[enemy(6)])
charge(True,'农民移动与英雄狂怒均不获得部队攻防加成',
       {'MovementPool':2,'MeleePool':2,'cards:played':2,'operation:0:success':True,'operation:0:attack':0,
        'operation:1:success':True,'operation:1:attack':2},
       [dict(kind='UnitUse',option=2),dict(kind='PlayCard',cardId='basic_card_021')],extraHandCards=['basic_card_021'])
charge(True,'乌顿剑士自伤能力仍支付伤牌，攻击6加3',
       {'Fame':3,'unitWounds':1,'operation:0:success':True,'operation:0:attack':9,'operation:0:unitWounds':'1',
        'operation:1:success':True,'operation:1:printed':9,'operation:1:kills':1},
       [dict(kind='UnitUse',option=1),dict(kind='Attack')],unit='army_2_014',enemies=[enemy(8)])
charge(True,'第二场战斗中原农民不再得到上场加成，禁伤仍持续',
       {'ChargeUnitBonus':0,'Fame':3,'operation:0:bonus':0,'operation:0:ban':True,
        'operation:2:success':True,'operation:2:attack':2,'operation:3:printed':2,'operation:3:kills':1},
       [dict(kind='EndBattle'),dict(kind='BeginBattle'),dict(kind='UnitUse'),dict(kind='Attack')],enemies=[enemy(2)])
charge(True,'禁伤跨战斗保留，到回合末失效，下一回合才可分配伤害',
       {'ChargeUnitBonus':0,'NoUnitDamage':False,'cards:played':0,'discard':3,'token:Red':0,'unitWounds':1,
        'operation:0:bonus':0,'operation:0:ban':True,
        'operation:2:success':False,'operation:2:unitWounds':'0','operation:4:ban':False,
        'operation:6:success':True,'operation:6:unitWounds':'1'},
       [dict(kind='EndBattle'),dict(kind='BeginBattle'),dict(kind='Damage',unitTargets=[0]),
        dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='BeginBattle'),dict(kind='Damage',unitTargets=[0])],
       enemies=[enemy(5,3)])

for reason,changed in [('非战斗',{'InBattle':False}),('已经进入近战阶段',{'CombatPhase':'Melee'}),('已经产生攻击',{'MeleePool':2}),
                       ('缺少强化红魔力',{'token:Red':0,'crystal:Red':0,'token:Gold':0})]:
    charge(True,reason+'：拒绝出牌并保留真实手牌与魔力',
           {'ChargeUnitBonus':0,'NoUnitDamage':False,'hand':1,'cards:played':0,
            'token:Red':changed.get('token:Red',2),'MeleePool':changed.get('MeleePool',0)},setup=changed)
    CASES[-1]['expectedException']='InvalidOperationException: '+('法力不足，不能支付所选效果' if reason=='缺少强化红魔力' else '冲锋陷阵只能在本场战斗开始时打出')
charge(False,'部队已经发动后不能补打第二张冲锋，拒绝前不扣红魔力',
       {'hand':1,'MeleePool':4,'operation:0:success':True,'operation:0:attack':4,
        'operation:1:success':False,'operation:1:bonus':2,'operation:1:red':2,
        'operation:1:exception':'冲锋陷阵只能在本场战斗开始时打出'},
       [dict(kind='UnitUse'),dict(kind='PlayCard',cardId='advanced_card_016',enhanced=True)],extraHandCards=['advanced_card_016'])

honor_limits = ['已列明附着、焚毁、部队攻防护甲及卡区分支；PvP夺旗、其他旗帜及完整游戏组合仍待联验']

def honor(strong, title, expected, ops=(), setup=None, **fields):
    options = dict(unitIds=['army_2_006','army_2_006'], initialReadyUnitIndices=[0,1],
                   initialHandCount=1, initialDrawCount=0, initialDiscardCards=[], playFromHand=True,
                   discardIndex=-1, removeIndex=-1, discardIndices=[])
    options.update(fields)
    defaults = {'hand':0, 'cards:played':0, 'cards:removed':int(strong), 'Wounds':0,
                'banner:count':0 if strong else 1, 'banner:total':1, 'banner:unique':1,
                'HonorBannerTurnBonus':int(strong)}
    defaults.update(expected)
    operation_case('items_000',strong,title,defaults,ops,
                   setup=dict({'InBattle':True,'SkipDraw':True},**(setup or {})), **options)
    CASES[-1]['limitations'] = honor_limits

for strong in (False,True):
    honor(strong,'两支原农民攻击并击杀原人类战士，名望与旗帜范围对应',
          {'Fame':6 if strong else 5,'banner:armor':'4,4' if strong else '4,3',
           'operation:0:success':True,'operation:0:attack':3,'operation:0:fame':1,'operation:0:unitArt':'army_2_006',
           'operation:1:success':True,'operation:1:attack':6 if strong else 5,'operation:1:fame':2 if strong else 1,
           'operation:2:success':True,'operation:2:required':5,'operation:2:kills':1,'operation:2:fame':6 if strong else 5},
          [dict(kind='UnitUse'),dict(kind='UnitUse',targetIndex=1),dict(kind='Attack')],
          enemies=[dict(id='monster_027',originalDefinition=True)])
    replacement=CASES.pop();legacy=next(c for c in CASES if c['id']==f'items_000_{int(strong):02}')
    replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)

for strong in (False,True):
    honor(strong,'实际农民格挡加值按附着范围生效',
          {'BlockPool':6 if strong else 5,'Fame':2 if strong else 1,
           'operation:0:success':True,'operation:0:block':3,'operation:0:fame':1,
           'operation:1:success':True,'operation:1:block':6 if strong else 5,'operation:1:fame':2 if strong else 1},
          [dict(kind='UnitUse',abilityChoice=1),dict(kind='UnitUse',targetIndex=1,abilityChoice=1)])
    honor(strong,'原农民移动不增加名望，耗竭后再次发动必须拒绝',
          {'MovementPool':2,'Fame':0,'operation:0:success':True,'operation:0:fame':0,
           'operation:1:success':False,'operation:1:fame':0},
          [dict(kind='UnitUse',option=2),dict(kind='UnitUse')])
    honor(strong,'实际伤害依次分给两支部队，护甲加值影响剩余英雄伤牌',
          {'Wounds':0 if strong else 1,'hand':0 if strong else 1,**({'handWounds':1} if not strong else {}),
           'banner:total':1 if strong else 2,'banner:unique':1 if strong else 2,
           'Fame':0,'operation:0:success':True,'operation:0:unitWounds':'1,0',
           'operation:1:success':True,'operation:1:unitWounds':'1,1','operation:1:heroWounds':0 if strong else 1},
          [dict(kind='Damage',unitTargets=[0]),dict(kind='Damage',targetIndex=1,unitTargets=[1])],
          enemies=[enemy(5,4),enemy(5,4)])

honor(False,'伤牌和耗竭不因附着清除；旗帜跨战斗、回合与轮末保留',
      {'unitWounds':1,'unitReady':False,'operation:0:count':1,'operation:1:count':1,
       'operation:2:count':1,'operation:2:draw':0,'operation:2:discard':0},
      [dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='EndRound')],
      initialReadyUnitIndices=[],unitWounds=1)
honor(False,'替换弃掉旧实例，选择回收后轮末将两张真实旗帜洗回牌库',
      {'banner:count':0,'banner:total':2,'banner:unique':2,'draw':2,'discard':0,
       'operation:0:success':True,'operation:0:count':1,'operation:0:discard':1,
       'operation:1:success':True,'operation:1:count':1,
       'operation:2:count':0,'operation:2:draw':2},
      [dict(kind='PlayCard',cardId='items_000'),dict(kind='ChooseBannerReturn',option=1),dict(kind='EndRound')],
      extraHandCards=['items_000'])
honor(False,'选择回收再取消，轮末仍保留原旗帜',
      {'operation:0:count':1,'operation:1:count':1,'operation:2:count':1,'operation:2:draw':0},
      [dict(kind='ChooseBannerReturn',option=1),dict(kind='ChooseBannerReturn',option=0),dict(kind='EndRound')])
honor(False,'解散附着部队，原旗帜进入弃牌且幸存部队无加成',
      {'banner:count':0,'banner:armor':'3','discard':1,
       'operation:0:success':True,'operation:0:count':0,'operation:0:discard':1},[dict(kind='Disband')])
honor(True,'焚毁增益跨战斗持续，回合末清除并永久移除神器',
      {'HonorBannerTurnBonus':0,'Fame':1,'operation:0:bonus':1,'operation:2:success':True,
       'operation:2:attack':3,'operation:2:fame':1,'operation:3:bonus':1,'operation:4:bonus':0,
       'banner:armor':'3,3'},
      [dict(kind='EndBattle'),dict(kind='BeginBattle'),dict(kind='UnitUse'),dict(kind='EndBattle'),dict(kind='EndTurn')])
for reason,fields in [('未选择部队',dict(omitUnitTarget=True)),('其他玩家部队',dict(foreignUnitTarget=True)),
                      ('已经销毁的部队',dict(unitDestroyed=True))]:
    honor(False,reason+'：保留手牌，拒绝附着',{'hand':1,'banner:count':0},**fields)
    CASES[-1]['expectedException']='InvalidOperationException: 旗帜必须分配给自有且未销毁的部队'

honor(False,'已附着的旗帜不能直接焚毁，仍保留原实例与加成',
      {'operation:0:success':False,'operation:0:count':1,'operation:0:removed':0},
      [dict(kind='PlayCard',cardId='items_000',enhanced=True)])
for strong in (False,True):
    honor(strong,'原再生在两场战斗之间重整同一部队，再次发动按原旗帜次数奖励',
          {'cards:played':1,'banner:total':2,'banner:unique':2,'MeleePool':3,'HealingPool':0,'Fame':1 if strong else 2,
           'operation:0:success':True,'operation:0:fame':1,'operation:2:success':True,
           'operation:2:healing':1,'operation:3:healing':0,
           'operation:4:success':True,'operation:4:attack':3,'operation:4:fame':1 if strong else 2},
          [dict(kind='UnitUse'),dict(kind='EndBattle'),dict(kind='PlayCard',cardId='advanced_card_015'),
           dict(kind='BeginBattle'),dict(kind='UnitUse')],extraHandCards=['advanced_card_015'])

command_limits=['列明真实招募、附着和回收范围已检查；PvP夺旗、特殊招募限制、完整游戏组合仍待联验']
def command(title, expected, ops=(), setup=None, **fields):
    options=dict(unitIds=['army_2_006'],recruitOffer=['army_2_006'],initialHandCount=1,initialDrawCount=0,
                 initialDiscardCards=[],playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[])
    options.update(fields)
    defaults={'InfluencePool':4,'TempCommandSlots':0,'CommandSlots':1,'FreeSlots':0,'BannerCommandCount':0,
              'hand':0,'cards:played':1,'cards:removed':0,'discard':0,'banner:count':0,'banner:total':1,'banner:unique':1}
    defaults.update(expected)
    operation_case('items_017',False,title,defaults,ops,setup=dict({'InBattle':False,'SkipDraw':True},**(setup or {})),**options)
    CASES[-1]['limitations']=command_limits

select_command=dict(kind='SelectCommandBanner')
recruit_farmer=dict(kind='Recruit',recruitLocation='Village')
command('影响力4不会增加通用指挥槽，未用旗帜回合末弃掉',
        {'InfluencePool':0,'cards:played':0,'discard':1,'operation:0:success':True,'operation:0:commanded':0,
         'operation:0:freeSlots':0,'operation:1:success':False},
        [dict(kind='EndTurn'),dict(kind='SelectCommandBanner')])
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_017_00')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)

command('满编时预选真实旗帜，支付影响力4招募原农民并代替指挥标记',
        {'InfluencePool':0,'cards:played':0,'banner:count':1,'BannerCommandCount':1,'banner:attached':'none,items_017',
         'operation:0:success':True,'operation:0:commanded':0,'operation:1:success':True,
         'operation:1:influence':0,'operation:1:commanded':1,'operation:1:freeSlots':0,
         'operation:2:success':True,'operation:2:faceDown':'False,True','MovementPool':2},
        [select_command,recruit_farmer,dict(kind='UnitUse',targetIndex=1,option=2)])
command('满编未选择旗帜时拒绝普通招募，保留影响力与供应',
        {'operation:0:success':False,'operation:0:influence':4,'recruit:offer':'army_2_006'},[recruit_farmer])
for title,offer,location in [('原暴徒费用5但只有影响力4',['army_2_011'],'Village'),
                              ('原农民不可在修道院招募',['army_2_006'],'Monastery')]:
    command(title+'：失败保留所选旗帜、供应与资源',
            {'banner:selected':'items_017','operation:0:success':True,'operation:1:success':False,
             'operation:1:influence':4,'operation:1:commanded':0,'recruit:offer':offer[0]},
            [select_command,dict(kind='Recruit',recruitLocation=location)],recruitOffer=offer)
command('先用普通指挥标记招募，再把本回合新部队改由旗帜指挥',
        {'InfluencePool':0,'CommandSlots':2,'FreeSlots':1,'BannerCommandCount':1,'cards:played':0,'banner:count':1,
         'operation:0:success':True,'operation:0:commanded':0,'operation:0:freeSlots':0,
         'operation:1:success':True,'operation:1:commanded':1,'operation:1:freeSlots':1},
        [recruit_farmer,dict(kind='AssignCommandBanner',targetIndex=1)],setup={'player:ExtraCommandSlots':1})
command('原有部队不属于本回合招募，不能绑定指挥旌旗',
        {'operation:0:success':False,'operation:0:commanded':0,'operation:0:freeSlots':0},[dict(kind='AssignCommandBanner')])
command('取消预选后满编招募仍拒绝，不产生临时指挥槽',
        {'banner:selected':'none','operation:2:success':False,'operation:2:influence':4},
        [select_command,dict(kind='SelectCommandBanner',option=1),recruit_farmer])
for spare in (False,True):
    command(('有空闲普通标记也不能接管' if spare else '满编')+'：轮末回收指挥旗帜必移除该部队',
            {'InfluencePool':0,'CommandSlots':2 if spare else 1,'FreeSlots':1 if spare else 0,
             'cards:played':0,'draw':1,'banner:owned':'army_2_006','banner:count':0,'BannerCommandCount':0,
             'operation:1:success':True,'operation:1:commanded':1,'operation:2:commanded':1,
             'operation:3:commanded':0,'operation:3:draw':1},
            [select_command,recruit_farmer,dict(kind='ChooseBannerReturn',targetIndex=1,option=1),dict(kind='EndRound')],
            setup={'player:ExtraCommandSlots':int(spare)})
command('实际发动使旗帜翻背面，跨回合/轮末保留，新轮随部队重整翻正面',
        {'InfluencePool':0,'cards:played':0,'BannerCommandCount':1,'banner:count':1,'banner:faceDown':'False,False',
         'operation:2:success':True,'operation:2:faceDown':'False,True','operation:3:commanded':1,
         'operation:4:commanded':1,'operation:5:faceDown':'False,False'},
        [select_command,recruit_farmer,dict(kind='UnitUse',targetIndex=1,option=2),dict(kind='EndTurn'),dict(kind='EndRound'),dict(kind='NewRound')])
command('解散旗帜指挥部队，原旗帜弃牌，普通指挥槽仍满',
        {'InfluencePool':0,'cards:played':0,'discard':1,'banner:owned':'army_2_006',
         'operation:2:success':True,'operation:2:commanded':0,'operation:2:freeSlots':0,'operation:2:discard':1},
        [select_command,recruit_farmer,dict(kind='Disband',targetIndex=1)])
command('给旗帜指挥部队换荣誉旌旗会使原部队离场，新旗帜保持未附着',
        {'InfluencePool':0,'cards:played':0,'discard':1,'hand':1,'banner:total':2,'banner:unique':2,'banner:owned':'army_2_006',
         'operation:3:success':True,'operation:3:commanded':0,'operation:3:discard':1},
        [select_command,recruit_farmer,dict(kind='SelectUnit',targetIndex=1),dict(kind='PlayCard',cardId='items_000')],
        extraHandCards=['items_000'])
command('原荣耀号召免费取得部队并用预选指挥旗帜指挥，白黑魔力实际支付',
        {'cards:played':1,'BannerCommandCount':1,'banner:count':1,'banner:total':2,'banner:unique':2,
         'token:White':1,'token:Black':1,'operation:1:success':True,'operation:1:influence':4,
         'operation:1:commanded':1,'operation:2:success':True,'MovementPool':2},
        [select_command,dict(kind='PlayCard',cardId='magic_020',enhanced=True),dict(kind='UnitUse',targetIndex=1,option=2)],
        extraHandCards=['magic_020'],setup={'DayPart':'Night'})
command('同一旗帜已经绑定，不能再供第二次满编招募使用',
        {'InfluencePool':0,'cards:played':0,'BannerCommandCount':1,'banner:count':1,
         'operation:2:success':False,'operation:3:success':False,'recruit:offer':'army_2_006'},
        [select_command,recruit_farmer,select_command,recruit_farmer],recruitOffer=['army_2_006','army_2_006'])

fortitude_limits=['列明附着、防护次数、毒素/麻痹/自伤和治疗；PvP及未实现的其他怪物附加效果、完整组合仍待联验']
def fortitude(strong,title,expected,ops=(),setup=None,**fields):
    options=dict(unitIds=['army_2_006','army_2_006'],initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],
                 playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[])
    options.update(fields)
    defaults={'hand':0,'Wounds':0,'cards:played':0,'cards:removed':int(strong),'FirstWoundIgnored':False,
              'banner:count':0 if strong else 1,'banner:total':1,'banner:unique':1}
    defaults.update(expected)
    operation_case('items_018',strong,title,defaults,ops,
                   setup=dict({'InBattle':not strong,'SkipDraw':True,'player:ExtraCommandSlots':1},**(setup or {})),**options)
    CASES[-1]['limitations']=fortitude_limits

protect=dict(kind='SelectFortitudeProtection',option=1)
damage_unit=dict(kind='Damage',unitTargets=[0])
poison_source=[dict(id='monster_005',originalDefinition=True)]
ordinary_source=[dict(id='monster_025',originalDefinition=True)]
fortitude(False,'原毒素攻击3被防护，不获得双伤牌，同场再次承伤拒绝',
          {'unitWounds':0,'operation:0:success':True,'operation:1:success':True,
           'operation:1:spent':'True,False','operation:1:wounds':'0,0','operation:2:success':False,
           'operation:2:wounds':'0,0'},[protect,damage_unit,damage_unit],enemies=poison_source)
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_018_00')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
fortitude(True,'实际治疗所有自有部队，不治疗英雄，也不直接重整耗竭部队',
          {'hand':2,'Wounds':2,'banner:total':3,'banner:unique':3,'banner:wounds':'0,0','banner:ready':'False,False',
           'banner:healed':2,'operation:0:success':False,'operation:0:healed':2},
          [dict(kind='UnitUse')],initialUnitWounds=[1,2],setup={'player:Wounds':2})
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_018_01')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
fortitude(False,'原麻痹攻击3第一次不销毁，下一场本轮无防护时销毁并弃旗',
          {'banner:count':0,'discard':1,'banner:owned':'army_2_006',
           'operation:1:success':True,'operation:1:destroyed':'False,False','operation:1:spent':'True,False',
           'operation:4:success':True,'operation:4:destroyed':'True,False','operation:4:discard':1},
          [protect,damage_unit,dict(kind='EndBattle'),dict(kind='BeginBattle'),damage_unit],
          enemies=[dict(id='monster_034',originalDefinition=True)])
fortitude(False,'不选择防护时原毒素仍造成两张部队伤牌',
          {'unitWounds':2,'banner:spent':'False,False','operation:0:success':True,'operation:0:wounds':'2,0'},
          [damage_unit],enemies=poison_source)
fortitude(False,'取消已选防护后仍受伤，不扣本轮次数',
          {'unitWounds':2,'banner:spent':'False,False','operation:2:success':True,'operation:2:wounds':'2,0'},
          [protect,dict(kind='SelectFortitudeProtection',option=0),damage_unit],enemies=poison_source)
fortitude(False,'附着第一支部队，第二支部队受毒素不被保护',
          {'banner:wounds':'0,2','banner:spent':'False,False','operation:1:success':True,'operation:1:wounds':'0,2'},
          [protect,dict(kind='Damage',unitTargets=[1])],enemies=poison_source)
fortitude(False,'次数跨战斗和回合保留，仅实际新轮恢复后可再次防护',
          {'unitWounds':0,'banner:spent':'True,False','operation:1:spent':'True,False','operation:3:spent':'True,False',
           'operation:4:spent':'False,False','operation:7:success':True,'operation:7:spent':'True,False'},
          [protect,damage_unit,dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='NewRound'),
           dict(kind='BeginBattle'),protect,damage_unit],enemies=ordinary_source)
fortitude(False,'乌顿剑士自伤被忽略且攻击6保留，随后仍可正常分配敌人伤害',
          {'MeleePool':6,'unitWounds':1,'operation:1:success':True,'operation:1:wounds':'0,0',
           'operation:1:attack':6,'operation:1:spent':'True,False','operation:2:success':True,'operation:2:wounds':'1,0'},
          [protect,dict(kind='UnitUse',option=1),damage_unit],unitIds=['army_2_014','army_2_006'],
          initialReadyUnitIndices=[0],enemies=ordinary_source)
fortitude(False,'原守护者魔像先以物理抗性吸收攻击3，无伤时不翻旗，但同场不能重复承伤',
          {'unitWounds':0,'banner:spent':'False,False','operation:1:success':True,'operation:1:wounds':'0,0',
           'operation:2:success':False},[protect,damage_unit,damage_unit],
          unitIds=['army_2_001','army_2_006'],enemies=ordinary_source)
fortitude(False,'回合结束取消未触发的防护选择，下回合未重新选择则正常受伤',
          {'unitWounds':1,'banner:spent':'False,False','operation:1:protection':'False,False',
           'operation:3:success':True,'operation:3:wounds':'1,0'},
          [protect,dict(kind='EndTurn'),dict(kind='BeginBattle'),damage_unit],enemies=ordinary_source)
fortitude(True,'战斗中焚毁必须拒绝，保留真实神器与部队伤牌',
          {'hand':1,'cards:removed':0,'unitWounds':1,'banner:wounds':'1,2','banner:healed':0},
          initialUnitWounds=[1,2],setup={'InBattle':True})
CASES[-1]['expectedException']='InvalidOperationException: 战斗中不能焚毁强韧旌旗治疗部队'
fortitude(True,'焚毁后施放原痊愈，仅实际治疗过的部队重整并可发动',
          {'cards:played':1,'banner:total':2,'banner:unique':2,'MovementPool':2,'HealingPool':2,'token:Green':1,
           'banner:healed':1,'operation:0:success':True,'operation:0:ready':'True,False',
           'operation:1:success':True,'operation:1:ready':'False,False'},
          [dict(kind='PlayCard',cardId='magic_001'),dict(kind='UnitUse',option=2)],
          initialUnitWounds=[1,0],extraHandCards=['magic_001'])
for reason,fields in [('未选部队',dict(omitUnitTarget=True)),('其他玩家部队',dict(foreignUnitTarget=True)),
                      ('已销毁部队',dict(unitDestroyed=True))]:
    fortitude(False,reason+'不能附着，保留手牌与卡区',{'hand':1,'banner:count':0},**fields)
    CASES[-1]['expectedException']='InvalidOperationException: 旗帜必须分配给自有且未销毁的部队'

courage_limits=['列明真实附着、战斗外每轮重整、焚毁及牌区生命周期；PvP转移与完整跨卡组合仍待联验']
def courage(strong,title,expected,ops=(),setup=None,**fields):
    options=dict(unitIds=['army_2_006','army_2_006'],initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],
                 playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[])
    options.update(fields)
    defaults={'hand':0,'Wounds':0,'cards:played':0,'cards:removed':int(strong),
              'banner:count':0 if strong else 1,'banner:total':1,'banner:unique':1,'banner:healed':0}
    defaults.update(expected)
    operation_case('items_003',strong,title,defaults,ops,
                   setup=dict({'InBattle':False,'SkipDraw':True,'player:ExtraCommandSlots':1},**(setup or {})),**options)
    CASES[-1]['limitations']=courage_limits

ready_courage=dict(kind='UseCourage')
farmer_move=dict(kind='UnitUse',option=2)
courage(False,'附着不自动重整；主动翻旗后原农民移动2，同轮再次翻旗拒绝',
        {'MovementPool':2,'banner:ready':'False,False','banner:spent':'True,False',
         'operation:0:success':False,'operation:0:ready':'False,False',
         'operation:1:success':True,'operation:1:ready':'True,False','operation:1:spent':'True,False',
         'operation:2:success':True,'operation:3:success':False},
        [farmer_move,ready_courage,farmer_move,ready_courage])
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_003_00')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
courage(True,'实际焚毁后两支原农民均可发动，合计移动4',
        {'MovementPool':4,'banner:ready':'False,False','operation:0:success':True,
         'operation:0:ready':'False,True','operation:1:success':True},
        [farmer_move,dict(kind='UnitUse',targetIndex=1,option=2)])
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_003_01')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
courage(False,'战斗中翻旗拒绝且不扣次数；战斗结束后才可重整',
        {'banner:ready':'True,False','banner:spent':'True,False','operation:0:success':False,
         'operation:0:spent':'False,False','operation:0:ready':'False,False','operation:2:success':True},
        [ready_courage,dict(kind='EndBattle'),ready_courage],setup={'InBattle':True})
for strong in [False,True]:
    courage(strong,'重整保留部队两张伤牌，受伤农民仍不能发动，也不治疗英雄',
            {'hand':1,'Wounds':1,'banner:total':2,'banner:unique':2,'unitWounds':2,
             'banner:ready':'True,True' if strong else 'True,False','MovementPool':0,
             f'operation:{0 if strong else 1}:success':False},
            ([ready_courage] if not strong else [])+[farmer_move],initialUnitWounds=[2,0],setup={'player:Wounds':1})
courage(False,'次数跨回合保留，仅新轮翻正后可再次发动',
        {'MovementPool':2,'banner:ready':'True,True','banner:spent':'True,False',
         'operation:2:spent':'True,False','operation:3:success':False,
         'operation:4:spent':'False,False','operation:5:success':True,'operation:6:success':True},
        [ready_courage,farmer_move,dict(kind='EndTurn'),ready_courage,dict(kind='NewRound'),farmer_move,ready_courage])
courage(False,'同ID第二张原旗帜独立计次；替换把第一张实际实例送弃牌',
        {'MovementPool':2,'banner:total':2,'banner:unique':2,'discard':1,'banner:spent':'True,False','banner:ready':'True,False',
         'operation:2:spent':'False,False','operation:2:discard':1,'operation:3:success':True},
        [ready_courage,farmer_move,dict(kind='PlayCard',cardId='items_003'),ready_courage],extraHandCards=['items_003'])
courage(False,'附着期间不在手牌，不能直接焚毁；拒绝后附着与就绪不变',
        {'banner:ready':'False,False','banner:spent':'False,False','operation:0:success':False},
        [dict(kind='PlayCard',cardId='items_003',enhanced=True)])
courage(False,'没有附着旗帜的另一支部队不能使用重整',
        {'banner:ready':'False,False','banner:spent':'False,False','operation:0:success':False},
        [dict(kind='UseCourage',targetIndex=1)])
for reason,fields in [('未选部队',dict(omitUnitTarget=True)),('其他玩家部队',dict(foreignUnitTarget=True)),
                      ('已销毁部队',dict(unitDestroyed=True))]:
    courage(False,reason+'不能附着，保留实际手牌',{'hand':1,'banner:count':0},**fields)
    CASES[-1]['expectedException']='InvalidOperationException: 旗帜必须分配给自有且未销毁的部队'
courage(True,'战斗中焚毁拒绝，保留手牌与全部部队耗竭状态',
        {'hand':1,'cards:removed':0,'banner:ready':'False,False'},setup={'InBattle':True})
CASES[-1]['expectedException']='InvalidOperationException: 战斗中不能焚毁勇气旌旗重整部队'
courage(False,'轮末选择回收实际旗帜，新轮抽回再附着后次数重置',
        {'banner:spent':'True,False','banner:ready':'True,True','operation:2:draw':1,
         'operation:2:count':0,'operation:4:spent':'False,False','operation:5:success':True},
        [ready_courage,dict(kind='ChooseBannerReturn',option=1),dict(kind='EndRound'),dict(kind='NewRound'),
         dict(kind='PlayCard',cardId='items_003'),ready_courage])

protection_limits=['列明附着护甲/火冰抗性与回合末英雄新伤移除；PvP及更完整伤牌流转组合仍待联验']
def protection(strong,title,expected,ops=(),setup=None,**fields):
    options=dict(unitIds=['army_2_006','army_2_006'],initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],
                 playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[])
    options.update(fields)
    defaults={'hand':0,'Wounds':0,'cards:played':0,'cards:removed':int(strong),
              'banner:count':0 if strong else 1,'banner:total':1,'banner:unique':1,
              'UnitsResistAll':False,'UnitArmorBonus':0,'banner:healed':0}
    defaults.update(expected)
    operation_case('items_002',strong,title,defaults,ops,
                   setup=dict({'InBattle':True,'SkipDraw':True,'player:ExtraCommandSlots':1},**(setup or {})),**options)
    CASES[-1]['limitations']=protection_limits

protection(False,'原物理攻击3仍令附着农民受伤，护甲4不代表物理抗性',
           {'unitWounds':1,'banner:armor':'4,3','banner:elementalResist':'True,False',
            'operation:0:success':True,'operation:0:wounds':'1,0'},[damage_unit],enemies=ordinary_source)
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_002_00')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
hero_damage=dict(kind='Damage')
protection(True,'焚毁后毒素产生手中与弃牌新伤，战斗结束不移除，回合末只清新伤',
           {'hand':2,'Wounds':2,'discard':1,'cards:removed':5,'banner:total':8,'banner:unique':8,
            'banner:wounds':'2,0','banner:cleanup':False,'banner:newWounds':0,
            'operation:0:heroWounds':4,'operation:0:discardWounds':3,'operation:0:newWounds':4,
            'operation:1:heroWounds':4,'operation:2:heroWounds':2,'operation:2:removed':5},
           [hero_damage,dict(kind='EndBattle'),dict(kind='EndTurn')],enemies=poison_source,
           initialUnitWounds=[2,0],initialDiscardCards=['w'],setup={'player:Wounds':2})
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_002_01')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
for title,enemy,wounds in [('火焰毒素攻击6先吸收4，再受毒素双伤且无英雄余伤','monster_001',2),
                           ('寒冰麻痹攻击3完全吸收，部队不被销毁','monster_034',0),
                           ('寒冷火焰残暴攻击4翻倍为8，双抗性吸收4后毒素双伤','monster_008',2)]:
    protection(False,title,{'unitWounds':wounds,'banner:armor':'4,3','operation:0:success':True,
                           'operation:0:destroyed':'False,False'},[damage_unit],enemies=[dict(id=enemy,originalDefinition=True)])
protection(False,'第二支无旗农民不获得冰抗性，被原麻痹攻击销毁',
           {'banner:armor':'4','operation:0:success':True,'operation:0:destroyed':'False,True'},
           [dict(kind='Damage',unitTargets=[1])],enemies=[dict(id='monster_034',originalDefinition=True)])
protection(False,'附着护甲和冰抗性跨战斗与回合保留，下一场仍能承伤',
           {'unitWounds':0,'banner:armor':'4,3','operation:0:success':True,'operation:4:success':True,
            'operation:4:destroyed':'False,False'},
           [damage_unit,dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='BeginBattle'),damage_unit],
           enemies=[dict(id='monster_034',originalDefinition=True)])
protection(False,'换成勇气旗后失去冰抗性，麻痹销毁部队并丢弃两张真实旗帜',
           {'banner:count':0,'banner:total':2,'banner:unique':2,'discard':2,
            'operation:0:discard':1,'operation:1:destroyed':'True,False','operation:1:success':True},
           [dict(kind='PlayCard',cardId='items_003'),damage_unit],extraHandCards=['items_003'],
           enemies=[dict(id='monster_034',originalDefinition=True)])
for reason,fields in [('未选部队',dict(omitUnitTarget=True)),('其他玩家部队',dict(foreignUnitTarget=True)),
                      ('已销毁部队',dict(unitDestroyed=True))]:
    protection(False,reason+'不能附着，保留手牌且不产生全体抗性',{'hand':1,'banner:count':0},**fields)
    CASES[-1]['expectedException']='InvalidOperationException: 旗帜必须分配给自有且未销毁的部队'
protection(True,'可选择不移除本回合新伤；焚毁仍永久消耗原神器',
           {'hand':2,'Wounds':2,'discard':2,'banner:total':5,'banner:unique':5,'banner:cleanup':False,
            'operation:1:success':True,'operation:3:heroWounds':2,'operation:3:discardWounds':2},
           [hero_damage,dict(kind='ChooseProtectionCleanup',option=0),dict(kind='EndBattle'),dict(kind='EndTurn')],enemies=poison_source)
protection(True,'原宁静抽回牌库旧伤不算新伤，回合末只移除本回合遭受的两伤',
           {'hand':1,'Wounds':1,'discard':1,'cards:removed':3,'banner:total':5,'banner:unique':5,
            'operation:0:newWounds':2,'operation:2:heroWounds':3,'operation:2:success':True,
            'operation:3:heroWounds':1},
           [hero_damage,dict(kind='EndBattle'),dict(kind='PlayCard',cardId='basic_card_001',option=1),dict(kind='EndTurn')],
           enemies=ordinary_source,extraHandCards=['basic_card_001'],initialDrawCards=['w'])
protection(True,'回合末能力到期，下一回合重新受伤不能继续免费移除',
           {'hand':2,'Wounds':2,'discard':0,'cards:removed':3,'banner:total':5,'banner:unique':5,
            'operation:2:heroWounds':0,'operation:5:heroWounds':2,'operation:7:heroWounds':2},
           [hero_damage,dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='StartTurn'),dict(kind='BeginBattle'),
            hero_damage,dict(kind='EndBattle'),dict(kind='EndTurn')],enemies=ordinary_source)
protection(True,'部队本回合受毒素双伤也不被焚毁效果治疗',
           {'unitWounds':2,'banner:wounds':'2,0','operation:0:success':True,'operation:2:wounds':'2,0'},
           [damage_unit,dict(kind='EndBattle'),dict(kind='EndTurn')],enemies=poison_source)

protection(True,'先焚毁也不能避免原火焰攻击9导致昏迷，未打出的原卡照常弃掉',
           {'discard':1,'cards:removed':6,'banner:total':7,'banner:unique':7,
            'operation:0:heroWounds':5,'operation:0:discard':1,'operation:0:removed':1,
            'operation:2:heroWounds':0,'operation:2:removed':6},
           [hero_damage,dict(kind='EndBattle'),dict(kind='EndTurn')],
           enemies=[dict(id='monster_036',originalDefinition=True)],extraHandCards=['basic_card_000'])

# Paste before def export() in tools/build_all_card_verification.py.
# Requires the small controller fragment in scene_controller_fragments.md.
gold_limits = ['本例列明实际金币包出牌、资源消费与卡区；完整游戏通用交涉/升级奖励等组合仍待联验']

def gold_case(strong, title, expected, ops=(), **fields):
    options = dict(unitIds=['army_2_006'], initialHandCount=1, initialDrawCount=0,
                   initialDiscardCards=[], playFromHand=True, discardIndex=-1, removeIndex=-1, discardIndices=[])
    options.update(fields)
    defaults = {'hand':0, 'cards:played':int(not strong), 'cards:removed':int(strong),
                'discard':0, 'Fame':10 if strong else 9, 'InfluencePool':9 if strong else 4,
                'gold:total':1, 'gold:unique':1}
    defaults.update(expected)
    operation_case('items_010', strong, title, defaults, ops,
                   setup={'InBattle':False, 'SkipDraw':True, 'player:Fame':7, 'player:ExtraCommandSlots':2}, **options)
    CASES[-1]['limitations'] = gold_limits

for strong in [False, True]:
    gold_case(strong, '原金币包' + ('焚毁' if strong else '基础') + '无需魔力，重复同实例不能再次得影响力与名望',
              {'token:Red':2, 'token:Blue':2, 'crystal:Blue':1,
               'operation:0:success':False, 'operation:0:influence':9 if strong else 4,
               'operation:0:fame':10 if strong else 9, 'gold:zone0':'removed' if strong else 'played'},
              [dict(kind='PlayArtifactInstance', cardId='items_010', targetIndex=0, enhanced=strong)])
    replacement=CASES.pop(); legacy=next(c for c in CASES if c['id']==f'items_010_{int(strong):02}')
    replacement['id']=legacy['id']; legacy.clear(); legacy.update(replacement)

gold_case(False, '基础金币包回合弃牌、轮末重洗、次轮抽回同实体再次使用',
          {'Fame':11, 'InfluencePool':4, 'gold:zone0':'played',
           'operation:0:influence':0, 'operation:0:fame':9, 'operation:0:zone0':'discard',
           'operation:1:zone0':'draw', 'operation:2:zone0':'hand',
           'operation:3:success':True, 'operation:3:fame':11},
          [dict(kind='EndTurn'),dict(kind='EndRound'),dict(kind='NewRound'),
           dict(kind='PlayCard',cardId='items_010')])
gold_case(True, '焚毁金币包回合末影响力清零，跨轮不回手、名望保留',
          {'InfluencePool':0,'gold:zone0':'removed',
           'operation:0:influence':0,'operation:0:fame':10,
           'operation:2:hand':0,'operation:2:removed':1,'operation:3:success':False},
          [dict(kind='EndTurn'),dict(kind='EndRound'),dict(kind='NewRound'),
           dict(kind='PlayArtifactInstance',cardId='items_010',targetIndex=0,enhanced=True)])
gold_case(False, '两张同ID实体分别基础与焚毁，已用实例重试拒绝且卡区不串换',
          {'InfluencePool':13,'Fame':12,'cards:removed':1,'gold:total':2,'gold:unique':2,
           'gold:zone0':'played','gold:zone1':'removed',
           'operation:0:success':False,'operation:0:zone1':'hand',
           'operation:1:success':True,'operation:1:zone0':'played','operation:1:zone1':'removed',
           'operation:2:success':False,'operation:2:influence':13,'operation:2:fame':12},
          [dict(kind='PlayArtifactInstance',cardId='items_010',targetIndex=0),
           dict(kind='PlayArtifactInstance',cardId='items_010',targetIndex=1,enhanced=True),
           dict(kind='PlayArtifactInstance',cardId='items_010',targetIndex=1,enhanced=True)],
          extraHandCards=['items_010'])
for strong in [False,True]:
    remaining = 5 if strong else 0
    gold_case(strong, '金币包影响力真实支付原农民费用4，获得部队后实际发动影响力2',
              {'InfluencePool':remaining+2,'recruit:offer':'',
               'recruit:owned':'army_2_006,army_2_006','recruit:ready':'False,False',
               'operation:0:success':True,'operation:0:cost':4,'operation:0:influence':remaining,
               'operation:0:units':2,'operation:1:success':True,'operation:1:influence':remaining+2,
               'operation:1:fame':10 if strong else 9},
              [dict(kind='Recruit',targetIndex=0,recruitLocation='Village'),
               dict(kind='UnitUse',targetIndex=1,option=1)],recruitOffer=['army_2_006'])
gold_case(False, '金币包基础影响力4不足原暴徒费用5，招募失败保留影响力、供给与已获名望',
          {'recruit:offer':'army_2_011','recruit:owned':'army_2_006',
           'operation:0:success':False,'operation:0:cost':5,'operation:0:influence':4,
           'operation:0:units':1,'operation:0:fame':9,'gold:zone0':'played'},
          [dict(kind='Recruit',targetIndex=0,recruitLocation='Village')],recruitOffer=['army_2_011'])
for strong in [False,True]:
    gold_case(False, '手中仍有同ID原牌时，非持有的相同牌面实例不能' + ('焚毁' if strong else '出牌'),
              {'hand':1,'gold:total':2,'gold:unique':2,'gold:zone0':'played','gold:zone1':'hand',
               'operation:0:success':False,'operation:0:influence':4,'operation:0:fame':9},
              [dict(kind='UnownedArtifact',cardId='items_010',enhanced=strong)],extraHandCards=['items_010'])


# Paste before def export(); requires WrathSnapshot observation described in README.
# Keep existing items_009_02..23 dice/module evidence unchanged, do not rerun them.
wrath_limits = ['本例联验原号角真实出牌/焚毁、掷骰与卡区或原怪物结算；其余既有骰面模块证据保留，完整游戏组合仍待联验']

def wrath_case(strong,title,expected,ops=(),**fields):
    options=dict(unitIds=['army_2_006'],initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],
                 playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[],
                 enemies=[dict(id='monster_017',originalDefinition=True)],manaRolls=[3])
    options.update(fields)
    defaults={'hand':0,'cards:played':int(not strong),'cards:removed':int(strong),'discard':0,
              'Wounds':0,'Fame':0,'SiegePool':5,'wrath:total':1,'wrath:unique':1}
    defaults.update(expected)
    operation_case('items_009',strong,title,defaults,ops,
                   setup={'InBattle':True,'SkipDraw':True},**options)
    CASES[-1]['limitations']=wrath_limits

wrath_case(False,'基础原号角红骰受1伤，重复同实体拒绝，攻城击败原城防目标后跨轮回手',
           {'hand':2,'cards:played':0,'SiegePool':0,'Wounds':1,'Fame':3,
            'wrath:total':2,'wrath:unique':2,'wrath:zone0':'hand',
            'dice:faces':'Red','dice:count':1,'operation:0:success':False,'operation:0:siege':5,
            'operation:1:printed':5,'operation:1:effective':5,'operation:1:required':4,'operation:1:kills':1,
            'operation:1:siege':0,'operation:1:fame':3,'operation:2:success':False,
            'operation:4:zone0':'discard','operation:5:zone0':'draw','operation:6:zone0':'hand'},
           [dict(kind='PlayArtifactInstance',cardId='items_009',targetIndex=0),
            dict(kind='RangedAttack',targetIndex=0),dict(kind='RangedAttack',targetIndex=0),
            dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='EndRound'),dict(kind='NewRound')],manaRolls=[0])
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_009_00')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
wrath_case(True,'零额外攻城仍为5且不掷骰，未用攻城战后清理，焚毁跨轮不回手',
           {'SiegePool':0,'wrath:zone0':'removed','dice:count':0,
            'operation:0:success':False,'operation:0:siege':5,'operation:1:siege':0,
            'operation:4:zone0':'removed','operation:4:hand':0},
           [dict(kind='PlayArtifactInstance',cardId='items_009',targetIndex=0,enhanced=True),
            dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='EndRound'),dict(kind='NewRound')],manaRolls=[],option=0)
replacement=CASES.pop();legacy=next(c for c in CASES if c['id']=='items_009_01')
replacement['id']=legacy['id'];legacy.clear();legacy.update(replacement)
wrath_case(True,'焚毁额外5全红触发昏迷，未打出原卡弃掉，现有攻城10仍击败原城防目标',
           {'hand':5,'cards:removed':1,'discard':1,'Wounds':5,'SiegePool':0,'Fame':3,
            'wrath:total':7,'wrath:unique':7,'wrath:zone0':'removed','wrath:zone1':'discard',
            'wrath:knockedOut':True,'dice:faces':'Red,Red,Red,Red,Red','dice:count':5,
            'operation:0:success':False,'operation:0:siege':10,'operation:0:knockedOut':True,
            'operation:1:printed':10,'operation:1:effective':10,'operation:1:required':4,'operation:1:kills':1},
           [dict(kind='PlayArtifactInstance',cardId='items_009',targetIndex=0,enhanced=True,option=5),
            dict(kind='RangedAttack',targetIndex=0)],option=5,manaRolls=[0,0,0,0,0],extraHandCards=['basic_card_000'])
for strong,bonus in [(False,1),(True,-1),(True,6)]:
    wrath_case(strong,'非法额外攻城'+str(bonus)+'保留实际手牌，不掷骰、不受伤且不产生攻击',
               {'hand':1,'cards:played':0,'cards:removed':0,'SiegePool':0,'dice:count':0,
                'wrath:zone0':'hand','token:Red':2,'crystal:Blue':1},option=bonus,manaRolls=[])
    CASES[-1]['expectedException']='InvalidOperationException: 愤怒号角额外攻击必须在允许范围内'
wrath_case(False,'两份原号角实体分别基础与焚毁额外2，白骰不受伤，重复焚毁拒绝',
           {'cards:removed':1,'SiegePool':12,'wrath:total':2,'wrath:unique':2,
            'wrath:zone0':'played','wrath:zone1':'removed','dice:faces':'White,White','dice:count':2,
            'operation:0:success':True,'operation:0:siege':12,'operation:1:success':False,'operation:1:siege':12},
           [dict(kind='PlayArtifactInstance',cardId='items_009',targetIndex=1,enhanced=True,option=2),
            dict(kind='PlayArtifactInstance',cardId='items_009',targetIndex=1,enhanced=True,option=2)],
           extraHandCards=['items_009'],manaRolls=[3,3,3])
for strong in [False,True]:
    wrath_case(False,'仍持有同ID原号角时，非持有实例不能'+('焚毁' if strong else '出牌')+'或再次掷骰',
               {'hand':1,'wrath:total':2,'wrath:unique':2,'wrath:zone0':'played','wrath:zone1':'hand',
                'dice:faces':'White','dice:count':1,'operation:0:success':False,'operation:0:siege':5},
               [dict(kind='UnownedArtifact',cardId='items_009',enhanced=strong)],extraHandCards=['items_009'],manaRolls=[3])


# Real assigned Fear Banner and explicitly selected burn targets.
fear_limits=['本例覆盖列明原部队、单攻击原敌、实际附着/焚毁及操作；单敌多段/召唤攻击、PvP、跨轮和更多组合仍待联验']
def fear(strong,title,expected,ops=(),setup=None,**fields):
    options=dict(unitIds=['army_2_006','army_2_006'],initialReadyUnitIndices=[0,1],
        initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],playFromHand=True,
        discardIndex=-1,removeIndex=-1,discardIndices=[],turnMachineStartPhase='Block',
        enemies=[dict(id='monster_025',originalDefinition=True) for _ in range(4)])
    options.update(fields)
    values={'hand':0,'Wounds':0,'Fame':0,'cards:played':0,'cards:removed':int(strong),
        'banner:count':0 if strong else 1,'banner:total':1,'banner:unique':1,
        'banner:ready':'True,True','banner:skip':'','banner:blocked':0}
    values.update(expected)
    operation_case('items_001',strong,title,values,ops,
        setup=dict({'SkipDraw':True,'player:ExtraCommandSlots':1},**(setup or {})),**options)
    CASES[-1]['limitations']=fear_limits

use_fear=dict(kind='UseFear',option=2)
def replace_fear(index):
    replacement=CASES.pop(); old=next(c for c in CASES if c['id']==f'items_001_{index:02}')
    replacement['id']=old['id'];old.clear();old.update(replacement)
fear(False,'附着不耗尽，指定第3个敌人取消；再用拒绝，其余三次攻击实际造成6伤',
    {'hand':6,'Wounds':6,'Fame':1,'banner:total':7,'banner:unique':7,'banner:ready':'False,True',
     'operation:0:success':True,'operation:0:skip':'2','operation:0:blocked':0,
     'operation:1:success':False,'operation:2:success':False,'operation:3:heroWounds':6},
    [use_fear,use_fear,dict(kind='UnitUse'),dict(kind='TurnDefense'),dict(kind='TurnFinish')])
replace_fear(0)
fear(True,'焚毁指定第4/2/3个敌人，仅第1个攻击造成2伤，战斗结束清除停攻',
    {'hand':2,'Wounds':2,'banner:total':3,'banner:unique':3,'operation:0:skip':'1,2,3',
     'operation:0:heroWounds':2,'operation:1:skip':''},
    [dict(kind='TurnDefense'),dict(kind='TurnFinish')],spellTargets=[3,1,2])
replace_fear(1)
for count in [0,1,2]:
    damage=(4-count)*2
    fear(True,f'焚毁明确选择{count}个原敌人，未选攻击实际承伤{damage}',
        {'hand':damage,'Wounds':damage,'banner:total':damage+1,'banner:unique':damage+1,
         'operation:0:skip':','.join(str(x) for x in sorted([3,1][:count])),
         'operation:0:heroWounds':damage},[dict(kind='TurnDefense'),dict(kind='TurnFinish')],spellTargets=[3,1][:count])
immune=[dict(id='monster_003',originalDefinition=True)]
fear(False,'奥术免疫不保护攻击：耗尽附着部队取消原敌寒冷火焰攻击，不计成功格挡',
    {'Fame':1,'banner:ready':'False,True','operation:0:skip':'0','operation:0:blocked':0,'operation:1:heroWounds':0},
    [dict(kind='UseFear'),dict(kind='TurnDefense'),dict(kind='TurnFinish')],enemies=immune)
fear(True,'焚毁整组选中普通与奥术免疫敌人，全部拒绝且原手牌不消费',
    {'hand':1,'cards:removed':0},spellTargets=[0,1],
    enemies=[dict(id='monster_025',originalDefinition=True)]+immune)
CASES[-1]['expectedException']='InvalidOperationException: 奥术免疫敌人不能成为恐惧旌旗焚毁效果的目标'
for label,extra,target,ready in [('受伤',dict(initialUnitWounds=[1,0]),0,'True,True'),
    ('耗尽',dict(initialReadyUnitIndices=[1]),0,'False,True'),('未附着的另一支',{},1,'True,True')]:
    fear(False,label+'部队不能发动恐惧旗，不奖励名望',
        {'banner:ready':ready,'operation:0:success':False},[dict(kind='UseFear',targetIndex=target)],**extra)
for target in [-1,4]:
    fear(False,f'不存在的敌人索引{target}拒绝且部队保持就绪',
        {'operation:0:success':False},[dict(kind='UseFear',option=target)])
for phase,ops in [('Ranged',[]),('AssignDamage',[dict(kind='TurnCloseBlock')]),
    ('Melee',[dict(kind='TurnDefense')]),('End',[dict(kind='TurnDefense'),dict(kind='TurnFinish')])]:
    damage=8 if phase in ('Melee','End') else 0
    fear(False,'真实调度器'+phase+'阶段不能发动，保留部队就绪和名望',
        {'hand':damage,'Wounds':damage,'banner:total':1+damage,'banner:unique':1+damage,
         f'operation:{len(ops)}:success':False,'banner:phase':phase},ops+[dict(kind='UseFear')],
         turnMachineStartPhase='Ranged' if phase=='Ranged' else 'Block')
fear(False,'已经成功格挡的原敌攻击不可再取消刷取名望',
    {'banner:blocked':1,'operation:1:success':False},
    [dict(kind='Block'),dict(kind='UseFear')],setup={'BlockPool':3})
fear(False,'同ID第二面原旗附着另一部队，已取消目标不消耗第二部队或重复奖励',
    {'Fame':1,'banner:count':2,'banner:total':2,'banner:unique':2,'banner:ready':'False,True',
     'banner:skip':'0','operation:3:success':False,'operation:3:ready':'False,True'},
    [dict(kind='UseFear'),dict(kind='SelectUnit',targetIndex=1),dict(kind='PlayCard',cardId='items_001'),
     dict(kind='UseFear',targetIndex=1)],extraHandCards=['items_001'])
for label,extra in [('无目标',dict(omitUnitTarget=True)),('其他玩家部队',dict(foreignUnitTarget=True)),('已销毁部队',dict(unitDestroyed=True))]:
    fear(False,label+'不能附着，保留实际原手牌',{'hand':1,'banner:count':0,
        'banner:ready':'False,True' if extra.get('unitDestroyed') else 'True,True'},**extra)
    CASES[-1]['expectedException']='InvalidOperationException: 旗帜必须分配给自有且未销毁的部队'
for targets in [[0,0],[0,1,2,3],[0,-1],[0,4]]:
    fear(True,'非法焚毁目标'+str(targets)+'整笔拒绝，不默认选择前三个',
        {'hand':1,'cards:removed':0},spellTargets=targets)
    CASES[-1]['expectedException']='InvalidOperationException: 恐惧旌旗必须选择至多三个不同的存活敌人'
fear(True,'非战斗焚毁拒绝，原手牌与资源守恒',{'hand':1,'cards:removed':0},
    turnMachineStartPhase='',setup={'InBattle':False})
CASES[-1]['expectedException']='InvalidOperationException: 恐惧旌旗焚毁效果只能用于本场战斗'
fear(False,'已经附着的实际旗不能当手牌再次焚毁',{'operation:0:success':False},
    [dict(kind='PlayArtifactInstance',cardId='items_001',enhanced=True)],spellTargets=[0])
fear(False,'替换为原勇气旗，恐惧旗实体弃掉且失去发动能力',
    {'discard':1,'banner:total':2,'banner:unique':2,'banner:attached':'items_003,none','operation:1:success':False},
    [dict(kind='PlayCard',cardId='items_003'),dict(kind='UseFear')],extraHandCards=['items_003'])


amulet_limits=['本例验证实际原护符、明确魔力选择/支付、列明地形或地点可见状态；公共骰取用、探索/传送揭示及完整费用叠加仍待联验']
def amulet(id,strong,title,expected,ops=(),setup=None,**fields):
    options=dict(unitIds=['army_2_006'],initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],
        playFromHand=True,discardIndex=-1,removeIndex=-1,discardIndices=[])
    options.update(fields)
    initial={f'token:{c}':0 for c in COLORS};initial.update({f'crystal:{c}':0 for c in COLORS})
    initial.update(SkipDraw=True);initial.update(setup or {})
    values={'hand':0,'Wounds':0,'cards:played':int(not strong),'cards:removed':int(strong),
        'amulet:total':1,'amulet:unique':1}
    values.update(expected)
    operation_case(id,strong,title,values,ops,setup=initial,**options)
    CASES[-1]['limitations']=amulet_limits
def amulet_replace(id,index):
    replacement=CASES.pop();old=next(c for c in CASES if c['id']==f'{id}_{index:02}')
    replacement['id']=old['id'];old.clear();old.update(replacement)
sun='items_014';dark='items_015'
amulet(sun,False,'黑夜森林实际付3移动；回合末恢复5费用、弃牌与金色许可失效',
    {'cards:played':0,'discard':1,'position:q':1,'token:Gold':0,'AllowGoldAtNight':False,'RevealSitesAsDay':False,
     'move:selectedCost':5,'operation:0:cost':3,'operation:0:movement':3,'operation:2:success':False},
    [move(1),dict(kind='EndTurn'),dict(kind='ConvertGold',option=0)],setup={'DayPart':'Night','MovementPool':6})
amulet_replace(sun,0)
amulet(sun,True,'黑夜焚毁得3金，实际选红支付原火焰魔球，剩2金且神器永久移除',
    {'RangedPool':5,'cards:played':1,'token:Gold':2,'token:Red':0,'amulet:total':2,'amulet:unique':2,
     'operation:0:success':True,'operation:1:success':True,'amulet:zone0':'removed','amulet:zone1':'played'},
    [dict(kind='ConvertGold',option=0),dict(kind='PlayCard',cardId='magic_009')],
    setup={'DayPart':'Night'},extraHandCards=['magic_009'])
amulet_replace(sun,1)
amulet(dark,False,'白昼黑暗护符给黑魔力，沙漠实际付3，回合末恢复5和黑色许可失效',
    {'cards:played':0,'discard':1,'position:q':1,'token:Black':0,'AllowBlackAtDay':False,
     'move:selectedCost':5,'operation:0:cost':3,'operation:0:movement':3},
    [move(1),dict(kind='EndTurn')],option=5,destinationTerrain='Desert',setup={'DayPart':'Day','MovementPool':6})
amulet_replace(dark,0)
amulet(dark,True,'白昼选择红黑黑，实际支付红黑施放原火焰风暴并得1伤；真实昼夜不变',
    {'SiegePool':8,'hand':1,'Wounds':1,'cards:played':1,'token:Black':1,'token:Red':0,
     'DayPart':'Day','AllowBlackAtDay':True,'amulet:total':3,'amulet:unique':3,'operation:0:success':True},
    [dict(kind='PlayCard',cardId='magic_009',enhanced=True)],option=5<<8|5<<16,extraHandCards=['magic_009'],setup={'DayPart':'Day'})
amulet_replace(dark,1)
amulet(sun,False,'白昼金色选蓝，实际强化原耐力再支付平原移动2',
    {'MovementPool':2,'token:Gold':0,'token:Blue':0,'cards:played':2,'position:q':1,
     'amulet:total':2,'amulet:unique':2,'AllowGoldAtNight':False,'operation:1:success':True,'operation:2:cost':2},
    [dict(kind='ConvertGold',option=1),dict(kind='PlayCard',cardId='basic_card_014',enhanced=True),move(1)],
    extraHandCards=['basic_card_014'],destinationTerrain='Plains')
amulet(sun,True,'白昼三金分别选绿白蓝，三个真实标记各消费一次',
    {'token:Gold':0,'token:Green':1,'token:White':1,'token:Blue':1,'AllowGoldAtNight':False,
     'operation:0:success':True,'operation:1:success':True,'operation:2:success':True},
    [dict(kind='ConvertGold',option=i) for i in [2,3,1]])
amulet(sun,False,'金色不能替代黑色或金色，合法选红后无库存再次转换拒绝',
    {'token:Gold':0,'token:Red':1,'operation:0:success':False,'operation:0:Gold':1,
     'operation:1:success':False,'operation:1:Gold':1,'operation:2:success':True,'operation:3:success':False},
    [dict(kind='ConvertGold',option=i) for i in [5,4,0,0]],setup={'DayPart':'Night'})
for id,time,terrain in [(sun,'Night','Forest'),(dark,'Day','Desert')]:
    amulet(id,False,'已有地形费用2不被护符抬高，连续实际移动各消费2',
        {'MovementPool':0,'position:q':2,'operation:0:cost':2,'operation:1:cost':2},
        [move(1),move(2)],setup={'DayPart':time,'MovementPool':4,'terrainCost:'+terrain:2},destinationTerrain=terrain)
amulet(sun,False,'皓日从夜间翻开相邻要塞和远方已探索遗迹；空地移动后揭示魔法塔',
    {'cards:played':0,'discard':1,'position:q':1,'amulet:revealed':3,'RevealSitesAsDay':False,
     'amulet:site:0,1':True,'amulet:site:2,0':True,'amulet:site:4,0':True,
     'amulet:site:0,0':False,'amulet:site:5,0':False,
     'operation:0:cost':2,'operation:0:revealed':3,'operation:1:revealed':3},
    [move(1),dict(kind='EndTurn')],setup={'DayPart':'Night','MovementPool':2},destinationTerrain='Plains',revealedDistance=4,
    mapPlaces=[dict(q=0,r=1,type='Keep',enemyDefinition=dict(id='monster_025',originalDefinition=True)),
        dict(q=2,r=0,type='MageTower',enemyDefinition=dict(id='monster_008',originalDefinition=True)),
        dict(q=4,r=0,type='AncientRuin',ruinToken='Altar'),dict(q=0,r=0,type='Dungeon'),
        dict(q=5,r=0,type='AncientRuin',ruinToken='Altar')])
for color,name in enumerate(COLORS):
    amulet(dark,False,'任意魔力包含'+name+'，实际原卡进已打出区',
        {'token:'+name:1,'AllowBlackAtDay':True},option=color)
amulet(dark,True,'焚毁三个同色绿色合法，保留3个独立标记',{'token:Green':3},option=0x020202)
amulet(dark,True,'黑夜选择蓝金黑；没有皓日许可时生成的金色仍不可用',
    {'token:Blue':1,'token:Gold':1,'token:Black':1,'AllowBlackAtDay':False,'AllowGoldAtNight':False,
     'operation:0:success':False},[dict(kind='ConvertGold',option=2)],
    option=1|4<<8|5<<16,setup={'DayPart':'Night'})
for strong,option in [(False,6),(False,256),(True,1536),(True,-1),(True,16777216)]:
    amulet(dark,strong,'非法颜色编码'+str(option)+'整体拒绝，保留原手牌与零魔力',
        {'hand':1,'cards:played':0,'cards:removed':0,'AllowBlackAtDay':False,
         **{'token:'+name:0 for name in COLORS}},option=option)
    CASES[-1]['expectedException']='InvalidOperationException: '+('黑暗护符的魔力颜色无效' if option in [6,1536] else '黑暗护符必须选择正确数量的魔力颜色')
for id in [sun,dark]:
    for strong in [False,True]:
        amulet(id,strong,'实际原护符重复使用拒绝，回合末基础弃牌或焚毁保留移除区',
            {'cards:played':0,'discard':int(not strong),'operation:0:success':False,
             'amulet:zone0':'removed' if strong else 'discard'},
            [dict(kind='PlayArtifactInstance',cardId=id,enhanced=strong),dict(kind='EndTurn')])


# Wisdom Book: real original material, explicit same-color offer, and downstream play.
def wisdom(strong,title,expected,ops=(),material='basic_card_021',advanced=None,spells=None,setup=None,**fields):
    advanced=['advanced_card_000'] if advanced is None else advanced
    spells=['magic_009'] if spells is None else spells
    initial={f'token:{color}':0 for color in COLORS}
    initial.update({f'crystal:{color}':0 for color in COLORS});initial.update(SkipDraw=True)
    initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[material],
        playFromHand=True,removeIndex=1,discardIndex=-1,discardIndices=[],advancedOffer=advanced,spellOffer=spells)
    options.update(fields)
    selected=(spells if strong else advanced)
    index=options.get('option',0)
    options['wisdomOfferArt']=selected[index] if 0<=index<len(selected) else ''
    material_index=options['removeIndex']
    hand=['items_013','basic_card_021','basic_card_014','wound_0','wound_1','wound_2'][:options['initialHandCount']]+options['extraHandCards']
    options['wisdomMaterialArt']=hand[material_index] if 0<=material_index<len(hand) and not hand[material_index].startswith('wound_') else ''
    result={'hand':1,'Wounds':0,'cards:played':int(not strong),'cards:removed':1+int(strong),
        'wisdom:total':3,'wisdom:unique':3,'wisdom:zone0':'removed' if strong else 'played','wisdom:zone1':'removed'}
    result.update(expected)
    operation_case('items_013',strong,title,result,ops,setup=initial,**options)
    CASES[-1]['limitations']=['已验证本例材料/供给选择、真实牌区及后续操作；市场回合末补位、全游戏持续状态及未列组合仍待联验']

for i,(material,advanced,spell,color) in enumerate([
    ('basic_card_021','advanced_card_000','magic_009','Red'),
    ('basic_card_014','advanced_card_001','magic_015','Blue'),
    ('basic_card_008','advanced_card_002','magic_021','White'),
    ('basic_card_000','advanced_card_003','magic_001','Green')]):
    for strong in [False,True]:
        wisdom(strong,color+'材料永久移除，同色原供给进手牌；结束回合核对神器去向',
            {'wisdom:handIds':spell if strong else advanced,'advancedOffer':1 if strong else 0,'spellOffer':0 if strong else 1,
             'cards:played':0,'discard':0 if strong else 1,'wisdom:zone0':'removed' if strong else 'discard',
             'crystal:'+color:int(strong),'played:wisdom:zone1':'removed','played:wisdom:selectionCleared':True,
             'played:wisdom:handIds':spell if strong else advanced},
            [dict(kind='EndTurn')],material=material,advanced=[advanced],spells=[spell],setup={'DayPart':'Night' if i%2 else 'Day'})
        if i==0: amulet_replace('items_013',int(strong))

for strong in [False,True]:
    wisdom(strong,'显式选择第二张同色供给，未选实体保留',
        {'wisdom:handIds':'magic_006' if strong else 'advanced_card_004',
         'wisdom:advanced':'advanced_card_000,advanced_card_004' if strong else 'advanced_card_000',
         'wisdom:spells':'magic_009' if strong else 'magic_009,magic_006','crystal:Red':int(strong)},
        advanced=['advanced_card_000','advanced_card_004'],spells=['magic_009','magic_006'],option=1)
    wisdom(strong,'高级行动同ID双实例，只永久移除所选第二实例',
        {'hand':2,'wisdom:total':4,'wisdom:unique':4,'wisdom:zone1':'hand','wisdom:zone2':'removed',
         'wisdom:handIds':'advanced_card_004,'+('magic_009' if strong else 'advanced_card_000'),'crystal:Red':int(strong)},
        material='advanced_card_004',extraHandCards=['advanced_card_004','advanced_card_004'],removeIndex=2)

for strong in [False,True]:
    for failure in ['missing','foreign','spell','self','wound','negative','range','color']:
        fields={};material='basic_card_021';advanced=['advanced_card_000'];spells=['magic_009'];hand=2;wounds=0
        error='智慧之书必须去除实际持有的一张行动牌'
        if failure=='missing': fields['removeIndex']=-1
        if failure=='foreign': fields['foreignRemove']=True
        if failure=='spell': material='magic_009'
        if failure=='self': fields['removeIndex']=0
        if failure=='wound': fields.update(initialHandCount=4,removeIndex=3);hand=5;wounds=1
        if failure in ['negative','range']:
            fields['option']=-1 if failure=='negative' else 1
            error='必须选择实际'+('法术供给中的卡牌' if strong else '高级行动供给中的卡牌')
        if failure=='color':
            advanced=['advanced_card_001'];spells=['magic_015']
            error='智慧之书只能取得与材料同色的'+('法术牌' if strong else '高级行动牌')
        wisdom(strong,'非法选择 '+failure+'：材料、神器、供给及资源整笔保留',
            {'hand':hand,'Wounds':wounds,'cards:played':0,'cards:removed':0,'wisdom:total':hand,'wisdom:unique':hand,
             'wisdom:zone0':'hand','wisdom:zone1':'hand','wisdom:advanced':','.join(advanced),'wisdom:spells':','.join(spells),
             **{'crystal:'+color:0 for color in COLORS},**{'token:'+color:0 for color in COLORS}},
            material=material,advanced=advanced,spells=spells,expectedException='InvalidOperationException: '+error,**fields)

wisdom(True,'红晶满3时溢出为临时红魔力；回合末清除临时量并保留晶体',
    {'crystal:Red':3,'token:Red':0,'played:token:Red':1,'played:crystal:Red':3,'wisdom:handIds':'magic_009'},
    [dict(kind='EndTurn')],setup={'crystal:Red':3})
wisdom(False,'取得原火焰箭矢后立即实际出牌，获得1红晶并在回合末弃两张牌',
    {'hand':0,'crystal:Red':1,'cards:played':0,'discard':2,'wisdom:zone0':'discard','wisdom:handIds':'',
     'operation:0:success':True,'operation:0:crystal:Red':1,'operation:0:played':2},
    [dict(kind='PlayCard',cardId='advanced_card_000'),dict(kind='EndTurn')])
wisdom(True,'取得原火焰魔球不豁免白昼强效限制；拒绝后实际用新红晶支付基础效果',
    {'hand':0,'crystal:Red':0,'RangedPool':5,'cards:played':1,'wisdom:handIds':'',
     'operation:0:success':False,'operation:0:crystal:Red':1,'operation:0:handIds':'magic_009',
     'operation:1:success':True,'operation:1:crystal:Red':0,'operation:1:ranged':5},
    [dict(kind='PlayCard',cardId='magic_009',enhanced=True),dict(kind='PlayCard',cardId='magic_009')],setup={'DayPart':'Day'})


reserve_a=['advanced_card_004','advanced_card_002']
reserve_s=['magic_006','magic_021']
offer_a=['advanced_card_001','advanced_card_000','advanced_card_003']
offer_s=['magic_015','magic_009','magic_005']
for strong in [False,True]:
    first_a=offer_a if strong else ['advanced_card_001','advanced_card_003','advanced_card_004']
    first_s=['magic_015','magic_005','magic_006'] if strong else offer_s
    wisdom(strong,'领取中间同色牌后立即补顶位；未选牌保留，结束回合不轮换',
        {'advancedOffer':3,'spellOffer':3,'wisdom:advanced':','.join(first_a),'wisdom:spells':','.join(first_s),
         'played:wisdom:advanced':','.join(first_a),'played:wisdom:spells':','.join(first_s),
         'wisdom:advancedDiscard':0,'wisdom:spellDiscard':0,'cards:played':0,'discard':int(not strong),
         'wisdom:zone0':'removed' if strong else 'discard','crystal:Red':int(strong)},
        [dict(kind='EndTurn')],advanced=offer_a,spells=offer_s,advancedReserve=reserve_a,spellReserve=reserve_s,option=1)
    final_a=offer_a if strong else ['advanced_card_001','advanced_card_003','advanced_card_002']
    final_s=['magic_015','magic_005','magic_021'] if strong else offer_s
    wisdom(strong,'同回合第二本智慧之书取得刚补入的原卡；两份真实材料各移除一次',
        {'hand':2,'wisdom:total':6,'wisdom:unique':6,'cards:removed':4 if strong else 2,'cards:played':0,
         'discard':0 if strong else 2,'wisdom:zone0':'removed' if strong else 'discard',
         'wisdom:zone2':'removed' if strong else 'discard','wisdom:zone3':'removed','crystal:Red':2 if strong else 0,
         'wisdom:handIds':','.join(['magic_009','magic_006'] if strong else ['advanced_card_000','advanced_card_004']),
         'played:wisdom:advanced':','.join(first_a),'played:wisdom:spells':','.join(first_s),
         'wisdom:advanced':','.join(final_a),'wisdom:spells':','.join(final_s),
         'wisdom:advancedDiscard':0,'wisdom:spellDiscard':0,
         'operation:0:success':True,'operation:1:success':True,'operation:1:zone3':'removed'},
        [dict(kind='SelectRemoval',targetIndex=3),dict(kind='PlayArtifactInstance',cardId='items_013',targetIndex=2,enhanced=strong,option=2),dict(kind='EndTurn')],
        extraHandCards=['basic_card_021','items_013','basic_card_021'],advanced=offer_a,spells=offer_s,
        advancedReserve=reserve_a,spellReserve=reserve_s,option=1)
    CASES[-1]['wisdomSecondOfferArt']='magic_006' if strong else 'advanced_card_004'
    wisdom(strong,'备用牌库为空时保持真实缺额，不复制或轮换未选牌',
        {'advancedOffer':3 if strong else 2,'spellOffer':2 if strong else 3,
         'wisdom:advanced':','.join(offer_a if strong else ['advanced_card_001','advanced_card_003']),
         'wisdom:spells':','.join(['magic_015','magic_005'] if strong else offer_s),
         'wisdom:advancedDiscard':0,'wisdom:spellDiscard':0,'crystal:Red':int(strong)},
        advanced=offer_a,spells=offer_s,advancedReserve=[],spellReserve=[],option=1)
    wisdom(strong,'越界领取拒绝，三张实际供给和备用队列保持原状',
        {'hand':2,'wisdom:total':2,'wisdom:unique':2,'cards:played':0,'cards:removed':0,
         'wisdom:zone0':'hand','wisdom:zone1':'hand','wisdom:advanced':','.join(offer_a),'wisdom:spells':','.join(offer_s),
         'wisdom:advancedDiscard':0,'wisdom:spellDiscard':0,'crystal:Red':0},
        advanced=offer_a,spells=offer_s,advancedReserve=reserve_a,spellReserve=reserve_s,option=3,
        expectedException='InvalidOperationException: 必须选择实际'+('法术供给中的卡牌' if strong else '高级行动供给中的卡牌'))

case('advanced_card_025',True,'真实支付蓝红魔力领取火焰魔球；法术供给立即补入原魔力熔毁',
    {'hand':0,'cards:played':1,'discard':1,'discard:ids':'magic_009','spellOffer':3,
     'spell:offer':'magic_015,magic_005,magic_006','token:Blue':0,'token:Red':0},option=1<<8,
    setup={**{f'token:{c}':0 for c in COLORS},**{f'crystal:{c}':0 for c in COLORS},'token:Blue':1,'token:Red':1,'CardToDiscard':'<none>'},
    limits=['本例验证真实费用、领取去向和即时补位；全游戏供给持久化及其他组合仍待联验'])
CASES[-1].update(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],playFromHand=True,
    discardIndex=-1,discardIndices=[],removeIndex=-1,spellOffer=offer_s,spellReserve=reserve_s)

for strong in [False,True]:
    after_a=[['advanced_card_000','advanced_card_003','advanced_card_004'],['advanced_card_003','advanced_card_004','advanced_card_002'],['advanced_card_004','advanced_card_002','advanced_card_001']] if strong else [
        ['advanced_card_003','advanced_card_004','advanced_card_002'],['advanced_card_004','advanced_card_002','advanced_card_001'],['advanced_card_002','advanced_card_001','advanced_card_003']]
    after_s=[['magic_005','magic_006','magic_021'],['magic_006','magic_021','magic_015'],['magic_021','magic_015','magic_005']] if strong else [
        ['magic_009','magic_005','magic_006'],['magic_005','magic_006','magic_021'],['magic_006','magic_021','magic_015']]
    expected={'wisdom:handIds':'magic_009' if strong else 'advanced_card_000','cards:played':0,'discard':0,
        'wisdom:zone0':'removed' if strong else 'draw','wisdom:round':4,'DayPart':'Night',
        'wisdom:advanced':','.join(after_a[-1]),'wisdom:spells':','.join(after_s[-1]),
        'wisdom:advancedDiscard':3,'wisdom:spellDiscard':3,'crystal:Red':int(strong)}
    for i in range(3):
        expected.update({f'operation:{i+1}:advanced':','.join(after_a[i]),f'operation:{i+1}:spells':','.join(after_s[i]),
            f'operation:{i+1}:round':i+2,f'operation:{i+1}:success':True})
    wisdom(strong,'取得原卡后连续三轮供给换底；原实体回到顶位且已取得卡不回市场',expected,
        [dict(kind='EndTurn')]+[dict(kind='EndMarketRound') for _ in range(3)],
        advanced=offer_a,spells=offer_s,advancedReserve=reserve_a,spellReserve=reserve_s,option=1,useRoundEngine=True)
    CASES[-1]['limitations']=['本例验证真实GameEngine轮次、供给换底牌序和取得卡隔离；完整玩家洗牌、部队市场及其他原卡组合仍待联验']
    after_a=[['advanced_card_000','advanced_card_003','advanced_card_001'],['advanced_card_003','advanced_card_001','advanced_card_000']] if strong else [
        ['advanced_card_003','advanced_card_001'],['advanced_card_001','advanced_card_003']]
    after_s=[['magic_005','magic_015'],['magic_015','magic_005']] if strong else [
        ['magic_009','magic_005','magic_015'],['magic_005','magic_015','magic_009']]
    expected={'wisdom:handIds':'magic_009' if strong else 'advanced_card_000','cards:played':0,'discard':0,
        'wisdom:zone0':'removed' if strong else 'draw','wisdom:round':3,'DayPart':'Day',
        'advancedOffer':3 if strong else 2,'spellOffer':2 if strong else 3,
        'wisdom:advanced':','.join(after_a[-1]),'wisdom:spells':','.join(after_s[-1]),
        'wisdom:advancedDiscard':2,'wisdom:spellDiscard':2,'crystal:Red':int(strong)}
    for i in range(2):
        expected.update({f'operation:{i+1}:advanced':','.join(after_a[i]),f'operation:{i+1}:spells':','.join(after_s[i]),f'operation:{i+1}:success':True})
    wisdom(strong,'备用为空且取得一张后，实际剩余供给在两轮中循环而不缩水或造牌',expected,
        [dict(kind='EndTurn')]+[dict(kind='EndMarketRound') for _ in range(2)],
        advanced=offer_a,spells=offer_s,advancedReserve=[],spellReserve=[],option=1,useRoundEngine=True)
    CASES[-1]['limitations']=['本例验证领取后无备用牌的真实轮次换底、缺额和卡区守恒；完整玩家洗牌、部队市场及其他原卡组合仍待联验']


# Original Practice: exact material removal, same-color offer, green payment and destinations.
def practice(strong,title,expected,ops=(),material='basic_card_021',advanced=None,setup=None,**fields):
    advanced=['advanced_card_000'] if advanced is None else advanced
    initial={f'token:{color}':0 for color in COLORS}
    initial.update({f'crystal:{color}':0 for color in COLORS});initial.update(SkipDraw=True)
    initial['token:Green']=1;initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[material],
        playFromHand=True,removeIndex=1,discardIndex=-1,discardIndices=[],advancedOffer=advanced,
        advancedReserve=[],spellOffer=[])
    options.update(fields)
    index=options.get('option',0)
    options['wisdomOfferArt']=advanced[index] if 0<=index<len(advanced) else ''
    hand=['advanced_card_027','basic_card_021','basic_card_014','wound_0','wound_1','wound_2'][:options['initialHandCount']]+options['extraHandCards']
    mi=options['removeIndex']
    options['wisdomMaterialArt']=hand[mi] if 0<=mi<len(hand) and not hand[mi].startswith('wound_') else ''
    result={'hand':int(strong),'discard':int(not strong),'Wounds':0,'cards:played':1,'cards:removed':1,
        'wisdom:total':3,'wisdom:unique':3,'wisdom:zone0':'played','wisdom:zone1':'removed',
        'token:Green':0 if strong else 1,'wisdom:selectionCleared':True}
    result.update(expected)
    operation_case('advanced_card_027',strong,title,result,ops,setup=initial,**options)
    CASES[-1]['limitations']=['本例核验修行原卡、实际材料和供给、费用及指定后续操作；跨轮补牌与其他卡组合未在本例覆盖']

for material,advanced,color in [('basic_card_021','advanced_card_000','红'),('basic_card_014','advanced_card_001','蓝'),
                               ('basic_card_008','advanced_card_002','白'),('basic_card_000','advanced_card_003','绿')]:
    for strong in [False,True]:
        practice(strong,color+'色实际材料永久移除，同色取得卡进入指定牌区并结束回合',
            {'hand':int(strong),'discard':1 if strong else 2,'cards:played':0,'wisdom:zone0':'discard',
             'wisdom:handIds':advanced if strong else '',
             'wisdom:discardIds':'advanced_card_027' if strong else 'advanced_card_027,'+advanced,
             'token:Green':0,'played:token:Green':0 if strong else 1,'advancedOffer':0},
            [dict(kind='EndTurn')],material=material,advanced=[advanced])

for strong in [False,True]:
    practice(strong,'两张同ID材料只去除所选第二实例；领取第二张同色供给并立即补位',
        {'hand':2 if strong else 1,'wisdom:total':4,'wisdom:unique':4,'wisdom:zone1':'hand','wisdom:zone2':'removed',
         'wisdom:handIds':'advanced_card_004,advanced_card_004' if strong else 'advanced_card_004',
         'wisdom:discardIds':'' if strong else 'advanced_card_004','wisdom:advanced':'advanced_card_000,advanced_card_003','advancedOffer':2},
        material='advanced_card_004',advanced=['advanced_card_000','advanced_card_004'],
        extraHandCards=['advanced_card_004','advanced_card_004'],removeIndex=2,option=1,advancedReserve=['advanced_card_003'])

for failure in ['missing','foreign','spell','self','wound','negative','range','color','empty','mana']:
    fields={};material='basic_card_021';advanced=['advanced_card_000'];hand=2;wounds=0;initial={}
    error='修行必须去除实际手牌中的行动牌'
    if failure=='missing': fields['removeIndex']=-1
    if failure=='foreign': fields['foreignRemove']=True
    if failure=='spell': material='magic_009'
    if failure=='self':
        fields['removeIndex']=0;error='正在打出的卡不能充当另一张弃牌或去除材料'
    if failure=='wound': fields.update(initialHandCount=4,removeIndex=3);hand=5;wounds=1
    if failure in ['negative','range','empty']:
        fields['option']=-1 if failure=='negative' else 1 if failure=='range' else 0
        if failure=='empty': advanced=[]
        error='修行必须选择实际高级行动供给'
    if failure=='color': advanced=['advanced_card_001'];error='修行只能取得与材料同色的高级行动牌'
    if failure=='mana': initial['token:Green']=0;error='法力不足，不能支付所选效果'
    practice(True,'非法选择 '+failure+'：出牌、材料、供给与绿色费用保持原状',
        {'hand':hand,'discard':0,'Wounds':wounds,'cards:played':0,'cards:removed':0,'wisdom:total':hand,'wisdom:unique':hand,
         'wisdom:zone0':'hand','wisdom:zone1':'hand','wisdom:advanced':','.join(advanced),'advancedOffer':len(advanced),
         'token:Green':0 if failure=='mana' else 1,'wisdom:selectionCleared':failure=='missing'},
        material=material,advanced=advanced,setup=initial,expectedException='InvalidOperationException: '+error,**fields)

practice(True,'强化取得火焰箭矢后立即实际出牌，获得红晶；结束回合两牌进弃牌',
    {'hand':0,'discard':2,'cards:played':0,'wisdom:zone0':'discard','wisdom:handIds':'','crystal:Red':1,
     'wisdom:discardIds':'advanced_card_000,advanced_card_027','operation:0:success':True,'advancedOffer':0},
    [dict(kind='PlayCard',cardId='advanced_card_000'),dict(kind='EndTurn')])

practice(False,'基础取得卡在弃牌区不能立即打出；拒绝后结束回合保持牌区守恒',
    {'hand':0,'discard':2,'cards:played':0,'wisdom:zone0':'discard','wisdom:handIds':'','crystal:Red':0,
     'token:Green':0,'wisdom:discardIds':'advanced_card_027,advanced_card_000','operation:0:success':False,'advancedOffer':0},
    [dict(kind='PlayCard',cardId='advanced_card_000'),dict(kind='EndTurn')])


# Ritual Attack: permanent material identity through actual original combat and new round.
def ritual_life(strong,title,expected,ops,material='basic_card_021',**fields):
    setup={f'token:{c}':0 for c in COLORS};setup.update({f'crystal:{c}':0 for c in COLORS});setup.update(SkipDraw=True)
    setup['token:Red']=1
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[material],
        playFromHand=True,removeIndex=1,discardIndex=-1,discardIndices=[],useRoundEngine=True,
        enemies=[dict(id='monster_002',originalDefinition=True)])
    options.update(fields)
    result={'hand':0,'draw':1,'discard':0,'cards:played':0,'cards:removed':1,'Wounds':0,
        'ritual:total':2,'ritual:unique':2,'ritual:zone0':'draw','ritual:zone1':'removed',
        'ritual:drawIds':'advanced_card_029','ritual:removedIds':material,'ritual:round':2,
        'ritual:selectionCleared':True,'token:Red':0,'DayPart':'Night'}
    result.update(expected)
    operation_case('advanced_card_029',strong,title,result,ops,setup=setup,**options)
    CASES[-1]['limitations']=['本例核验真实材料/原攻击类型/指定敌人/回合及换轮牌区；抗性组合与完整战斗轮序沿用独立证据，未把该例等同整场游戏通过']
    CASES[-1]['ritualMaterialArt']='advanced_card_029' if options['removeIndex']==0 else material

for strong in [False,True]:
    for material,element,kind,amount in [('basic_card_021','Fire' if strong else 'Physical','Melee',6 if strong else 5),
        ('basic_card_016','ColdFire' if strong else 'Ice','Melee',4 if strong else 3),
        ('basic_card_008','Fire' if strong else 'Physical','Ranged',4 if strong else 3),
        ('basic_card_000','Fire' if strong else 'Physical','Siege',3 if strong else 2)]:
        ritual_life(strong,'永久移除原材料，实际'+kind+'攻击毒巫师，再验回合及换轮去向',
            {'played:'+kind+'Pool':amount,'played:attackElement':element,'played:token:Red':0 if strong else 1,
             'Fame':4 if amount>=5 else 0,'operation:0:printed':amount,'operation:0:effective':amount,
             'operation:0:required':5,'operation:0:kills':int(amount>=5),'operation:0:success':True,
             'operation:2:zone0':'discard','operation:2:zone1':'removed','operation:2:discardIds':'advanced_card_029'},
            [dict(kind='Attack' if kind=='Melee' else 'RangedAttack'),dict(kind='EndBattle'),dict(kind='EndTurn'),dict(kind='EndMarketRound')],
            material=material)

for strong in [False,True]:
    ritual_life(strong,'同ID的高级行动材料只移除所选第二实例，换轮后不回牌库',
        {'hand':1,'ritual:total':3,'ritual:unique':3,'ritual:zone1':'hand','ritual:zone2':'removed',
         'ritual:handIds':'advanced_card_004','Fame':0,'played:MeleePool':6 if strong else 5,'played:token:Red':0 if strong else 1},
        [dict(kind='EndTurn'),dict(kind='EndMarketRound')],material='advanced_card_004',
        extraHandCards=['advanced_card_004','advanced_card_004'],removeIndex=2)

for self in [False,True]:
    material='basic_card_021' if self else 'magic_009'
    message='正在打出的卡不能充当另一张弃牌或去除材料' if self else '仪式攻击只能去除有颜色定义的行动卡'
    ritual_life(True,'拒绝以自身作材料' if self else '拒绝原法术充当行动材料',
        {'hand':2,'draw':0,'cards:removed':0,'ritual:zone0':'hand','ritual:zone1':'hand','ritual:drawIds':'',
         'ritual:removedIds':'','ritual:selectionCleared':False,'ritual:round':1,'DayPart':'Day','token:Red':1},[],
        material=material,removeIndex=0 if self else 1,expectedException='InvalidOperationException: '+message)



# Direct regressions for shared end-turn cleanup: real cards, both refill branches.
for id,strong,opt,pool,amount,color,refill in [
    ('basic_card_024',False,2,'RangedPool',1,None,False),
    ('advanced_card_003',True,0,'SiegePool',3,'Green',False),
    ('basic_card_016',True,0,'BlockPool',5,'Blue',True)]:
    setup={f'token:{c}':0 for c in COLORS};setup.update({f'crystal:{c}':0 for c in COLORS})
    if color:setup['token:'+color]=1
    expected={'played:'+pool:amount,'hand':int(refill),'draw':int(not refill),'discard':1,'cards:played':0,'cards:removed':0,
        'Wounds':0,'Fame':0,'discard:ids':id,'hand:ids':'basic_card_000' if refill else '',
        'operation:0:success':True,'operation:2:success':False,'token:Blue':0,'token:White':0,'token:Green':0}
    operation_case(id,strong,'未使用的原卡战斗点数在回合末失效，下回合不能用于攻击',expected,
        [dict(kind='EndTurn',refill=refill),dict(kind='StartTurn'),dict(kind='Attack')],setup=setup,
        option=opt,initialHandCount=1,initialDrawCount=1,initialDiscardCards=[],extraHandCards=[],playFromHand=True,
        removeIndex=-1,discardIndex=-1,discardIndices=[],enemies=[dict(id='monster_002',originalDefinition=True)])
    CASES[-1]['limitations']=['本例仅补验公共回合末战斗点数失效及下一回合拒绝；其他原卡分支沿用独立历史证据']
    CASES[-1]['turnPowerExpiry']=True



# Original Learning: optional delayed purchase, original offer and exact printed costs.
def learning_case(strong,title,expected,ops=(),setup=None,**fields):
    initial={f'token:{c}':0 for c in COLORS}
    initial.update({f'crystal:{c}':0 for c in COLORS})
    initial.update(SkipDraw=True,InBattle=False)
    initial['token:White']=1
    initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[],
        playFromHand=True,removeIndex=-1,discardIndex=-1,discardIndices=[],option=1,
        advancedOffer=['advanced_card_001','advanced_card_000','advanced_card_003'],
        advancedReserve=['advanced_card_004'],spellOffer=[])
    options.update(fields)
    result={'hand':0,'discard':0,'Wounds':0,'cards:played':1,'cards:removed':0,
        'learning:total':1,'learning:unique':1,'learning:zone0':'played',
        'token:White':0 if strong else 1,'learning:used':False,'learning:modes':2 if strong else 1}
    result.update(expected)
    operation_case('advanced_card_026',strong,title,result,list(ops),setup=initial,**options)
    CASES[-1]['limitations']=['本例覆盖原学习的指定费用、供给选择、真实牌区及回合操作；多份学习与复制牌叠加、完整交涉与其他组合仍待联验']

def learn_op(strong,index=1): return dict(kind='Learn',option=index,enhanced=strong)
learning_offer='advanced_card_001,advanced_card_000,advanced_card_003'
learning_refilled='advanced_card_001,advanced_card_003,advanced_card_004'
for strong in [False,True]:
    bonus=4 if strong else 2;cost=9 if strong else 6;mode=2 if strong else 1
    learning_case(strong,'已有足够影响力仍不自动购买；可放弃能力，回合结束后不能购牌',
        {'InfluencePool':0,'token:White':0,'cards:played':0,'discard':1,'learning:zone0':'discard',
         'learning:used':False,'learning:modes':0,'learning:advanced':learning_offer,
         'played:InfluencePool':20+bonus,'played:learning:used':False,'played:learning:advanced':learning_offer,
         'operation:0:influence':0,'operation:0:modes':0,'operation:1:success':False,
         'operation:1:exception':'没有本回合所选模式的学习能力','operation:1:advanced':learning_offer},
        [dict(kind='EndTurn'),learn_op(strong)],setup={'InfluencePool':20})
    learning_case(strong,'明确购买第二张火焰箭矢，支付原费用、拒绝重复购牌，回合后能力失效',
        {'InfluencePool':0,'token:White':0,'hand':int(strong),'discard':1 if strong else 2,'cards:played':0,
         'learning:total':2,'learning:unique':2,'learning:zone0':'discard','learning:used':False,'learning:modes':0,
         'learning:handIds':'advanced_card_000' if strong else '',
         'learning:discardIds':'advanced_card_026' if strong else 'advanced_card_026,advanced_card_000',
         'learning:advanced':learning_refilled,'played:InfluencePool':20+bonus,'played:learning:used':False,
         'operation:0:success':True,'operation:0:beforeInfluence':20+bonus,'operation:0:requestedCost':cost,
         'operation:0:influence':20+bonus-cost,'operation:0:used':True,'operation:0:gained':'advanced_card_000',
         'operation:0:handIds':'advanced_card_000' if strong else '',
         'operation:0:discardIds':'' if strong else 'advanced_card_000',
         'operation:0:advanced':learning_refilled,'operation:1:success':False,
         'operation:1:exception':'本回合已经使用学习购牌','operation:1:influence':20+bonus-cost,
         'operation:2:used':False,'operation:2:modes':0,'operation:3:success':False,
         'operation:3:exception':'没有本回合所选模式的学习能力'},
        [learn_op(strong),learn_op(strong),dict(kind='EndTurn'),learn_op(strong)],setup={'InfluencePool':20})
    learning_case(strong,'费用不足保留机会；再打出原承诺获得4影响力，然后合法购买',
        {'InfluencePool':0,'token:White':0,'hand':int(strong),'discard':2 if strong else 3,'cards:played':0,
         'learning:total':3,'learning:unique':3,'learning:zone0':'discard','learning:zone1':'discard',
         'learning:used':False,'learning:modes':0,'learning:advanced':learning_refilled,
         'learning:handIds':'advanced_card_000' if strong else '',
         'learning:discardIds':'basic_card_008,advanced_card_026'+('' if strong else ',advanced_card_000'),
         'played:InfluencePool':cost-4,'played:learning:white':1,'played:learning:used':False,
         'operation:0:success':False,'operation:0:exception':'学习购牌影响力不足',
         'operation:0:influence':cost-4,'operation:0:used':False,'operation:0:advanced':learning_offer,
         'operation:1:success':True,'operation:1:influence':cost,'operation:1:white':0,
         'operation:2:success':True,'operation:2:influence':0,'operation:2:used':True,
         'operation:2:gained':'advanced_card_000','operation:2:advanced':learning_refilled},
        [learn_op(strong),dict(kind='PlayCard',cardId='basic_card_008',enhanced=True),learn_op(strong),dict(kind='EndTurn')],
        setup={'InfluencePool':1 if strong else 0,'token:White':2 if strong else 1},extraHandCards=['basic_card_008'])
    learning_case(strong,'负数及越界目标、未打出的模式都拒绝；保留影响力和机会供合法选择',
        {'InfluencePool':20+bonus-cost,'hand':int(strong),'discard':int(not strong),
         'learning:total':2,'learning:unique':2,'learning:used':True,'learning:modes':mode,
         'learning:advanced':learning_refilled,'played:InfluencePool':20+bonus,
         'operation:0:success':False,'operation:1:success':False,'operation:2:success':False,
         'operation:0:exception':'学习必须选择实际高级行动供给','operation:1:exception':'学习必须选择实际高级行动供给',
         'operation:2:exception':'没有本回合所选模式的学习能力',
         **{f'operation:{i}:influence':20+bonus for i in range(3)},
         **{f'operation:{i}:used':False for i in range(3)},
         **{f'operation:{i}:advanced':learning_offer for i in range(3)},
         'operation:3:success':True,'operation:3:influence':20+bonus-cost,'operation:3:gained':'advanced_card_000'},
        [learn_op(strong,-1),learn_op(strong,3),learn_op(not strong),learn_op(strong)],setup={'InfluencePool':20})

learning_case(True,'没有白色魔力时强化出牌拒绝，手牌和影响力保持，不能获得购牌能力',
    {'InfluencePool':0,'hand':1,'discard':0,'cards:played':0,'learning:zone0':'hand','token:White':0,
     'learning:used':False,'learning:modes':0,'learning:advanced':learning_offer},
    setup={'token:White':0},expectedException='InvalidOperationException: 法力不足，不能支付所选效果')


# Blood of Ancients: real offer identity, matching payment and scoped borrowed effect.
def bloodline_case(strong,title,expected,ops=(),setup=None,offer='advanced_card_000',**fields):
    initial={f'token:{c}':0 for c in COLORS}
    initial.update({f'crystal:{c}':0 for c in COLORS})
    initial.update(SkipDraw=True,InBattle=False)
    if strong:initial['token:Red']=1
    initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[],
        playFromHand=True,removeIndex=-1,discardIndex=-1,discardIndices=[],option=1,
        advancedOffer=['advanced_card_019',offer,'advanced_card_003' if offer=='advanced_card_001' else 'advanced_card_001'],advancedReserve=['advanced_card_004'],spellOffer=[])
    options.update(fields)
    result={'Wounds':1,'handWounds':1,'hand':1,'discard':0,'cards:played':1,'cards:removed':0,
        'bloodline:total':2,'bloodline:unique':2,'bloodline:zone0':'played','token:Red':0}
    result.update(expected)
    operation_case('advanced_card_030',strong,title,result,list(ops),setup=initial,**options)
    CASES[-1]['limitations']=['本例核验远古血脉原供给、费用、伤牌去向和指定后续操作；完整复制组合、材料选择和地图/战斗消费仍按影响范围继续联验']

for color,id in [(0,'advanced_card_000'),(1,'advanced_card_001'),(2,'advanced_card_003'),(3,'advanced_card_002')]:
    mana=COLORS[color];crystal=color>=2
    bloodline_case(False,'基础支付'+mana+('晶体' if crystal else '魔力')+'取得同色原卡；伤牌与所得卡留手，原血脉回合弃牌',
        {'hand':2,'discard':1,'cards:played':0,'bloodline:total':3,'bloodline:unique':3,'bloodline:zone0':'discard',
         'bloodline:handActions':id,'bloodline:discardActions':'advanced_card_030',
         'bloodline:advanced':'advanced_card_019,'+('advanced_card_003' if id=='advanced_card_001' else 'advanced_card_001')+',advanced_card_004',
         'bloodline:wounds':1,'bloodline:discardWounds':0,f'crystal:{mana}':0,
         'played:bloodline:zone0':'played','played:bloodline:handActions':id,'played:bloodline:total':3,
         'operation:0:success':True,'operation:0:wounds':1,'operation:0:discardWounds':0},
        [dict(kind='EndTurn')],offer=id,option=1|(color<<8),setup={('crystal:' if crystal else 'token:')+mana:1})

for id,pool,element in [('advanced_card_000','RangedPool','Fire'),('advanced_card_001','RangedPool','Ice'),
                        ('advanced_card_003','SiegePool','Physical'),('advanced_card_026','InfluencePool','Physical')]:
    for discard in [False,True]:
        points=4 if pool=='InfluencePool' else 3
        bloodline_case(True,'借用'+CARDS[id]['card_name']+'的原强效，免印刷色费、供给原位保留；伤牌进'+('弃牌' if discard else '手牌'),
            {'Wounds':0 if discard else 1,'handWounds':0 if discard else 1,'hand':0 if discard else 1,
             'discard':2 if discard else 1,'cards:played':0,'bloodline:zone0':'discard',
             'bloodline:handActions':'','bloodline:discardActions':'advanced_card_030',
             'bloodline:advanced':f'advanced_card_019,{id},'+('advanced_card_003' if id=='advanced_card_001' else 'advanced_card_001'),
             'bloodline:wounds':0 if discard else 1,'bloodline:discardWounds':1 if discard else 0,
             f'played:{pool}':points,'played:bloodline:zone0':'played','played:bloodline:total':2,
             'played:bloodline:wounds':0 if discard else 1,'played:bloodline:discardWounds':1 if discard else 0,
             'operation:0:success':True,'operation:0:ranged':0,'operation:0:siege':0},
            [dict(kind='EndTurn')],offer=id,setup={'BloodlineWoundToDiscard':discard})

for strong,reason,option,setup,offer,error in [
    (False,'基础费用不足',257,{},'advanced_card_001','法力不足，不能支付所选效果'),
    (False,'基础选择不同色',257,{'token:Red':1,'token:Blue':1},'advanced_card_000','远古血脉必须支付与所获高级行动同色的魔力'),
    (False,'基础负数索引',-1,{'token:Red':1},'advanced_card_000','远古血脉必须选择实际高级行动供给'),
    (False,'原供给被伪造为法术',1,{'token:Red':1},'magic_000','远古血脉必须选择有效原版高级行动牌'),
    (True,'缺少血脉本身的红色费用',1,{'token:Red':0},'advanced_card_001','法力不足，不能支付所选效果'),
    (True,'自然之力强效内部选项非法',1,{'BloodlineEffectOption':2},'advanced_card_038','自然之力选项无效')]:
    bloodline_case(strong,reason+'：拒绝并保留源牌、全部魔力及供给，不先造成伤牌',
        {'Wounds':0,'handWounds':0,'hand':1,'discard':0,'cards:played':0,'bloodline:total':1,'bloodline:unique':1,
         'bloodline:zone0':'hand','bloodline:handActions':'advanced_card_030','bloodline:wounds':0,'bloodline:discardWounds':0,
         'bloodline:advanced':f'advanced_card_019,{offer},'+('advanced_card_003' if offer=='advanced_card_001' else 'advanced_card_001'),
         'token:Red':setup.get('token:Red',1 if strong else 0),'token:Blue':setup.get('token:Blue',0)},
        offer=offer,setup=setup,option=option,expectedException='InvalidOperationException: '+error)

for enough in [False,True]:
    expected={'bloodline:advanced':'advanced_card_019,advanced_card_025,advanced_card_001','token:Blue':0}
    if enough:
        expected.update({'hand':1,'discard':2,'cards:played':0,'bloodline:total':3,'bloodline:unique':3,
            'bloodline:zone0':'discard','bloodline:discardActions':'advanced_card_030,magic_009','bloodline:spells':'',
            'played:bloodline:token:Red':0,'played:bloodline:wounds':1,'played:bloodline:discardActions':'magic_009',
            'operation:0:success':True})
    else:
        expected.update({'Wounds':0,'handWounds':0,'hand':1,'cards:played':0,'bloodline:total':1,'bloodline:unique':1,
            'bloodline:zone0':'hand','bloodline:wounds':0,'bloodline:discardWounds':0,'token:Red':1,'bloodline:spells':'magic_009'})
    bloodline_case(True,'借用魔法天赋：红色本体费用与获取红色法术的额外费用'+('整笔支付2点' if enough else '仅1点不足，全部拒绝'),
        expected,[dict(kind='EndTurn')] if enough else [],offer='advanced_card_025',spellOffer=['magic_009'],spellReserve=[],
        setup={'token:Red':2 if enough else 1},
        **({} if enough else {'expectedException':'InvalidOperationException: 法力不足，不能支付所选效果'}))


# Decompose: original action material, permanent zone, crystal rewards and real consumers.
def decompose_case(strong,title,expected,ops=(),material='basic_card_021',setup=None,**fields):
    initial={f'token:{c}':0 for c in COLORS}
    initial.update({f'crystal:{c}':0 for c in COLORS})
    initial.update(SkipDraw=True,InBattle=False);initial['token:Red']=1;initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[material],
        playFromHand=True,removeIndex=1,discardIndex=-1,discardIndices=[],option=99)
    options.update(fields)
    result={'hand':0,'discard':0,'Wounds':0,'cards:played':1,'cards:removed':1,'token:Red':0 if strong else 1,
        'decompose:total':2,'decompose:unique':2,'decompose:zone0':'played','decompose:zone1':'removed',
        'decompose:removedIds':material,'decompose:selectionCleared':True}
    result.update(expected)
    operation_case('advanced_card_020',strong,title,result,list(ops),setup=initial,**options)
    CASES[-1]['decomposeMaterialArt']=material if options['removeIndex']>=0 else ''
    if options['removeIndex']==0:CASES[-1]['decomposeMaterialArt']='advanced_card_020'
    CASES[-1]['limitations']=['本例核验分解原材料、魔晶、指定消费与卡区生命周期；其他复制和完整回合组合仍按影响范围继续联验']

for material,color in [('basic_card_021','Red'),('basic_card_016','Blue'),('advanced_card_003','Green'),('advanced_card_002','White')]:
    for strong in [False,True]:
        rewards={f'crystal:{c}':(int(c!=color) if strong else 2*int(c==color)) for c in COLORS[:4]}
        decompose_case(strong,'按'+color+'色实际原行动材料获得魔晶；回合弃牌、换轮后材料仍永久移除',
            {**rewards,'token:Red':0,'cards:played':0,'draw':1,'decompose:zone0':'draw','decompose:round':2,
             'decompose:drawIds':'advanced_card_020','decompose:handIds':'',
             'played:decompose:zone0':'played','operation:0:success':True,'operation:0:zone0':'discard',
             'operation:1:success':True,'operation:1:zone0':'draw','operation:1:zone1':'removed'},
            [dict(kind='EndTurn'),dict(kind='EndMarketRound')],material=material,useRoundEngine=True)

for material,next_id,color,strong,pool,amount in [
    ('basic_card_021','basic_card_021','Red',False,'MeleePool',4),
    ('basic_card_016','basic_card_016','Blue',False,'BlockPool',5),
    ('advanced_card_003','basic_card_000','Green',False,'MovementPool',4),
    ('advanced_card_002','basic_card_008','White',False,'InfluencePool',4),
    ('basic_card_021','basic_card_016','Blue',True,'BlockPool',5)]:
    rewards={f'crystal:{c}':int(c in ['Green','White']) if strong else int(c==color) for c in COLORS[:4]}
    decompose_case(strong,'所得'+color+'晶体实际支付下一张原卡强化，材料和出牌实例不串换',
        {**rewards,pool:amount,'token:Red':0,'cards:played':2,'decompose:total':3,'decompose:unique':3,
         'decompose:zone2':'played','operation:0:success':True,'operation:0:zone1':'removed',
         f'played:decompose:crystal:{color}':1 if strong else 2},
        [dict(kind='PlayCard',cardId=next_id,enhanced=True)],material=material,extraHandCards=[material,next_id],
        setup={'token:Red':1 if strong else 0})

for stored in [2,3]:
    decompose_case(False,'红晶库存'+str(stored)+'时奖励2颗，超上限转临时标记并在回合末清空',
        {'crystal:Red':3,'token:Red':0,'cards:played':0,'discard':1,'decompose:zone0':'discard',
         'played:decompose:crystal:Red':3,'played:decompose:token:Red':stored-1,
         'operation:0:success':True,'operation:0:token:Red':0,'operation:0:crystal:Red':3},
        [dict(kind='EndTurn')],setup={'token:Red':0,'crystal:Red':stored})
decompose_case(True,'先花红晶再按蓝材料奖励其他三色；红晶补回，绿白满库存溢出',
    {**{f'crystal:{c}':3 for c in COLORS[:4]},'token:Red':0,'cards:played':0,'discard':1,'decompose:zone0':'discard',
     'played:decompose:token:Green':1,'played:decompose:token:White':1,'played:decompose:token:Red':0,
     'operation:0:success':True,'operation:0:token:Green':0,'operation:0:token:White':0},
    [dict(kind='EndTurn')],material='basic_card_016',setup={'token:Red':0,**{f'crystal:{c}':3 for c in COLORS[:4]}})

for strong in [False,True]:
    rewards={f'crystal:{c}':int(c!='Red') if strong else 2*int(c=='Red') for c in COLORS[:4]}
    decompose_case(strong,'同ID两张狂怒只永久移除所选实体，另一张保留手牌并跨轮守恒',
        {**rewards,'hand':1,'draw':1,'cards:played':0,'token:Red':0,'decompose:zone0':'draw','decompose:zone1':'hand',
         'decompose:zone2':'removed','decompose:total':3,'decompose:unique':3,'decompose:round':2,
         'decompose:handIds':'basic_card_021','decompose:drawIds':'advanced_card_020',
         'operation:0:success':True,'operation:1:success':True},
        [dict(kind='EndTurn'),dict(kind='EndMarketRound')],extraHandCards=['basic_card_021','basic_card_021'],removeIndex=2,useRoundEngine=True)

for reason,material,fields,setup,error in [
    ('未选择材料','basic_card_021',{'removeIndex':-1},{},'分解必须选择实际手牌中的行动牌'),
    ('同ID外来实例','basic_card_021',{'foreignRemove':True},{},'分解必须选择实际手牌中的行动牌'),
    ('法术不能作行动材料','magic_009',{}, {},'分解必须选择实际手牌中的行动牌'),
    ('神器不能作行动材料','items_000',{}, {},'分解必须选择实际手牌中的行动牌'),
    ('正在出牌的分解不能作材料','basic_card_021',{'removeIndex':0},{},'正在打出的卡不能充当另一张弃牌或去除材料'),
    ('缺少红色费用','basic_card_021',{}, {'token:Red':0},'法力不足，不能支付所选效果')]:
    decompose_case(True,reason+'：保留全部牌区与资源，不能先移除或给奖励',
        {'hand':2,'cards:played':0,'cards:removed':0,'token:Red':setup.get('token:Red',1),
         'decompose:zone0':'hand','decompose:zone1':'hand','decompose:removedIds':'',
         'decompose:selectionCleared':fields.get('removeIndex')==-1},
        material=material,setup=setup,expectedException='InvalidOperationException: '+error,**fields)

for valid in [False,True]:
    expected={'cards:removed':int(valid),'bloodline:advanced':'advanced_card_020','decompose:total':3 if valid else 2,
        'bloodline:total':3 if valid else 2,'bloodline:unique':3 if valid else 2,
        'decompose:unique':3 if valid else 2,'decompose:zone0':'played' if valid else 'hand',
        'decompose:zone1':'removed' if valid else 'hand','decompose:removedIds':'basic_card_021' if valid else '',
        'decompose:selectionCleared':valid,'token:Red':0 if valid else 1,
        **{f'crystal:{c}':int(valid) for c in ['Blue','Green','White']}}
    if not valid:expected.update(Wounds=0,handWounds=0,hand=2,**{'cards:played':0,'bloodline:total':2,'bloodline:unique':2,'bloodline:zone0':'hand'})
    bloodline_case(True,'借用原分解'+('消耗实际红色行动材料，取得另三色魔晶' if valid else '外来材料在伤牌与红费之前拒绝'),
        expected,advancedOffer=['advanced_card_020'],advancedReserve=[],option=0,removeIndex=1,
        extraHandCards=['basic_card_021'],foreignRemove=not valid,
        **({} if valid else {'expectedException':'InvalidOperationException: 分解必须选择实际手牌中的行动牌'}))
    CASES[-1]['decomposeMaterialArt']='basic_card_021'


# Maximal Effect: actual source/material, fixed original choices, real card zones.
def overload_case(strong,title,expected,ops=(),material='basic_card_021',setup=None,**fields):
    initial={f'token:{c}':0 for c in COLORS}
    initial.update({f'crystal:{c}':0 for c in COLORS})
    initial.update(SkipDraw=True,InBattle=False);initial['token:Red']=1;initial.update(setup or {})
    options=dict(initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],extraHandCards=[material],
        playFromHand=True,removeIndex=1,discardIndex=-1,discardIndices=[],option=0)
    options.update(fields)
    result={'hand':0,'discard':0,'Wounds':0,'cards:played':1,'cards:removed':1,'token:Red':0 if strong else 1,
        'overload:total':2,'overload:unique':2,'overload:zone0':'played','overload:zone1':'removed',
        'overload:removedIds':material,'overload:selectionCleared':True}
    result.update(expected)
    operation_case('advanced_card_024',strong,title,result,list(ops),setup=initial,**options)
    CASES[-1]['overloadMaterialArt']=material if options['removeIndex']>=0 else ''
    if options['removeIndex']==0:CASES[-1]['overloadMaterialArt']='advanced_card_024'
    CASES[-1]['limitations']=['验证实际原材料、固定原选项、费用和本例后续结算；各次不同选择/材料、动态供给与嵌套复制仍待补齐，不判整卡通过']

for material,strong,pool,total in [('basic_card_000',False,'MovementPool',6),('basic_card_000',True,'MovementPool',8),
        ('basic_card_008',False,'InfluencePool',6),('basic_card_008',True,'InfluencePool',8),
        ('basic_card_021',False,'MeleePool',6),('basic_card_021',True,'MeleePool',8),
        ('basic_card_016',True,'BlockPool',10),('advanced_card_003',True,'SiegePool',6)]:
    overload_case(strong,'原材料'+material+'：真实出牌、回合弃牌与换轮永久移除',
        {'played:'+pool:total,'token:Red':0,'cards:played':0,'draw':1,'overload:zone0':'draw','overload:round':2,
         'overload:drawIds':'advanced_card_024','operation:0:success':True,'operation:1:success':True,
         'operation:0:zone0':'discard','operation:1:zone1':'removed'},
        [dict(kind='EndTurn'),dict(kind='EndMarketRound')],material=material,useRoundEngine=True,
        setup={'InBattle':pool in ['MeleePool','BlockPool','SiegePool']})

for choice,pool in [(0,'MovementPool'),(1,'InfluencePool'),(2,'MeleePool'),(3,'BlockPool')]:
    overload_case(True,'本能强效：原选项'+str(choice)+'重复两次，其余数值保持0',
        {pool:8},material='basic_card_027',option=choice)

for title,fields,reason in [
        ('外来同ID材料',{'foreignRemove':True},'效能过载必须去除实际手牌中的另一张行动牌'),
        ('没有材料',{'removeIndex':-1},'效能过载必须去除实际手牌中的另一张行动牌'),
        ('源牌不能去除自己',{'removeIndex':0},'正在打出的卡不能充当另一张弃牌或去除材料'),
        ('本能非法选项',{'material':'basic_card_027','option':4},'本能必须选择移动、影响、攻击或格挡'),
        ('宁静不能在战斗中抽牌',{'material':'basic_card_001','option':1,'setup':{'InBattle':True}},'战斗中不能使用宁静的治疗或抽牌效果'),
        ('缺宿主红色魔力',{'setup':{'token:Red':0}},'法力不足，不能支付所选效果')]:
    overload_case(True,title,{'hand':2,'cards:played':0,'cards:removed':0,'token:Red':fields.get('setup',{}).get('token:Red',1),
        'overload:zone0':'hand','overload:zone1':'hand','overload:removedIds':'','overload:selectionCleared':fields.get('removeIndex')==-1},
        expectedException='InvalidOperationException: '+reason,**fields)

for red in [2,3]:
    enough=red==3
    overload_case(True,'重复魔法天赋：宿主红1＋两次各额外红1，现有'+str(red),
        {'discard':2,'discard:ids':'magic_007,magic_006','spellOffer':1,'overload:spells':'magic_008','overload:total':4,'overload:unique':4} if enough else
        {'hand':2,'cards:played':0,'cards:removed':0,'token:Red':2,'spellOffer':3,'overload:zone0':'hand',
         'overload:zone1':'hand','overload:removedIds':'','overload:selectionCleared':False,
         'discard:ids':'','overload:spells':'magic_006,magic_007,magic_008'},
        material='advanced_card_025',spellOffer=['magic_006','magic_007','magic_008'],spellReserve=[],setup={'token:Red':red},
        **({} if enough else {'expectedException':'InvalidOperationException: 法力不足，不能支付所选效果'}))

overload_case(True,'决心强效两次合为格挡10，抵挡原毒巫师攻击后全部消费',
    {'played:BlockPool':10,'BlockPool':0,'combat:printed':10,'combat:effective':10,'combat:required':5,'combat:wounds':0},
    material='basic_card_016',combatPhase='Block',enemies=[dict(id='monster_002',originalDefinition=True)],setup={'InBattle':True})


# Explicit choices for each repeated use. Dynamic draw/offer continuation remains partial.
def overload_selection(option=0, remove=-1, discard=-1, foreign=False):
    return dict(option=option,removeIndex=remove,discardIndex=discard,foreignRemove=foreign)

def overload_selection_case(strong,title,expected,selections,material,children=(),ops=(),**fields):
    expected={'overload:choicesCleared':True,**expected}
    overload_case(strong,title,expected,ops,material=material,
        extraHandCards=[material,*children],overloadSelections=selections,**fields)
    CASES[-1]['limitations']=['本例验证预先选定的逐次原选项、实际材料及结算；抽牌后续选、动态供给及嵌套选择尚未全部验收，不判整卡通过']

for strong in [False,True]:
    overload_selection_case(strong,'本能逐次选择：'+('攻击4、格挡4' if strong else '移动2、影响2、攻击2'),
        dict(MovementPool=0 if strong else 2,InfluencePool=0 if strong else 2,MeleePool=4 if strong else 2,BlockPool=4 if strong else 0),
        [overload_selection(i) for i in ([2,3] if strong else [0,1,2])], 'basic_card_027')

overload_selection_case(True,'魔力结晶强效分别选择蓝、绿，免材料蓝色印刷费用',
    {'crystal:Red':0,'crystal:Blue':1,'crystal:Green':1,'crystal:White':0,'token:Blue':0},
    [overload_selection(1),overload_selection(2)],'basic_card_015')

for strong in [False,True]:
    children=['basic_card_021','basic_card_016']+([] if strong else ['basic_card_008'])
    n=len(children)+2
    rewards=[1,1,2,2] if strong else [2,2,0,2]
    overload_selection_case(strong,'分解逐次使用不同真实材料，全部永久移除并换轮核对',
        {'cards:removed':n-1,'cards:played':0,'draw':1,'token:Red':0,
         'overload:zone0':'draw','overload:total':n,'overload:unique':n,'overload:round':2,
         'overload:removedIds':','.join(['advanced_card_020',*children]),'overload:drawIds':'advanced_card_024',
         **{f'overload:zone{i}':'removed' for i in range(2,n)},
         **{f'crystal:{c}':v for c,v in zip(COLORS[:4],rewards)},
         'operation:0:success':True,'operation:1:success':True},
        [overload_selection(remove=i) for i in range(2,n)],'advanced_card_020',children,
        [dict(kind='EndTurn'),dict(kind='EndMarketRound')],useRoundEngine=True)

overload_selection_case(False,'魔法天赋分别弃三张实际红蓝绿原卡，取得三色许可',
    {'discard':3,'discard:ids':'basic_card_000,basic_card_016,basic_card_021',
     'overload:total':5,'overload:unique':5,'overload:spellColors':'Red,Blue,Green',
     'overload:zone2':'discard','overload:zone3':'discard','overload:zone4':'discard'},
    [overload_selection(discard=i) for i in [2,3,4]],'advanced_card_025',
    ['basic_card_021','basic_card_016','basic_card_000'])

for reason,index,foreign,message in [
    ('重复材料',2,False,'效能过载各次材料必须是不同实际实例，且不能重复使用原卡材料'),
    ('外来同ID材料',3,True,'分解必须选择实际手牌中的行动牌'),
    ('正在出牌的宿主',0,False,'正在打出的卡不能充当另一张弃牌或去除材料'),
    ('已作为复制来源的材料',1,False,'效能过载各次材料必须是不同实际实例，且不能重复使用原卡材料')]:
    overload_selection_case(True,'第二次选择'+reason+'，拒绝且不先消费或发奖励',
        {'hand':4,'cards:played':0,'cards:removed':0,'token:Red':1,
         'overload:total':4,'overload:unique':4,'overload:removedIds':'','overload:selectionCleared':False,
         'overload:choicesCleared':False,**{f'overload:zone{i}':'hand' for i in range(4)},
         **{f'crystal:{c}':0 for c in COLORS[:4]}},
        [overload_selection(remove=2),overload_selection(remove=index,foreign=foreign)],
        'advanced_card_020',['basic_card_021','basic_card_016'],
        expectedException='InvalidOperationException: '+message)

for count in [0,1,3]:
    overload_selection_case(True,'强效选择数量'+str(count)+'不等于两次，扣费前拒绝',
        {'hand':2,'cards:played':0,'cards:removed':0,'token:Red':1,'overload:zone0':'hand','overload:zone1':'hand',
         'overload:removedIds':'','overload:selectionCleared':False,'overload:choicesCleared':False},
        [overload_selection() for _ in range(count)],'basic_card_027',
        expectedException='InvalidOperationException: 效能过载必须为每次原效果提供完整选择')

overload_selection_case(True,'第二次本能选项非法，第一次合法移动也不执行',
    {'hand':2,'cards:played':0,'cards:removed':0,'token:Red':1,'overload:zone0':'hand','overload:zone1':'hand',
     'overload:removedIds':'','overload:selectionCleared':False,'overload:choicesCleared':False,
     'MovementPool':0,'InfluencePool':0,'MeleePool':0,'BlockPool':0},
    [overload_selection(0),overload_selection(4)],'basic_card_027',
    expectedException='InvalidOperationException: 本能必须选择移动、影响、攻击或格挡')

def overload_continuation(title,material,expected,ops=(),strong=True,**fields):
    fields.setdefault('spellOffer',[])
    fields.setdefault('spellReserve',[])
    overload_case(strong,title,{'overload:remaining':0,**expected},ops,
        material=material,overloadStepwise=True,**fields)
    CASES[-1]['limitations']=['本例以每次确认后的实际状态验证续选、费用与后处理；未覆盖所有内层原效果、嵌套选择和阶段入口，整卡保持partial']

overload_continuation('先领红法术，补位后旧红选项拒绝，再选择实际绿色法术','advanced_card_025',
    {'discard':2,'discard:ids':'magic_000,magic_006','overload:total':4,'overload:unique':4,'overload:spells':'',
     'played:overload:remaining':1,'played:overload:spells':'magic_000','played:token:Green':1,'token:Green':0,
     'operation:0:success':False,'operation:0:exception':'魔法天赋额外魔力必须与所获法术同色',
     'operation:0:remaining':1,'operation:0:spells':'magic_000','operation:0:discardIds':'magic_006',
     'operation:1:success':True,'operation:1:remaining':0},
    [dict(kind='RepeatNext',option=0),dict(kind='RepeatNext',option=2)],
    spellOffer=['magic_006'],spellReserve=['magic_000'],setup={'token:Red':2,'token:Green':1})
CASES[-1]['captureAfterOperations']=[0]

overload_continuation('第二次绿费不足保留第一次所得，再改选有红费的实际法术','advanced_card_025',
    {'discard':2,'discard:ids':'magic_007,magic_006','overload:total':4,'overload:unique':4,'overload:spells':'magic_000',
     'played:overload:remaining':1,'played:token:Red':1,
     'operation:0:success':False,'operation:0:exception':'本次重复效果的额外魔力不足','operation:0:remaining':1,
     'operation:0:token:Red':1,'operation:0:spells':'magic_000,magic_007','operation:0:discardIds':'magic_006',
     'operation:1:success':True,'operation:1:remaining':0},
    [dict(kind='RepeatNext',option=2),dict(kind='RepeatNext',option=256)],
    spellOffer=['magic_006'],spellReserve=['magic_000','magic_007'],setup={'token:Red':3})

overload_continuation('宁静第一次抽牌后继续选择，非法选项不重复抽牌，第三次改为治疗','basic_card_001',
    {'hand':2,'draw':0,'overload:total':4,'overload:unique':4,'overload:handIds':'basic_card_021,basic_card_016',
     'played:hand':1,'played:draw':1,'played:overload:remaining':2,
     'overload:healing':1,
     'operation:0:success':False,'operation:0:exception':'宁静必须选择治疗或抽牌','operation:0:remaining':2,
     'operation:0:hand':1,'operation:0:draw':1,'operation:1:success':True,'operation:1:remaining':1,
     'operation:1:hand':2,'operation:1:draw':0,'operation:2:success':True,'operation:2:remaining':0},
    [dict(kind='RepeatNext',option=5),dict(kind='RepeatNext',option=1),dict(kind='RepeatNext',option=0)],
    strong=False,option=1,initialDrawCards=['basic_card_021','basic_card_016'])

overload_continuation('决心两次格挡是一个整体：中途格挡和结束回合拒绝，完成后挡原毒巫师','basic_card_016',
    {'played:BlockPool':5,'BlockPool':0,'combat:printed':10,'combat:required':5,'combat:wounds':0,
     'operation:0:success':False,'operation:0:exception':'必须先完成当前卡牌的重复效果','operation:0:remaining':1,'operation:0:block':5,
     'operation:1:success':False,'operation:1:exception':'必须先完成当前卡牌的重复效果','operation:1:remaining':1,
     'operation:2:success':True,'operation:2:remaining':0,'operation:2:block':10,
     'operation:3:success':True,'operation:3:remaining':0,'operation:3:block':0},
    [dict(kind='RepeatBlock'),dict(kind='RepeatEndTurn'),dict(kind='RepeatNext'),dict(kind='RepeatBlock')],
    enemies=[dict(id='monster_002',originalDefinition=True)],setup={'InBattle':True})

overload_continuation('两次狂怒完成后才合并转远程，并且伏击加成只触发一次','basic_card_021',
    {'played:MeleePool':4,'played:RangedPool':0,'MeleePool':0,'RangedPool':10,'AmbushTriggered':True,'NextMeleeAsRanged':False,
     'operation:0:success':True,'operation:0:remaining':0,'operation:0:ranged':10,
     'operation:1:success':False,'operation:1:exception':'没有待继续的重复效果','operation:1:ranged':10},
    [dict(kind='RepeatNext'),dict(kind='RepeatNext')],setup={'InBattle':True,'AmbushAttackBonus':2,'NextMeleeAsRanged':True})

overload_continuation('其他玩家和另一次出牌不能劫持结晶续选，原玩家继续得到绿晶','basic_card_015',
    {'hand':1,'overload:total':3,'overload:unique':3,'overload:zone2':'hand','MovementPool':0,
     'crystal:Blue':1,'crystal:Green':1,'operation:0:success':False,'operation:0:exception':'重复效果不属于当前玩家或已完成',
     'operation:0:remaining':1,'operation:1:success':False,'operation:1:exception':'必须先完成当前卡牌的重复效果',
     'operation:1:remaining':1,'operation:2:success':True,'operation:2:remaining':0},
    [dict(kind='RepeatWrongPlayer',option=2),dict(kind='RepeatOtherCard',cardId='basic_card_000'),dict(kind='RepeatNext',option=2)],
    option=1,extraHandCards=['basic_card_015','basic_card_000'])

overload_continuation('宁静首步在战斗中非法，过载红费与实际材料均不消费','basic_card_001',
    {'hand':2,'cards:played':0,'cards:removed':0,'token:Red':1,'overload:zone0':'hand','overload:zone1':'hand',
     'overload:removedIds':'','overload:selectionCleared':False},
    option=1,setup={'InBattle':True},expectedException='InvalidOperationException: 战斗中不能使用宁静的治疗或抽牌效果')

overload_continuation('行进完成两次原强效后才移动，原平原成本实际扣除2','basic_card_000',
    {'played:MovementPool':4,'MovementPool':6,'position:q':1,
     'operation:0:success':False,'operation:0:exception':'本次移动未执行','operation:0:remaining':1,'operation:0:q':0,'operation:0:move':4,
     'operation:1:success':True,'operation:1:remaining':0,'operation:1:move':8,
     'operation:2:success':True,'operation:2:q':1,'operation:2:move':6},
    [dict(kind='RepeatMove',q=1,r=0),dict(kind='RepeatNext'),dict(kind='RepeatMove',q=1,r=0)],
    mapTerrains=[dict(q=1,r=0,terrain='Plains')])

def overload_permission_case(title,expected,ops,material='basic_card_021',stepwise=False,setup=None,**fields):
    initial={'InBattle':True,'DayPart':'Night','token:Red':0};initial.update(setup or {})
    options=dict(extraHandCards=['advanced_card_025',material,material,material],
        spellOffer=['magic_009','magic_015','magic_021'],spellReserve=[])
    if stepwise:options.update(overloadStepwise=True,discardIndex=2)
    else:options['overloadSelections']=[overload_selection(discard=i) for i in [2,3,4]]
    options.update(fields)
    values={'hand':0,'discard':3,'overload:total':5,'overload:unique':5,'overload:remaining':0,
        'overload:zone2':'discard','overload:zone3':'discard','overload:zone4':'discard',
        'overload:spells':'magic_009,magic_015,magic_021','overload:offerIdentity':True,'overload:choicesCleared':True,
        'overload:spellColors':'','token:Red':initial['token:Red']}
    values.update(expected)
    overload_case(False,title,values,ops,material='advanced_card_025',setup=initial,**options)
    CASES[-1]['limitations']=['验证本例原许可的真实施法、费用、供给身份、卡区与后续；其余法术和嵌套复制仍未全部验收，不判整卡通过']
    last=next(o for o in reversed(ops) if o['kind']=='CastOffer')
    idx=last.get('targetIndex',0)
    CASES[-1]['overloadPermissionArt']=options['spellOffer'][idx] if 0<=idx<len(options['spellOffer']) else ''

for material,color,index,element in [('basic_card_021','Red',0,'Fire'),('basic_card_016','Blue',1,'Ice')]:
    expected={'RangedPool':15,'attackElement':element,f'token:{color}':1,'discard:ids':','.join([material]*3)}
    for i in range(4):
        expected.update({f'operation:{i}:success':i<3,f'operation:{i}:permissions':max(0,2-i),
            f'operation:{i}:RangedPool':5*min(3,i+1),f'operation:{i}:token:{color}':max(1,3-i)})
    expected['operation:3:exception']='没有本回合该颜色的供给施法许可'
    overload_permission_case('三个实际'+color+'弃牌允许三次原法术，每次付费，第四次拒绝',expected,
        [dict(kind='CastOffer',targetIndex=index) for _ in range(4)],material=material,setup={f'token:{color}':4})

overload_permission_case('错色、非法索引和缺魔力均不能消费三个许可',
    {'RangedPool':0,'overload:spellColors':'Red,Red,Red',
     **{f'operation:{i}:success':False for i in range(4)},**{f'operation:{i}:permissions':3 for i in range(4)},
     'operation:0:exception':'没有本回合该颜色的供给施法许可',
     'operation:1:exception':'供给施法必须选择当前供给中的法术',
     'operation:2:exception':'供给施法必须选择当前供给中的法术','operation:3:exception':'法力不足，不能支付所选效果'},
    [dict(kind='CastOffer',targetIndex=i) for i in [1,-1,3,0]])

for night in [False,True]:
    expected={'overload:spellColors':','.join(['Red']*(1 if night else 3)),
        'SiegePool':16 if night else 0,'Wounds':2 if night else 0,'hand':2 if night else 0,
        'overload:total':7 if night else 5,'overload:unique':7 if night else 5,
        'token:Red':1 if night else 3,'token:Black':0 if night else 2}
    count=3 if night else 1
    for i in range(count):
        success=night and i<2
        expected.update({f'operation:{i}:success':success,f'operation:{i}:permissions':max(1,2-i) if night else 3})
    expected[f'operation:{count-1}:exception']='法力不足，不能支付所选效果' if night else '白昼不能施放强效法术'
    overload_permission_case(('夜晚' if night else '白昼')+'实际供给强效法术：原色与黑费、伤牌及拒绝后许可保持',expected,
        [dict(kind='CastOffer',targetIndex=0,enhanced=True) for _ in range(count)],
        setup={'DayPart':'Night' if night else 'Day','token:Red':3,'token:Black':2})

for skip in [False,True]:
    overload_permission_case('结束回合'+('跳过补牌' if skip else '正常补牌')+'都清理三次未用许可，不能跨回合施法',
        {'cards:played':0,'discard':4,'overload:zone0':'discard','crystal:Red':1,
         'discard:ids':'advanced_card_024,basic_card_021,basic_card_021,basic_card_021',
         'operation:0:success':True,'operation:0:permissions':0,'operation:1:success':False,
         'operation:1:permissions':0,'operation:1:exception':'没有本回合该颜色的供给施法许可'},
        [dict(kind='EndTurn',refill=not skip),dict(kind='CastOffer',targetIndex=0)],setup={'crystal:Red':1})

ops=[dict(kind='CastOffer',targetIndex=0),
     dict(kind='RepeatNext',repeatedSelection={**overload_selection(discard=0),'foreignDiscard':True}),
     dict(kind='RepeatNext',repeatedSelection=overload_selection(discard=0)),
     dict(kind='RepeatNext',repeatedSelection=overload_selection(discard=0)),
     *[dict(kind='CastOffer',targetIndex=0) for _ in range(3)]]
expected={'RangedPool':15,'token:Red':0,'played:overload:remaining':2,'played:overload:spellColors':'Red',
    'operation:0:exception':'必须先完成当前卡牌的重复效果','operation:1:exception':'魔法天赋必须弃掉实际持有的有色卡牌'}
for i,(success,remaining,permissions) in enumerate([(False,2,1),(False,2,1),(True,1,2),(True,0,3),(True,0,2),(True,0,1),(True,0,0)]):
    expected.update({f'operation:{i}:success':success,f'operation:{i}:remaining':remaining,f'operation:{i}:permissions':permissions})
overload_permission_case('实际手牌续选：中途施法和外来弃牌拒绝，完成后原法术正常施放三次',expected,ops,stepwise=True,setup={'token:Red':3})
CASES[-1]['captureAfterOperations']=[1]

overload_permission_case('原火焰投石魔像造成4伤，供给火焰风暴自伤达到5后真实昏迷弃牌',
    {'Wounds':5,'hand':5,'discard':4,'SiegePool':8,'token:Red':0,'token:Black':0,
     'overload:total':11,'overload:unique':11,'overload:zone5':'discard','overload:spellColors':'Red,Red',
     'overload:battleWounds':5,'overload:knockedOut':True,
     'discard:ids':'basic_card_000,basic_card_021,basic_card_021,basic_card_021',
     'operation:0:success':True,'operation:0:wounds':4,'operation:0:battleWounds':4,'operation:0:knockedOut':False,
     'operation:1:success':True,'operation:2:success':True,'operation:2:permissions':2,'operation:2:knockedOut':True},
    [dict(kind='Block',targetIndex=0),dict(kind='RepeatAttackPhase'),dict(kind='CastOffer',targetIndex=0,enhanced=True)],
    extraHandCards=['advanced_card_025','basic_card_021','basic_card_021','basic_card_021','basic_card_000'],
    enemies=[dict(id='monster_067',originalDefinition=True)],setup={'token:Red':1,'token:Black':1})
CASES[-1]['captureAfterOperations']=[0]

def basic_acceptance_case(card_id,strong,title,expected,ops=(),setup=None,**fields):
    initial={**{'token:'+c:0 for c in COLORS},**{'crystal:'+c:0 for c in COLORS},**(setup or {})}
    result={**{'token:'+c:0 for c in COLORS},**{'crystal:'+c:0 for c in COLORS},**expected}
    operation_case(card_id,strong,'基本验收：'+title,result,list(ops),setup=initial,
        playFromHand=True,basicAcceptance=True,initialHandCount=1,initialDrawCount=0,initialDiscardCards=[],**fields)
    CASES[-1]['limitations']=['本阶段验证原卡基本效果与直接结算；罕见组合、多层复制和完整边界保留扩展验收']

for strong,invalid in [(False,False),(True,False),(False,True)]:
    material='w' if strong or invalid else 'basic_card_021'
    expected={'crystal:Red':1,'hand':1 if invalid else 2,'draw':0,'discard':1,
        'Wounds':int(invalid),'cards:played':0,'cards:removed':0,
        'basic:zone0':'discard' if invalid else 'hand','basic:zone1':'hand' if invalid else 'discard',
        'basic:total':2 if invalid else 3,'basic:unique':2 if invalid else 3,
        'operation:0:success':not invalid,'operation:1:success':True}
    basic_acceptance_case('basic_card_019',strong,'伤牌不能支付基础回收' if invalid else '实际支付并在补牌前回收原卡',expected,
        [dict(kind='SelectJoyfulReturn',targetIndex=0),dict(kind='EndTurn',refill=not invalid)],
        setup={'token:Blue' if strong else 'token:Red':1},extraHandCards=[material],
        initialDrawCards=[] if invalid else ['basic_card_016'],captureAfterOperations=[0])

for decline in [False,True]:
    expected={'Wounds':1,'handWounds':1,'hand':1,'cards:played':1,'crystal:Green':0 if decline else 1,
        'ManaMeltdownChoices:count':0,'operation:0:success':True,
        'other:0:wounds':0,'other:1:wounds':0,'other:2:wounds':1,'other:2:handWounds':1,
        'other:0:crystal:Blue':0,'other:1:crystal:Green':0,'basic:zone0':'played'}
    basic_acceptance_case('magic_006',False,'先实际丢晶，再'+('放弃所得魔晶' if decline else '选择绿色魔晶'),expected,
        [dict(kind='ChooseMeltdownCrystal',option=-1 if decline else 2)],setup={'token:Red':1},
        otherPlayers=[dict(crystals=values({'Blue':1})),dict(crystals=values({'Green':1})),dict(crystals=[])])
basic_acceptance_case('magic_006',False,'取得实际绿晶后支付原复原法术',
    {'Wounds':0,'hand':0,'cards:played':2,'crystal:White':1,'HealingPool':3,
     'other:0:crystal:Green':0,'other:0:wounds':0,'operation:0:success':True,
     'basic:zone0':'played','basic:zone1':'played'},
    [dict(kind='PlayCard',cardId='magic_005')],setup={'token:Red':1,'crystal:White':1,'CurrentTerrain':'Plains'},
    destinationTerrain='Plains',extraHandCards=['magic_005'],otherPlayers=[dict(crystals=values({'Green':1}))],removeIndex=-1,discardIndex=-1,discardIndices=[])
for color in COLORS[:4]:
    other_color='Green' if color=='Blue' else 'Blue'
    basic_acceptance_case('magic_006',True,color+'按施法前魔晶数受伤再获得两晶',
        {'Wounds':1,'handWounds':1,'hand':1,'cards:played':1,'crystal:'+color:3,
         'other:0:wounds':2,'other:0:handWounds':2,'other:0:crystal:'+color:2,
         'other:1:wounds':0,'other:1:crystal:'+other_color:3,'basic:zone0':'played'},
        setup={'token:Red':1,'token:Black':1,'crystal:'+color:1},option=COLORS.index(color),
        otherPlayers=[dict(crystals=values({color:2})),dict(crystals=values({other_color:3}))])

for burn,enemy,color,furies,strong,printed,effective,fame,choice in [
    (False,'monster_027','White',1,False,5,5,4,0),(True,'monster_008','Green',2,True,16,8,8,0),
    (True,'monster_030','Red',0,False,8,8,5,0),(True,'monster_028','Blue',0,False,8,8,5,0),
    (False,'monster_030','White',1,False,5,5,5,1)]:
    ops=[dict(kind='PlayCard',cardId='basic_card_021',enhanced=strong) for _ in range(furies)]
    attack_index=len(ops)
    ops.extend([dict(kind='Attack',targetIndex=0),dict(kind='EndBattle'),dict(kind='EndTurn')])
    basic_acceptance_case('items_019',burn,'击败原敌人'+enemy+'获得'+color+'魔晶',
        {'crystal:'+color:1,'Fame':fame,'hand':0,'cards:played':0,'cards:removed':int(burn),
         'discard':furies+int(not burn),'Wounds':0,'basic:zone0':'removed' if burn else 'discard',
         'basic:unique':1+furies,'basic:total':1+furies,
         'operation:'+str(attack_index)+':kills':1,'operation:'+str(attack_index)+':printed':printed,
         'operation:'+str(attack_index)+':effective':effective},ops,setup={'token:Red':furies if strong else 0,'InBattle':True,'CombatPhase':'Melee'},
        extraHandCards=['basic_card_021']*furies,combatPhase='Melee',
        enemies=[dict(id=enemy,originalDefinition=True)],captureAfterOperations=[attack_index],
        removeIndex=-1,discardIndex=-1,discardIndices=[],option=choice)



# Basic acceptance second batch: real consumption and round lifetimes.

"""Insert after basic_acceptance_case and before export; do not run as a standalone generator.

Six normal scene chains cover ten cards. Earlier mode/choice results remain in the
evidence ledger; these cases add the missing real consumption, not an all-card rerun.

Required thin controller adapters (no substitute rules):
1. BeginInteraction: RecruitmentService.BeginInteraction(player, context,
   Enum.Parse<RecruitLocation>(operation.recruitLocation)), then success=True.
2. At the end of each operation for basicAcceptance, record only requested Snapshot()
   fields as operation:<index>:state:<field>. Needed fields are the exact keys used below:
   pools, Fame/Reputation/Wounds/hand, cards:played, token/crystal fields, the
   basic:zone* fields, NextCardEnhanced/BoostValue/InNegotiation. Do not recursively
   copy prior operation:* or played:* fields. Existing attack/recruit result fields
   such as printed/effective/required/kills/cost/owned/ready are already recorded.
3. Basic Snapshot additionally exposes spellOffer:ids (comma-joined actual IDs)
   and spellOffer:sameInstances (same references/order as _originalSpellOffer).
4. Basic UI shows current Influence/Melee/Ranged/Block totals and step result;
   retain the root original art while displaying the actual follow-up card art.
   For recruit/attack show the selected original unit/enemy and consumed amount.
5. monster_007 uses existing cards/monster (7).jpg (viewed: armor4/fame3);
   ensure Catalog.enemyArtwork references it. No generated/downloaded artwork.

Player setup uses player:TempCommandSlots, never read-only CommandSlots. The
existing controller needs one unit, so every fixture explicitly owns an unhurt
original farmer; its abilities are not used. All default materials are cleared
except the explicit actual discard used by Improvisation.
"""


def _consumption_state(index, mapping):
    native = {'success', 'printed', 'effective', 'required', 'kills', 'cost', 'units', 'unitId', 'owned', 'offer', 'ready'}
    return {f"operation:{index}:{'' if key in native else 'state:'}{key}": value
            for key, value in mapping.items()}


# Full Focus + Blood Rage: original strong attack9, self-wound1, boost3 => attack12.
# Covers basic_card_006 and advanced_card_004.
basic_acceptance_case(
    'basic_card_006', True, '全神贯注免费强化原血色狂怒，攻击12实际击败人类战士',
    {'MeleePool': 0, 'Fame': 4, 'Wounds': 1, 'handWounds': 1, 'hand': 1,
     'cards:played': 2, 'discard': 0, 'cards:removed': 0,
     'NextCardEnhanced': False, 'BoostValue': 0,
     'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:total': 3, 'basic:unique': 3,
     'played:NextCardEnhanced': True, 'played:BoostValue': 3, 'played:token:Green': 0,
     **_consumption_state(0, {'success': True, 'MeleePool': 12, 'Wounds': 1, 'handWounds': 1,
         'cards:played': 2, 'token:Red': 0, 'NextCardEnhanced': False, 'BoostValue': 0,
         'basic:zone0': 'played', 'basic:zone1': 'played'}),
     **_consumption_state(1, {'success': True, 'printed': 12, 'effective': 12, 'required': 5,
         'kills': 1, 'MeleePool': 0, 'Fame': 4, 'Wounds': 1})},
    [dict(kind='PlayCard', cardId='advanced_card_004', enhanced=False, option=1),
     dict(kind='Attack', targetIndex=0)],
    setup={'token:Green': 1, 'InBattle': True, 'CombatPhase': 'Melee'},
    extraHandCards=['advanced_card_004'], combatPhase='Melee',
    enemies=[dict(id='monster_027', originalDefinition=True)], unitIds=['army_2_006'],
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[0])


# Threaten2 + Noble2 (+Fame1 inside actual interaction) + CallForHelp3+one wound=4.
# The accumulated8 pays the actual printed8 of supported original army_003.
# Covers basic_card_022, basic_card_012 and advanced_card_019.
basic_acceptance_case(
    'basic_card_022', False, '三张原交涉牌凑足8影响，实际招募原烈焰枪手',
    {'InfluencePool': 0, 'Fame': 1, 'Reputation': 0, 'Wounds': 1, 'handWounds': 1,
     'hand': 1, 'cards:played': 3, 'discard': 0, 'cards:removed': 0,
     'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:zone2': 'played',
     'basic:zone3': 'hand', 'basic:total': 4, 'basic:unique': 4,
     'played:InfluencePool': 2, 'played:Fame': 0, 'played:Reputation': 0,
     **_consumption_state(0, {'success': True, 'InfluencePool': 2, 'InNegotiation': True}),
     **_consumption_state(1, {'success': True, 'InfluencePool': 4, 'Fame': 1,
         'Reputation': 0, 'basic:zone1': 'played'}),
     **_consumption_state(2, {'success': True, 'InfluencePool': 8, 'Fame': 1,
         'Wounds': 1, 'cards:played': 3, 'basic:zone2': 'played'}),
     **_consumption_state(3, {'success': True, 'cost': 8, 'InfluencePool': 0,
         'units': 2, 'unitId': 'army_003', 'owned': 'army_2_006,army_003',
         'offer': '', 'ready': 'False,True', 'Fame': 1, 'Reputation': 0})},
    [dict(kind='BeginInteraction', recruitLocation='Keep'),
     dict(kind='PlayCard', cardId='basic_card_012', enhanced=False),
     dict(kind='PlayCard', cardId='advanced_card_019', enhanced=False),
     dict(kind='Recruit', targetIndex=0, recruitLocation='Keep')],
    setup={'InBattle': False, 'player:Reputation': 0, 'player:TempCommandSlots': 1},
    extraHandCards=['basic_card_012', 'advanced_card_019', 'w'],
    recruitOffer=['army_003'], unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[2])


# Crystallize turns the only red token into a red crystal. Improvisation spends
# that very resource for its printed enhancement, discards the actual March,
# and consumes attack5 against the original human warrior.
# Covers basic_card_015 and basic_card_023.
basic_acceptance_case(
    'basic_card_015', False, '晶化所得红晶支付原随机应变，实际弃牌并攻击5',
    {'MeleePool': 0, 'Fame': 4, 'Wounds': 0, 'hand': 0, 'cards:played': 2,
     'discard': 1, 'cards:removed': 0,
     'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:zone2': 'discard',
     'basic:total': 3, 'basic:unique': 3,
     'played:token:Red': 0, 'played:crystal:Red': 1, 'played:hand': 2,
     **_consumption_state(0, {'success': True, 'MeleePool': 5, 'crystal:Red': 0,
         'token:Red': 0, 'cards:played': 2, 'hand': 0,
         'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:zone2': 'discard'}),
     **_consumption_state(1, {'success': True, 'printed': 5, 'effective': 5, 'required': 5,
         'kills': 1, 'MeleePool': 0, 'Fame': 4})},
    [dict(kind='PlayCard', cardId='basic_card_023', enhanced=True, option=2),
     dict(kind='Attack', targetIndex=0)],
    setup={'token:Red': 1, 'InBattle': True, 'CombatPhase': 'Melee'}, option=0,
    extraHandCards=['basic_card_023', 'basic_card_000'], combatPhase='Melee',
    enemies=[dict(id='monster_027', originalDefinition=True)], unitIds=['army_2_006'],
    removeIndex=-1, discardIndex=2, discardIndices=[], captureAfterOperations=[0])


# Covers advanced_card_002. Original wolf-rider art: armor4, fame3, no fortification.
basic_acceptance_case(
    'advanced_card_002', True, '迅捷箭矢支付白魔力，远程4击败原兽人狼骑兵',
    {'RangedPool': 0, 'Fame': 3, 'Wounds': 0, 'hand': 0, 'cards:played': 1,
     'discard': 0, 'cards:removed': 0, 'basic:zone0': 'played',
     'basic:total': 1, 'basic:unique': 1,
     'played:RangedPool': 4, 'played:token:White': 0,
     **_consumption_state(0, {'success': True, 'printed': 4, 'effective': 4, 'required': 4,
         'kills': 1, 'RangedPool': 0, 'Fame': 3})},
    [dict(kind='RangedAttack', targetIndex=0)],
    setup={'token:White': 1, 'InBattle': True, 'CombatPhase': 'Ranged'},
    combatPhase='Ranged', enemies=[dict(id='monster_007', originalDefinition=True)],
    unitIds=['army_2_006'], removeIndex=-1, discardIndex=-1, discardIndices=[])


# Covers advanced_card_008: choose attack, not influence. Reputation is paid once.
basic_acceptance_case(
    'advanced_card_008', True, '胁迫选择攻击7并声誉减2，实际击敗原人类战士',
    {'MeleePool': 0, 'InfluencePool': 0, 'Fame': 4, 'Reputation': -2,
     'Wounds': 0, 'hand': 0, 'cards:played': 1, 'discard': 0, 'cards:removed': 0,
     'basic:zone0': 'played', 'basic:total': 1, 'basic:unique': 1,
     'played:MeleePool': 7, 'played:InfluencePool': 0,
     'played:Reputation': -2, 'played:token:Red': 0,
     **_consumption_state(0, {'success': True, 'printed': 7, 'effective': 7, 'required': 5,
         'kills': 1, 'MeleePool': 0, 'Fame': 4, 'Reputation': -2})},
    [dict(kind='Attack', targetIndex=0)],
    setup={'token:Red': 1, 'InBattle': True, 'CombatPhase': 'Melee'}, option=1,
    combatPhase='Melee', enemies=[dict(id='monster_027', originalDefinition=True)],
    unitIds=['army_2_006'], removeIndex=-1, discardIndex=-1, discardIndices=[])


# Covers advanced_card_033 after the real SpellForgeEffect fix: select two
# different red spells, gain two red crystals, spend one on actual Fury4.
basic_acceptance_case(
    'advanced_card_033', True, '法术熔炉选择两张红法术得两晶，支付原狂怒并攻击',
    {'MeleePool': 0, 'Fame': 3, 'crystal:Red': 1, 'Wounds': 0, 'hand': 0,
     'cards:played': 2, 'discard': 0, 'cards:removed': 0,
     'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:total': 2, 'basic:unique': 2,
     'spellOffer:ids': 'magic_009,magic_010', 'spellOffer:sameInstances': True,
     'played:crystal:Red': 2, 'played:crystal:Blue': 0, 'played:token:Blue': 0,
     'played:spellOffer:ids': 'magic_009,magic_010', 'played:spellOffer:sameInstances': True,
     **_consumption_state(0, {'success': True, 'MeleePool': 4, 'crystal:Red': 1,
         'token:Red': 0, 'cards:played': 2, 'basic:zone1': 'played'}),
     **_consumption_state(1, {'success': True, 'printed': 4, 'effective': 4, 'required': 4,
         'kills': 1, 'MeleePool': 0, 'Fame': 3})},
    [dict(kind='PlayCard', cardId='basic_card_021', enhanced=True), dict(kind='Attack', targetIndex=0)],
    setup={'token:Blue': 1, 'InBattle': True, 'CombatPhase': 'Melee'}, option=256,
    extraHandCards=['basic_card_021'], spellOffer=['magic_009', 'magic_010'], spellReserve=[],
    combatPhase='Melee', enemies=[dict(id='monster_007', originalDefinition=True)],
    unitIds=['army_2_006'], removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[0])


"""Paste before export() after basic_acceptance_case is defined; emits exactly 4 cases.

Evidence basis: MK.Tests.Cards.ManaClaimBasicStateTests (4 backend cases passed
in the parent's current batch). This is scene configuration only: no rule model,
no automatic Unity invocation, no changes to the shared generator/controller.

Required thin adapters (not implemented by this snippet):
1. Case DTO manaSourceRolls:int[]; construct the same GameEngine with all actual
   [self]+OtherPlayers and manaRandom=ScriptedRollRandom(sequence). Use its Globals.Mana
   as context.ManaSource. No second source, no extra initial ResetSource. For the
   night case advance that engine's clock once before play; basic face indices
   0..3 have the same meaning in both DayPart lists.
2. OtherPlayer DTO tokens:CardVerificationValue[], extraHandCards:string[]; construct
   real DeedCards once in setup and keep an ActionContext for each other player.
3. TakeManaDie(option=color) calls source.Take(self,color), rejects null, records
   success. It only holds an ordinary die, exactly as the production API does.
   It must not fabricate a token as a second effect of Mana Claim.
4. OtherStartTurn(playerIndex=index), OtherEndTurn(playerIndex=index), OtherPlayCard(playerIndex=index,cardId):
   use that actual player, its persistent context, same source, PlayerTurnEngine,
   CardPlayActions and original definition. Select an existing matching hand
   instance; never create a new card in the operation. OtherEndTurn skips refill
   using the same existing operation.refill convention as EndTurn.
5. Existing EndRound uses _roundEngine.EndRound() when useRoundEngine is true;
   then sync current/all other contexts' DayPart with its real Clock.DayPart.
6. Snapshot only needs these new keys:
   mana:sourceCount=source.Dice.Count;
   mana:heldCount=RoundClaims owned by self count;
   mana:heldFaces=comma-joined Face of self's held claims;
   mana:ordinaryHeld=self.HeldManaDie!=null;
   mana:identity=union of source Dice + RoundClaims keys + all normal held dice
   exactly equals saved original reference set, with no duplicate instances;
   mana:perTurn:Red=self.TokensPerTurn[Red];
   other:0:token:Red=other real tokens; other:0:curseActive=ManaCurseColor.HasValue.
   Reuse existing token:Red, other:0:wounds, other:0:crystal:Red keys.
   Every operation copies observed Snapshot keys to operation:{i}:state:<key>.
   After play copy to played:<key>. Success stays operation:{i}:success.
   Empty mana:heldFaces is ''; boolean reports use True/False strings.

Minimum visible checks: original magic_012 top/bottom art and printed costs,
actual source/retained dice, 3->2 spending, 0->1 deferred gain, other wounds
0->1->1->2->2. Keep a selected intermediate capture and final capture for the
three normal cases; the missing-die case needs only its rejection result.

Deterministic dice sequences are finite, and consume only legitimate rolls:
immediate: constructor 3 + round-end reset 3;
deferred: constructor 3 + ordinary die return 1 + round-end reset 3;
curse: constructor 4 + round-end reset 4;
missing: constructor 3. Do not call an extra ResetSource in scene setup.
"""


_claim_common = dict(
    useRoundEngine=True,
    removeIndex=-1,
    discardIndex=-1,
    discardIndices=[],
    destinationTerrain='Plains',
)

# 1. Actual red source die grants 3, one pays the existing original Fireball.
# EndTurn consumes unused tokens but leaves the same claimed die outside source.
basic_acceptance_case(
    'magic_012', False, '立即三红实际支付原火球，宣夺骰回合保留到轮末',
    {
        'hand': 0, 'discard': 0, 'draw': 2, 'cards:played': 0,
        'basic:zone0': 'draw', 'basic:zone1': 'draw',
        'played:token:Red': 3, 'played:token:Blue': 0,
        'played:mana:heldCount': 1, 'played:mana:sourceCount': 2,
        'mana:sourceCount': 3, 'mana:heldCount': 0,
        'mana:heldFaces': '', 'mana:identity': True,
        'operation:0:success': True, 'operation:0:state:token:Red': 2,
        'operation:0:state:mana:sourceCount': 2, 'operation:0:state:mana:heldCount': 1,
        'operation:0:state:mana:heldFaces': 'Red', 'operation:0:state:mana:identity': True,
        'operation:1:state:token:Red': 0, 'operation:1:state:mana:sourceCount': 2,
        'operation:1:state:mana:heldCount': 1, 'operation:1:state:mana:heldFaces': 'Red',
        'operation:1:state:mana:identity': True, 'operation:2:success': True,
    },
    [dict(kind='PlayCard', cardId='magic_009'), dict(kind='EndTurn'), dict(kind='EndRound')],
    setup={'token:Blue': 1, 'DayPart': 'Day', 'InBattle': False},
    option=0, manaSourceRolls=[0, 1, 2, 0, 1, 2],
    extraHandCards=['magic_009'], captureAfterOperations=[0, 1], **_claim_common,
)

# 2. No immediate reward. Claim red, separately hold an ordinary blue die,
# then verify normal-return versus round-return and two actual StartTurn grants.
basic_acceptance_case(
    'magic_012', False, '递延下回合才给一红，普通蓝骰先归还，轮末停止发放',
    {
        'hand': 0, 'discard': 0, 'draw': 1, 'cards:played': 0,
        'basic:zone0': 'draw', 'mana:sourceCount': 3,
        'played:token:Red': 0, 'played:token:Blue': 0,
        'played:mana:perTurn:Red': 1, 'played:mana:heldCount': 1,
        'mana:heldCount': 0, 'mana:heldFaces': '', 'mana:identity': True,
        'mana:ordinaryHeld': False, 'mana:perTurn:Red': 0,
        'operation:0:success': True, 'operation:0:state:token:Red': 0,
        'operation:0:state:mana:sourceCount': 1, 'operation:0:state:mana:heldCount': 1,
        'operation:0:state:mana:ordinaryHeld': True, 'operation:0:state:mana:perTurn:Red': 1,
        'operation:1:state:mana:sourceCount': 2, 'operation:1:state:mana:heldCount': 1,
        'operation:1:state:mana:ordinaryHeld': False, 'operation:1:state:mana:identity': True,
        'operation:2:state:token:Red': 1, 'operation:2:state:mana:perTurn:Red': 1,
        'operation:4:state:token:Red': 1, 'operation:4:state:mana:perTurn:Red': 1,
        'operation:6:success': True, 'operation:7:state:token:Red': 0,
        'operation:7:state:mana:perTurn:Red': 0,
    },
    [dict(kind='TakeManaDie', option=1), dict(kind='EndTurn'),
     dict(kind='StartTurn'), dict(kind='EndTurn'), dict(kind='StartTurn'),
     dict(kind='EndTurn'), dict(kind='EndRound'), dict(kind='StartTurn')],
    setup={'token:Blue': 1, 'DayPart': 'Day', 'InBattle': False},
    option=256, manaSourceRolls=[0, 1, 2, 1, 0, 1, 2],
    captureAfterOperations=[2], **_claim_common,
)

# 3. Four real Fireball instances are in the other player's initial hand.
# First two casts use the two red tokens; the next two use one real red crystal
# each. The last cast happens after the real round end, hence no new curse wound.
# This differs only in the last payment source from the backend's final token
# payment; both original token/crystal paths and the same curse expiry were tested.
basic_acceptance_case(
    'magic_012', True, '诅咒实际红标记与红晶支付，每回合一次，轮末解除',
    {
        'hand': 0, 'draw': 1, 'discard': 0, 'cards:played': 0,
        'basic:zone0': 'draw', 'mana:sourceCount': 4,
        'played:token:Red': 3, 'played:token:Blue': 0, 'played:token:Black': 0,
        'played:mana:heldCount': 1, 'played:other:0:curseActive': True,
        'mana:heldCount': 0, 'mana:heldFaces': '', 'mana:identity': True,
        'other:0:wounds': 2, 'other:0:token:Red': 0,
        'other:0:crystal:Red': 0, 'other:0:curseActive': False,
        'other:0:handWounds': 2,
        'operation:0:state:token:Red': 0, 'operation:0:state:mana:heldCount': 1,
        'operation:1:state:other:0:wounds': 0, 'operation:1:state:other:0:token:Red': 2,
        'operation:2:success': True, 'operation:2:state:other:0:wounds': 1,
        'operation:2:state:other:0:token:Red': 1, 'operation:2:state:other:0:crystal:Red': 2,
        'operation:3:success': True, 'operation:3:state:other:0:wounds': 1,
        'operation:3:state:other:0:token:Red': 0,
        'operation:6:success': True, 'operation:6:state:other:0:wounds': 2,
        'operation:6:state:other:0:crystal:Red': 1,
        'operation:8:success': True, 'operation:8:state:other:0:curseActive': False,
        'operation:10:success': True, 'operation:10:state:other:0:wounds': 2,
        'operation:10:state:other:0:crystal:Red': 0,
    },
    [dict(kind='EndTurn'), dict(kind='OtherStartTurn', playerIndex=0),
     dict(kind='OtherPlayCard', playerIndex=0, cardId='magic_009'),
     dict(kind='OtherPlayCard', playerIndex=0, cardId='magic_009'),
     dict(kind='OtherEndTurn', playerIndex=0), dict(kind='OtherStartTurn', playerIndex=0),
     dict(kind='OtherPlayCard', playerIndex=0, cardId='magic_009'),
     dict(kind='OtherEndTurn', playerIndex=0), dict(kind='EndRound'),
     dict(kind='OtherStartTurn', playerIndex=0),
     dict(kind='OtherPlayCard', playerIndex=0, cardId='magic_009')],
    setup={'token:Blue': 1, 'token:Black': 1, 'DayPart': 'Night', 'InBattle': False},
    option=0, manaSourceRolls=[0, 1, 2, 3, 0, 1, 2, 3],
    otherPlayers=[dict(tokens=values({'Red': 2}), crystals=values({'Red': 2}),
                       extraHandCards=['magic_009'] * 4)],
    captureAfterOperations=[2], **_claim_common,
)

# 4. A green/blue/white source must not silently supply a non-existent red die.
basic_acceptance_case(
    'magic_012', False, '源中没有红骰时保留蓝费用、实际原骰和手牌',
    {
        'token:Blue': 1, 'hand': 1, 'draw': 0, 'discard': 0,
        'cards:played': 0, 'basic:zone0': 'hand',
        'mana:sourceCount': 3, 'mana:heldCount': 0,
        'mana:heldFaces': '', 'mana:identity': True,
        'mana:ordinaryHeld': False, 'mana:perTurn:Red': 0,
    },
    setup={'token:Blue': 1, 'DayPart': 'Day', 'InBattle': False},
    option=0, manaSourceRolls=[1, 2, 3],
    expectedException='InvalidOperationException: 魔力源中没有所选基本色骰',
    **_claim_common,
)


"""德鲁伊法杖 6 个已通过后台的正常分支；本文件不导入或执行生成器。

主任务集成方式：加载本片段后，在生成器 export() 前调用
    add_druid_staff_basic_scenes(basic_acceptance_case)

仅使用已有 DTO / 操作。薄适配器需求：
1. 主任务已约定按 expected 请求保存 played:<Snapshot key> 和
   operation:N:state:<Snapshot key>；不需新增规则或 DTO 字段。
2. 主卡为 items_021 且 playFromHand 时复用 CardPlayActions.PlayArtifact，
   discardIndex=1 是真实材料；焚毁 discardIndex=-1。禁用默认去除材料和自动多弃牌。
3. basicAcceptance UI 展示原材料卡图/颜色、部队原图、治疗池和伤数、
   坐标/路径及源卡所在牌区；PlayCard 后展示原狂怒图与支付后的红晶。
4. 白色移动发生在主卡执行时，不再调用 Move 或 TemporalPortal，也不产生传送回合状态。
5. BeginBattle 复用现有操作；UnitUse option=0 是原 army_003 无费攻击/格挡5，
   abilityChoice=0 选攻击。HealUnit option=1 表示治1伤、费用由真实等级计算。
6. 对原图的 UI 断言由主任务已有自动化导出统一添加；此片段不伪造原图快照值。

这 6 例是正常成功分支，2 个后台失败守恒用例不在此重复场景扩矩阵。
原 items_021_00/01 模块报告保留历史，不把它们升级为整卡完成。
"""


def add_druid_staff_basic_scenes(basic_acceptance_case):
    def add(burn, title, final, played, ops, steps=None, **fields):
        expected = {"Wounds": 0, "handWounds": 0, "cards:played": 0,
                    "cards:removed": int(burn), "HealingPool": 0, **final}
        expected.update({"played:" + key: value for key, value in played.items()})
        for index in range(len(ops)):
            expected[f"operation:{index}:success"] = True
        for index, state in (steps or {}).items():
            expected.update({f"operation:{index}:state:{key}": value
                             for key, value in state.items()})
        fixture = dict(destinationTerrain="Plains", removeIndex=-1,
                       discardIndex=-1 if burn else 1, discardIndices=[],
                       unitIds=["army_2_006"], initialUnitWounds=[0],
                       extraHandCards=[], captureAfterOperations=[])
        fixture.update(fields)
        basic_acceptance_case("items_021", burn, title, expected, ops,
                              setup={"InBattle": False, "CurrentTerrain": "Plains"}, **fixture)

    add(False, "弃原敏捷白卡，沿已揭示路径免费移动两格并检查弃牌", {
        "position:q": 2, "position:r": 0, "MovementPool": 0,
        "hand": 0, "discard": 2, "basic:zone0": "discard", "basic:zone1": "discard",
        "basic:total": 2, "basic:unique": 2, "TemporalPortalTurn": False,
        "TemporalPortalPending": False,
    }, {
        "position:q": 2, "position:r": 0, "MovementPool": 0,
        "cards:played": 1, "cards:removed": 0, "hand": 0, "discard": 1,
        "basic:zone0": "played", "basic:zone1": "discard",
        "TemporalPortalTurn": False, "TemporalPortalPending": False,
    }, [dict(kind="EndTurn")], {
        0: {"position:q": 2, "position:r": 0, "cards:played": 0,
            "discard": 2, "basic:zone0": "discard"},
    }, option=0, extraHandCards=["basic_card_007"], revealedDistance=2,
        travelPath=[dict(q=1, r=0), dict(q=2, r=0)])

    add(False, "弃原决心蓝卡取得两红晶，再实际支付原狂怒强效", {
        "crystal:Red": 1, "MeleePool": 0, "hand": 0, "discard": 3,
        "basic:zone0": "discard", "basic:zone1": "discard", "basic:zone2": "discard",
        "basic:total": 3, "basic:unique": 3,
    }, {
        "crystal:Red": 2, "cards:played": 1, "cards:removed": 0,
        "hand": 1, "discard": 1, "basic:zone0": "played",
        "basic:zone1": "discard", "basic:zone2": "hand",
    }, [dict(kind="BeginBattle"), dict(kind="PlayCard", cardId="basic_card_021", enhanced=True),
        dict(kind="EndBattle"), dict(kind="EndTurn")], {
        0: {"InBattle": True, "crystal:Red": 2, "MeleePool": 0},
        1: {"crystal:Red": 1, "MeleePool": 4, "hand": 0,
            "cards:played": 2, "basic:zone2": "played"},
        2: {"InBattle": False, "MeleePool": 0, "crystal:Red": 1},
        3: {"discard": 3, "cards:played": 0, "basic:zone0": "discard"},
    }, option=1, extraHandCards=["basic_card_016", "basic_card_021"], captureAfterOperations=[1])

    add(False, "弃原狂怒红卡，重整原三级烈焰枪手并实际发动攻击5", {
        "unitLevel": 3, "units:ready": "False", "units:wounds": "0",
        "MeleePool": 0, "hand": 0, "discard": 2,
        "basic:zone0": "discard", "basic:zone1": "discard",
        "basic:total": 2, "basic:unique": 2,
    }, {
        "units:ready": "True", "units:wounds": "0", "unitCanActivate": True,
        "cards:played": 1, "hand": 0, "discard": 1,
        "basic:zone0": "played", "basic:zone1": "discard",
    }, [dict(kind="BeginBattle"), dict(kind="UnitUse", targetIndex=0, option=0, abilityChoice=0),
        dict(kind="EndBattle"), dict(kind="EndTurn")], {
        0: {"InBattle": True, "units:ready": "True", "MeleePool": 0},
        1: {"units:ready": "False", "units:wounds": "0", "MeleePool": 5},
        2: {"InBattle": False, "MeleePool": 0},
        3: {"cards:played": 0, "discard": 2, "basic:zone0": "discard"},
    }, option=2, extraHandCards=["basic_card_021"], unitIds=["army_003"], captureAfterOperations=[1])

    add(False, "弃原行进绿卡，治疗3分别消费于英雄1伤及原二级魔像1伤", {
        "hand": 0, "discard": 2, "unitLevel": 2,
        "units:ready": "False", "units:wounds": "0",
        "basic:zone0": "discard", "basic:zone1": "discard", "basic:zone2": "",
        "basic:total": 2, "basic:unique": 2,
    }, {
        "HealingPool": 3, "Wounds": 1, "handWounds": 1, "hand": 1,
        "units:wounds": "1", "units:ready": "False", "cards:played": 1,
        "discard": 1, "basic:zone0": "played", "basic:zone1": "discard", "basic:zone2": "hand",
    }, [dict(kind="HealHand", option=1), dict(kind="HealUnit", targetIndex=0, option=1),
        dict(kind="EndTurn")], {
        0: {"HealingPool": 2, "Wounds": 0, "handWounds": 0, "hand": 0,
            "units:wounds": "1", "basic:zone2": ""},
        1: {"HealingPool": 0, "units:wounds": "0", "units:ready": "False", "Wounds": 0},
        2: {"discard": 2, "cards:played": 0, "basic:zone0": "discard"},
    }, option=3, extraHandCards=["basic_card_000", "w"], unitIds=["army_2_001"],
        initialUnitWounds=[1], captureAfterOperations=[1])

    add(True, "焚毁选择白色移动和蓝色得晶，不弃材料且原法杖永久移除", {
        "position:q": 2, "position:r": 0, "MovementPool": 0, "crystal:Blue": 2,
        "hand": 1, "discard": 0, "basic:zone0": "removed", "basic:zone1": "hand",
        "basic:total": 2, "basic:unique": 2,
    }, {
        "position:q": 2, "position:r": 0, "MovementPool": 0, "crystal:Blue": 2,
        "hand": 1, "discard": 0, "cards:played": 0, "cards:removed": 1,
        "basic:zone0": "removed", "basic:zone1": "hand",
        "TemporalPortalTurn": False, "TemporalPortalPending": False,
    }, [dict(kind="EndTurn")], {
        0: {"position:q": 2, "crystal:Blue": 2, "hand": 1, "discard": 0,
            "cards:removed": 1, "basic:zone0": "removed", "basic:zone1": "hand"},
    }, option=(1 << 16) | (1 << 24), extraHandCards=["basic_card_021"],
        travelPath=[dict(q=1, r=0), dict(q=2, r=0)], revealedDistance=2)

    add(True, "焚毁红绿两项，重整后治疗原三级部队，随后真实发动攻击5", {
        "hand": 0, "discard": 0, "unitLevel": 3, "units:wounds": "0",
        "units:ready": "False", "MeleePool": 0,
        "basic:zone0": "removed", "basic:total": 1, "basic:unique": 1,
    }, {
        "HealingPool": 3, "units:ready": "True", "units:wounds": "1",
        "unitCanActivate": False, "hand": 0, "discard": 0,
        "cards:played": 0, "cards:removed": 1, "basic:zone0": "removed",
    }, [dict(kind="HealUnit", targetIndex=0, option=1), dict(kind="BeginBattle"),
        dict(kind="UnitUse", targetIndex=0, option=0, abilityChoice=0),
        dict(kind="EndBattle"), dict(kind="EndTurn")], {
        0: {"HealingPool": 0, "units:wounds": "0", "units:ready": "True", "unitCanActivate": True},
        1: {"InBattle": True, "units:ready": "True", "units:wounds": "0"},
        2: {"MeleePool": 5, "units:ready": "False", "units:wounds": "0"},
        3: {"InBattle": False, "MeleePool": 0},
        4: {"cards:played": 0, "cards:removed": 1, "discard": 0, "basic:zone0": "removed"},
    }, option=2 | (3 << 16), unitIds=["army_003"], initialUnitWounds=[1], captureAfterOperations=[0, 2])


add_druid_staff_basic_scenes(basic_acceptance_case)

"""Append after the existing six consumption cases, with basic_acceptance_case in scope.

Expected next id: advanced_card_033_03 (current catalog ends at _02).
No extra controller adapter required. Uses only existing requested snapshots.
One base case, one selected original red spell, one real crystal payment.
"""

basic_acceptance_case(
    'advanced_card_033', False, '法术熔炉基础选择一张真实红法术得一晶，支付原狂怒并攻击',
    {'MeleePool': 0, 'Fame': 3, 'Wounds': 0, 'hand': 0,
     'crystal:Red': 0, 'cards:played': 2, 'discard': 0, 'cards:removed': 0,
     'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:total': 2, 'basic:unique': 2,
     'spellOffer:ids': 'magic_009,magic_010', 'spellOffer:sameInstances': True,
     'played:crystal:Red': 1, 'played:crystal:Blue': 0,
     'played:token:Blue': 0, 'played:token:Red': 0,
     'played:hand': 1, 'played:cards:played': 1,
     'played:basic:zone0': 'played', 'played:basic:zone1': 'hand',
     'played:spellOffer:ids': 'magic_009,magic_010',
     'played:spellOffer:sameInstances': True,
     'operation:0:success': True,
     'operation:0:state:MeleePool': 4,
     'operation:0:state:crystal:Red': 0,
     'operation:0:state:token:Red': 0,
     'operation:0:state:cards:played': 2,
     'operation:0:state:basic:zone1': 'played',
     'operation:0:state:spellOffer:sameInstances': True,
     'operation:1:success': True,
     'operation:1:printed': 4, 'operation:1:effective': 4,
     'operation:1:required': 4, 'operation:1:kills': 1,
     'operation:1:state:MeleePool': 0, 'operation:1:state:Fame': 3,
     'operation:1:state:spellOffer:ids': 'magic_009,magic_010',
     'operation:1:state:spellOffer:sameInstances': True},
    [dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='Attack', targetIndex=0)],
    setup={'InBattle': True, 'CombatPhase': 'Melee'}, option=1,
    extraHandCards=['basic_card_021'], spellOffer=['magic_009', 'magic_010'], spellReserve=[],
    combatPhase='Melee', enemies=[dict(id='monster_007', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[0])



# Printed terrain movement, using real cards and successive adjacent map cells.
for number in (9, 10, 11):
    for strong in (False, True):
        card_id = f'advanced_card_{number:03}'
        setup = {'DayPart': 'Night' if number == 10 or (number == 11 and strong) else 'Day',
                 'CurrentTerrain': 'Plains'}
        ops, snapshots, extra = [], {}, []
        initial_move = 4 if strong and number != 10 else 2
        played = {'MovementPool': initial_move, 'position:q': 0, 'position:r': 0,
                  'token:White': 0, 'token:Blue': int(number == 10 and strong),
                  'token:Green': int(number == 10), 'cards:played': 1}
        if strong: setup['token:' + {9:'Blue',10:'White',11:'Green'}[number]] = 1
        if number == 10:
            setup['token:Green'] = 1
            if strong: setup['token:Blue'] = 1
        if number == 10 or (number == 11 and not strong):
            extra = ['basic_card_000']
            ops.append(dict(kind='PlayCard',cardId='basic_card_000',enhanced=number == 10))
            snapshots[0] = {'MovementPool':6 if number == 10 else 4, 'token:Green':0,
                            'token:White':0,'token:Blue':int(number == 10 and strong), 'cards:played':2}
        terrains = (['Lake','Swamp'] if strong else ['Swamp']) if number == 9 else (
            ['Plains','Desert','Wasteland'] + (['Lake'] if strong else []) if number == 10 else
            ['Forest','Swamp' if strong else 'Plains'])
        costs = ([1,1] if strong else [1]) if number == 9 else (
            [0,1,2,0] if strong else [1,2,3]) if number == 10 else [2,2]
        terrain_map = [dict(q=i+1,r=0,terrain=t) for i,t in enumerate(terrains)]
        expected = {}
        pool = 6 if number == 10 else 4 if number == 11 else initial_move
        if number == 11 and strong:
            terrain_map.append(dict(q=0,r=1,terrain='Mountain'))
            ops.append(dict(kind='Move',q=0,r=1))
            expected.update({'operation:0:success':False,'operation:0:cost':2147483647})
            snapshots[0] = {'MovementPool':4,'position:q':0,'position:r':0}
        for q,cost in enumerate(costs,1):
            i=len(ops); ops.append(dict(kind='Move',q=q,r=0)); pool-=cost
            expected.update({f'operation:{i}:success':True,f'operation:{i}:cost':cost})
            snapshots[i]={'MovementPool':pool,'position:q':q,'position:r':0,
                          'token:White':0,'token:Blue':int(number == 10 and strong and q < 4)}
        for i, snapshot in snapshots.items():
            expected.setdefault(f'operation:{i}:success',True)
            expected.update({f'operation:{i}:state:'+k:v for k,v in snapshot.items()})
        expected.update({'played:'+k:v for k,v in played.items()})
        expected.update({'MovementPool':pool,'position:q':len(terrains),'position:r':0,
                         'hand':0,'discard':0,'Wounds':0,'cards:played':1+len(extra),'cards:removed':0,
                         'basic:zone0':'played','basic:total':1+len(extra),'basic:unique':1+len(extra)})
        if extra: expected['basic:zone1']='played'
        title={9:'冰霜之桥按原文实际穿越沼泽与湖泊',10:'清风之歌白色强化与蓝色穿湖分开支付，逐格验证三地形减费',
               11:'迷途寻踪实际消费地形成本，普通减费不开放山脉'}[number]
        basic_acceptance_case(card_id,strong,title,expected,ops,setup=setup,
            extraHandCards=extra,unitIds=['army_2_006'],unitWounds=0,
            destinationTerrain='Plains',revealedDistance=len(terrains),mapTerrains=terrain_map,
            removeIndex=-1,discardIndex=-1,discardIndices=[],
            captureAfterOperations=[0] if number == 10 and strong else [])



# Basic recruitment rewards: printed price, real supply and post-recruit rewards.
for card_id,strong in [('basic_card_026',False),('advanced_card_014',False),('advanced_card_014',True)]:
    intimidate = card_id == 'basic_card_026'
    source_pool = 2 if intimidate else 6 if strong else 3
    extra = ['basic_card_008'] if not intimidate and not strong else []
    cost = 2 if intimidate else 4
    final_pool = 0 if intimidate else 2 if strong else 1
    ops = [dict(kind='PlayCard',cardId='basic_card_008',enhanced=False)] if extra else []
    ops += [dict(kind='BeginInteraction',recruitLocation='Village'),
            dict(kind='Recruit',targetIndex=0,recruitLocation='Village')]
    expected={'InfluencePool':final_pool,'Reputation':-1 if intimidate else 1,'Fame':int(strong),
              'Wounds':0,'hand':0,'cards:played':1+len(extra),'cards:removed':0,'discard':0,
              'basic:zone0':'played','basic:total':1+len(extra),'basic:unique':1+len(extra),
              'recruit:offer':'','recruit:owned':'army_2_006,army_2_006','recruit:ready':'False,True',
              'played:InfluencePool':source_pool,'played:Reputation':0,'played:Fame':0,
              'played:token:White':0,'played:hand':len(extra),'played:cards:played':1}
    if extra: expected['basic:zone1']='played'
    if intimidate:
        expected.update({'played:RecruitDiscount':2,'played:IntimidateUsed':True,
                         'RecruitDiscount':0,'IntimidateUsed':False})
    for i,op in enumerate(ops):
        if op['kind']=='Recruit':
            state={'success':True,'cost':cost,'InfluencePool':final_pool,'Reputation':-1 if intimidate else 1,
                   'Fame':int(strong),'units':2,'unitId':'army_2_006','owned':'army_2_006,army_2_006',
                   'offer':'','ready':'False,True'}
        else:
            state={'success':True,'InfluencePool':source_pool+2*len(extra),'Reputation':0,'Fame':0,
                   'token:White':0,'cards:played':1+len(extra)}
            if op['kind']=='BeginInteraction': state['InNegotiation']=True
        expected.update(_consumption_state(i,state))
    basic_acceptance_case(card_id,strong,
        '无情威压基础实际折价招募原农民，成功后声誉减1' if intimidate else '英雄事迹实际招募原农民，成功后按原文获得声誉与名望',
        expected,ops,setup={'InBattle':False,'player:TempCommandSlots':1,**({'token:White':1} if strong else {})},
        extraHandCards=extra,recruitOffer=['army_2_006'],unitIds=['army_2_006'],unitWounds=0,
        removeIndex=-1,discardIndex=-1,discardIndices=[])



# Real earlier block -> Counter Strike; source-owned Shield Bash -> armor -> attack.
for counter,strong,swift in [(True,False,False),(True,True,False),(False,False,True),(False,True,True),(False,True,False)]:
    source='basic_card_016' if counter else 'advanced_card_031'
    followup='advanced_card_028' if counter else 'basic_card_021'
    source_strong=True if counter else strong
    followup_strong=strong if counter else not(strong and swift)
    printed=5 if source_strong else 3
    effective=printed*(2 if swift else 1)
    needed=6 if swift else 4
    armor=4 if counter or not strong else 1 if swift else 3
    attack=(7 if strong else 4) if counter else 4 if followup_strong else 2
    fame=3 if swift else 2
    setup={'InBattle':True,'CombatPhase':'Block'}
    if source_strong: setup['token:Blue']=1
    if followup_strong: setup['token:Red']=1
    expected={'Wounds':0,'handWounds':0,'Fame':fame,'MeleePool':0,'BlockPool':0,
        'enemyArmor:0':armor,'hand':0,'cards:played':2,'cards:removed':0,'discard':0,
        'basic:zone0':'played','basic:zone1':'played','basic:total':2,'basic:unique':2,
        'SwiftBlockBonus':0,'BlockedEnemiesThisTurn':1,
        'played:BlockPool':printed,'played:MeleePool':0,'played:token:Blue':0,
        'played:token:Red':int(followup_strong),'played:hand':1,
        'played:basic:zone0':'played','played:basic:zone1':'hand',
        **_consumption_state(0,{'success':True,'printed':printed,'effective':effective,'required':needed,
            'kills':0,'BlockPool':0,'MeleePool':0,'Wounds':0,'Fame':0,'enemyArmor:0':armor,
            'SwiftBlockBonus':0,'BlockedEnemiesThisTurn':1}),
        **_consumption_state(1,{'success':True,'CombatPhase':'Melee','MeleePool':0,'Wounds':0}),
        **_consumption_state(2,{'success':True,'MeleePool':attack,'hand':0,'cards:played':2,
            'basic:zone1':'played','token:Red':0,'token:Blue':0,'enemyArmor:0':armor}),
        **_consumption_state(3,{'success':True,'printed':attack,'effective':attack,'required':armor,
            'kills':1,'MeleePool':0,'Fame':fame,'Wounds':0})}
    basic_acceptance_case(source,source_strong,
        '先实际格挡，再打出反戈一击'+('强化' if strong else '基础')+'取得已完成格挡奖励并击杀原敌人' if counter else
        '盾牌重击'+('强化' if strong else '基础')+('对迅捷' if swift else '对普通攻击')+'实际格挡并用原狂怒检验敌人护甲',
        expected,[dict(kind='Block',targetIndex=0),dict(kind='EnterMelee'),
            dict(kind='PlayCard',cardId=followup,enhanced=followup_strong,option=0),dict(kind='Attack',targetIndex=0)],
        setup=setup,extraHandCards=[followup],combatPhase='Block',
        enemies=[dict(id='monster_007' if swift else 'monster_013',originalDefinition=True)],
        unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[],
        captureAfterOperations=[0])


basic_acceptance_case('advanced_card_031',True,'盾牌重击成功格挡原冰抗敌人，蓝色附带减甲被冰抗阻止',
    {'Wounds':0,'handWounds':0,'Fame':0,'MeleePool':0,'BlockPool':0,'SwiftBlockBonus':0,
     'BlockedEnemiesThisTurn':1,'enemyArmor:0':6,'hand':0,'cards:played':3,'cards:removed':0,'discard':0,
     'basic:zone0':'played','basic:zone1':'played','basic:zone2':'played','basic:total':3,'basic:unique':3,
     'played:BlockPool':5,'played:token:Blue':1,'played:hand':2,'played:cards:played':1,
     **_consumption_state(0,{'success':True,'BlockPool':10,'token:Blue':0,'hand':1,'cards:played':2}),
     **_consumption_state(1,{'success':True,'BlockPool':12,'token:Blue':0,'hand':0,'cards:played':3}),
     **_consumption_state(2,{'success':True,'printed':12,'effective':6,'required':5,'kills':0,
         'BlockPool':0,'Wounds':0,'Fame':0,'enemyArmor:0':6,'SwiftBlockBonus':0})},
    [dict(kind='PlayCard',cardId='basic_card_016',enhanced=True),
     dict(kind='PlayCard',cardId='basic_card_016',enhanced=False,option=1),dict(kind='Block',targetIndex=0)],
    setup={'InBattle':True,'CombatPhase':'Block','token:Blue':2},combatPhase='Block',
    extraHandCards=['basic_card_016','basic_card_016'],enemies=[dict(id='monster_028',originalDefinition=True)],
    unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[])


"""Append after existing cases with basic_acceptance_case/_consumption_state in scope.

Expected appended IDs: advanced_card_006_02..05 (current list ends at _01).
Thin operations supplied by root: ConvertMoveToAttack / ConvertMoveToRanged,
option = number of attack points produced, calling the real ActionSystem.
EnterMelee, PlayCard, Move, Attack, and RangedAttack already exist.
No rules are implemented here. Same four flows as AgilityBasicStateTests.
"""


def _agility_zones(count):
    return {'hand': 0, 'cards:played': count, 'discard': 0, 'cards:removed': 0,
            'basic:total': count, 'basic:unique': count,
            **{f'basic:zone{i}': 'played' for i in range(count)}}


# Basic/enhanced movement is a real map option. The conversion permission is only in combat.
for strong in (False, True):
    movement = 4 if strong else 2
    ops = [dict(kind='ConvertMoveToAttack', option=1),
           dict(kind='ConvertMoveToRanged', option=1), dict(kind='Move', q=1, r=0)]
    expected = {
        **_agility_zones(1), 'Wounds': 0, 'handWounds': 0, 'Fame': 0,
        'MovementPool': 0, 'MeleePool': 0, 'RangedPool': 0,
        'position:q': 2 if strong else 1, 'position:r': 0,
        'played:MovementPool': movement, 'played:token:White': 0,
        'played:MeleePool': 0, 'played:RangedPool': 0,
        'played:cards:played': 1, 'played:basic:zone0': 'played',
        **_consumption_state(0, {'success': False, 'MovementPool': movement,
            'MeleePool': 0, 'RangedPool': 0, 'position:q': 0, 'position:r': 0}),
        **_consumption_state(1, {'success': False, 'MovementPool': movement,
            'MeleePool': 0, 'RangedPool': 0, 'position:q': 0, 'position:r': 0}),
        **_consumption_state(2, {'success': True, 'cost': 2,
            'MovementPool': movement - 2, 'position:q': 1, 'position:r': 0})}
    if strong:
        ops.append(dict(kind='Move', q=2, r=0))
        expected.update(_consumption_state(3, {'success': True, 'cost': 2,
            'MovementPool': 0, 'position:q': 2, 'position:r': 0}))
    basic_acceptance_case('advanced_card_006', strong,
        '灵动原移动力逐格消费，战斗外不能兑换攻击', expected, ops,
        setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains',
               **({'token:White': 1} if strong else {})},
        destinationTerrain='Plains', revealedDistance=2,
        mapTerrains=[dict(q=1, r=0, terrain='Plains'), dict(q=2, r=0, terrain='Plains')],
        unitIds=['army_2_006'], unitWounds=0,
        removeIndex=-1, discardIndex=-1, discardIndices=[])


basic_acceptance_case('advanced_card_006', False,
    '灵动基础移动2兑近战2，原狂怒补2后实际击败哥布林',
    {**_agility_zones(2), 'Wounds': 0, 'handWounds': 0, 'Fame': 2,
     'MovementPool': 0, 'MeleePool': 0, 'RangedPool': 0,
     'DefeatedEnemyIndices:count': 1, 'DefeatedEnemyIndices:0': True,
     'played:MovementPool': 2, 'played:MoveCostAttack': 1, 'played:MoveCostRanged': 0,
     'played:hand': 1, 'played:cards:played': 1, 'played:basic:zone0': 'played',
     'played:basic:zone1': 'hand',
     **_consumption_state(0, {'success': False, 'MovementPool': 2, 'RangedPool': 0, 'MeleePool': 0}),
     **_consumption_state(1, {'success': True, 'MovementPool': 0, 'MeleePool': 2, 'RangedPool': 0}),
     **_consumption_state(2, {'success': True, 'MeleePool': 4, 'hand': 0,
         'cards:played': 2, 'basic:zone1': 'played'}),
     **_consumption_state(3, {'success': True, 'printed': 4, 'effective': 4,
         'required': 4, 'kills': 1, 'MeleePool': 0, 'Fame': 2, 'Wounds': 0})},
    [dict(kind='ConvertMoveToRanged', option=1), dict(kind='ConvertMoveToAttack', option=2),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=False, option=0),
     dict(kind='Attack', targetIndex=0)],
    setup={'InBattle': True, 'CombatPhase': 'Melee'}, combatPhase='Melee',
    extraHandCards=['basic_card_021'], enemies=[dict(id='monster_013', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[1])


basic_acceptance_case('advanced_card_006', True,
    '灵动强化混用两种兑换，先远程击败狼骑兵再近战击败哥布林',
    {**_agility_zones(4), 'Wounds': 0, 'handWounds': 0, 'Fame': 5,
     'MovementPool': 0, 'MeleePool': 0, 'RangedPool': 0,
     'DefeatedEnemyIndices:count': 2, 'DefeatedEnemyIndices:0': True, 'DefeatedEnemyIndices:1': True,
     'played:MovementPool': 4, 'played:MoveCostAttack': 1, 'played:MoveCostRanged': 2,
     'played:token:White': 0, 'played:token:Green': 1,
     'played:hand': 3, 'played:cards:played': 1,
     'played:basic:zone0': 'played', 'played:basic:zone1': 'hand',
     'played:basic:zone2': 'hand', 'played:basic:zone3': 'hand',
     **_consumption_state(0, {'success': True, 'MovementPool': 8, 'token:Green': 0,
         'hand': 2, 'cards:played': 2, 'basic:zone1': 'played', 'basic:zone2': 'hand'}),
     **_consumption_state(1, {'success': True, 'MovementPool': 10,
         'hand': 1, 'cards:played': 3, 'basic:zone2': 'played'}),
     **_consumption_state(2, {'success': True, 'MovementPool': 2, 'RangedPool': 4, 'MeleePool': 0}),
     **_consumption_state(3, {'success': True, 'printed': 4, 'effective': 4,
         'required': 4, 'kills': 1, 'RangedPool': 0, 'MovementPool': 2, 'Fame': 3,
         'DefeatedEnemyIndices:count': 1, 'DefeatedEnemyIndices:0': True}),
     **_consumption_state(4, {'success': True, 'CombatPhase': 'Melee', 'MovementPool': 2, 'MeleePool': 0}),
     **_consumption_state(5, {'success': True, 'MovementPool': 0, 'MeleePool': 2, 'RangedPool': 0}),
     **_consumption_state(6, {'success': True, 'MeleePool': 4, 'hand': 0,
         'cards:played': 4, 'basic:zone3': 'played'}),
     **_consumption_state(7, {'success': True, 'printed': 4, 'effective': 4,
         'required': 4, 'kills': 1, 'MeleePool': 0, 'Fame': 5, 'Wounds': 0,
         'DefeatedEnemyIndices:count': 2, 'DefeatedEnemyIndices:1': True})},
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='PlayCard', cardId='basic_card_000', enhanced=False),
     dict(kind='ConvertMoveToRanged', option=4), dict(kind='RangedAttack', targetIndex=0),
     dict(kind='EnterMelee'), dict(kind='ConvertMoveToAttack', option=2),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=False, option=0),
     dict(kind='Attack', targetIndex=1)],
    setup={'InBattle': True, 'CombatPhase': 'Ranged', 'token:White': 1, 'token:Green': 1},
    combatPhase='Ranged', extraHandCards=['basic_card_000', 'basic_card_000', 'basic_card_021'],
    enemies=[dict(id='monster_007', originalDefinition=True), dict(id='monster_013', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[2])


# Diplomacy: one paid conversion and actual block for each printed choice.
for strong,choice,enemy,element,amount,left in [(False,0,'monster_013','Physical',4,0),(True,0,'monster_029','Ice',3,1),(True,1,'monster_012','Fire',4,0)]:
    ops=[] if strong else [dict(kind='PlayCard',cardId='basic_card_008')]
    n=len(ops)
    expected={**_agility_zones(1 if strong else 2),'InfluencePool':left,'BlockPool':0,'Wounds':0,'handWounds':0,'Fame':0,
        'InfluenceAsBlock':True,'InfluenceBlockElement':element,'BlockedEnemiesThisTurn':1,
        'played:InfluencePool':4 if strong else 2,'played:token:White':0,'played:BlockPool':0,
        'played:InfluenceBlockElement':element,'played:hand':0 if strong else 1,
        **_consumption_state(n,{'success':False,'cost':0,'InfluencePool':4,'BlockPool':0}),
        **_consumption_state(n+1,{'success':True,'cost':amount,'InfluencePool':left,'BlockPool':amount,'blockElement':element}),
        **_consumption_state(n+2,{'success':True,'printed':amount,'effective':amount,'required':amount,'kills':0,
            'InfluencePool':left,'BlockPool':0,'Wounds':0,'BlockedEnemiesThisTurn':1})}
    if not strong: expected.update(_consumption_state(0,{'success':True,'InfluencePool':4,'hand':0,'cards:played':2}))
    ops.extend([dict(kind='ConvertInfluenceToBlock',option=5),dict(kind='ConvertInfluenceToBlock',option=amount),dict(kind='Block',targetIndex=0)])
    basic_acceptance_case('advanced_card_018',strong,'外交'+('强化'+element if strong else '基础物理')+'：实际影响力支付与原敌人格挡',
        expected,ops,option=choice,setup={'InBattle':True,'CombatPhase':'Block',**({'token:White':1} if strong else {})},
        extraHandCards=[] if strong else ['basic_card_008'],combatPhase='Block',
        enemies=[dict(id=enemy,originalDefinition=True)],unitIds=['army_2_006'],unitWounds=0,
        removeIndex=-1,discardIndex=-1,discardIndices=[],captureAfterOperations=[n+1])


"""Append with basic_acceptance_case / _consumption_state in scope.

Three core flows mirror KeepMovingBasicStateTests. Root supplies only the thin
PlaySidewaysMove operation, selecting the actual hand instance by cardId and
calling ActionSystem.PlaySideways(..., SidewaysType.Move).
Expected appended IDs: basic_card_017_02..04 (verify against current generator).
"""


def _keepmoving_zones(count):
    return {'hand': 0, 'cards:played': count, 'discard': 0, 'cards:removed': 0,
            'basic:total': count, 'basic:unique': count,
            **{f'basic:zone{i}': 'played' for i in range(count)}}


_keepmoving_map = [dict(q=1, r=0, terrain='Forest'),
                   dict(q=2, r=0, terrain='Hills'),
                   dict(q=3, r=0, terrain='Forest')]

for strong in (False, True):
    own_move = 4 if strong else 2
    after_agility = 7 if strong else 5
    after_sideways = 9 if strong else 6
    ops = [dict(kind='PlayCard', cardId='basic_card_008', enhanced=False),
           dict(kind='PlayCard', cardId='advanced_card_006', enhanced=False),
           dict(kind='PlaySidewaysMove', cardId='basic_card_021'),
           dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0)]
    expected = {
        **_keepmoving_zones(4), 'MovementPool': 0, 'InfluencePool': 2,
        'MeleePool': 0, 'Wounds': 0, 'handWounds': 0,
        'MoveBonusNext': 0, 'MoveBonusAll': 1 if strong else 0,
        'position:q': 3 if strong else 2, 'position:r': 0,
        'played:MovementPool': own_move, 'played:token:Blue': 0,
        'played:MoveBonusNext': 0 if strong else 1,
        'played:MoveBonusAll': 1 if strong else 0,
        'played:cards:played': 1, 'played:hand': 3,
        'played:basic:zone0': 'played', 'played:basic:zone1': 'hand',
        'played:basic:zone2': 'hand', 'played:basic:zone3': 'hand',
        **_consumption_state(0, {'success': True, 'MovementPool': own_move,
            'InfluencePool': 2, 'MoveBonusNext': 0 if strong else 1,
            'MoveBonusAll': 1 if strong else 0, 'cards:played': 2,
            'hand': 2, 'basic:zone1': 'played', 'basic:zone2': 'hand'}),
        **_consumption_state(1, {'success': True, 'MovementPool': after_agility,
            'MoveBonusNext': 0, 'MoveBonusAll': 1 if strong else 0,
            'cards:played': 3, 'hand': 1, 'basic:zone2': 'played',
            'basic:zone3': 'hand'}),
        **_consumption_state(2, {'success': True, 'MovementPool': after_sideways,
            'MeleePool': 0, 'InfluencePool': 2, **_keepmoving_zones(4)}),
        **_consumption_state(3, {'success': True, 'cost': 3,
            'MovementPool': after_sideways - 3, 'position:q': 1, 'position:r': 0}),
        **_consumption_state(4, {'success': True, 'cost': 3,
            'MovementPool': after_sideways - 6, 'position:q': 2, 'position:r': 0})}
    if strong:
        ops.append(dict(kind='Move', q=3, r=0))
        expected.update(_consumption_state(5, {'success': True, 'cost': 3,
            'MovementPool': 0, 'position:q': 3, 'position:r': 0}))
    basic_acceptance_case('basic_card_017', strong,
        '永不止步' + ('强化对后续灵动和横置各加1，实际移动消费9' if strong
                    else '基础跳过承诺，只加成下一张灵动，横置不重复加成，实际消费6'),
        expected, ops,
        setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains',
               **({'token:Blue': 1} if strong else {})},
        extraHandCards=['basic_card_008', 'advanced_card_006', 'basic_card_021'],
        destinationTerrain='Plains', revealedDistance=3, mapTerrains=_keepmoving_map,
        unitIds=['army_2_006'], unitWounds=0,
        removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[2])


basic_acceptance_case('basic_card_017', False,
    '永不止步基础先加成实际横置狂怒，后续原行进不再加成，实际消费6',
    {**_keepmoving_zones(3), 'MovementPool': 0, 'MeleePool': 0,
     'Wounds': 0, 'handWounds': 0, 'MoveBonusNext': 0, 'MoveBonusAll': 0,
     'position:q': 2, 'position:r': 0,
     'played:MovementPool': 2, 'played:MoveBonusNext': 1,
     'played:cards:played': 1, 'played:hand': 2,
     'played:basic:zone0': 'played', 'played:basic:zone1': 'hand', 'played:basic:zone2': 'hand',
     **_consumption_state(0, {'success': True, 'MovementPool': 4, 'MeleePool': 0,
         'MoveBonusNext': 0, 'cards:played': 2, 'hand': 1, 'discard': 0,
         'basic:zone1': 'played', 'basic:zone2': 'hand', 'basic:total': 3, 'basic:unique': 3}),
     **_consumption_state(1, {'success': True, 'MovementPool': 6,
         'MoveBonusNext': 0, **_keepmoving_zones(3)}),
     **_consumption_state(2, {'success': True, 'cost': 3,
         'MovementPool': 3, 'position:q': 1, 'position:r': 0}),
     **_consumption_state(3, {'success': True, 'cost': 3,
         'MovementPool': 0, 'position:q': 2, 'position:r': 0})},
    [dict(kind='PlaySidewaysMove', cardId='basic_card_021'),
     dict(kind='PlayCard', cardId='basic_card_000', enhanced=False),
     dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0)],
    setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains'},
    extraHandCards=['basic_card_021', 'basic_card_000'], destinationTerrain='Plains',
    revealedDistance=3, mapTerrains=_keepmoving_map, unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], captureAfterOperations=[0])



# Battle Rage: previously received wounds and cumulative multi-enemy cap.
for strong,earlier in [(False,True),(True,True),(False,False),(True,False)]:
    main='basic_card_008' if earlier else 'basic_card_025'
    wound_count=(4 if strong else 2) if earlier else (6 if strong else 4)
    extra=(['basic_card_025'] if earlier else [])+([] if strong else ['basic_card_021'])
    original_count=1+len(extra)
    enemy='monster_014' if strong and earlier else 'monster_013'
    fame=4 if enemy=='monster_014' else 2
    armor=6 if enemy=='monster_014' else 4
    attack=8 if strong else 5
    ops=[]; expected={**_agility_zones(original_count),'hand':wound_count,'Wounds':wound_count,'handWounds':wound_count,
        'basic:total':original_count+wound_count,'basic:unique':original_count+wound_count,
        'Fame':fame,'MeleePool':0,'BlockPool':0,'InfluencePool':2 if earlier else 0,
        'BattleWoundsReceived':wound_count,'AttackBonusLimit':4 if strong else 1,
        'played:MeleePool':0 if earlier else 4 if strong else 2,
        'played:token:Red':1 if strong and earlier else 0,'played:hand':len(extra)}
    for i in range(1 if earlier else 3 if strong else 2):
        ops.append(dict(kind='Block',targetIndex=i))
        cumulative=(4 if strong else 2) if earlier else (i+1)*2
        expected.update(_consumption_state(i,{'success':True,'printed':0,'effective':0,'required':7 if enemy=='monster_014' else 4,
            'Wounds':cumulative,'BattleWoundsReceived':cumulative,
            'MeleePool':0 if earlier else (4 if strong else 2)+min(cumulative,4 if strong else 1)}))
    ops.append(dict(kind='EnterMelee'))
    if earlier:
        i=len(ops);ops.append(dict(kind='PlayCard',cardId='basic_card_025',enhanced=strong))
        expected.update(_consumption_state(i,{'success':True,'MeleePool':8 if strong else 3,'token:Red':0,'basic:zone1':'played'}))
        rage_capture=i
    else: rage_capture=len(ops)-2
    if not strong: ops.append(dict(kind='PlayCard',cardId='basic_card_021'))
    i=len(ops);ops.append(dict(kind='Attack',targetIndex=0))
    expected.update(_consumption_state(i,{'success':True,'printed':attack,'effective':attack,'required':armor,'kills':1,'Fame':fame,'MeleePool':0,'Wounds':wound_count}))
    basic_acceptance_case(main,False if earlier else strong,
        ('实际受伤后打出战斗之怒' if earlier else '战斗之怒分次承伤共享本场上限')+('强化' if strong else '基础'),
        expected,ops,setup={'InBattle':True,'CombatPhase':'Block',**({'token:Red':1} if strong else {})},
        combatPhase='Block',extraHandCards=extra,
        enemies=[dict(id=enemy,originalDefinition=True) for _ in range(1 if earlier else 3 if strong else 2)],
        unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[],captureAfterOperations=[rage_capture])



# Quick Reflexes: original reduction applied before Swift block need and actual damage.
for strong in (False, True):
    wounds = 2 if strong else 3
    basic_acceptance_case('basic_card_010', strong,
        '迅捷反应' + ('强化减攻2，迅捷所需格挡2，决心实际挡住' if strong else '基础减攻1，迅捷所需格挡4，决心不足实际承伤'),
        {**_agility_zones(2), 'hand': wounds, 'Wounds': wounds, 'handWounds': wounds,
         'basic:total': 2+wounds, 'basic:unique': 2+wounds,
         'BattleWoundsReceived': wounds, 'Fame': 0, 'MeleePool': 0, 'RangedPool': 0,
         'MovementPool': 0, 'BlockPool': 0, 'AttackReduction': 0,
         'AttackReductionTimes': 0, 'PendingAttackReductions:count': 0,
         'played:AttackReduction': 2 if strong else 1, 'played:AttackReductionTimes': 1,
         'played:token:Blue': 0, 'played:MovementPool': 0, 'played:RangedPool': 0,
         'played:cards:played': 1, 'played:hand': 1,
         'played:basic:zone0': 'played', 'played:basic:zone1': 'hand',
         'operation:0:before': 3, 'operation:0:after': 1 if strong else 2,
         **_consumption_state(0, {'success': True, 'AttackReduction': 0, 'AttackReductionTimes': 0,
             'PendingAttackReductions:count': 1, 'PendingAttackReductions:(1, 0)': 2 if strong else 1}),
         **_consumption_state(1, {'success': True, 'BlockPool': 2, **_agility_zones(2)}),
         **_consumption_state(2, {'success': True, 'printed': 2, 'effective': 2,
             'required': 2 if strong else 4, 'Wounds': 0 if strong else 1,
             'BattleWoundsReceived': 0 if strong else 1, 'BlockPool': 0,
             'PendingAttackReductions:count': 0}),
         **_consumption_state(3, {'success': True, 'printed': 0, 'effective': 0,
             'required': 4, 'Wounds': wounds, 'BattleWoundsReceived': wounds})},
        [dict(kind='ReduceEnemyAttack', targetIndex=1),
         dict(kind='PlayCard', cardId='basic_card_016', option=1),
         dict(kind='Block', targetIndex=1), dict(kind='Block', targetIndex=0)],
        setup={'InBattle': True, 'CombatPhase': 'Block', **({'token:Blue': 1} if strong else {})},
        option=2, combatPhase='Block', extraHandCards=['basic_card_016'],
        enemies=[dict(id='monster_013', originalDefinition=True), dict(id='monster_007', originalDefinition=True)],
        unitIds=['army_2_006'], unitWounds=0, removeIndex=-1, discardIndex=-1, discardIndices=[],
        captureAfterOperations=[0])



# Outmaneuver: original single/two attacks, complete defense before conditional attack.
for mode in ('base', 'strong_single', 'strong_two_enemies', 'strong_two_attacks'):
    strong=mode!='base'; two=mode.startswith('strong_two')
    wounded=mode=='strong_two_enemies'; multi=mode=='strong_two_attacks'
    old_wounds=0 if strong else 1; new_wounds=2 if wounded else 0
    reward=0 if wounded else 2 if strong else 1
    extra=(['basic_card_016'] if not strong else [])+['basic_card_021']+(['w'] if not strong else [])
    count=1+len(extra); played_count=2 if strong else 3
    enemy='monster_058' if multi else 'monster_013'
    target_count=2 if wounded else 1
    ops=[dict(kind='ReduceEnemyAttack',targetIndex=0,attackIndex=0)]
    if two: ops.append(dict(kind='ReduceEnemyAttack',targetIndex=1 if wounded else 0,attackIndex=1 if multi else 0))
    if not strong: ops.append(dict(kind='PlayCard',cardId='basic_card_016',option=1))
    defense=len(ops);ops.append(dict(kind='TurnDefense',targetIndex=0))
    fury=len(ops);ops.append(dict(kind='PlayCard',cardId='basic_card_021',enhanced=not strong or wounded))
    attack=len(ops);ops.append(dict(kind='TurnAttack',targetIndex=0));ops.append(dict(kind='TurnFinish'))
    total_wounds=old_wounds+new_wounds
    expected={
        'hand':total_wounds,'handWounds':total_wounds,'Wounds':total_wounds,
        'cards:played':played_count,'cards:removed':0,'discard':0,
        'basic:total':count+new_wounds,'basic:unique':count+new_wounds,
        **{f'basic:zone{i}':'played' for i in range(played_count)},
        'MeleePool':0,'BlockPool':0,'MovementPool':0,'RangedPool':0,'Fame':2,
        'BattleWoundsReceived':new_wounds,'AttackIfNoWound':0,'AttackReduction':0,
        'AttackReductionTimes':0,'PendingAttackReductions:count':0,'InBattle':False,'GamePhase':'End',
        'Enemies:count':target_count,'DefeatedEnemyIndices:count':1,
        'enemy:0:attackCount':2 if multi else 1,'enemy:0:attack:0':1 if multi else 4,
        'played:MeleePool':0,'played:AttackIfNoWound':2 if strong else 1,
        'played:AttackReduction':2 if two or not strong else 4,'played:AttackReductionTimes':2 if two else 1,
        'played:token:White':0,'played:cards:played':1,'played:hand':len(extra),
        'operation:0:before':1 if multi else 4,'operation:0:after':0 if multi or mode=='strong_single' else 2,
        **_consumption_state(0,{'success':True,'AttackReductionTimes':1 if two else 0,'PendingAttackReductions:count':1}),
        f'operation:{defense}:blockPrinted':0 if strong else 2,
        f'operation:{defense}:blockEffective':0 if strong else 2,
        f'operation:{defense}:blockRequired':2 if wounded or not strong else 0,
        f'operation:{defense}:wounds':new_wounds,
        **_consumption_state(defense,{'success':True,'MeleePool':reward,'AttackIfNoWound':0,
            'PendingAttackReductions:count':0,'Wounds':total_wounds,'BattleWoundsReceived':new_wounds,'GamePhase':'Melee'}),
        **_consumption_state(fury,{'success':True,'MeleePool':4 if strong else 5,'token:Red':0,'cards:played':played_count}),
        **_consumption_state(attack,{'success':True,'printed':4 if strong else 5,'effective':4 if strong else 5,
            'required':4,'kills':1,'Fame':2,'MeleePool':0,'Wounds':total_wounds}),
        **_consumption_state(attack+1,{'success':True,'InBattle':False,'AttackIfNoWound':0})}
    if not strong: expected['basic:zone3']='hand'
    if multi: expected['enemy:0:attack:1']=1
    if two:
        expected.update({'operation:1:before':1 if multi else 4,'operation:1:after':0 if multi else 2,
            **_consumption_state(1,{'success':True,'PendingAttackReductions:count':2,'AttackReductionTimes':0})})
    basic_acceptance_case('advanced_card_036',strong,
        {'base':'迂回躲避基础实际减攻格挡，旧伤不阻止近战加1',
         'strong_single':'迂回躲避强化一次减4，无新伤获得攻击2并实际击杀',
         'strong_two_enemies':'迂回躲避强化两敌各减2，实际收到两伤不获奖励',
         'strong_two_attacks':'迂回躲避强化同一原敌人两次各减2，独立攻击且名望仅一次'}[mode],
        expected,ops,setup={'CombatPhase':'Block',**({'token:White':1} if strong else {}),
            **({'token:Red':1} if not strong or wounded else {})},
        option=1 if two else 0,combatPhase='Block',turnMachineStartPhase='Block',extraHandCards=extra,
        enemies=[dict(id=enemy,originalDefinition=True) for _ in range(target_count)],
        unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[],
        captureAfterOperations=[defense])


"""Four original-card scenes; append where basic_acceptance_case/_consumption_state exist.

No execution or generator modification is performed by this snippet's author.
013 enhanced needs TurnBegin to create the formal TurnMachine when none exists,
after real movement (do not initialize combat with turnMachineStartPhase).
"""


def _drawpull_dice(source_count, held, ordinary, zones):
    return {'mana:sourceCount': source_count, 'mana:turnHeld': held,
            'mana:ordinaryHeld': ordinary, 'mana:identity': True,
            **{f'mana:die{i}:zone': zone for i, zone in enumerate(zones)}}


def _drawpull_end(wounds=0):
    return {**_drawpull_dice(3, 0, False, ['source'] * 3),
            'ExtraManaDice': 0, 'ExtraBlackManaDice': 0,
            'MovementPool': 0, 'MeleePool': 0, 'Wounds': wounds,
            'hand': wounds, 'handWounds': wounds, 'cards:played': 0,
            'discard': 3, 'cards:removed': 0,
            'basic:total': 3 + wounds, 'basic:unique': 3 + wounds,
            'basic:zone0': 'discard', 'basic:zone1': 'discard', 'basic:zone2': 'discard'}


_drawpull_map = [dict(q=q, r=0, terrain='Plains') for q in range(1, 5)]

for card_id in ['basic_card_009', 'basic_card_013']:
    pull = card_id == 'basic_card_013'
    expected = {**_drawpull_end(), 'position:q': 4, 'position:r': 0,
        'played:ExtraManaDice': 1, 'played:ExtraBlackManaDice': 1 if pull else 0,
        'played:mana:sourceCount': 3, 'played:mana:turnHeld': 0,
        'played:mana:ordinaryHeld': False, 'played:mana:identity': True,
        'played:cards:played': 1, 'played:hand': 2,
        'played:token:Green': 0, 'played:MovementPool': 0,
        'played:basic:zone0': 'played', 'played:basic:zone1': 'hand', 'played:basic:zone2': 'hand'}
    ops = []
    if pull:
        ops.append(dict(kind='UseSourceDie', targetIndex=1, option=2))
        expected.update(_consumption_state(0, {'success': False,
            **_drawpull_dice(3, 0, False, ['source'] * 3),
            'ExtraManaDice': 1, 'ExtraBlackManaDice': 1, 'token:Green': 0,
            'mana:die1:face': 'Black', 'cards:played': 1, 'hand': 2}))
    normal = len(ops)
    ops.append(dict(kind='UseSourceDie', targetIndex=0, option=2))
    ops.append(dict(kind='PlayCard', cardId='basic_card_000', enhanced=True))
    ops.append(dict(kind='UseExtraSourceDie', targetIndex=1, option=2))
    ops.append(dict(kind='PlayCard', cardId='basic_card_000', enhanced=True))
    expected.update(_consumption_state(normal, {'success': True,
        **_drawpull_dice(2, 1, True, ['turn', 'source', 'source']),
        'token:Green': 1, 'ExtraManaDice': 1}))
    expected.update(_consumption_state(normal + 1, {'success': True, 'token:Green': 0,
        'MovementPool': 4, 'cards:played': 2, 'hand': 1,
        'basic:zone1': 'played', 'basic:zone2': 'hand'}))
    expected.update(_consumption_state(normal + 2, {'success': True,
        **_drawpull_dice(1, 2, True, ['turn', 'turn', 'source']),
        'token:Green': 1, 'ExtraManaDice': 0, 'ExtraBlackManaDice': 0,
        'mana:die1:face': 'Black' if pull else 'Green'}))
    expected.update(_consumption_state(normal + 3, {'success': True, 'token:Green': 0,
        'MovementPool': 8, 'cards:played': 3, 'hand': 0,
        'basic:zone0': 'played', 'basic:zone1': 'played', 'basic:zone2': 'played',
        'basic:total': 3, 'basic:unique': 3}))
    for q in range(1, 5):
        step = len(ops)
        ops.append(dict(kind='Move', q=q, r=0))
        expected.update(_consumption_state(step, {'success': True, 'cost': 2,
            'MovementPool': 8 - q * 2, 'position:q': q, 'position:r': 0}))
    capture = len(ops) - 1
    end = len(ops)
    ops.append(dict(kind='EndTurn'))
    expected.update(_consumption_state(end, {'success': True, **_drawpull_end()}))
    basic_acceptance_case(card_id, False,
        '魔力征引仅额外黑骰产绿，两个原行进实际消费8移动并归还原骰' if pull
        else '魔力汲取额外原骰支付第二张行进，实际消费8移动并归还原骰',
        expected, ops, setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains'},
        manaSourceFaces=['Green', 'Black' if pull else 'Green', 'White'],
        extraHandCards=['basic_card_000', 'basic_card_000'],
        destinationTerrain='Plains', revealedDistance=4, mapTerrains=_drawpull_map,
        unitIds=['army_2_006'], unitWounds=0, removeIndex=-1, discardIndex=-1, discardIndices=[],
        captureAfterOperations=[capture])


ops = [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
       dict(kind='PlayCard', cardId='basic_card_000', enhanced=True)]
expected = {**_drawpull_end(), 'position:q': 4, 'position:r': 0,
    'mana:die1:face': 'Green', 'mana:die2:face': 'White',
    'played:token:Blue': 0, 'played:token:Green': 2, 'played:MovementPool': 0,
    'played:mana:die1:face': 'Green', 'played:mana:die1:zone': 'turn',
    'played:mana:die0:zone': 'turn', 'played:mana:die2:zone': 'source',
    'played:mana:sourceCount': 1, 'played:mana:turnHeld': 2,
    'played:mana:ordinaryHeld': True, 'played:mana:identity': True,
    'played:cards:played': 1, 'played:hand': 2, 'played:basic:zone0': 'played',
    **_consumption_state(0, {'success': True, 'token:Green': 1, 'MovementPool': 4,
        'cards:played': 2, 'hand': 1, 'basic:zone1': 'played', 'basic:zone2': 'hand'}),
    **_consumption_state(1, {'success': True, 'token:Green': 0, 'MovementPool': 8,
        'cards:played': 3, 'hand': 0, 'basic:zone2': 'played'})}
for q in range(1, 5):
    step = len(ops)
    ops.append(dict(kind='Move', q=q, r=0))
    expected.update(_consumption_state(step, {'success': True, 'cost': 2,
        'MovementPool': 8 - q * 2, 'position:q': q, 'position:r': 0}))
ops.append(dict(kind='EndTurn'))
expected.update(_consumption_state(6, {'success': True, **_drawpull_end(),
    'mana:die1:face': 'Green', 'mana:die2:face': 'White'}))
basic_acceptance_case('basic_card_009', True,
    '魔力汲取一颗原骰转绿产2，支付两张行进并以绿面归还', expected, ops,
    setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains'}, option=2,
    manaSourceFaces=['Blue', 'Red', 'White'], manaPaymentDieIndex=0, manaTransformIndices=[1],
    extraHandCards=['basic_card_000', 'basic_card_000'],
    destinationTerrain='Plains', revealedDistance=4, mapTerrains=_drawpull_map,
    unitIds=['army_2_006'], unitWounds=0, removeIndex=-1, discardIndex=-1, discardIndices=[],
    captureAfterOperations=[5])


basic_acceptance_case('basic_card_013', True,
    '魔力征引两颗原骰分别产绿红，行进与狂怒真实消费后保留转色归还',
    {**_drawpull_end(2), 'Fame': 2, 'position:q': 2, 'position:r': 0, 'InBattle': False,
     'mana:die1:face': 'Green', 'mana:die2:face': 'Red',
     'played:token:White': 0, 'played:token:Green': 1, 'played:token:Red': 1,
     'played:mana:sourceCount': 0, 'played:mana:turnHeld': 3,
     'played:mana:ordinaryHeld': True, 'played:mana:identity': True,
     'played:mana:die0:zone': 'turn', 'played:mana:die1:zone': 'turn', 'played:mana:die2:zone': 'turn',
     'played:mana:die1:face': 'Green', 'played:mana:die2:face': 'Red',
     'played:cards:played': 1, 'played:hand': 2, 'played:basic:zone0': 'played',
     **_consumption_state(0, {'success': True, 'token:Green': 0, 'token:Red': 1,
         'MovementPool': 4, 'cards:played': 2, 'hand': 1, 'basic:zone1': 'played'}),
     **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
     **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
     **_consumption_state(3, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
     **_consumption_state(4, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
     'operation:5:wounds': 2,
     **_consumption_state(5, {'success': True, 'Wounds': 2, 'handWounds': 2,
         'BattleWoundsReceived': 2, 'CombatPhase': 'Melee', 'hand': 3, 'MeleePool': 0}),
     **_consumption_state(6, {'success': True, 'token:Red': 0, 'MeleePool': 4,
         'cards:played': 3, 'hand': 2, 'basic:zone2': 'played', 'basic:total': 5, 'basic:unique': 5}),
     **_consumption_state(7, {'success': True, 'printed': 4, 'effective': 4, 'required': 4,
         'kills': 1, 'Fame': 2, 'MeleePool': 0, 'Wounds': 2}),
     **_consumption_state(8, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
     **_consumption_state(9, {'success': True, **_drawpull_end(2),
         'mana:die1:face': 'Green', 'mana:die2:face': 'Red'})},
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0),
     dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1), dict(kind='TurnDefense', targetIndex=0),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'InBattle': False, 'DayPart': 'Day', 'CurrentTerrain': 'Plains'}, option=2,
    manaSourceFaces=['White', 'Red', 'Blue'], manaPaymentDieIndex=0, manaTransformIndices=[1, 2],
    extraHandCards=['basic_card_000', 'basic_card_021'], enemies=[dict(id='monster_013', originalDefinition=True)],
    destinationTerrain='Plains', revealedDistance=4, mapTerrains=_drawpull_map,
    unitIds=['army_2_006'], unitWounds=0, removeIndex=-1, discardIndex=-1, discardIndices=[],
    captureAfterOperations=[8])


# Original Wild Harvest: an optional material belongs to one successful move.
def _harvest_cards(count, played, discarded, hand, zones, wounds=0):
    return {'basic:total':count+wounds,'basic:unique':count+wounds,
        'cards:played':played,'cards:removed':0,'discard':discarded,
        'hand':hand,'Wounds':wounds,'handWounds':wounds,
        **{f'basic:zone{i}':zone for i,zone in enumerate(zones)}}

_harvest_map=[dict(q=q,r=0,terrain='Plains') for q in range(1,4)]
_harvest_base_end=_harvest_cards(4,0,3,1,['discard','discard','discard','hand'])
basic_acceptance_case('basic_card_003',False,
    '野蛮收获一次弃宁静得绿晶支付行进，第二次拒绝后正常移动',
    {**_harvest_base_end,'MovementPool':0,'HarvestTriggers':0,'position:q':3,'position:r':0,
     'played:MovementPool':2,'played:HarvestTriggers':1,'played:crystal:Green':0,
     'played:cards:played':1,'played:discard':0,'played:hand':3,
     **_consumption_state(0,{'success':True,'cost':2,'MovementPool':0,'position:q':1,
        'HarvestTriggers':0,'crystal:Green':1,**_harvest_cards(4,1,1,2,['played','discard','hand','hand'])}),
     'operation:0:spent':2,
     **_consumption_state(1,{'success':True,'MovementPool':4,'crystal:Green':0,'token:Green':0,
        **_harvest_cards(4,2,1,1,['played','discard','played','hand'])}),
     **_consumption_state(2,{'success':False,'cost':2,'MovementPool':4,'position:q':1,'crystal:Red':0,
        'HarvestTriggers':0,**_harvest_cards(4,2,1,1,['played','discard','played','hand'])}),
     'operation:2:spent':0,
     **_consumption_state(3,{'success':True,'cost':2,'MovementPool':2,'position:q':2,'crystal:Red':0}),
     **_consumption_state(4,{'success':True,'cost':2,'MovementPool':0,'position:q':3}),
     **_consumption_state(5,{'success':True,'HarvestTriggers':0,**_harvest_base_end})},
    [dict(kind='MoveHarvest',q=1,r=0,targetIndex=1,cardId='basic_card_001',option=2),
     dict(kind='PlayCard',cardId='basic_card_000',enhanced=True),
     dict(kind='MoveHarvest',q=2,r=0,targetIndex=3,cardId='basic_card_021',option=0),
     dict(kind='Move',q=2,r=0),dict(kind='Move',q=3,r=0),dict(kind='EndTurn')],
    setup={'InBattle':False,'DayPart':'Day','CurrentTerrain':'Plains'},
    extraHandCards=['basic_card_001','basic_card_000','basic_card_021'],
    destinationTerrain='Plains',revealedDistance=3,mapTerrains=_harvest_map,
    unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[],
    captureAfterOperations=[0,2])

_harvest_strong_end=_harvest_cards(5,0,5,2,['discard']*5,2)
basic_acceptance_case('basic_card_003',True,
    '野蛮收获每格弃原卡，神器选红晶支付狂怒并实际击杀',
    {**_harvest_strong_end,'MovementPool':0,'MeleePool':0,'HarvestTriggers':0,
     'position:q':2,'position:r':0,'InBattle':False,'Fame':2,
     'played:token:Green':0,'played:MovementPool':4,'played:HarvestTriggers':-1,
     'played:crystal:Green':0,'played:crystal:Red':0,'played:cards:played':1,'played:discard':0,'played:hand':4,
     **_consumption_state(0,{'success':True,'cost':2,'MovementPool':2,'position:q':1,
        'HarvestTriggers':-1,'crystal:Green':1,**_harvest_cards(5,1,1,3,['played','discard','hand','hand','hand'])}),
     'operation:0:spent':2,
     **_consumption_state(1,{'success':True,'MovementPool':6,'crystal:Green':0,'token:Green':0,
        **_harvest_cards(5,2,1,2,['played','discard','hand','played','hand'])}),
     **_consumption_state(2,{'success':True,'cost':2,'MovementPool':4,'position:q':2,'crystal:Red':1,
        'HarvestTriggers':-1,**_harvest_cards(5,2,2,1,['played','discard','discard','played','hand'])}),
     'operation:2:spent':2,
     **_consumption_state(3,{'success':True,'InBattle':True,'CombatPhase':'Ranged'}),
     **_consumption_state(4,{'success':True,'CombatPhase':'Block','kills':0}),
     'operation:5:wounds':2,
     **_consumption_state(5,{'success':True,'Wounds':2,'handWounds':2,'BattleWoundsReceived':2,
        'hand':3,'CombatPhase':'Melee','MeleePool':0,'crystal:Red':1}),
     **_consumption_state(6,{'success':True,'crystal:Red':0,'token:Red':0,'MeleePool':4,
        **_harvest_cards(5,3,2,2,['played','discard','discard','played','played'],2)}),
     **_consumption_state(7,{'success':True,'printed':4,'effective':4,'required':4,'kills':1,'Fame':2,'MeleePool':0}),
     **_consumption_state(8,{'success':True,'InBattle':False}),
     **_consumption_state(9,{'success':True,'HarvestTriggers':0,'MovementPool':0,**_harvest_strong_end})},
    [dict(kind='MoveHarvest',q=1,r=0,targetIndex=1,cardId='basic_card_001',option=2),
     dict(kind='PlayCard',cardId='basic_card_000',enhanced=True),
     dict(kind='MoveHarvest',q=2,r=0,targetIndex=2,cardId='items_008',option=0),
     dict(kind='TurnBegin'),dict(kind='TurnRanged',option=1),dict(kind='TurnDefense',targetIndex=0),
     dict(kind='PlayCard',cardId='basic_card_021',enhanced=True),
     dict(kind='TurnAttack',targetIndex=0),dict(kind='TurnFinish'),dict(kind='EndTurn')],
    setup={'InBattle':False,'DayPart':'Day','CurrentTerrain':'Plains','token:Green':1},
    extraHandCards=['basic_card_001','items_008','basic_card_000','basic_card_021'],
    enemies=[dict(id='monster_013',originalDefinition=True)],
    destinationTerrain='Plains',revealedDistance=3,mapTerrains=_harvest_map,
    unitIds=['army_2_006'],unitWounds=0,removeIndex=-1,discardIndex=-1,discardIndices=[],
    captureAfterOperations=[0,2,7])



# Blood Ritual / Pure Magic basic acceptance, September 18.

"""Append where basic_acceptance_case, _consumption_state and _harvest_cards exist.

Four normal original Blood Ritual paths. Expectations are independent printed
rules: wound1, base red crystal1+any token1; strong printed red1, three independent
tokens, optional preselected conversion. Every received resource used below is
consumed through a real original card or existing ConvertGold/Move command.

Required DTO: bloodRitualColors:string[], bloodRitualCrystalColor:string/null.
Required thin observation: TurnRanged records actual printed/effective/required
before applying its actual allocation; existing kills is observed after removal.
A/C expect 8/8/4/1; D expects 5/5/4/1. No second RangedAttack call, no test-side
enemy removal. TurnDefense after a ranged kill has no surviving enemy and 0 wounds.
The ordinary B defense legitimately causes 2 new wounds before melee.

No new conversion operation or persistent permission. C/D crystal selection is
set before playing the ritual, so the played:* checkpoint proves actual payment.
Each scene keeps at most two operation captures plus the normal final capture.
"""

_blood_scene_fields = dict(
    enemies=[dict(id='monster_013', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[],
    destinationTerrain='Plains', revealedDistance=4,
    mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(5)],
)

# A. Night / base: newly created black + red crystal pay the whole Firestorm.
basic_acceptance_case(
    'advanced_card_012', False, '基础黑魔力与红晶支付原火焰风暴，攻城火8击杀原敌人',
    {
        **_harvest_cards(2, 0, 2, 2, ['discard', 'discard'], 2),
        'draw': 0, 'Fame': 2, 'SiegePool': 0, 'RangedPool': 0,
        'InBattle': False, 'CombatPhase': 'End',
        'played:token:Black': 1, 'played:crystal:Red': 1,
        'played:Wounds': 1, 'played:handWounds': 1,
        'played:basic:zone0': 'played', 'played:basic:zone1': 'hand',
        **_consumption_state(0, {'success': True, 'token:Black': 0, 'crystal:Red': 0,
            'token:Red': 0, 'SiegePool': 8, 'RangedPool': 0, 'BattleWoundsReceived': 2,
            **_harvest_cards(2, 2, 0, 2, ['played', 'played'], 2)}),
        **_consumption_state(1, {'success': True, 'printed': 8, 'effective': 8,
            'required': 4, 'kills': 1, 'Fame': 2, 'SiegePool': 0,
            'CombatPhase': 'Block', 'Wounds': 2}),
        'operation:2:wounds': 0,
        **_consumption_state(2, {'success': True, 'Wounds': 2, 'CombatPhase': 'Melee'}),
        **_consumption_state(3, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
        **_consumption_state(4, {'success': True,
            **_harvest_cards(2, 0, 2, 2, ['discard', 'discard'], 2)}),
    },
    [dict(kind='PlayCard', cardId='magic_009', enhanced=True),
     dict(kind='TurnRanged', targetIndex=0), dict(kind='TurnDefense', targetIndex=0),
     dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Night', 'InBattle': True}, option=5,
    turnMachineStartPhase='Ranged', extraHandCards=['magic_009'],
    captureAfterOperations=[0, 1], **_blood_scene_fields,
)

# B. Day / strong: independently green+gold+red, explicitly no conversion.
# Green and converted gold each pay March4; four plains moves cost2 each.
# Only then begin combat, take 2 enemy wounds, and spend the generated red on Fury4.
_blood_b_after_play = _harvest_cards(4, 1, 0, 4, ['played', 'hand', 'hand', 'hand'], 1)
_blood_b_final = _harvest_cards(4, 0, 4, 3, ['discard'] * 4, 3)
_blood_b_expected = {
    **_blood_b_final, 'draw': 0, 'Fame': 2, 'MovementPool': 0, 'MeleePool': 0,
    'position:q': 4, 'position:r': 0, 'InBattle': False, 'CombatPhase': 'End',
    **{'played:' + key: value for key, value in _blood_b_after_play.items()},
    'played:token:Green': 1, 'played:token:Gold': 1, 'played:token:Red': 1,
    'played:crystal:Red': 0, 'played:crystal:Green': 0,
    **_consumption_state(0, {'success': True, 'token:Green': 0, 'MovementPool': 4,
        **_harvest_cards(4, 2, 0, 3, ['played', 'played', 'hand', 'hand'], 1)}),
    **_consumption_state(1, {'success': True, 'token:Gold': 0, 'token:Green': 1,
        'token:Red': 1, 'MovementPool': 4}),
    **_consumption_state(2, {'success': True, 'token:Green': 0, 'token:Red': 1,
        'MovementPool': 8,
        **_harvest_cards(4, 3, 0, 2, ['played', 'played', 'played', 'hand'], 1)}),
    **_consumption_state(7, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
    **_consumption_state(8, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
    'operation:9:wounds': 2,
    **_consumption_state(9, {'success': True, 'Wounds': 3, 'handWounds': 3,
        'BattleWoundsReceived': 2, 'hand': 4, 'CombatPhase': 'Melee'}),
    **_consumption_state(10, {'success': True, 'token:Red': 0, 'MeleePool': 4,
        **_harvest_cards(4, 4, 0, 3, ['played'] * 4, 3)}),
    **_consumption_state(11, {'success': True, 'printed': 4, 'effective': 4,
        'required': 4, 'kills': 1, 'Fame': 2, 'MeleePool': 0, 'Wounds': 3}),
    **_consumption_state(12, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
    **_consumption_state(13, {'success': True, **_blood_b_final}),
}
for q in range(1, 5):
    _blood_b_expected.update(_consumption_state(q + 2, {
        'success': True, 'cost': 2, 'MovementPool': 8 - 2 * q, 'position:q': q,
    }))
basic_acceptance_case(
    'advanced_card_012', True, '绿金红三色不转晶，两行进走四格后红魔力狂怒击杀',
    _blood_b_expected,
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='ConvertGold', option=2),
     dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     *[dict(kind='Move', q=q, r=0) for q in range(1, 5)],
     dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1),
     dict(kind='TurnDefense', targetIndex=0),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'token:Red': 1, 'CurrentTerrain': 'Plains'},
    bloodRitualColors=['Green', 'Gold', 'Red'], bloodRitualCrystalColor=None,
    extraHandCards=['basic_card_000', 'basic_card_000', 'basic_card_021'],
    captureAfterOperations=[2, 11], **_blood_scene_fields,
)

# C. Night / strong: the third new token (red) is converted, not the first green.
_blood_c_final = _harvest_cards(3, 0, 3, 2, ['discard'] * 3, 2)
basic_acceptance_case(
    'advanced_card_012', True, '第三枚红魔力转晶，绿行进两格后红晶黑魔力支付火焰风暴',
    {
        **_blood_c_final, 'draw': 0, 'Fame': 2, 'MovementPool': 0, 'SiegePool': 0,
        'position:q': 2, 'position:r': 0, 'InBattle': False, 'CombatPhase': 'End',
        'played:token:Red': 0, 'played:crystal:Red': 1,
        'played:token:Green': 1, 'played:token:Black': 1,
        'played:Wounds': 1, 'played:handWounds': 1,
        **_consumption_state(0, {'success': True, 'token:Green': 0, 'MovementPool': 4,
            'crystal:Red': 1,
            **_harvest_cards(3, 2, 0, 2, ['played', 'played', 'hand'], 1)}),
        **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
        **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
        **_consumption_state(3, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
        **_consumption_state(4, {'success': True, 'crystal:Red': 0, 'token:Black': 0,
            'SiegePool': 8, 'BattleWoundsReceived': 1,
            **_harvest_cards(3, 3, 0, 2, ['played'] * 3, 2)}),
        **_consumption_state(5, {'success': True, 'printed': 8, 'effective': 8,
            'required': 4, 'kills': 1, 'Fame': 2, 'SiegePool': 0, 'CombatPhase': 'Block'}),
        'operation:6:wounds': 0,
        **_consumption_state(6, {'success': True, 'Wounds': 2, 'CombatPhase': 'Melee'}),
        **_consumption_state(7, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
        **_consumption_state(8, {'success': True, **_blood_c_final}),
    },
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0), dict(kind='TurnBegin'),
     dict(kind='PlayCard', cardId='magic_009', enhanced=True),
     dict(kind='TurnRanged', targetIndex=0), dict(kind='TurnDefense', targetIndex=0),
     dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Night', 'InBattle': False, 'token:Red': 1, 'CurrentTerrain': 'Plains'},
    bloodRitualColors=['Green', 'Black', 'Red'], bloodRitualCrystalColor='Red',
    extraHandCards=['basic_card_000', 'magic_009'], captureAfterOperations=[0, 5],
    **_blood_scene_fields,
)

# D. Preexisting blue is an additional paid conversion cost; it is not among the
# three new token colors. The resulting blue crystal pays original Snowstorm.
_blood_d_final = _harvest_cards(2, 0, 2, 1, ['discard', 'discard'], 1)
basic_acceptance_case(
    'advanced_card_012', True, '原有蓝魔力与红印刷费整笔支付，所得蓝晶施放风雪交加击杀',
    {
        **_blood_d_final, 'draw': 0, 'Fame': 2, 'RangedPool': 0, 'SiegePool': 0,
        'InBattle': False, 'CombatPhase': 'End',
        'played:token:Blue': 0, 'played:crystal:Blue': 1,
        'played:token:Green': 1, 'played:token:Black': 1, 'played:token:Red': 1,
        'played:Wounds': 1, 'played:handWounds': 1,
        **_consumption_state(0, {'success': True, 'crystal:Blue': 0, 'token:Blue': 0,
            'RangedPool': 5, 'SiegePool': 0, 'BattleWoundsReceived': 1,
            'token:Red': 1, 'token:Green': 1, 'token:Black': 1,
            **_harvest_cards(2, 2, 0, 1, ['played', 'played'], 1)}),
        **_consumption_state(1, {'success': True, 'printed': 5, 'effective': 5,
            'required': 4, 'kills': 1, 'Fame': 2, 'RangedPool': 0, 'CombatPhase': 'Block'}),
        'operation:2:wounds': 0,
        **_consumption_state(2, {'success': True, 'Wounds': 1, 'CombatPhase': 'Melee'}),
        **_consumption_state(3, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
        **_consumption_state(4, {'success': True, 'token:Red': 0, 'token:Green': 0,
            'token:Black': 0, **_blood_d_final}),
    },
    [dict(kind='PlayCard', cardId='magic_015'), dict(kind='TurnRanged', targetIndex=0),
     dict(kind='TurnDefense', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': True, 'token:Red': 1, 'token:Blue': 1},
    turnMachineStartPhase='Ranged', bloodRitualColors=['Green', 'Black', 'Red'],
    bloodRitualCrystalColor='Blue', extraHandCards=['magic_015'],
    captureAfterOperations=[0, 1], **_blood_scene_fields,
)


"""Append where basic_acceptance_case/_consumption_state are available; do not execute separately.

Eight cases: Green base/strong, White base/strong, Blue base/strong, Red base/strong.
If the old list still ends at advanced_card_013_01, appended suffixes are _02..09.
Controller needs advanced_card_013 enabled for TurnDefense blockPrinted/Effective/Required.
Red's Melee initialization really resolves incoming damage before the source card:
monster_013 => two hand wounds; monster_027 => three. Those are intentionally asserted.
"""


def _pure_magic_zones(wounds=0, ended=False):
    return {'hand': wounds, 'handWounds': wounds, 'Wounds': wounds,
            'cards:played': 0 if ended else 1, 'discard': 1 if ended else 0,
            'cards:removed': 0, 'basic:zone0': 'discard' if ended else 'played',
            'basic:total': 1 + wounds, 'basic:unique': 1 + wounds}


for color, option, pool in [('Green', 2, 'MovementPool'), ('White', 3, 'InfluencePool'),
                            ('Blue', 1, 'BlockPool'), ('Red', 0, 'MeleePool')]:
    for strong in (False, True):
        value = 7 if strong else 4
        wounds = (3 if strong else 2) if color == 'Red' else 0
        setup = {'DayPart': 'Day', 'InBattle': color in ('Blue', 'Red'), f'token:{color}': 1}
        if strong:
            setup['token:Blue'] = setup.get('token:Blue', 0) + 1
        expected = {**_pure_magic_zones(wounds, ended=True),
                    'MovementPool': 0, 'InfluencePool': 0, 'BlockPool': 0, 'MeleePool': 0,
                    'played:token:Red': 0, 'played:token:Blue': 0,
                    'played:token:Green': 0, 'played:token:White': 0,
                    **{f'played:{k}': v for k, v in _pure_magic_zones(wounds).items()},
                    **{f'played:{k}': value if k == pool else 0
                       for k in ['MovementPool', 'InfluencePool', 'BlockPool', 'MeleePool']}}
        fields = dict(unitIds=['army_2_006'], unitWounds=0,
                      removeIndex=-1, discardIndex=-1, discardIndices=[])
        ops = []

        if color == 'Green':
            terrains = ['Forest', 'Plains', 'Plains'] if strong else ['Plains', 'Plains']
            fields.update(destinationTerrain='Plains', revealedDistance=len(terrains),
                          mapTerrains=[dict(q=i + 1, r=0, terrain=t) for i, t in enumerate(terrains)])
            setup['CurrentTerrain'] = 'Plains'
            remaining = value
            for i, terrain in enumerate(terrains):
                cost = 3 if terrain == 'Forest' else 2
                remaining -= cost
                ops.append(dict(kind='Move', q=i + 1, r=0))
                expected.update(_consumption_state(i, {'success': True, 'cost': cost,
                    'MovementPool': remaining, 'position:q': i + 1, 'position:r': 0,
                    'basic:zone0': 'played', 'cards:played': 1}))
            expected.update({'position:q': len(terrains), 'position:r': 0, 'Fame': 0})
            title = f'纯净魔法绿选项支付完整费用，移动{value}逐格实际消费'
            capture = len(ops) - 1

        elif color == 'White':
            setup['player:TempCommandSlots'] = 1
            fields['recruitOffer'] = ['army_2_006']
            ops = [dict(kind='BeginInteraction', recruitLocation='Village'),
                   dict(kind='Recruit', targetIndex=0, recruitLocation='Village')]
            expected.update({'Fame': 0, 'Reputation': 0,
                'recruit:offer': '', 'recruit:owned': 'army_2_006,army_2_006',
                'recruit:ready': 'False,True',
                **_consumption_state(0, {'success': True, 'InfluencePool': value,
                    'InNegotiation': True, 'Reputation': 0}),
                **_consumption_state(1, {'success': True, 'cost': 4, 'unitId': 'army_2_006',
                    'units': 2, 'offer': '', 'InfluencePool': value - 4,
                    'recruit:owned': 'army_2_006,army_2_006', 'recruit:ready': 'False,True',
                    'Fame': 0, 'Reputation': 0, 'basic:zone0': 'played'})})
            title = f'纯净魔法白选项支付完整费用，影响{value}实际招募原农民'
            capture = 1

        elif color == 'Blue':
            fields.update(combatPhase='Block', turnMachineStartPhase='Block',
                          enemies=[dict(id='monster_027' if strong else 'monster_013', originalDefinition=True)])
            setup['CombatPhase'] = 'Block'
            ops = [dict(kind='TurnDefense', targetIndex=0), dict(kind='TurnFinish')]
            expected.update({'Fame': 0, 'InBattle': False, 'CombatPhase': 'End',
                'operation:0:blockPrinted': value, 'operation:0:blockEffective': value,
                'operation:0:blockRequired': 6 if strong else 4, 'operation:0:wounds': 0,
                **_consumption_state(0, {'success': True, 'BlockPool': 0, 'Wounds': 0,
                    'handWounds': 0, 'BattleWoundsReceived': 0, 'CombatPhase': 'Melee',
                    'cards:played': 1, 'basic:zone0': 'played'}),
                **_consumption_state(1, {'success': True, 'InBattle': False, 'CombatPhase': 'End'})})
            title = f'纯净魔法蓝选项支付' + ('两点蓝' if strong else '一点蓝') + f'，格挡{value}实际挡住原敌人'
            capture = 0

        else:
            fields.update(combatPhase='Melee', turnMachineStartPhase='Melee',
                          enemies=[dict(id='monster_027' if strong else 'monster_013', originalDefinition=True)])
            setup['CombatPhase'] = 'Melee'
            ops = [dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish')]
            expected.update({'Fame': 4 if strong else 2, 'InBattle': False, 'CombatPhase': 'End',
                'played:BattleWoundsReceived': wounds,
                **_consumption_state(0, {'success': True, 'printed': value, 'effective': value,
                    'required': 5 if strong else 4, 'kills': 1, 'Fame': 4 if strong else 2,
                    'MeleePool': 0, 'Wounds': wounds, 'handWounds': wounds,
                    'cards:played': 1, 'basic:zone0': 'played', 'basic:total': 1 + wounds,
                    'basic:unique': 1 + wounds}),
                **_consumption_state(1, {'success': True, 'InBattle': False, 'CombatPhase': 'End',
                    'Wounds': wounds})})
            title = f'纯净魔法红选项支付完整费用，攻击{value}实际击败原敌人'
            capture = 0

        end = len(ops)
        ops.append(dict(kind='EndTurn'))
        expected.update(_consumption_state(end, {'success': True, **_pure_magic_zones(wounds, ended=True),
            'MovementPool': 0, 'InfluencePool': 0, 'BlockPool': 0, 'MeleePool': 0}))
        basic_acceptance_case('advanced_card_013', strong, title, expected, ops,
            setup=setup, option=option, captureAfterOperations=[capture], **fields)



# Steady Tempo / Crystal Mastery basic acceptance.

"""Append after basic_acceptance_case/_consumption_state; do not run independently.

Four advanced_card_017 cases: base bottom, strong top, strong decline, base empty.
With existing _00/_01, expected appended suffixes are _02..05.
steady:pending means SteadyTempoReturns.Count (selected returns), not Played.Count.
Only capture the explicit return selection and the generator's automatic final result.
The strong-empty-deck path remains covered by the fifth backend test.
"""


for strong, recycle, empty, label in [
    (False, True, False, '移动2实际消费，回合末本卡置底并按原顺序补牌'),
    (True, True, False, '支付蓝色移动4，本卡置顶后优先补回手牌'),
    (True, False, False, '支付蓝色移动4，主动放弃回收后正常弃牌'),
    (False, True, True, '移动2实际消费，空牌库不允许基础置底而正常弃牌'),
]:
    source = 'advanced_card_017'
    draw_cards = [] if empty else ['basic_card_000', 'basic_card_008']
    total = 1 + len(draw_cards)
    movement = 4 if strong else 2
    steps = movement // 2
    returned = recycle and (strong or not empty)
    final_hand = ([source] + draw_cards if strong else draw_cards + [source]) if returned else draw_cards
    played = {
        'hand': 0, 'hand:ids': '', 'draw': len(draw_cards), 'draw:ids': ','.join(draw_cards),
        'discard': 0, 'discard:ids': '', 'cards:played': 1, 'cards:removed': 0,
        'basic:zone0': 'played', 'basic:total': total, 'basic:unique': total,
        'Wounds': 0, 'handWounds': 0, 'steady:selected': False, 'steady:pending': 0,
    }
    final = {
        'hand': len(final_hand), 'hand:ids': ','.join(final_hand), 'draw': 0, 'draw:ids': '',
        'discard': 0 if returned else 1, 'discard:ids': '' if returned else source,
        'cards:played': 0, 'cards:removed': 0, 'basic:zone0': 'hand' if returned else 'discard',
        'basic:total': total, 'basic:unique': total, 'Wounds': 0, 'handWounds': 0,
        'MovementPool': 0, 'steady:selected': False, 'steady:pending': 0,
        'position:q': steps, 'position:r': 0, 'Fame': 0,
    }
    expected = {
        **final, **{f'played:{key}': value for key, value in played.items()},
        'played:MovementPool': movement, 'played:token:Blue': 0,
    }
    ops = []
    for q in range(1, steps + 1):
        index = len(ops)
        ops.append(dict(kind='Move', q=q, r=0))
        expected.update(_consumption_state(index, {
            'success': True, 'cost': 2, 'MovementPool': movement - 2 * q,
            'position:q': q, 'position:r': 0, 'token:Blue': 0,
            **played,
        }))

    select_index = len(ops)
    ops.append(dict(kind='SelectSteadyTempoReturn', targetIndex=0, option=int(recycle)))
    expected.update(_consumption_state(select_index, {
        'success': True, **played, 'MovementPool': 0,
        'steady:selected': recycle, 'steady:pending': int(recycle),
        'token:Blue': 0, 'position:q': steps, 'position:r': 0,
    }))
    end_index = len(ops)
    ops.append(dict(kind='EndTurn', refill=True))
    expected.update(_consumption_state(end_index, {'success': True, **final, 'token:Blue': 0}))

    basic_acceptance_case(source, strong, label, expected, ops,
        setup={'DayPart': 'Day', 'InBattle': False, 'CurrentTerrain': 'Plains',
               'token:Blue': 1 if strong else 0},
        initialDrawCards=draw_cards, unitIds=['army_2_006'], unitWounds=0,
        removeIndex=-1, discardIndex=-1, discardIndices=[],
        destinationTerrain='Plains', revealedDistance=steps,
        mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(1, steps + 1)],
        captureAfterOperations=[select_index])


"""Append after basic_acceptance_case, _consumption_state and _harvest_cards.

Two printed Crystal Mastery main paths, with independent numeric expectations.
The second case belongs to source basic_card_000 for chronological execution;
advanced_card_021 is the actual hand card played at operation 2. Acceptance
ledger must credit that actual-play evidence to advanced_card_021. Display its
original art/rules via crystalMasteryScenario=True, without changing rules.
Required observation: spent:Color snapshots of context.SpentCrystals (zero if
absent), plus existing ReturnSpentCrystals reflection. All operations snapshot.
Each case has at most two intermediate captures, followed by normal final.
"""

_crystal_scene = dict(
    crystalMasteryScenario=True,
    enemies=[dict(id='monster_013', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[],
    destinationTerrain='Plains', revealedDistance=4,
    mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(5)],
)

# Base: currently owned Green1 -> Green2; two distinct original Marches spend
# both crystals, generating Move8; four Plains cost2 each; no refund permission.
_crystal_base_final = _harvest_cards(3, 0, 3, 0, ['discard'] * 3)
_crystal_base_expected = {
    **_crystal_base_final, 'draw': 0, 'MovementPool': 0,
    'position:q': 4, 'position:r': 0, 'crystal:Green': 0,
    'spent:Green': 0, 'ReturnSpentCrystals': False,
    'played:crystal:Green': 2, 'played:spent:Green': 0,
    'played:ReturnSpentCrystals': False,
    **{'played:' + key: value for key, value in
       _harvest_cards(3, 1, 0, 2, ['played', 'hand', 'hand']).items()},
    **_consumption_state(0, {'success': True, 'crystal:Green': 1,
        'spent:Green': 1, 'MovementPool': 4, 'ReturnSpentCrystals': False,
        **_harvest_cards(3, 2, 0, 1, ['played', 'played', 'hand'])}),
    **_consumption_state(1, {'success': True, 'crystal:Green': 0,
        'spent:Green': 2, 'MovementPool': 8, 'ReturnSpentCrystals': False,
        **_harvest_cards(3, 3, 0, 0, ['played'] * 3)}),
    **_consumption_state(6, {'success': True, 'crystal:Green': 0, 'crystal:Red':0, 'crystal:Blue':0,
        'spent:Green': 0, 'ReturnSpentCrystals': False, **_crystal_base_final}),
}
for _q in range(1, 5):
    _crystal_base_expected.update(_consumption_state(_q + 1, {
        'success': True, 'cost': 2, 'MovementPool': 8 - 2 * _q,
        'position:q': _q, 'crystal:Green': 0, 'spent:Green': 2,
        'ReturnSpentCrystals': False,
    }))
basic_acceptance_case(
    'advanced_card_021', False, '已有绿晶增至两颗付两原行进，四格移动后不退款',
    _crystal_base_expected,
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     *[dict(kind='Move', q=q, r=0) for q in range(1, 5)], dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'crystal:Green': 1,
           'CurrentTerrain': 'Plains'}, option=2,
    extraHandCards=['basic_card_000', 'basic_card_000'],
    captureAfterOperations=[1], **_crystal_scene,
)

# Strong: actual first March spends Green BEFORE Mastery; Mastery's only
# printed fee is Blue; later Fury pays Red after legitimate enemy damage.
# The second March is a separate starting hand instance and remains in hand
# through the first EndTurn. No draw/discard manipulation grants it back.
_crystal_first_end = _harvest_cards(4, 0, 3, 3,
    ['discard', 'discard', 'discard', 'hand'], 2)
_crystal_second_end = _harvest_cards(4, 0, 4, 2, ['discard'] * 4, 2)
_crystal_strong_expected = {
    **_crystal_second_end, 'draw': 0, 'MovementPool': 0, 'MeleePool': 0,
    'position:q': 4, 'position:r': 0, 'Fame': 2,
    'crystal:Green': 0, 'crystal:Blue': 1, 'crystal:Red': 1,
    'spent:Green': 0, 'spent:Blue': 0, 'spent:Red': 0,
    'ReturnSpentCrystals': False, 'InBattle': False, 'CombatPhase': 'End',
    'played:crystal:Green': 0, 'played:crystal:Blue': 1, 'played:crystal:Red': 1,
    'played:spent:Green': 1, 'played:spent:Blue': 0, 'played:spent:Red': 0,
    'played:ReturnSpentCrystals': False, 'played:MovementPool': 4,
    **{'played:' + key: value for key, value in
       _harvest_cards(4, 1, 0, 3, ['played', 'hand', 'hand', 'hand']).items()},
    **_consumption_state(0, {'success': True, 'cost': 2, 'MovementPool': 2,
        'position:q': 1, 'crystal:Green': 0, 'spent:Green': 1}),
    **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 0,
        'position:q': 2, 'crystal:Blue': 1, 'spent:Green': 1,
        'spent:Blue': 0, 'ReturnSpentCrystals': False}),
    **_consumption_state(2, {'success': True, 'crystal:Green': 0,
        'crystal:Blue': 0, 'crystal:Red': 1, 'spent:Green': 1,
        'spent:Blue': 1, 'spent:Red': 0, 'ReturnSpentCrystals': True,
        **_harvest_cards(4, 2, 0, 2, ['played', 'played', 'hand', 'hand'])}),
    **_consumption_state(3, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
    **_consumption_state(4, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
    'operation:5:wounds': 2,
    **_consumption_state(5, {'success': True, 'CombatPhase': 'Melee',
        'BattleWoundsReceived': 2, 'crystal:Red': 1, 'spent:Red': 0,
        **_harvest_cards(4, 2, 0, 4, ['played', 'played', 'hand', 'hand'], 2)}),
    **_consumption_state(6, {'success': True, 'crystal:Red': 0,
        'crystal:Blue': 0, 'crystal:Green': 0, 'spent:Red': 1,
        'spent:Blue': 1, 'spent:Green': 1, 'MeleePool': 4,
        'ReturnSpentCrystals': True,
        **_harvest_cards(4, 3, 0, 3, ['played', 'played', 'played', 'hand'], 2)}),
    **_consumption_state(7, {'success': True, 'printed': 4, 'effective': 4,
        'required': 4, 'kills': 1, 'MeleePool': 0, 'Fame': 2}),
    **_consumption_state(8, {'success': True, 'InBattle': False, 'CombatPhase': 'End',
        'ReturnSpentCrystals': True, 'spent:Green': 1, 'spent:Blue': 1, 'spent:Red': 1}),
    **_consumption_state(9, {'success': True, 'crystal:Green': 1,
        'crystal:Blue': 1, 'crystal:Red': 1, 'spent:Green': 0,
        'spent:Blue': 0, 'spent:Red': 0, 'ReturnSpentCrystals': False,
        **_crystal_first_end}),
    **_consumption_state(10, {'success': True, 'TurnActionCommitted': False,
        'crystal:Green': 1, 'ReturnSpentCrystals': False, **_crystal_first_end}),
    **_consumption_state(11, {'success': True, 'crystal:Green': 0,
        'crystal:Blue': 1, 'crystal:Red': 1, 'spent:Green': 1,
        'spent:Blue': 0, 'spent:Red': 0, 'ReturnSpentCrystals': False, 'MovementPool': 4,
        **_harvest_cards(4, 1, 3, 2, ['discard', 'discard', 'discard', 'played'], 2)}),
    **_consumption_state(12, {'success': True, 'cost': 2, 'position:q': 3,
        'MovementPool': 2, 'crystal:Green': 0, 'spent:Green': 1}),
    **_consumption_state(13, {'success': True, 'cost': 2, 'position:q': 4,
        'MovementPool': 0, 'crystal:Green': 0, 'spent:Green': 1}),
    **_consumption_state(14, {'success': True, 'crystal:Green': 0,
        'crystal:Blue': 1, 'crystal:Red': 1, 'spent:Green': 0,
        'spent:Blue': 0, 'spent:Red': 0, 'ReturnSpentCrystals': False,
        **_crystal_second_end}),
}
basic_acceptance_case(
    'basic_card_000', True, '魔晶精通真实出牌返还前中后三色晶，下一回合花返绿不再退款',
    _crystal_strong_expected,
    [dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0),
     dict(kind='PlayCard', cardId='advanced_card_021', enhanced=True),
     dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1),
     dict(kind='TurnDefense', targetIndex=0),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'),
     dict(kind='EndTurn'), dict(kind='StartTurn'),
     dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='Move', q=3, r=0), dict(kind='Move', q=4, r=0), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'crystal:Green': 1,
           'crystal:Blue': 1, 'crystal:Red': 1, 'CurrentTerrain': 'Plains'},
    extraHandCards=['advanced_card_021', 'basic_card_021', 'basic_card_000'],
    captureAfterOperations=[2, 9], **_crystal_scene,
)


# Mana Storm and Ambush basic acceptance
"""Pending Mana Storm scenes; append after existing basic acceptance helpers.
No production changes or execution. Backend fixture: ManaStormBasicStateTests.
Use real source in the same GameEngine, no manaSourceFaces/TurnTo overrides.
Constructor rolls use five-face indices; card rolls use six-face indices;
EndTurn returns use five-face indices. Exact finite sequences are below.
UseSourceDie/UseExtraSourceDie targetIndex refers to saved ORIGINAL die identity,
not the shrinking source index. Existing mana:* snapshots observe all identities.
Base's six-face range is independently asserted in the backend; strong scene
results themselves contain both Black and Gold, proving unfiltered card rolls.
UI source-dice panel must include advanced_card_022 (same existing dice panel).
Three main scenes, one intermediate capture plus normal final per scene.
"""

_storm_common = dict(
    useRoundEngine=True, enemies=[dict(id='monster_013', originalDefinition=True)],
    unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[],
    destinationTerrain='Plains', revealedDistance=6,
    mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(7)],
)

# Base: [Green,Blue,White] -> [Red,Blue,White], same three source entities.
# Green crystal pays March4, ordinary Red die pays Fury4 after actual defense.
_storm_base_final = _harvest_cards(3, 0, 3, 2, ['discard'] * 3, 2)
_storm_base_expected = {
    **_storm_base_final, 'draw': 0, 'position:q': 2, 'position:r': 0,
    'MovementPool': 0, 'MeleePool': 0, 'Fame': 2, 'InBattle': False,
    'CombatPhase': 'End', 'mana:sourceCount': 3, 'mana:turnHeld': 0,
    'mana:ordinaryHeld': False, 'mana:identity': True,
    'ExtraManaDice': 0, 'BlackManaWild': False, 'GoldManaWild': False,
    'mana:die0:face': 'Blue', 'mana:die1:face': 'Blue', 'mana:die2:face': 'White',
    **{f'mana:die{i}:zone': 'source' for i in range(3)},
    'played:crystal:Green': 1, 'played:mana:sourceCount': 3,
    'played:mana:turnHeld': 0, 'played:mana:ordinaryHeld': False,
    'played:mana:identity': True, 'played:mana:die0:face': 'Red',
    'played:mana:die1:face': 'Blue', 'played:mana:die2:face': 'White',
    **{f'played:mana:die{i}:zone': 'source' for i in range(3)},
    **{'played:' + k: v for k, v in
       _harvest_cards(3, 1, 0, 2, ['played', 'hand', 'hand']).items()},
    **_consumption_state(0, {'success': True, 'crystal:Green': 0,
        'spent:Green': 1, 'MovementPool': 4,
        **_harvest_cards(3, 2, 0, 1, ['played', 'played', 'hand'])}),
    **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
    **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
    **_consumption_state(3, {'success': True, 'token:Red': 1, 'mana:sourceCount': 2,
        'mana:turnHeld': 1, 'mana:ordinaryHeld': True, 'mana:die0:zone': 'turn',
        'mana:identity': True}),
    **_consumption_state(4, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
    **_consumption_state(5, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
    'operation:6:wounds': 2,
    **_consumption_state(6, {'success': True, 'Wounds': 2, 'handWounds': 2,
        'hand': 3, 'BattleWoundsReceived': 2, 'CombatPhase': 'Melee'}),
    **_consumption_state(7, {'success': True, 'token:Red': 0, 'MeleePool': 4,
        **_harvest_cards(3, 3, 0, 2, ['played'] * 3, 2)}),
    **_consumption_state(8, {'success': True, 'printed': 4, 'effective': 4,
        'required': 4, 'kills': 1, 'Fame': 2, 'MeleePool': 0}),
    **_consumption_state(9, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
    **_consumption_state(10, {'success': True, 'mana:sourceCount': 3,
        'mana:turnHeld': 0, 'mana:ordinaryHeld': False, 'mana:identity': True,
        'mana:die0:face': 'Blue', 'mana:die0:zone': 'source', **_storm_base_final}),
}
basic_acceptance_case(
    'advanced_card_022', False, '原源骰取绿晶付行进，重掷红骰仍能普通取用并付狂怒击杀',
    _storm_base_expected,
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0),
     dict(kind='UseSourceDie', targetIndex=0, option=0),
     dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1),
     dict(kind='TurnDefense', targetIndex=0),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'CurrentTerrain': 'Plains'}, option=0,
    manaSourceRolls=[2, 1, 3, 0, 1], extraHandCards=['basic_card_000', 'basic_card_021'],
    captureAfterOperations=[3], **_storm_common,
)

for _storm_day in ['Day', 'Night']:
    # Ordinary Green by day; ordinary Gold at night. Extra picks retain exact
    # die identities and both sequences yield Green3 + Red1 for original cards.
    _storm_order = [0, 1, 2, 3] if _storm_day == 'Day' else [2, 0, 1, 3]
    _storm_final = _harvest_cards(5, 0, 5, 2, ['discard'] * 5, 2)
    _storm_expected = {
        **_storm_final, 'draw': 0, 'position:q': 6, 'position:r': 0,
        'MovementPool': 0, 'MeleePool': 0, 'Fame': 2, 'InBattle': False,
        'CombatPhase': 'End', 'mana:sourceCount': 4, 'mana:turnHeld': 0,
        'mana:ordinaryHeld': False, 'mana:identity': True,
        'ExtraManaDice': 0, 'BlackManaWild': False, 'GoldManaWild': False,
        **{f'mana:die{i}:face': 'Blue' for i in range(4)},
        **{f'mana:die{i}:zone': 'source' for i in range(4)},
        'played:token:White': 0, 'played:ExtraManaDice': 3,
        'played:BlackManaWild': True, 'played:GoldManaWild': True,
        'played:mana:sourceCount': 4, 'played:mana:turnHeld': 0,
        'played:mana:ordinaryHeld': False, 'played:mana:identity': True,
        **{f'played:mana:die{i}:face': c for i, c in enumerate(['Green', 'Black', 'Gold', 'Red'])},
        **{f'played:mana:die{i}:zone': 'source' for i in range(4)},
        **{'played:' + k: v for k, v in _harvest_cards(5, 1, 0, 4,
           ['played', 'hand', 'hand', 'hand', 'hand']).items()},
    }
    _storm_ops = []
    for _i, _die in enumerate(_storm_order):
        _storm_ops.append(dict(kind='UseSourceDie' if _i == 0 else 'UseExtraSourceDie',
                               targetIndex=_die, option=0 if _i == 3 else 2))
        _storm_expected.update(_consumption_state(_i, {
            'success': True, 'mana:sourceCount': 3 - _i, 'mana:turnHeld': _i + 1,
            'mana:ordinaryHeld': True, 'mana:identity': True,
            f'mana:die{_die}:zone': 'turn', 'ExtraManaDice': 3 - _i,
            'token:Green': min(_i + 1, 3), 'token:Red': 1 if _i == 3 else 0,
        }))
    for _i in range(3):
        _storm_ops.append(dict(kind='PlayCard', cardId='basic_card_000', enhanced=True))
        _storm_expected.update(_consumption_state(4 + _i, {
            'success': True, 'token:Green': 2 - _i, 'token:Red': 1,
            'MovementPool': 4 * (_i + 1),
            **_harvest_cards(5, 2 + _i, 0, 3 - _i,
                ['played'] * (2 + _i) + ['hand'] * (3 - _i)),
        }))
    for _q in range(1, 7):
        _storm_ops.append(dict(kind='Move', q=_q, r=0))
        _storm_expected.update(_consumption_state(6 + _q, {
            'success': True, 'cost': 2, 'position:q': _q, 'MovementPool': 12 - 2 * _q,
        }))
    _storm_ops += [dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1),
        dict(kind='TurnDefense', targetIndex=0),
        dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
        dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')]
    _storm_expected.update({
        **_consumption_state(13, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
        **_consumption_state(14, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
        'operation:15:wounds': 2,
        **_consumption_state(15, {'success': True, 'Wounds': 2, 'handWounds': 2,
            'hand': 3, 'BattleWoundsReceived': 2, 'CombatPhase': 'Melee'}),
        **_consumption_state(16, {'success': True, 'token:Red': 0, 'MeleePool': 4,
            **_harvest_cards(5, 5, 0, 2, ['played'] * 5, 2)}),
        **_consumption_state(17, {'success': True, 'printed': 4, 'effective': 4,
            'required': 4, 'kills': 1, 'Fame': 2, 'MeleePool': 0}),
        **_consumption_state(18, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
        **_consumption_state(19, {'success': True, 'mana:sourceCount': 4,
            'mana:turnHeld': 0, 'mana:ordinaryHeld': False, 'mana:identity': True,
            'ExtraManaDice': 0, 'BlackManaWild': False, 'GoldManaWild': False,
            **_storm_final}),
    })
    basic_acceptance_case(
        'advanced_card_022', True,
        f'{_storm_day}六面重掷，普通加三额外骰转三绿一红，实际移动击杀与回合归还',
        _storm_expected, _storm_ops,
        setup={'DayPart': _storm_day, 'InBattle': False, 'CurrentTerrain': 'Plains', 'token:White': 1},
        manaSourceRolls=[0, 1, 2, 3, 2, 5, 4, 0, 1, 1, 1, 1],
        extraHandCards=['basic_card_000', 'basic_card_000', 'basic_card_000', 'basic_card_021'],
        otherPlayers=[dict(unitIds=[])], captureAfterOperations=[3], **_storm_common,
    )

"""Four primary Ambush flows; append with basic_acceptance_case/_consumption_state.

Order: ranged-first base/strong, block-first base/strong, normally 023_02..05.
No new gameplay operation: Move / PlayCard / TurnBegin / TurnRanged /
TurnDefense / TurnAttack / TurnFinish / EndTurn already exist.
Root thin adapter: include advanced_card_023 in TurnDefense's existing
blockPrinted/blockEffective/blockRequired recording. No direct Block before it.
Unused next-turn permission has its separate backend test, not another scene here.
"""


def _ambush_zones(ids, played_count, wounds=0, ended=False):
    return {
        'hand': len(ids) - played_count + wounds, 'handWounds': wounds, 'Wounds': wounds,
        'cards:played': 0 if ended else played_count, 'discard': played_count if ended else 0,
        'draw': 0, 'cards:removed': 0, 'basic:total': len(ids) + wounds,
        'basic:unique': len(ids) + wounds,
        **{f'basic:zone{i}': ('discard' if ended else 'played') if i < played_count else 'hand'
           for i in range(len(ids))},
    }


for ranged_first in (True, False):
    for strong in (False, True):
        ids = ['advanced_card_023'] + (['basic_card_007'] if ranged_first else []) + ['basic_card_016', 'basic_card_021']
        movement = 4 if strong else 2
        steps = movement // 2
        wounds = 2 if ranged_first else 0
        fame = 4 if ranged_first else 2
        bonus_attack, bonus_block = (2, 4) if strong else (1, 2)
        setup = {'DayPart': 'Day', 'InBattle': False, 'CurrentTerrain': 'Plains',
                 'token:Green': int(strong), 'token:White': int(ranged_first), 'token:Red': 1}
        final = {**_ambush_zones(ids, len(ids), wounds, ended=True),
                 'MovementPool': 0, 'RangedPool': 0, 'MeleePool': 0, 'BlockPool': 0,
                 'Fame': fame, 'InBattle': False, 'CombatPhase': 'End',
                 'position:q': steps, 'position:r': 0}
        initial = _ambush_zones(ids, 1)
        expected = {**final, **{f'played:{k}': v for k, v in initial.items()},
                    'played:MovementPool': movement, 'played:token:Green': 0,
                    'played:token:White': int(ranged_first), 'played:token:Red': 1,
                    'played:AmbushAttackBonus': bonus_attack, 'played:AmbushBlockBonus': bonus_block,
                    'played:AmbushTriggered': False, 'played:RangedPool': 0, 'played:BlockPool': 0}
        ops = []
        for q in range(1, steps + 1):
            i = len(ops)
            ops.append(dict(kind='Move', q=q, r=0))
            expected.update(_consumption_state(i, {'success': True, 'cost': 2,
                'MovementPool': movement - q * 2, 'position:q': q, 'position:r': 0,
                'AmbushTriggered': False, **initial}))

        i = len(ops)
        ops.append(dict(kind='TurnBegin'))
        expected.update(_consumption_state(i, {'success': True, 'CombatPhase': 'Ranged',
            'InBattle': True, 'MovementPool': 0, 'AmbushTriggered': False}))

        if ranged_first:
            i = len(ops)
            ops.append(dict(kind='PlayCard', cardId='basic_card_007', enhanced=True))
            expected.update(_consumption_state(i, {'success': True, 'token:White': 0,
                'RangedPool': 3 + bonus_attack, 'MeleePool': 0, 'BlockPool': 0,
                'AmbushTriggered': True, **_ambush_zones(ids, 2)}))
            capture_index = i
            i = len(ops)
            ops.append(dict(kind='TurnRanged', targetIndex=0))
            expected.update(_consumption_state(i, {'success': True, 'kills': 1,
                'RangedPool': 0, 'Fame': 2, 'CombatPhase': 'Block',
                'AmbushTriggered': True, **_ambush_zones(ids, 2)}))
            block = 2
            played_before_block = 2
        else:
            i = len(ops)
            ops.append(dict(kind='TurnRanged', option=1))
            expected.update(_consumption_state(i, {'success': True, 'kills': 0,
                'RangedPool': 0, 'Fame': 0, 'CombatPhase': 'Block', 'AmbushTriggered': False}))
            block = 2 + bonus_block
            played_before_block = 1

        i = len(ops)
        ops.append(dict(kind='PlayCard', cardId='basic_card_016', enhanced=False, option=1))
        expected.update(_consumption_state(i, {'success': True, 'BlockPool': block,
            'AmbushTriggered': True, 'MeleePool': 0,
            **_ambush_zones(ids, played_before_block + 1)}))
        i = len(ops)
        ops.append(dict(kind='TurnDefense', targetIndex=0))
        expected.update({f'operation:{i}:blockPrinted': block, f'operation:{i}:blockEffective': block,
                         f'operation:{i}:blockRequired': 4, f'operation:{i}:wounds': wounds})
        expected.update(_consumption_state(i, {'success': True, 'BlockPool': 0,
            'BattleWoundsReceived': wounds, 'CombatPhase': 'Melee', 'MeleePool': 0,
            **_ambush_zones(ids, played_before_block + 1, wounds)}))
        if not ranged_first:
            capture_index = i

        i = len(ops)
        ops.append(dict(kind='PlayCard', cardId='basic_card_021', enhanced=True))
        expected.update(_consumption_state(i, {'success': True, 'token:Red': 0,
            'MeleePool': 4, 'AmbushTriggered': True, **_ambush_zones(ids, len(ids), wounds)}))
        i = len(ops)
        ops.append(dict(kind='TurnAttack', targetIndex=0))
        expected.update(_consumption_state(i, {'success': True, 'printed': 4, 'effective': 4,
            'required': 4, 'kills': 1, 'MeleePool': 0, 'Fame': fame,
            **_ambush_zones(ids, len(ids), wounds)}))
        i = len(ops)
        ops.append(dict(kind='TurnFinish'))
        expected.update(_consumption_state(i, {'success': True, 'InBattle': False,
            'CombatPhase': 'End', 'MeleePool': 0, 'BlockPool': 0, 'Wounds': wounds}))
        i = len(ops)
        ops.append(dict(kind='EndTurn'))
        expected.update(_consumption_state(i, {'success': True, **final}))

        label = ('首张原敏捷远程加成实杀，后续格挡与近战不再加成' if ranged_first
                 else '首张原决心格挡加成实挡，后续狂怒按原攻击结算')
        basic_acceptance_case('advanced_card_023', strong, label, expected, ops,
            setup=setup, extraHandCards=ids[1:], initialDrawCards=[],
            unitIds=['army_2_006'], unitWounds=0,
            removeIndex=-1, discardIndex=-1, discardIndices=[],
            enemies=[dict(id='monster_013', originalDefinition=True) for _ in range(2 if ranged_first else 1)],
            destinationTerrain='Plains', revealedDistance=steps,
            mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(1, steps + 1)],
            captureAfterOperations=[capture_index])


"""Four pending original Peaceful Moment scene branches; no execution.
Requires agreed production PeacefulMomentActions.ConvertHealing(p,ctx,points)
and CardUnitActions.TryReady(p,ctx,unit,peacefulMoment:true).
Thin operations: PeacefulHealing(option=points), PeacefulReady(targetIndex).
Observe existing snapshots plus PeacefulMomentAvailable/Ready/Action booleans.
Healing uses existing HealHand; unit combat uses existing UnitUse, no fake rules.
Source art is advanced_card_035 with corrected single White printed fee.
Initial player:Wounds creates real old wounds after saved original card refs;
healed wounds leave hand and do not enter Discard/Removed. New combat wounds
are different actual entities; backend checks reference identity explicitly.
Each scene keeps one intermediate capture and the ordinary final capture.
"""

_peace_common = dict(unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[])
_peace_expired = {'PeacefulMomentAvailable': False, 'PeacefulMomentReady': False,
    'PeacefulMomentAction': False, 'HealInfluenceRate': 0, 'InfluencePool': 0, 'HealingPool': 0}

_peace_base_final = _harvest_cards(2, 0, 2, 0, ['discard', 'discard'])
basic_acceptance_case(
    'advanced_card_035', False, '本回合两次付影响治疗真实伤牌，原承诺补充影响继续同一行动',
    {
        **_peace_base_final, **_peace_expired, 'draw': 0,
        'played:InfluencePool': 3, 'played:HealingPool': 0,
        'played:PeacefulMomentAvailable': True, 'played:PeacefulMomentReady': False,
        'played:PeacefulMomentAction': False, 'played:TurnActionCommitted': False,
        **{'played:' + k: v for k, v in
           _harvest_cards(2, 1, 0, 3, ['played', 'hand'], 2).items()},
        **_consumption_state(0, {'success': True, 'InfluencePool': 1, 'HealingPool': 1,
            'PeacefulMomentAction': True, 'TurnActionCommitted': True, 'Wounds': 2}),
        **_consumption_state(1, {'success': True, 'InfluencePool': 1, 'HealingPool': 0,
            'HealedHandWoundsThisTurn': 1,
            **_harvest_cards(2, 1, 0, 2, ['played', 'hand'], 1)}),
        **_consumption_state(2, {'success': True, 'InfluencePool': 3,
            **_harvest_cards(2, 2, 0, 1, ['played', 'played'], 1)}),
        **_consumption_state(3, {'success': True, 'InfluencePool': 1, 'HealingPool': 1,
            'PeacefulMomentAction': True, 'TurnActionCommitted': True}),
        **_consumption_state(4, {'success': True, 'InfluencePool': 1, 'HealingPool': 0,
            'HealedHandWoundsThisTurn': 2,
            **_harvest_cards(2, 2, 0, 0, ['played', 'played'])}),
        **_consumption_state(5, {'success': True, **_peace_expired, **_peace_base_final}),
    },
    [dict(kind='PeacefulHealing', option=1), dict(kind='HealHand', option=1),
     dict(kind='PlayCard', cardId='basic_card_008'),
     dict(kind='PeacefulHealing', option=1), dict(kind='HealHand', option=1), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'player:Wounds': 2},
    extraHandCards=['basic_card_008'], captureAfterOperations=[4], **_peace_common,
)

_peace_strong_final = _harvest_cards(2, 0, 2, 3, ['discard', 'discard'], 3)
basic_acceptance_case(
    'advanced_card_035', True, '白费影响六加承诺，治旧伤并付六重整III级原部队，下回合实攻五',
    {
        **_peace_strong_final, **_peace_expired, 'draw': 0,
        'unitLevel': 3, 'unitReady': False, 'unitWounds': 0,
        'Fame': 4, 'MeleePool': 0, 'InBattle': False, 'CombatPhase': 'End',
        'played:token:White': 0, 'played:token:Black': 0, 'played:InfluencePool': 6,
        'played:PeacefulMomentAvailable': True, 'played:PeacefulMomentReady': True,
        'played:PeacefulMomentAction': False, 'played:TurnActionCommitted': False,
        'played:unitLevel': 3, 'played:unitReady': False, 'played:unitWounds': 0,
        **{'played:' + k: v for k, v in
           _harvest_cards(2, 1, 0, 2, ['played', 'hand'], 1).items()},
        **_consumption_state(0, {'success': True, 'InfluencePool': 8,
            **_harvest_cards(2, 2, 0, 1, ['played', 'played'], 1)}),
        **_consumption_state(1, {'success': True, 'InfluencePool': 6, 'HealingPool': 1,
            'PeacefulMomentAction': True, 'TurnActionCommitted': True}),
        **_consumption_state(2, {'success': True, 'InfluencePool': 6, 'HealingPool': 0,
            'HealedHandWoundsThisTurn': 1,
            **_harvest_cards(2, 2, 0, 0, ['played', 'played'])}),
        **_consumption_state(3, {'success': True, 'InfluencePool': 0,
            'unitReady': True, 'unitWounds': 0, 'unitLevel': 3}),
        **_consumption_state(4, {'success': True, **_peace_expired,
            'unitReady': True, **_harvest_cards(2, 0, 2, 0, ['discard', 'discard'])}),
        **_consumption_state(5, {'success': True, 'unitReady': True,
            'TurnActionCommitted': False, 'PeacefulMomentAction': False}),
        **_consumption_state(6, {'success': True, 'InBattle': True, 'CombatPhase': 'Ranged'}),
        **_consumption_state(7, {'success': True, 'kills': 0, 'CombatPhase': 'Block'}),
        'operation:8:wounds': 3,
        **_consumption_state(8, {'success': True, 'Wounds': 3, 'handWounds': 3,
            'BattleWoundsReceived': 3, 'CombatPhase': 'Melee'}),
        **_consumption_state(9, {'success': True, 'unitReady': False, 'unitWounds': 0,
            'MeleePool': 5}),
        **_consumption_state(10, {'success': True, 'printed': 5, 'effective': 5,
            'required': 5, 'kills': 1, 'Fame': 4, 'MeleePool': 0}),
        **_consumption_state(11, {'success': True, 'InBattle': False, 'CombatPhase': 'End'}),
        **_consumption_state(12, {'success': True, **_peace_strong_final, **_peace_expired,
            'unitReady': False, 'unitWounds': 0}),
    },
    [dict(kind='PlayCard', cardId='basic_card_008'),
     dict(kind='PeacefulHealing', option=1), dict(kind='HealHand', option=1),
     dict(kind='PeacefulReady', targetIndex=0), dict(kind='EndTurn'), dict(kind='StartTurn'),
     dict(kind='TurnBegin'), dict(kind='TurnRanged', option=1), dict(kind='TurnDefense', targetIndex=0),
     dict(kind='UnitUse', targetIndex=0, option=0, abilityChoice=0),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': False, 'token:White': 1, 'player:Wounds': 1},
    unitIds=['army_003'], unitWounds=0, removeIndex=-1, discardIndex=-1, discardIndices=[],
    enemies=[dict(id='monster_027', originalDefinition=True)],
    extraHandCards=['basic_card_008'], captureAfterOperations=[3],
)

# Ordinary influence remains a normal recruitment action: no healing conversion.
for _peace_strong in [False, True]:
    _peace_count = 1 if _peace_strong else 2
    _peace_recruit_start = 0 if _peace_strong else 1
    _peace_recruit_final = _harvest_cards(_peace_count, 0, _peace_count, 0, ['discard'] * _peace_count)
    _peace_recruit_expected = {
        **_peace_recruit_final, **_peace_expired, 'draw': 0, 'Reputation': 0, 'Fame': 0,
        'recruit:offer': '', 'recruit:owned': 'army_2_006,army_2_006',
        'recruit:ready': 'False,True',
        'played:InfluencePool': 6 if _peace_strong else 3,
        'played:token:White': 0, 'played:PeacefulMomentAction': False,
        'played:TurnActionCommitted': False,
        **{'played:' + k: v for k, v in _harvest_cards(_peace_count, 1, 0,
           _peace_count - 1, ['played'] + ['hand'] * (_peace_count - 1)).items()},
        **_consumption_state(_peace_recruit_start, {'success': True,
            'InfluencePool': 6 if _peace_strong else 5, 'InNegotiation': True,
            'TurnActionCommitted': True, 'PeacefulMomentAction': False}),
        **_consumption_state(_peace_recruit_start + 1, {'success': True, 'cost': 4,
            'unitId': 'army_2_006', 'units': 2, 'offer': '',
            'InfluencePool': 2 if _peace_strong else 1, 'PeacefulMomentAction': False,
            'recruit:owned': 'army_2_006,army_2_006', 'recruit:ready': 'False,True',
            'Reputation': 0, 'Wounds': 0}),
        **_consumption_state(_peace_recruit_start + 2,
            {'success': True, **_peace_expired, **_peace_recruit_final}),
    }
    _peace_ops = []
    if not _peace_strong:
        _peace_ops.append(dict(kind='PlayCard', cardId='basic_card_008'))
        _peace_recruit_expected.update(_consumption_state(0, {'success': True, 'InfluencePool': 5,
            'TurnActionCommitted': False, **_harvest_cards(2, 2, 0, 0, ['played', 'played'])}))
    _peace_ops += [dict(kind='BeginInteraction', recruitLocation='Village'),
        dict(kind='Recruit', targetIndex=0, recruitLocation='Village'), dict(kind='EndTurn')]
    basic_acceptance_case(
        'advanced_card_035', _peace_strong,
        '强化仅作普通影响力付四招募原农民' if _peace_strong else '基础加原承诺仅作普通影响力付四招募原农民',
        _peace_recruit_expected, _peace_ops,
        setup={'DayPart': 'Day', 'InBattle': False, 'player:TempCommandSlots': 1,
               'token:White': 1 if _peace_strong else 0},
        extraHandCards=[] if _peace_strong else ['basic_card_008'],
        recruitOffer=['army_2_006'], captureAfterOperations=[_peace_recruit_start + 1],
        **_peace_common,
    )


"""Append with basic_acceptance_case/_consumption_state; do not run separately.

Eight original 037 choices, ordered Move base/strong, Influence base/strong,
Attack base/strong, Block base/strong (normally _02..09).
Only thin adapter requested: allow advanced_card_037 in existing TurnDefense
blockPrinted/blockEffective/blockRequired capture. Existing PlayCard, Move,
BeginInteraction, Recruit and TurnMachine operations handle every consumer.
Attack starts at real Melee: original027 already dealt three NEW hand wounds.
Material indices are bound before this damage, so old wound index1 stays exact.
"""


def _resolve_zones(ids, zones, new_wounds=0):
    old_hand_wounds = sum(card == 'w' and zone == 'hand' for card, zone in zip(ids, zones))
    return {'hand': zones.count('hand') + new_wounds,
            'handWounds': old_hand_wounds + new_wounds, 'Wounds': old_hand_wounds + new_wounds,
            'discard': zones.count('discard'), 'cards:played': zones.count('played'),
            'draw': 0, 'cards:removed': 0, 'basic:total': len(ids) + new_wounds,
            'basic:unique': len(ids) + new_wounds,
            **{f'basic:zone{i}': zone for i, zone in enumerate(zones)}}


for choice, label, pool in [(0, '移动', 'MovementPool'), (1, '影响力', 'InfluencePool'),
                             (2, '攻击', 'MeleePool'), (3, '格挡', 'BlockPool')]:
    for strong in (False, True):
        ids = ['advanced_card_037']
        materials = []
        fields = {'unitIds': ['army_2_006'], 'unitWounds': 0,
                  'removeIndex': -1, 'discardIndex': -1, 'discardIndices': [], 'initialDrawCards': []}
        setup = {'DayPart': 'Day', 'InBattle': choice in (2, 3), 'token:Green': int(strong)}
        new_wounds = 3 if choice == 2 else 0
        if choice == 1:
            ids += ['basic_card_008'] if strong else ['w', 'basic_card_008']
            materials = [1]
            fields['recruitOffer'] = ['army_2_006']
            setup['player:TempCommandSlots'] = 1
        elif choice == 2:
            ids += ['w', 'basic_card_008' if strong else 'basic_card_021']
            materials = [1, 2] if strong else [1]
            fields.update(combatPhase='Melee', turnMachineStartPhase='Melee',
                          enemies=[dict(id='monster_027', originalDefinition=True)])
            setup['CombatPhase'] = 'Melee'
        elif choice == 3:
            if strong:
                ids += ['basic_card_000', 'basic_card_008']
                materials = [1, 2]
            ids += ['basic_card_016', 'basic_card_021']
            fields.update(combatPhase='Block', turnMachineStartPhase='Block',
                          enemies=[dict(id='monster_027' if strong else 'monster_013', originalDefinition=True)])
            setup.update({'CombatPhase': 'Block', 'token:Red': 1})
        else:
            setup['CurrentTerrain'] = 'Plains'
            fields.update(destinationTerrain='Plains', revealedDistance=1,
                mapTerrains=[dict(q=1, r=0, terrain='Forest' if strong else 'Plains')])

        if strong:
            fields['discardIndices'] = materials
        elif materials:
            fields['discardIndex'] = materials[0]
        value = (3 + len(materials) * 2) if strong else (2 + int(bool(materials)))
        zones = ['played'] + ['discard' if i in materials else 'hand' for i in range(1, len(ids))]
        expected = {**{f'played:{k}': v for k, v in _resolve_zones(ids, zones, new_wounds).items()},
                    'played:token:Green': 0, 'played:HealedHandWoundsThisTurn': 0,
                    **{f'played:{p}': value if p == pool else 0
                       for p in ['MovementPool', 'InfluencePool', 'MeleePool', 'BlockPool', 'RangedPool']}}
        ops = []

        def follow(card_id, enhanced=False, option=0, output=None):
            index = len(ops)
            ops.append(dict(kind='PlayCard', cardId=card_id, enhanced=enhanced, option=option))
            actual = next(i for i, c in enumerate(ids) if c == card_id and zones[i] == 'hand')
            zones[actual] = 'played'
            expected.update(_consumption_state(index, {'success': True,
                **_resolve_zones(ids, zones, new_wounds), **(output or {})}))

        if choice == 0:
            ops.append(dict(kind='Move', q=1, r=0))
            expected.update(_consumption_state(0, {'success': True, 'cost': value, 'MovementPool': 0,
                'position:q': 1, 'position:r': 0, **_resolve_zones(ids, zones)}))
            expected.update({'position:q': 1, 'position:r': 0, 'Fame': 0})
            capture = 0

        elif choice == 1:
            if not strong:
                follow('basic_card_008', output={'InfluencePool': 5})
            i = len(ops)
            ops.append(dict(kind='BeginInteraction', recruitLocation='Village'))
            expected.update(_consumption_state(i, {'success': True, 'InfluencePool': 5,
                'InNegotiation': True, 'Reputation': 0}))
            i = len(ops)
            ops.append(dict(kind='Recruit', targetIndex=0, recruitLocation='Village'))
            expected.update(_consumption_state(i, {'success': True, 'cost': 4, 'unitId': 'army_2_006',
                'units': 2, 'offer': '', 'InfluencePool': 1, 'Fame': 0, 'Reputation': 0,
                'recruit:owned': 'army_2_006,army_2_006', 'recruit:ready': 'False,True',
                **_resolve_zones(ids, zones)}))
            expected.update({'Fame': 0, 'Reputation': 0, 'recruit:offer': '',
                'recruit:owned': 'army_2_006,army_2_006', 'recruit:ready': 'False,True'})
            capture = i

        else:
            if choice == 2:
                expected['played:BattleWoundsReceived'] = 3
                if not strong:
                    follow('basic_card_021', output={'MeleePool': 5})
                attack = 7 if strong else 5
                armor, fame = 5, 4
            else:
                if not strong:
                    follow('basic_card_016', option=1, output={'BlockPool': 4})
                i = len(ops)
                ops.append(dict(kind='TurnDefense', targetIndex=0))
                block = 7 if strong else 4
                expected.update({f'operation:{i}:blockPrinted': block,
                    f'operation:{i}:blockEffective': block, f'operation:{i}:blockRequired': 6 if strong else 4,
                    f'operation:{i}:wounds': 0})
                expected.update(_consumption_state(i, {'success': True, 'BlockPool': 0,
                    'CombatPhase': 'Melee', 'BattleWoundsReceived': 0, **_resolve_zones(ids, zones)}))
                follow('basic_card_021', enhanced=True, output={'MeleePool': 4, 'token:Red': 0})
                if strong:
                    follow('basic_card_016', output={'MeleePool': 6})
                attack, armor, fame = (6, 5, 4) if strong else (4, 4, 2)
            i = len(ops)
            ops.append(dict(kind='TurnAttack', targetIndex=0))
            expected.update(_consumption_state(i, {'success': True, 'printed': attack,
                'effective': attack, 'required': armor, 'kills': 1, 'MeleePool': 0,
                'Fame': fame, **_resolve_zones(ids, zones, new_wounds)}))
            capture = i
            i = len(ops)
            ops.append(dict(kind='TurnFinish'))
            expected.update(_consumption_state(i, {'success': True, 'InBattle': False,
                'CombatPhase': 'End', 'MeleePool': 0, 'Wounds': new_wounds}))
            expected.update({'InBattle': False, 'CombatPhase': 'End', 'Fame': fame})

        i = len(ops)
        ops.append(dict(kind='EndTurn'))
        final = {**_resolve_zones(ids, ['discard'] * len(ids), new_wounds),
                 'MovementPool': 0, 'InfluencePool': 0, 'MeleePool': 0, 'BlockPool': 0,
                 'RangedPool': 0, 'HealedHandWoundsThisTurn': 0}
        expected.update(final)
        expected.update(_consumption_state(i, {'success': True, **final}))
        material_label = '不弃牌' if not materials else ('弃真实普通牌' if strong and choice in (1, 3)
                          else '弃真实旧伤与原普通牌' if strong else '弃真实旧伤')
        basic_acceptance_case('advanced_card_037', strong, f'{label}选择：{material_label}，原效果{value}实际消费',
            expected, ops, setup=setup, option=choice, extraHandCards=ids[1:],
            captureAfterOperations=[capture], **fields)


"""One base Nature Force scene (append after existing 038 cases, normally 038_07).

Existing TurnDefense needs its thin unitTargets adapter: build UnitDamageAllocation
for targetIndex/attackIndex from the actual _damageUnits and pass it as the fourth
argument to TurnMachine.ResolveDefense. No standalone Damage call before defense.
Backend additionally checks the same farmer takes one wound in the next combat;
this scene checks first-combat damage and removal of temporary resistance only.
"""


def _nature_zones(played_count, ended=False):
    return {
        'hand': 2 - played_count, 'handWounds': 0, 'Wounds': 0,
        'cards:played': 0 if ended else played_count,
        'discard': played_count if ended else 0, 'draw': 0, 'cards:removed': 0,
        'basic:total': 2, 'basic:unique': 2,
        **{f'basic:zone{i}': ('discard' if ended else 'played')
           if i < played_count else 'hand' for i in range(2)},
    }


_nature_unit = {'unitReady': True, 'unitCanActivate': True,
                'unitWounds': 0, 'unitLevel': 1}
_nature_final = {
    **_nature_zones(2, ended=True), **_nature_unit,
    'unitPhysicalResist': False, 'Fame': 3,
    'MovementPool': 0, 'BlockPool': 0, 'MeleePool': 0,
    'RangedPool': 0, 'SiegePool': 0, 'InBattle': False, 'CombatPhase': 'End',
}
_nature_expected = {
    **_nature_final,
    **{f'played:{key}': value for key, value in {
        **_nature_zones(1), **_nature_unit, 'unitPhysicalResist': True,
        'token:Green': 0, 'token:Red': 1,
        'MovementPool': 0, 'BlockPool': 0, 'MeleePool': 0,
        'RangedPool': 0, 'SiegePool': 0, 'Fame': 0,
        'InBattle': True, 'CombatPhase': 'Block',
    }.items()},
    # Original monster_007: physical attack3, Swift, armor4, fame3.
    # Swift doubles required BLOCK, but incoming damage remains3. The original
    # farmer armor3 with temporary physical resistance absorbs all3, no wounds.
    'operation:0:wounds': 0,
    'operation:0:assignedUnits': 1,
    **_consumption_state(0, {
        'success': True, **_nature_zones(1), **_nature_unit,
        'unitPhysicalResist': True, 'BlockPool': 0, 'MeleePool': 0,
        'BattleWoundsReceived': 0, 'Fame': 0, 'token:Red': 1,
        'InBattle': True, 'CombatPhase': 'Melee',
    }),
    **_consumption_state(1, {
        'success': True, **_nature_zones(2), **_nature_unit,
        'unitPhysicalResist': True, 'token:Red': 0, 'token:Green': 0,
        'MeleePool': 4, 'BlockPool': 0, 'CombatPhase': 'Melee',
    }),
    **_consumption_state(2, {
        'success': True, 'printed': 4, 'effective': 4, 'required': 4, 'kills': 1,
        **_nature_zones(2), **_nature_unit, 'unitPhysicalResist': True,
        'MeleePool': 0, 'Fame': 3,
    }),
    **_consumption_state(3, {
        'success': True, **_nature_zones(2), **_nature_unit,
        'unitPhysicalResist': False, 'InBattle': False,
        'CombatPhase': 'End', 'Fame': 3, 'BlockPool': 0, 'MeleePool': 0,
    }),
    **_consumption_state(4, {'success': True, **_nature_final}),
}
basic_acceptance_case(
    'advanced_card_038', False,
    '原农民物抗承受原狼骑兵3点攻击零伤，狂怒强化击杀后战末清物抗',
    _nature_expected,
    [dict(kind='TurnDefense', targetIndex=0, attackIndex=0, unitTargets=[0]),
     dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
     dict(kind='TurnAttack', targetIndex=0), dict(kind='TurnFinish'), dict(kind='EndTurn')],
    setup={'DayPart': 'Day', 'InBattle': True, 'CombatPhase': 'Block', 'token:Red': 1},
    combatPhase='Block', turnMachineStartPhase='Block',
    extraHandCards=['basic_card_021'], initialDrawCards=[],
    unitIds=['army_2_006'], unitWounds=0, unitInitiallyReady=True,
    removeIndex=-1, discardIndex=-1, discardIndices=[],
    enemies=[dict(id='monster_007', originalDefinition=True)], captureAfterOperations=[0])


"""Four original Mountain Lore main branches; append after basic helpers.
No generator/test execution. Use existing EndTurn(refill=True), which requests
real PlayerTurnEngine refill instead of the basic scenario default SkipDraw.
No new mutation operation or hand-count adapter is needed.

One thin observation requested: snapshot mountain:safe evaluates existing
CardTravelActions.IsSafeDestination(player,ctx,player.Position,ctx.OtherPlayers).
This records the actual pre-EndTurn mountain permission at operation 0; backend
also verifies that losing it rejects EndTurn before any card/resource cleanup.
All other keys already exist or reflect the new MountainLoreMode property.
Display original art, real terrain, move cost and the hand/draw ordered IDs.
Only operation 0 has an intermediate capture; the final frame shows real refill.
"""

_mountain_draw = ['basic_card_000', 'basic_card_001', 'basic_card_007', 'basic_card_008',
                  'basic_card_014', 'basic_card_016', 'basic_card_021', 'basic_card_024']
for _strong, _start, _end, _cost, _hand in [
    (False, 'Plains', 'Hills', 3, 6),
    (True, 'Plains', 'Mountain', 5, 7),
    (True, 'Plains', 'Hills', 3, 6),
    (False, 'Hills', 'Plains', 2, 5),
]:
    _movement = 5 if _strong else 3
    _mode = 2 if _strong else 1
    _final = {
        **_harvest_cards(9, 0, 1, _hand, ['discard']),
        'draw': 8 - _hand, 'draw:ids': ','.join(_mountain_draw[_hand:]),
        'hand:ids': ','.join(_mountain_draw[:_hand]), 'discard:ids': 'advanced_card_039',
        'NextDrawBonus': 0, 'MountainLoreMode': 0, 'MountainSafe': False, 'LakeSafe': False,
        'MovementPool': 0, 'position:q': 1, 'position:r': 0, 'CurrentTerrain': _end,
    }
    _expected = {
        **_final,
        'played:MovementPool': _movement, 'played:NextDrawBonus': 0,
        'played:MountainLoreMode': _mode, 'played:MountainSafe': _strong,
        'played:token:Green': 0, 'played:CurrentTerrain': _start,
        'played:draw': 8, 'played:draw:ids': ','.join(_mountain_draw),
        **{'played:' + k: v for k, v in _harvest_cards(9, 1, 0, 0, ['played']).items()},
        **_consumption_state(0, {
            'success': True, 'cost': _cost, 'MovementPool': _movement - _cost,
            'position:q': 1, 'position:r': 0, 'CurrentTerrain': _end,
            'MountainLoreMode': _mode, 'MountainSafe': _strong,
            'NextDrawBonus': 0, 'mountain:safe': True,
            'draw': 8, 'draw:ids': ','.join(_mountain_draw),
            **_harvest_cards(9, 1, 0, 0, ['played']),
        }),
        'operation:1:endingTerrain': _end, 'operation:1:safeBeforeEnd': True,
        **_consumption_state(1, {'success': True, **_final}),
    }
    basic_acceptance_case(
        'advanced_card_039', _strong,
        f'{_start}实际移动至{_end}付{_cost}，回合末真实补到{_hand}张并保持原牌序',
        _expected, [dict(kind='Move', q=1, r=0), dict(kind='EndTurn', refill=True)],
        setup={'DayPart': 'Day', 'InBattle': False, 'CurrentTerrain': _start,
               'token:Green': 1 if _strong else 0},
        initialDrawCards=_mountain_draw, unitIds=['army_2_006'], unitWounds=0,
        removeIndex=-1, discardIndex=-1, discardIndices=[],
        destinationTerrain=_end, revealedDistance=1,
        mapTerrains=[dict(q=0, r=0, terrain=_start), dict(q=1, r=0, terrain=_end)],
        captureAfterOperations=[0],
    )


"""Append four Power of Crystals scenes, normally advanced_card_040_02..05.

Root DTO contract: selectedActionManaColor string; manaPowerBonusChoices int[].
Main and bonus choices both 0 Move / 1 Heal / 2 Draw. Only strong source needs
explicit color. Source's choices clear after resolution, not inherited by March.
Existing formal operations only: PlayCard, Move, HealHand, HealUnit, UnitUse, EndTurn.
Healed hand wounds leave all player zones (not permanent RemovedCards); their
tracked basic:zone is the empty string, and basic:total/unique decrease accordingly.
"""


def _power_cards(hand_ids, draw_ids, played, total, ended=False, extra_zones=None):
    state = {
        'hand': len(hand_ids), 'hand:ids': ','.join(hand_ids),
        'draw': len(draw_ids), 'draw:ids': ','.join(draw_ids),
        'cards:played': 0 if ended else played, 'discard': played if ended else 0,
        'cards:removed': 0, 'basic:zone0': 'discard' if ended else 'played',
        'basic:total': total, 'basic:unique': total,
        'Wounds': hand_ids.count('w'), 'handWounds': hand_ids.count('w'),
    }
    state.update(extra_zones or {})
    return state


for branch in ('base', 'move', 'heal', 'draw'):
    strong = branch != 'base'
    main = {'base': 2, 'move': 0, 'heal': 1, 'draw': 2}[branch]
    each = 2 if branch == 'move' else 1
    crystals = {f'crystal:{color}': each for color in ('Red', 'Blue', 'Green', 'White')}
    setup = {'DayPart': 'Day', 'InBattle': False, 'CurrentTerrain': 'Plains', **crystals}
    fields = dict(unitIds=['army_2_006'], unitWounds=1 if branch == 'heal' else 0,
                  unitInitiallyReady=True, removeIndex=-1, discardIndex=-1, discardIndices=[],
                  destinationTerrain='Plains', revealedDistance=2,
                  mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in (1, 2)])
    if strong:
        chosen = 'Blue' if branch == 'heal' else 'Green'
        setup[f'token:{chosen}'] = 1
        fields.update(selectedActionManaColor=chosen,
                      manaPowerBonusChoices={'move': [1, 2], 'heal': [1], 'draw': [0]}[branch])
    else:
        setup['crystal:Green'] = 0
    ops, expected = [], {}

    def step(operation, state):
        index = len(ops)
        ops.append(operation)
        expected.update(_consumption_state(index, {'success': True, **state}))
        return index

    if branch == 'base':
        fields.update(extraHandCards=['basic_card_000'], initialDrawCards=[])
        after = _power_cards(['basic_card_000'], [], 1, 2, extra_zones={'basic:zone1': 'hand'})
        after.update(MovementPool=0, HealingPool=0)
        consumed = {**crystals, 'crystal:Green': 0}
        both = _power_cards([], [], 2, 2, extra_zones={'basic:zone1': 'played'})
        capture = step(dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
                       {**both, **consumed, 'MovementPool': 4, 'HealingPool': 0})
        step(dict(kind='Move', q=1, r=0), {**both, 'cost': 2, 'MovementPool': 2, 'position:q': 1, 'position:r': 0})
        step(dict(kind='Move', q=2, r=0), {**both, 'cost': 2, 'MovementPool': 0, 'position:q': 2, 'position:r': 0})
        final = {**_power_cards([], [], 2, 2, ended=True, extra_zones={'basic:zone1': 'discard'}),
                 **consumed, 'position:q': 2, 'position:r': 0, 'unitReady': True, 'unitWounds': 0}
        label = '缺少绿色才可得晶，原行进强化实际支付并移动两格'
    elif branch == 'move':
        fields.update(extraHandCards=['w'], initialDrawCards=['basic_card_000', 'basic_card_008'])
        after = _power_cards(['w', 'basic_card_000'], ['basic_card_008'], 1, 4,
                             extra_zones={'basic:zone1': 'hand'})
        after.update(MovementPool=4, HealingPool=1)
        healed = _power_cards(['basic_card_000'], ['basic_card_008'], 1, 3,
                              extra_zones={'basic:zone1': ''})
        capture = step(dict(kind='HealHand', option=1),
                       {**healed, **crystals, 'HealingPool': 0, 'MovementPool': 4})
        step(dict(kind='Move', q=1, r=0), {**healed, 'cost': 2, 'MovementPool': 2, 'position:q': 1, 'position:r': 0})
        step(dict(kind='Move', q=2, r=0), {**healed, 'cost': 2, 'MovementPool': 0, 'position:q': 2, 'position:r': 0})
        final = {**_power_cards(['basic_card_000'], ['basic_card_008'], 1, 3, ended=True,
                                extra_zones={'basic:zone1': ''}), **crystals,
                 'position:q': 2, 'position:r': 0, 'unitReady': True, 'unitWounds': 0}
        label = '绿标记付款保留两套晶，移动主效独立选治疗与抽牌并逐项消费'
    elif branch == 'heal':
        fields.update(extraHandCards=['w', 'w'], initialDrawCards=[])
        after = _power_cards(['w', 'w'], [], 1, 3,
                             extra_zones={'basic:zone1': 'hand', 'basic:zone2': 'hand'})
        after.update(MovementPool=0, HealingPool=3, unitWounds=1, unitReady=True, unitCanActivate=False)
        step(dict(kind='HealUnit', targetIndex=0, option=1),
             {**_power_cards(['w', 'w'], [], 1, 3, extra_zones={'basic:zone1': 'hand', 'basic:zone2': 'hand'}),
              'HealingPool': 2, 'unitWounds': 0, 'unitReady': True, 'unitCanActivate': True,
              'MovementPool': 0})
        healed = _power_cards([], [], 1, 1, extra_zones={'basic:zone1': '', 'basic:zone2': ''})
        step(dict(kind='HealHand', option=2), {**healed, 'HealingPool': 0, 'unitWounds': 0,
              'unitCanActivate': True, 'MovementPool': 0})
        capture = step(dict(kind='UnitUse', targetIndex=0, option=2),
                       {**healed, **crystals, 'unitWounds': 0, 'unitReady': False,
                        'unitCanActivate': False, 'MovementPool': 2, 'HealingPool': 0})
        step(dict(kind='Move', q=1, r=0), {**healed, 'cost': 2, 'MovementPool': 0,
              'position:q': 1, 'position:r': 0, 'unitReady': False})
        final = {**_power_cards([], [], 1, 1, ended=True, extra_zones={'basic:zone1': '', 'basic:zone2': ''}),
                 **crystals, 'position:q': 1, 'position:r': 0,
                 'unitWounds': 0, 'unitReady': False, 'unitCanActivate': False}
        label = '蓝标记择一付款，治疗3分别治原农民与两张手伤，再真实发动部队移动'
    else:
        fields.update(extraHandCards=[], initialDrawCards=['basic_card_000', 'basic_card_008', 'basic_card_021'])
        after = _power_cards(['basic_card_000', 'basic_card_008'], ['basic_card_021'], 1, 4)
        after.update(MovementPool=2, HealingPool=0)
        step(dict(kind='Move', q=1, r=0), {**after, 'cost': 2, 'MovementPool': 0, 'position:q': 1, 'position:r': 0})
        played_march = _power_cards(['basic_card_008'], ['basic_card_021'], 2, 4)
        capture = step(dict(kind='PlayCard', cardId='basic_card_000', enhanced=False),
                       {**played_march, **crystals, 'MovementPool': 2, 'HealingPool': 0})
        step(dict(kind='Move', q=2, r=0), {**played_march, 'cost': 2, 'MovementPool': 0,
              'position:q': 2, 'position:r': 0})
        final = {**_power_cards(['basic_card_008'], ['basic_card_021'], 2, 4, ended=True),
                 **crystals, 'position:q': 2, 'position:r': 0, 'unitWounds': 0, 'unitReady': True}
        label = '精确抽两张原牌与独立移动奖励，抽到的原行进继续实际出牌移动'

    after.update(crystals)
    after.update({'token:Green': 0, 'token:Blue': 0})
    final.update(MovementPool=0, HealingPool=0, InfluencePool=0, MeleePool=0, BlockPool=0,
                 RangedPool=0, SiegePool=0, Fame=0, InBattle=False)
    expected.update(final)
    expected.update({f'played:{key}': value for key, value in after.items()})
    step(dict(kind='EndTurn', refill=False), final)
    basic_acceptance_case('advanced_card_040', strong, label, expected, ops,
                          option=main, setup=setup, captureAfterOperations=[capture], **fields)


"""Two main Explosive Bolt scenes; append after the existing 041 fee cases.

Use generated IDs, since the fee-only cases may already have consumed suffixes.
Root thin adapter: ChooseExplosiveBoltArmor(targetIndex=0) resolves current
context.Enemies[0] after TurnRanged remapping. TurnDefense should include 041 in
the existing blockPrinted/blockEffective/blockRequired result recording.
The double-kill and melee-use paths stay in the independent backend fixture.
"""


def _bolt_zones(ids, played, wounds, ended=False):
    return {
        'hand': len(ids) - played + wounds, 'Wounds': wounds, 'handWounds': wounds,
        'draw': 0, 'discard': played if ended else 0, 'cards:played': 0 if ended else played,
        'cards:removed': 0, 'basic:total': len(ids) + wounds, 'basic:unique': len(ids) + wounds,
        **{f'basic:zone{i}': ('discard' if ended else 'played') if i < played else 'hand'
           for i in range(len(ids))},
    }


for strong in (False, True):
    source = 'advanced_card_041'
    ids = [source] + ([] if strong else ['basic_card_007']) + ['basic_card_016', 'basic_card_021']
    wounds, fame = (0, 6) if strong else (1, 4)
    setup = {'DayPart': 'Day', 'InBattle': True, 'CombatPhase': 'Ranged', 'token:Blue': 1}
    if strong:
        setup.update({'token:White': 1, 'token:Red': 1})
    fields = {'extraHandCards': ids[1:], 'initialDrawCards': [],
              'unitIds': ['army_2_006'], 'unitWounds': 0, 'unitInitiallyReady': True,
              'removeIndex': -1, 'discardIndex': -1, 'discardIndices': [],
              'combatPhase': 'Ranged', 'turnMachineStartPhase': 'Ranged',
              'enemies': [dict(id='monster_032', originalDefinition=True),
                          dict(id='monster_027' if strong else 'monster_013', originalDefinition=True)]}
    if strong:
        fields['selectedActionManaColor'] = 'White'
    played = 1
    initial = {**_bolt_zones(ids, played, wounds), 'RangedPool': 3 if strong else 0,
               'MeleePool': 0, 'BlockPool': 0, 'ExplosiveBoltArmorUses': 0, 'ArmorReducePerKill': int(strong),
               'token:White': 0, 'token:Blue': 1, 'token:Red': int(strong),
               'crystal:White': int(not strong), 'crystal:Red': int(not strong),
               'unitReady': True, 'unitWounds': 0, 'Fame': 0, 'CombatPhase': 'Ranged'}
    final = {**_bolt_zones(ids, len(ids), wounds, ended=True), 'Fame': fame,
             'RangedPool': 0, 'MeleePool': 0, 'BlockPool': 0, 'SiegePool': 0,
             'ExplosiveBoltArmorUses': 0, 'ArmorReducePerKill': 0, 'enemyArmor:0': 5 if strong else 4, 'unitReady': not strong, 'unitWounds': 0,
             'InBattle': False, 'CombatPhase': 'End'}
    expected = {**final, **{f'played:{key}': value for key, value in initial.items()}}
    ops = []

    def step(operation, state):
        index = len(ops)
        ops.append(operation)
        expected.update(_consumption_state(index, {'success': True, **state}))
        return index

    if not strong:
        played += 1
        capture = step(dict(kind='PlayCard', cardId='basic_card_007', enhanced=True),
             {**_bolt_zones(ids, played, wounds), 'crystal:White': 0, 'crystal:Red': 1,
              'RangedPool': 3, 'ExplosiveBoltArmorUses': 0})
    step(dict(kind='TurnRanged', targetIndex=0),
         {**_bolt_zones(ids, played, wounds), 'kills': 1, 'Fame': 2, 'RangedPool': 0,
          'ExplosiveBoltArmorUses': int(strong), 'enemyArmor:0': 5 if strong else 4,
          'CombatPhase': 'Block'})
    if strong:
        capture = step(dict(kind='ChooseExplosiveBoltArmor', targetIndex=0),
                       {**_bolt_zones(ids, played, wounds), 'ExplosiveBoltArmorUses': 0,
                        'enemyArmor:0': 4, 'Fame': 2, 'MeleePool': 0, 'BlockPool': 0})
        expected.update({f'operation:{capture}:targetId': 'monster_027',
                         f'operation:{capture}:armorBefore': 5, f'operation:{capture}:armorAfter': 4,
                         f'operation:{capture}:uses': 0})
    played += 1
    step(dict(kind='PlayCard', cardId='basic_card_016', enhanced=True, option=1),
         {**_bolt_zones(ids, played, wounds), 'token:Blue': 0, 'BlockPool': 5,
          'ExplosiveBoltArmorUses': 0})
    if strong:
        step(dict(kind='UnitUse', targetIndex=0, option=0, abilityChoice=1),
             {**_bolt_zones(ids, played, wounds), 'BlockPool': 7, 'MeleePool': 0,
              'unitReady': False, 'unitWounds': 0, 'ExplosiveBoltArmorUses': 0})
    defense = step(dict(kind='TurnDefense', targetIndex=0),
                   {**_bolt_zones(ids, played, wounds), 'BlockPool': 0, 'MeleePool': 0,
                    'CombatPhase': 'Melee', 'ExplosiveBoltArmorUses': 0,
                    'unitReady': not strong, 'unitWounds': 0})
    expected.update({f'operation:{defense}:wounds': 0,
                     f'operation:{defense}:blockPrinted': 7 if strong else 5,
                     f'operation:{defense}:blockEffective': 7 if strong else 5,
                     f'operation:{defense}:blockRequired': 6 if strong else 4})
    played += 1
    fury_step = step(dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
                     {**_bolt_zones(ids, played, wounds), 'token:Red': 0, 'crystal:Red': 0,
                      'crystal:White': 0, 'MeleePool': 4, 'ExplosiveBoltArmorUses': 0})
    step(dict(kind='TurnAttack', targetIndex=0),
         {**_bolt_zones(ids, played, wounds), 'printed': 4, 'effective': 4, 'required': 4,
          'kills': 1, 'Fame': fame, 'MeleePool': 0, 'ExplosiveBoltArmorUses': 0})
    step(dict(kind='TurnFinish'),
         {**_bolt_zones(ids, played, wounds), 'ExplosiveBoltArmorUses': 0,
          'InBattle': False, 'CombatPhase': 'End', 'unitReady': not strong, 'unitWounds': 0})
    step(dict(kind='EndTurn'), final)
    basic_acceptance_case(source, strong,
        '远程实杀后指定真实幸存者减甲，原狂怒4点攻击实际击杀' if strong
        else '实际自伤与白红双晶分别支付原敏捷远程和狂怒近战',
        expected, ops, setup=setup, captureAfterOperations=[capture], **fields)


"""Two Blood Surge primary flows, append normally as advanced_card_042_02/_03.

No artificial wound operation. Original041 creates a real wound outside combat;
two original032 attacks create four more via TurnMachine.ResolveDefense. Source
strong pays the existing selectedActionManaColor='Green'. Its removed first new
wound is recorded in RemovedCards, while tracked original old wound stays Hand.
Existing operations suffice. Capture after actual defense (draw limit reached)
and the generator's automatic final capture; no per-card/color extra scenes.
"""


_surge_draw = ['basic_card_021', 'basic_card_016', 'basic_card_000', 'basic_card_008', 'basic_card_001']


def _surge_cards(hand, draw_ids, played, wounds, total, removed, bolt_played=True, ended=False):
    return {
        'hand': hand, 'draw': len(draw_ids), 'draw:ids': ','.join(draw_ids),
        'cards:played': 0 if ended else played, 'discard': played if ended else 0,
        'cards:removed': removed, 'Wounds': wounds, 'handWounds': wounds,
        'basic:zone0': 'discard' if ended else 'played', 'basic:zone1': 'hand',
        'basic:zone2': ('discard' if ended else 'played') if bolt_played else 'hand',
        'basic:total': total, 'basic:unique': total,
    }


for strong in (False, True):
    removed = int(strong)
    wounds = 5 if strong else 6
    drawn = 4 if strong else 3
    remaining_draw = _surge_draw[drawn:]
    initial = {
        **_surge_cards(2, _surge_draw, 1, 1, 8, 0, bolt_played=False),
        'WoundDrawRemaining': 3, 'FirstWoundIgnored': strong,
        'token:Green': 0, 'token:Red': 0, 'crystal:Red': 0, 'crystal:White': 0,
        'MeleePool': 0, 'RangedPool': 0, 'BlockPool': 0, 'InBattle': False,
    }
    self_state = {
        **_surge_cards(2 if strong else 3, _surge_draw[1:], 2, 1 if strong else 2, 9, removed),
        'WoundDrawRemaining': 3 if strong else 2, 'FirstWoundIgnored': False,
        'crystal:Red': 1, 'crystal:White': 1, 'InBattle': False,
        'MeleePool': 0, 'RangedPool': 0, 'BlockPool': 0,
    }
    defense_state = {
        **_surge_cards(9, remaining_draw, 2, wounds, 13, removed),
        'WoundDrawRemaining': 0, 'FirstWoundIgnored': False, 'BattleWoundsReceived': 4,
        'IsKnockedOut': False, 'MeleePool': 0, 'BlockPool': 0, 'CombatPhase': 'Melee',
        'crystal:Red': 1, 'crystal:White': 1,
    }
    final = {
        **_surge_cards(7, remaining_draw, 4, wounds, 13, removed, ended=True),
        'WoundDrawRemaining': 0, 'FirstWoundIgnored': False,
        'crystal:White': 1, 'crystal:Red': 0,
        'MeleePool': 0, 'RangedPool': 0, 'BlockPool': 0, 'Fame': 4,
        'InBattle': False, 'CombatPhase': 'End',
    }
    expected = {**final, **{f'played:{key}': value for key, value in initial.items()}}
    ops = []

    def step(operation, state):
        index = len(ops)
        ops.append(operation)
        expected.update(_consumption_state(index, {'success': True, **state}))
        return index

    step(dict(kind='PlayCard', cardId='advanced_card_041', enhanced=False), self_state)
    step(dict(kind='TurnBegin'), {**self_state, 'InBattle': True, 'CombatPhase': 'Ranged',
                                 'BattleWoundsReceived': 0, 'IsKnockedOut': False})
    step(dict(kind='TurnRanged', option=1), {**self_state, 'InBattle': True,
                                          'CombatPhase': 'Block', 'kills': 0})
    capture = step(dict(kind='TurnDefense', targetIndex=0), defense_state)
    expected[f'operation:{capture}:wounds'] = 4
    step(dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
         {**_surge_cards(8, remaining_draw, 3, wounds, 13, removed),
          'crystal:Red': 0, 'crystal:White': 1, 'MeleePool': 4,
          'WoundDrawRemaining': 0, 'FirstWoundIgnored': False, 'IsKnockedOut': False})
    step(dict(kind='PlayCard', cardId='basic_card_016', enhanced=False, option=0),
         {**_surge_cards(7, remaining_draw, 4, wounds, 13, removed), 'MeleePool': 6,
          'WoundDrawRemaining': 0, 'FirstWoundIgnored': False})
    step(dict(kind='TurnAttack', attackTargets=[0, 1]),
         {**_surge_cards(7, remaining_draw, 4, wounds, 13, removed),
          'printed': 6, 'effective': 6, 'required': 6, 'kills': 2, 'Fame': 4, 'MeleePool': 0})
    step(dict(kind='TurnFinish'),
         {**_surge_cards(7, remaining_draw, 4, wounds, 13, removed),
          'InBattle': False, 'CombatPhase': 'End', 'Fame': 4})
    step(dict(kind='EndTurn', refill=False), final)
    fields = {'selectedActionManaColor': 'Green'} if strong else {}
    basic_acceptance_case('advanced_card_042', strong,
        '首张实际新伤移除并抽牌，后续三次触发，抽到原牌真实联攻' if strong
        else '原041自伤与真实防御伤只触发前三次抽牌，原狂怒决心联攻',
        expected, ops, setup={'DayPart': 'Day', 'InBattle': False, 'token:Green': int(strong)},
        extraHandCards=['w', 'advanced_card_041'], initialDrawCards=_surge_draw,
        unitIds=[], unitWounds=0,
        removeIndex=-1, discardIndex=-1, discardIndices=[],
        enemies=[dict(id='monster_032', originalDefinition=True) for _ in range(2)],
        captureAfterOperations=[capture], **fields)


"""Four Cold Stare primary scenes; normally advanced_card_043_02..05.

Proposed thin input DTO (root to confirm): selectedEnemyIndex,
selectedEnemyAttackIndex. Resolve these against the actual current context.Enemies
before playing the source; do not create a new Monster as the selected target.
Formal context fields assumed: SelectedEnemy / SelectedEnemyAttackIndex.
Existing selectedActionManaColor handles Blue-or-White payment.
TurnDefense needs the existing printed/effective/required recording for043.
No new damage command, no extra scene for the backend immunity rejection.
"""


def _stare_zones(ids, played, wounds=0, ended=False):
    return {
        'hand': len(ids) - played + wounds, 'draw': 0, 'Wounds': wounds, 'handWounds': wounds,
        'cards:played': 0 if ended else played, 'discard': played if ended else 0,
        'cards:removed': 0, 'basic:total': len(ids) + wounds, 'basic:unique': len(ids) + wounds,
        **{f'basic:zone{i}': ('discard' if ended else 'played') if i < played else 'hand'
           for i in range(len(ids))},
    }


for combat in (False, True):
    for strong in (False, True):
        ids = ['advanced_card_043'] + (
            (['basic_card_021'] if strong else ['basic_card_016', 'basic_card_021', 'basic_card_021'])
            if combat else ([] if strong else ['basic_card_008']))
        wounds = (2 if strong else 3) if combat else 0
        fame = (2 if strong else 8) if combat else 0
        setup = {'DayPart': 'Day', 'InBattle': combat}
        fields = dict(extraHandCards=ids[1:], initialDrawCards=[], unitIds=['army_2_006'], unitWounds=0,
                      removeIndex=-1, discardIndex=-1, discardIndices=[])
        if strong:
            color = 'Blue' if combat else 'White'
            setup[f'token:{color}'] = 1
            fields['selectedActionManaColor'] = color
        if combat:
            setup['token:Blue'] = 1
            setup['token:Red'] = 1 if strong else 2
            fields.update(combatPhase='Block', turnMachineStartPhase='Block',
                          selectedEnemyIndex=0, selectedEnemyAttackIndex=0,
                          enemies=[dict(id=enemy, originalDefinition=True)
                                   for enemy in (['monster_058', 'monster_032'] if strong else ['monster_001'])])
        else:
            setup['player:TempCommandSlots'] = 1
            fields['recruitOffer'] = ['army_2_006']
        if combat: fields['unitIds'] = []
        played = 1
        initial = {**_stare_zones(ids, played), 'InfluencePool': 0 if combat else 5 if strong else 3,
                   'MeleePool': 0, 'BlockPool': 0, 'RangedPool': 0,
                   'token:White': 0, 'token:Blue': int(combat and not strong),
                   'token:Red': (1 if strong else 2) if combat else 0}
        final = {**_stare_zones(ids, len(ids), wounds, ended=True), 'Fame': fame, 'Reputation': 0,
                 'InfluencePool': 0, 'MeleePool': 0, 'BlockPool': 0, 'RangedPool': 0,
                 'InBattle': False}
        expected = {**final, **{f'played:{key}': value for key, value in initial.items()}}
        if combat:
            expected.update({'played:cold:suppress': not strong, 'played:cold:skip': strong,
                             'cold:suppress': False, 'cold:skip': False})
        ops = []

        def step(operation, state):
            index = len(ops)
            ops.append(operation)
            expected.update(_consumption_state(index, {'success': True, **state}))
            return index

        if not combat:
            if not strong:
                played += 1
                step(dict(kind='PlayCard', cardId='basic_card_008', enhanced=False),
                     {**_stare_zones(ids, played), 'InfluencePool': 5})
            step(dict(kind='BeginInteraction', recruitLocation='Village'),
                 {'InNegotiation': True, 'InfluencePool': 5, 'Reputation': 0})
            capture = step(dict(kind='Recruit', targetIndex=0, recruitLocation='Village'),
                           {**_stare_zones(ids, played), 'cost': 4, 'unitId': 'army_2_006', 'units': 2,
                            'offer': '', 'InfluencePool': 1, 'Fame': 0, 'Reputation': 0,
                            'recruit:owned': 'army_2_006,army_2_006', 'recruit:ready': 'False,True'})
            final.update({'recruit:offer': '', 'recruit:owned': 'army_2_006,army_2_006',
                          'recruit:ready': 'False,True'})
            label = '白色择一付款得影响力5，正式招募原农民' if strong else '影响力3联合原承诺，正式招募原农民'
        else:
            if not strong:
                played += 1
                step(dict(kind='PlayCard', cardId='basic_card_016', enhanced=True, option=1),
                     {**_stare_zones(ids, played), 'token:Blue': 0, 'BlockPool': 5,
                      'InfluencePool': 0, 'MeleePool': 0})
            capture = step(dict(kind='TurnDefense', targetIndex=1 if strong else 0),
                           {**_stare_zones(ids, played, wounds), 'BlockPool': 0, 'MeleePool': 0,
                            'CombatPhase': 'Melee', 'BattleWoundsReceived': wounds, 'IsKnockedOut': False})
            expected[f'operation:{capture}:wounds'] = wounds
            if not strong:
                expected.update({f'operation:{capture}:blockPrinted': 5,
                                 f'operation:{capture}:blockEffective': 2,
                                 f'operation:{capture}:blockRequired': 6})
            for n in range(1 if strong else 2):
                played += 1
                step(dict(kind='PlayCard', cardId='basic_card_021', enhanced=True),
                     {**_stare_zones(ids, played, wounds), 'MeleePool': 4 * (n + 1),
                      'token:Red': 0 if strong else 1 - n})
            attack = 4 if strong else 8
            step(dict(kind='TurnAttack', targetIndex=0),
                 {**_stare_zones(ids, played, wounds), 'printed': attack, 'effective': attack,
                  'required': attack, 'kills': 1, 'Fame': fame, 'MeleePool': 0})
            step(dict(kind='TurnFinish'),
                 {**_stare_zones(ids, played, wounds), 'InBattle': False, 'CombatPhase': 'End', 'Fame': fame})
            final['CombatPhase'] = 'End'
            label = '只停所选原058两次攻击，其余敌人正常伤2后原狂怒实杀' if strong else '去原火焰敌人毒素但保留火色，低效格挡伤3且无毒弃伤，原双狂怒实杀'
        expected.update(final)
        step(dict(kind='EndTurn'), final)
        basic_acceptance_case('advanced_card_043', strong, label, expected, ops,
                              option=int(combat), setup=setup, captureAfterOperations=[capture], **fields)


"""Three pending Mind Read / Mind Steal scene drafts, no execution.

Production context contract proposed with backend draft:
  MindReadChoices[actualOtherPlayer] = actualHandCard / null only if no matching card;
  MindReadRevealedHands[actualOtherPlayer] = actual hand reference snapshot;
  MindStealTarget = selected actual Action card / null to decline.
All input selections validated as one transaction before printed payment.

Minimal DTO: mindReadDiscardIndices:int[] (one per otherPlayers; -1 reveal),
mindStealPlayerIndex:int=-1 (decline, or select that player's chosen discard).
Resolve these indices ONCE against existing actual initial other hand instances.
Reuse OtherPlayer.extraHandCards, PlayCard, Move, EndTurn, EndRound. Do not create
a card when selecting, stealing or using the stolen hand card.

Minimal observation additions (read only):
 other:i:handIds / discardIds: current ordered original IDs;
 mind:revealed:i: IDs in actual effect-produced reveal snapshot, checked at played;
 mind:choice:i:zone: selected reference's current actual owner/zone, e.g.
   other:0:discard, self:hand, self:played, self:draw;
 mind:total / unique / identity: union of actual hand,played,draw,discard,removed
   across self and others matches saved INITIAL all-player reference set exactly.
Keep controller's saved chosen references for observation after EndTurn cleanup.
Do not infer ownership from matching ID. No randomized draw order assertion.

UI must expose selected other card / verified revealed hand, and optional theft
selection/decline; normal source art remains original magic_018. Each scene has
one intermediate capture after using gained green crystal plus normal final.
"""

_mind_common = dict(
    removeIndex=-1, discardIndex=-1, discardIndices=[],
    unitIds=[], unitWounds=0,
    destinationTerrain='Plains', revealedDistance=2,
    mapTerrains=[dict(q=q, r=0, terrain='Plains') for q in range(3)],
)
_mind_consume_ops = [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
    dict(kind='Move', q=1, r=0), dict(kind='Move', q=2, r=0), dict(kind='EndTurn')]

# Base: actual spell at A index1 (not first card and not A's other green action),
# B proves absence by revealing its entire red/blue hand. Own March consumes crystal.
_mind_base_final = _harvest_cards(2, 0, 2, 0, ['discard', 'discard'])
basic_acceptance_case(
    'magic_018', False, '选绿实际弃指定同色法术，无同色玩家展示原手牌，所得晶付行进',
    {
        **_mind_base_final, 'draw': 0, 'MovementPool': 0, 'position:q': 2, 'position:r': 0,
        'mind:total': 7, 'mind:unique': 7, 'mind:identity': True,
        'mind:choice:0:zone': 'other:0:discard',
        'other:0:hand': 2, 'other:0:discard': 1,
        'other:0:handIds': 'basic_card_021,basic_card_000', 'other:0:discardIds': 'magic_005',
        'other:1:hand': 2, 'other:1:discard': 0,
        'other:1:handIds': 'basic_card_021,basic_card_016', 'other:1:discardIds': '',
        'played:token:White': 0, 'played:crystal:Green': 1,
        'played:mind:revealed:1': 'basic_card_021,basic_card_016',
        'played:mind:choice:0:zone': 'other:0:discard',
        'played:mind:identity': True,
        **{'played:' + k: v for k, v in _harvest_cards(2, 1, 0, 1, ['played', 'hand']).items()},
        **_consumption_state(0, {'success': True, 'crystal:Green': 0, 'spent:Green': 1,
            'MovementPool': 4, 'mind:identity': True,
            **_harvest_cards(2, 2, 0, 0, ['played', 'played'])}),
        **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
        **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
        **_consumption_state(3, {'success': True, 'mind:identity': True, **_mind_base_final}),
    }, _mind_consume_ops,
    setup={'DayPart': 'Day', 'InBattle': False, 'token:White': 1, 'CurrentTerrain': 'Plains'},
    option=2, extraHandCards=['basic_card_000'],
    otherPlayers=[dict(extraHandCards=['basic_card_021', 'magic_005', 'basic_card_000']),
                  dict(extraHandCards=['basic_card_021', 'basic_card_016'])],
    mindReadDiscardIndices=[1, -1], mindStealPlayerIndex=-1,
    captureAfterOperations=[0], **_mind_common,
)

# Strong selection: A's green Tranquility stays theirs; explicitly steal B's
# green March. Only source belongs to saved self fixture at initial setup.
basic_acceptance_case(
    'magic_018', True, '显式偷第二位玩家本次原行进，所得晶实际支付，轮末永久归属守恒',
    {
        **_harvest_cards(2, 0, 0, 0, ['draw']), 'draw': 2,
        'MovementPool': 0, 'position:q': 2, 'position:r': 0,
        'mind:total': 5, 'mind:unique': 5, 'mind:identity': True,
        'mind:choice:0:zone': 'other:0:draw', 'mind:choice:1:zone': 'self:draw',
        'other:0:handIds': 'basic_card_021', 'other:0:discardIds': '',
        'other:1:handIds': 'basic_card_016', 'other:1:discardIds': '',
        'played:token:White': 0, 'played:token:Black': 0, 'played:crystal:Green': 1,
        'played:mind:choice:0:zone': 'other:0:discard', 'played:mind:choice:1:zone': 'self:hand',
        'played:mind:identity': True, 'played:hand:ids': 'basic_card_000',
        **{'played:' + k: v for k, v in _harvest_cards(2, 1, 0, 1, ['played']).items()},
        **_consumption_state(0, {'success': True, 'crystal:Green': 0, 'spent:Green': 1,
            'MovementPool': 4, 'mind:choice:1:zone': 'self:played', 'mind:identity': True,
            **_harvest_cards(2, 2, 0, 0, ['played'])}),
        **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
        **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
        **_consumption_state(3, {'success': True, 'mind:choice:1:zone': 'self:discard',
            'mind:choice:0:zone': 'other:0:discard', 'mind:identity': True,
            **_harvest_cards(2, 0, 2, 0, ['discard'])}),
        **_consumption_state(4, {'success': True, 'mind:choice:1:zone': 'self:draw',
            'mind:choice:0:zone': 'other:0:draw', 'mind:identity': True,
            'draw': 2, 'discard': 0, 'basic:zone0': 'draw'}),
    }, _mind_consume_ops + [dict(kind='EndRound')],
    setup={'DayPart': 'Night', 'InBattle': False, 'token:White': 1, 'token:Black': 1,
           'CurrentTerrain': 'Plains'}, option=2, useRoundEngine=True,
    extraHandCards=[],
    otherPlayers=[dict(extraHandCards=['basic_card_021', 'basic_card_001']),
                  dict(extraHandCards=['basic_card_016', 'basic_card_000'])],
    mindReadDiscardIndices=[1, 1], mindStealPlayerIndex=1,
    captureAfterOperations=[0], **_mind_common,
)

# Strong explicit decline leaves both opponents' selected card instances in
# their discard piles; the spell must never be offered as an eligible theft.
_mind_decline_final = _harvest_cards(2, 0, 2, 0, ['discard', 'discard'])
basic_acceptance_case(
    'magic_018', True, '强化明确放弃偷取，原行动与法术各留原主弃牌，所得晶仍可实际消费',
    {
        **_mind_decline_final, 'draw': 0, 'MovementPool': 0, 'position:q': 2, 'position:r': 0,
        'mind:total': 4, 'mind:unique': 4, 'mind:identity': True,
        'mind:choice:0:zone': 'other:0:discard', 'mind:choice:1:zone': 'other:1:discard',
        'other:0:handIds': '', 'other:0:discardIds': 'basic_card_000',
        'other:1:handIds': '', 'other:1:discardIds': 'magic_005',
        'played:token:White': 0, 'played:token:Black': 0, 'played:crystal:Green': 1,
        'played:hand:ids': 'basic_card_000', 'played:mind:identity': True,
        'played:mind:choice:0:zone': 'other:0:discard',
        'played:mind:choice:1:zone': 'other:1:discard',
        **{'played:' + k: v for k, v in _harvest_cards(2, 1, 0, 1, ['played', 'hand']).items()},
        **_consumption_state(0, {'success': True, 'crystal:Green': 0, 'spent:Green': 1,
            'MovementPool': 4, 'mind:identity': True,
            **_harvest_cards(2, 2, 0, 0, ['played', 'played'])}),
        **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 1}),
        **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 2}),
        **_consumption_state(3, {'success': True, 'mind:identity': True, **_mind_decline_final}),
    }, _mind_consume_ops,
    setup={'DayPart': 'Night', 'InBattle': False, 'token:White': 1, 'token:Black': 1,
           'CurrentTerrain': 'Plains'}, option=2,
    extraHandCards=['basic_card_000'],
    otherPlayers=[dict(extraHandCards=['basic_card_000']), dict(extraHandCards=['magic_005'])],
    mindReadDiscardIndices=[0, 0], mindStealPlayerIndex=-1,
    captureAfterOperations=[0], **_mind_common,
)


"""头环3个主场景草稿：未运行，勿自动导入执行生成器。
调用 add_headband_basic_scenes(basic_acceptance_case)。

待root确认/实现的薄接线（不得将这些缺失当作已有验收）：
1. PlayArtifact items_022映射；option仅为实际供应下标，不打包。
   新DTO headbandSkillOption在调用前赋给ctx.HeadbandSkillOption，独立控制原技能选项。
   草稿与tmp/HeadbandBasicStateTests.cs一致，不新增模拟效果。
2. 新DTO skillOfferIndices；按skillPoolId加载一次原技能，清空Offer后依数组
   加入真实对象。不可复用skillIndices，那是初始化玩家已持有技能的字段。
3. 新快照 skillOffer:sources、skills:sources 用逗号连接实际SourceId；
   SkillOffer/Skills计数只作辅助，不能替代身份。后台另查ReferenceEquals。
4. 头环正常选择的原技能也要显示skill_000原图、具体原文和合法选项；
   SkillUse已有操作，焚毁后其目标必须是刚取得对象。
5. played:/operation:N:state:均沿用按expected请求保存已有快照机制。
6. 禁止直接用Reusable决定次数；正常禁止原互动技能。负例留后台就近验证，
   这3个场景只验主要成功链，不重复全技能39例。
7. skills:sources与skillOffer:sources为新快照键；其余资源/位置/牌区键已有。
   草稿未声称一次性覆盖原技能所有合法颜色；暗火此批选红并真实消费。
"""


def add_headband_basic_scenes(basic_acceptance_case):
    pool = "hero_skill_pool_000"

    def add(burn, title, final, played, ops, states, **fields):
        expected = {"Wounds": 0, "handWounds": 0, "cards:played": 0,
                    "cards:removed": int(burn), **final}
        expected.update({"played:" + k: v for k, v in played.items()})
        for index in range(len(ops)):
            expected[f"operation:{index}:success"] = True
        for index, state in states.items():
            expected.update({f"operation:{index}:state:{k}": v for k, v in state.items()})
        setup = {"InBattle": False, "CurrentTerrain": "Plains", **fields.pop("setup", {})}
        fixture = dict(removeIndex=-1, discardIndex=-1, discardIndices=[],
                       extraHandCards=[], destinationTerrain="Plains", revealedDistance=1,
                       skillPoolId=pool, skillIndices=[], headbandSkillOption=0, unitIds=[],
                       initialUnitWounds=[], captureAfterOperations=[])
        fixture.update(fields)
        basic_acceptance_case("items_022", burn, title, expected, ops, setup=setup, **fixture)

    add(False, "公共原黑暗道路白昼执行两次，支付平原移动并弃头环", {
        "MovementPool": 0, "position:q": 1, "position:r": 0, "hand": 0, "discard": 1,
        "basic:zone0": "discard", "basic:total": 1, "basic:unique": 1,
        "skillOffer": 2, "skillOffer:sources": pool + ":09," + pool + ":00", "skills:sources": "",
    }, {
        "MovementPool": 2, "position:q": 0, "cards:played": 1, "basic:zone0": "played",
        "skillOffer": 2, "skillOffer:sources": pool + ":09," + pool + ":00", "skills:sources": "",
    }, [dict(kind="Move", q=1, r=0), dict(kind="EndTurn")], {
        0: {"MovementPool": 0, "position:q": 1, "position:r": 0, "basic:zone0": "played"},
        1: {"cards:played": 0, "discard": 1, "basic:zone0": "discard"},
    }, option=1, skillOfferIndices=[9, 0], captureAfterOperations=[0])

    add(False, "公共原暗火魔法仅一次，实际支付红标记发动原狂怒", {
        "crystal:Red": 1, "token:Red": 0, "MeleePool": 0, "hand": 0, "discard": 2,
        "basic:zone0": "discard", "basic:zone1": "discard", "basic:total": 2, "basic:unique": 2,
        "skillOffer": 2, "skillOffer:sources": pool + ":00," + pool + ":04", "skills:sources": "",
    }, {
        "crystal:Red": 1, "token:Red": 1, "token:Black": 0, "MeleePool": 0,
        "cards:played": 1, "hand": 1, "basic:zone0": "played", "basic:zone1": "hand",
        "skillOffer:sources": pool + ":00," + pool + ":04", "skills:sources": "",
    }, [dict(kind="BeginBattle"), dict(kind="PlayCard", cardId="basic_card_021", enhanced=True),
        dict(kind="EndBattle"), dict(kind="EndTurn")], {
        1: {"crystal:Red": 1, "token:Red": 0, "MeleePool": 4, "cards:played": 2,
            "basic:zone0": "played", "basic:zone1": "played"},
        2: {"InBattle": False, "MeleePool": 0},
        3: {"cards:played": 0, "discard": 2, "basic:zone0": "discard", "basic:zone1": "discard"},
    }, option=1, skillOfferIndices=[0, 4], extraHandCards=["basic_card_021"], captureAfterOperations=[1])

    add(True, "焚毁取得公共原黑暗道路，再正式发动并移动，跨回合持有", {
        "MovementPool": 0, "position:q": 1, "position:r": 0, "hand": 0, "discard": 0,
        "basic:zone0": "removed", "basic:total": 1, "basic:unique": 1,
        "skillOffer": 1, "skillOffer:sources": pool + ":04", "skills:sources": pool + ":00",
    }, {
        "MovementPool": 0, "position:q": 0, "cards:played": 0, "cards:removed": 1,
        "basic:zone0": "removed", "skillOffer": 1, "skillOffer:sources": pool + ":04",
        "skills:sources": pool + ":00",
    }, [dict(kind="SkillUse", targetIndex=0, option=0), dict(kind="Move", q=1, r=0),
        dict(kind="EndTurn"), dict(kind="StartTurn")], {
        0: {"MovementPool": 2, "skills:sources": pool + ":00", "basic:zone0": "removed"},
        1: {"MovementPool": 0, "position:q": 1, "position:r": 0},
        2: {"cards:removed": 1, "discard": 0, "skills:sources": pool + ":00"},
        3: {"skills:sources": pool + ":00", "basic:zone0": "removed"},
    }, option=1, setup={"DayPart": "Night"}, skillOfferIndices=[4, 0], captureAfterOperations=[1])

add_headband_basic_scenes(basic_acceptance_case)
for _hb in CASES[-3:]:
    _hb['expected'] += values({'headband:identity':True,'played:headband:identity':True,
        'headband:selectedZone':'本人技能' if _hb['enhanced'] else '公共供应'})
# Dark Fire's actual red token strengthens original Fury; spend its attack against the original skeleton.
_hb=CASES[-2]
_hb['enemies']=[dict(id='monster_032',originalDefinition=True)]
_hb['title'] += '，实际击杀原兽人骷髅战士'
_hb['operations'].insert(2,dict(kind='Attack',targetIndex=0))
for _v in _hb['expected']:
    for _old in (3,2):
        _prefix=f'operation:{_old}:'
        if _v['key'].startswith(_prefix):
            _v['key']=f'operation:{_old+1}:'+_v['key'][len(_prefix):];break
_hb['expected'] += values({'Fame':2,'operation:2:success':True,'operation:2:printed':4,
    'operation:2:effective':4,'operation:2:required':3,'operation:2:kills':1,'operation:2:fame':2,
    'operation:2:state:MeleePool':0,'operation:2:state:Fame':2})


"""Four original magic_016 main scene drafts. Do not execute on import.
Use existing basic_acceptance_case and _consumption_state helpers.
New operations/DTO follow root's already agreed thin adapters:
 useTurnEngine only for strong: real GameEngine StartNewRound+BeginNextTurn once;
 EngineEndTurn always requests normal refill, letting the card itself skip it;
 EngineNextTurn.playerIndex selects persistent context (0 self / 1 other), while
 actual player/id is selected by the engine and recorded as operation:N:id.
 DiscardHandExact / RemoveHandExact.targetIndex indexes initial own card refs.
 ExploreActual records cost/tileId/tileSame/countryCount as native operation keys.
 exploreTileIds=[77] creates actual TileDeck reference; q2 must initially be empty.
 RampagingDefense/Attack/Finish consume the real movement-created encounter.
 No fabricated start-turn loop, copied cards, monster death or tile placements.
"""

def _warp_cards(total, played, discard, hand, draw, zones, removed=0, aside=0, wounds=0):
    return {'basic:total': total, 'basic:unique': total,
            'cards:played': played, 'discard': discard, 'hand': hand, 'draw': draw,
            'cards:removed': removed, 'basic:removed': removed, 'basic:aside': aside,
            'Wounds': wounds, 'handWounds': wounds,
            **{f'basic:zone{i}': zone for i, zone in enumerate(zones)}}

_warp_common = dict(unitIds=['army_2_006'], unitWounds=0,
    removeIndex=-1, discardIndex=-1, discardIndices=[], destinationTerrain='Plains')

# 1. Two paid leaps over actual mountain/lake. A separate initial March proves
# the next-turn two-space move is rejected without spending its actual Move2.
_warp_leap_final = _warp_cards(3, 1, 2, 0, 0, ['discard', 'discard', 'played'])
basic_acceptance_case('magic_016', False, '蓝费空间扭曲两次跨二格各付平原二，次回合相同距离拒绝不扣费',
    {**_warp_leap_final, 'MovementPool': 2, 'SpaceBendingActive': False,
     'IgnoreRampaging': False, 'position:q': 4, 'position:r': 0,
     'played:token:Blue': 0, 'played:token:Green': 1,
     'played:MovementPool': 0, 'played:SpaceBendingActive': True, 'played:IgnoreRampaging': True,
     **_consumption_state(0, {'success': True, 'token:Green': 0, 'MovementPool': 4,
         **_warp_cards(3, 2, 0, 1, 0, ['played', 'played', 'hand'])}),
     **_consumption_state(1, {'success': True, 'cost': 2, 'MovementPool': 2, 'position:q': 2}),
     **_consumption_state(2, {'success': True, 'cost': 2, 'MovementPool': 0, 'position:q': 4}),
     **_consumption_state(3, {'success': True, 'SpaceBendingActive': False, 'IgnoreRampaging': False,
         **_warp_cards(3, 0, 2, 1, 0, ['discard', 'discard', 'hand'])}),
     **_consumption_state(4, {'success': True, 'SpaceBendingActive': False}),
     **_consumption_state(5, {'success': True, 'MovementPool': 2, **_warp_leap_final}),
     **_consumption_state(6, {'success': False, 'MovementPool': 2, 'position:q': 4})},
    [dict(kind='PlayCard', cardId='basic_card_000', enhanced=True),
     dict(kind='Move', q=2, r=0), dict(kind='Move', q=4, r=0), dict(kind='EndTurn'),
     dict(kind='StartTurn'), dict(kind='PlayCard', cardId='basic_card_000'), dict(kind='Move', q=6, r=0)],
    setup={'DayPart': 'Day', 'InBattle': False, 'token:Blue': 1, 'token:Green': 1},
    extraHandCards=['basic_card_000', 'basic_card_000'], revealedDistance=6,
    mapTerrains=[dict(q=i,r=0,terrain=t) for i,t in enumerate(
        ['Plains','Mountain','Plains','Lake','Plains','Mountain','Plains'])],
    captureAfterOperations=[2], **_warp_common)

# 2. Existing source and March provide real blue fee / Move2; actual distant
# exploration removes tile77 from its supply, places that same reference, leaves hero at origin.
_warp_explore_final = _warp_cards(2, 0, 2, 0, 0, ['discard', 'discard'])
basic_acceptance_case('magic_016', False, '距二格实际探索付二移动，原供给板块77同实例入地图且玩家不移动',
    {**_warp_explore_final, 'MovementPool': 0, 'position:q': 0, 'position:r': 0,
     'warp:exploredIds': '77', 'warp:tilesSame': True, 'SpaceBendingActive': False,
     'played:token:Blue': 0, 'played:MovementPool': 0, 'played:SpaceBendingActive': True,
     **_consumption_state(0, {'success': True, 'MovementPool': 2,
         **_warp_cards(2, 2, 0, 0, 0, ['played', 'played'])}),
     'operation:1:cost': 2, 'operation:1:tileId': 77,
     'operation:1:tileSame': True, 'operation:1:countryCount': 0,
     **_consumption_state(1, {'success': True, 'MovementPool': 0, 'position:q': 0,
         'warp:exploredIds': '77', 'warp:tilesSame': True}),
     **_consumption_state(2, {'success': True, **_warp_explore_final})},
    [dict(kind='PlayCard',cardId='basic_card_000'), dict(kind='ExploreActual',q=2,r=0,option=0),
     dict(kind='EndTurn')],
    setup={'DayPart':'Day','InBattle':False,'token:Blue':1}, extraHandCards=['basic_card_000'],
    exploreTileIds=[77], revealedDistance=1,
    mapTerrains=[dict(q=0,r=0,terrain='Plains'),dict(q=1,r=0,terrain='Plains')],
    captureAfterOperations=[1], **_warp_common)

# 3. The same adjacent move near original monster032 is harmless this turn and
# creates a real encounter next turn. Strong March uses initial green crystal
# then Fury uses initial red crystal; those crystals survive the first EndTurn.
_warp_rampage_final = _warp_cards(6, 0, 4, 2, 0, ['discard']*4, wounds=2)
basic_acceptance_case('magic_016', False, '本回合不激怒原肆虐敌，次回合同路触发实际承伤与击杀',
    {**_warp_rampage_final, 'MovementPool':0, 'MeleePool':0, 'Fame':2, 'Reputation':1,
     'position:q':1,'position:r':0,'InBattle':False,'warp:rampaging':False,'warp:guardAlive':False,
     'SpaceBendingActive':False,'IgnoreRampaging':False,
     'played:token:Blue':0,'played:token:Green':1,'played:crystal:Green':1,'played:crystal:Red':1,
     'played:warp:guardAlive':True,'played:warp:guardId':'monster_032',
     **_consumption_state(0, {'success':True,'MovementPool':4,'token:Green':0}),
     **_consumption_state(1, {'success':True,'cost':2,'MovementPool':2,'position:q':1,
         'InBattle':False,'warp:rampaging':False,'warp:guardAlive':True}),
     **_consumption_state(2, {'success':True,'cost':2,'MovementPool':0,'position:q':0,
         'InBattle':False,'warp:rampaging':False,'warp:guardAlive':True}),
     **_consumption_state(3, {'success':True,'SpaceBendingActive':False,'IgnoreRampaging':False,
         'crystal:Green':1,'crystal:Red':1,
         **_warp_cards(4,0,2,2,0,['discard','discard','hand','hand'])}),
     **_consumption_state(4, {'success':True,'TurnActionCommitted':False}),
     **_consumption_state(5, {'success':True,'MovementPool':4,'crystal:Green':0,'spent:Green':1}),
     **_consumption_state(6, {'success':True,'cost':2,'MovementPool':2,'position:q':1,
         'InBattle':True,'TurnActionCommitted':True,'warp:rampaging':True,
         'warp:guardAlive':True,'warp:guardId':'monster_032','CombatPhase':'Ranged'}),
     **_consumption_state(7, {'success':False,'MovementPool':2,'position:q':1}),
     'operation:8:wounds':2,
     **_consumption_state(8, {'success':True,'Wounds':2,'handWounds':2,'CombatPhase':'Melee',
         **_warp_cards(6,1,2,3,0,['discard','discard','played','hand'],wounds=2)}),
     **_consumption_state(9, {'success':True,'crystal:Red':0,'spent:Red':1,'MeleePool':4,
         **_warp_cards(6,2,2,2,0,['discard','discard','played','played'],wounds=2)}),
     **_consumption_state(10, {'success':True,'printed':4,'effective':4,'required':3,'kills':1,
         'Fame':2,'MeleePool':0}),
     **_consumption_state(11, {'success':True,'warp:guardAlive':False,'warp:rampaging':False,
         'InBattle':False,'Reputation':1,'Fame':2,'CombatPhase':'End'}),
     **_consumption_state(12, {'success':False,'position:q':1,'MovementPool':2}),
     **_consumption_state(13, {'success':True,'MovementPool':0,**_warp_rampage_final})},
    [dict(kind='PlayCard',cardId='basic_card_000',enhanced=True),
     dict(kind='Move',q=1,r=0),dict(kind='Move',q=0,r=0),dict(kind='EndTurn'),dict(kind='StartTurn'),
     dict(kind='PlayCard',cardId='basic_card_000',enhanced=True),dict(kind='Move',q=1,r=0),
     dict(kind='Move',q=0,r=0),dict(kind='RampagingDefense'),
     dict(kind='PlayCard',cardId='basic_card_021',enhanced=True),
     dict(kind='RampagingAttack',targetIndex=0),dict(kind='RampagingFinish'),
     dict(kind='Move',q=0,r=0),dict(kind='EndTurn')],
    setup={'DayPart':'Day','InBattle':False,'token:Blue':1,'token:Green':1,
           'crystal:Green':1,'crystal:Red':1},
    extraHandCards=['basic_card_000','basic_card_000','basic_card_021'],revealedDistance=1,
    mapTerrains=[dict(q=0,r=0,terrain='Plains'),dict(q=1,r=0,terrain='Plains')],
    mapPlaces=[dict(q=0,r=1,type='OrcRampaging',enemy=True,
                    enemyDefinition=dict(id='monster_032',originalDefinition=True))],
    captureAfterOperations=[6,11], **_warp_common)

# 4. Strong card source is played by the scenario before operations. Returned
# order is March then Promise, because those are the only other actual Played.
_warp_engine_final = _warp_cards(7,0,0,2,4,['draw','draw','draw','draw','removed'],removed=1)
basic_acceptance_case('magic_016', True, '正式引擎A到A到B，已打出原卡返手本卡暂存，轮末同实例恢复',
    {**_warp_engine_final,'MovementPool':0,'InfluencePool':0,'ExtraTurn':False,
     'position:q':2,'position:r':0,'warp:turnOrder':'1,1,2','warp:asideIds':'','warp:asideCount':0,
     'hand:ids':'basic_card_007,basic_card_014','removed:ids':'basic_card_016',
     'played:token:Blue':0,'played:token:Black':0,'played:ExtraTurn':True,'played:SkipDraw':True,
     'played:warp:turnOrder':'1','played:draw:ids':'basic_card_007,basic_card_014',
     **{'played:'+k:v for k,v in _warp_cards(7,1,0,4,2,['played','hand','hand','hand','hand']).items()},
     **_consumption_state(0, {'success':True,'MovementPool':2}),
     **_consumption_state(1, {'success':True,'cost':2,'position:q':1,'MovementPool':0}),
     **_consumption_state(2, {'success':True,'InfluencePool':2}),
     **_consumption_state(3, {'success':True,'basic:zone3':'discard','discard':1}),
     **_consumption_state(4, {'success':True,'basic:zone4':'removed','cards:removed':1}),
     **_consumption_state(5, {'success':True,'ExtraTurn':False,'InfluencePool':0,
         'warp:asideIds':'magic_016','warp:asideCount':1,'hand:ids':'basic_card_000,basic_card_008',
         'draw:ids':'basic_card_007,basic_card_014',
         **_warp_cards(7,0,1,2,2,['aside','hand','hand','discard','removed'],removed=1,aside=1)}),
     'operation:6:id':1,
     **_consumption_state(6, {'success':True,'warp:turnOrder':'1,1','TurnActionCommitted':False}),
     **_consumption_state(7, {'success':True,'MovementPool':2,'basic:zone1':'played'}),
     **_consumption_state(8, {'success':True,'cost':2,'position:q':2,'MovementPool':0}),
     **_consumption_state(9, {'success':True,'InfluencePool':2,'basic:zone2':'played'}),
     **_consumption_state(10, {'success':True,'ExtraTurn':False,'InfluencePool':0,
         'hand:ids':'basic_card_007,basic_card_014','draw:ids':'','warp:asideCount':1,
         **_warp_cards(7,0,3,2,0,['aside','discard','discard','discard','removed'],removed=1,aside=1)}),
     'operation:11:id':2,
     **_consumption_state(11, {'success':True,'warp:turnOrder':'1,1,2'}),
     **_consumption_state(12, {'success':True,'warp:turnOrder':'1,1,2'}),
     **_consumption_state(13, {'success':True,'warp:asideCount':0,'warp:asideIds':'',**_warp_engine_final})},
    [dict(kind='PlayCard',cardId='basic_card_000'),dict(kind='Move',q=1,r=0),
     dict(kind='PlayCard',cardId='basic_card_008'),dict(kind='DiscardHandExact',targetIndex=3),
     dict(kind='RemoveHandExact',targetIndex=4),dict(kind='EngineEndTurn'),
     dict(kind='EngineNextTurn',playerIndex=0),dict(kind='PlayCard',cardId='basic_card_000'),
     dict(kind='Move',q=2,r=0),dict(kind='PlayCard',cardId='basic_card_008'),dict(kind='EngineEndTurn'),
     dict(kind='EngineNextTurn',playerIndex=1),dict(kind='EngineEndTurn'),dict(kind='EndRound')],
    setup={'DayPart':'Night','InBattle':False,'token:Blue':1,'token:Black':1},
    useTurnEngine=True,useRoundEngine=True,extraHandCards=['basic_card_000','basic_card_008','basic_card_001','basic_card_016'],
    initialDrawCards=['basic_card_007','basic_card_014'],otherPlayers=[dict(unitIds=[])],revealedDistance=2,
    mapTerrains=[dict(q=q,r=0,terrain='Plains') for q in range(3)],
    captureAfterOperations=[5,10], **_warp_common)


"""神秘宝盒4个基本场景草稿；未执行，未导入生成器。

集成入口：add_mystic_box_basic_scenes(basic_acceptance_case)。
所有case.enhanced=False表示实际揭示盒；只有UseMysticBox操作enhanced=True才借原神器焚毁。

最薄适配约定（新字段/键均在此显式列明，不声称当前已经存在）：
1. 唯一新增case DTO字段 artifactSupplyIds，按顺序new ArtifactCard并SetDeck。
   保存两个实际对象用于身份观察。不要用被测结果推导expected或重新new对象回供应。
2. UseMysticBox复用现有operation DTO：enhanced=借用焚毁，option=原神器选项；
   本场源盒始终_artifactFixtureInstances[0]；targetIndex仅为本场自有部队目标。
   调用CardPlayActions.UseMysticBox；赋旗目标从实际_player.Units取，不创建临时原旗。
3. ProbeArtifactBottom仅允许紧随成功EndTurn，且context.MysticBox已清空。
   这是回合末探针，不是要调用EndRound。调用原Supply.Draw两次，之后不再游戏操作。
   operation:N:beforeCount/afterCount来自Count；firstId/secondId来自两个实际抽出对象；
   firstIdentity/secondIdentity分别ReferenceEquals原second/top对象，不能只比较Id。
   success只表示合法执行；顺序/身份由独立expected断言，不能直接设成True常量。
4. 新box快照：
   box:revealedId = pending?.RevealedArtifact.Id ?? ""；box:used = pending?.Used ?? false；
   box:supplyCount = 实际Supply.Count；box:boxZone = 手/已打出/弃/移除/附着实际盒引用的区域，
   字面值hand/played/discard/removed/attached；不能以模式猜牌区。
   box:identity = 同一盒在玩家各牌区+实际附着区恰好一份，且pending若存在，
   它的Box和RevealedArtifact分别等于原盒与原top引用。
   box:bannerEffectId = 实际目标部队.BannerEffectId ?? ""；
   box:bannerIdentity = ReferenceEquals实际目标部队.Banner, 原盒；无旗时False。
   box:recruitedIdentity = 新招募部队.Card与本场原recruitOffer[0]为同一引用。
5. played:/operation:N:state:沿用按expected保存快照机制。招募已有operation:cost、
   influence、unitId、units、offer键可直接复用；不是新增规则。
6. 当前controller强制至少一支初始部队（空unitIds会退到假部队且[0]会被访问）。
   本片段沿用一支耗尽的原农民作为不参与金币包操作的基线，招募后为两支，
   ready=False,True。后台为空初始队伍，但被测招募费用/新供给身份/盒结算相同。
   不为这四例扩大空部队UI重构；招募身份断言区分原基线与真正供给对象。
7. 旧basic快照不统计附着区，所以附旗中途不用basic:total/zone0冒充全区守恒；
   用box:identity/box:boxZone证明附着实物。回合末盒回普通牌区后再断言basic:total=1。

截图最少位置（每个操作仍全部断言，减少的是图片而非步骤）：
- 通用揭示画面：原盒+原揭示神器图/原文、Fame0、供应2→1、实际盒Played。
- 不使用：EndTurn后（op0）看原盒回手、供应2；最终探针看原second→原top身份。
- 金币包两例：Use后op0看4/9影响及Fame3/4；Recruit后op1看真实农民费用4和余额0/5；
  最终画面看原盒弃牌/永久移除及实际供应顺序。Probe会把供应从2抽至0，勿误判为没归还。
- 勇气旗：Use后op0看“盒本体items024/有效勇气旗items003”及未自动重整；
  Move后op3看真实部队已用、移动已付2；最终看卸旗弃盒与供应顺序。
原图使用现有主卡与后续图槽；正常图是items024，揭示图items010/003，后续部队army_2_006。
"""


def add_mystic_box_basic_scenes(basic_acceptance_case):
    def snapshots(expected, played, states):
        expected.update({"played:" + k: v for k, v in played.items()})
        for index, state in states.items():
            expected.update({f"operation:{index}:state:{k}": v for k, v in state.items()})
        return expected

    def probe(expected, index, top):
        expected.update({
            f"operation:{index}:beforeCount": 2,
            f"operation:{index}:afterCount": 0,
            f"operation:{index}:firstId": "items_004",
            f"operation:{index}:secondId": top,
            f"operation:{index}:firstIdentity": True,
            f"operation:{index}:secondIdentity": True,
        })

    def add(title, top, final, played, ops, states, **fields):
        expected = {
            "Wounds": 0, "handWounds": 0, "MovementPool": 0, "InfluencePool": 0,
            "cards:played": 0, "cards:removed": 0, "draw": 0,
            "basic:total": 1, "basic:unique": 1,
            "box:revealedId": "", "box:used": False, "box:supplyCount": 0,
            "box:identity": True, "box:bannerIdentity": False, "box:bannerEffectId": "",
            **final,
        }
        for index in range(len(ops)):
            expected[f"operation:{index}:success"] = True
        snapshots(expected, {
            "Fame": 0, "InfluencePool": 0, "cards:played": 1, "cards:removed": 0,
            "hand": 0, "discard": 0, "basic:zone0": "played",
            "box:revealedId": top, "box:used": False, "box:supplyCount": 1,
            "box:boxZone": "played", "box:identity": True, **played,
        }, states)
        probe(expected, len(ops) - 1, top)
        fixture = dict(
            artifactSupplyIds=[top, "items_004"], removeIndex=-1, discardIndex=-1,
            discardIndices=[], extraHandCards=[], destinationTerrain="Plains", revealedDistance=1,
            unitIds=["army_2_006"], initialUnitWounds=[0], captureAfterOperations=[],
        )
        fixture.update(fields)
        basic_acceptance_case("items_024", False, title, expected, ops,
            setup={"player:Fame": 0, "player:Reputation": 0, "player:ExtraCommandSlots": 1,
                   "InBattle": False, "CurrentTerrain": "Plains"}, **fixture)

    add("揭示原金币包但不使用，盒回手且同一神器回供应底", "items_010", {
        "Fame": 0, "hand": 1, "discard": 0, "basic:zone0": "hand", "box:boxZone": "hand",
    }, {}, [dict(kind="EndTurn"), dict(kind="ProbeArtifactBottom")], {
        0: {"Fame": 0, "hand": 1, "discard": 0, "cards:played": 0,
            "basic:zone0": "hand", "box:boxZone": "hand", "box:revealedId": "",
            "box:used": False, "box:supplyCount": 2, "box:identity": True},
    }, captureAfterOperations=[0])

    # 原图独立预期：金币包普通影响4/名望2，焚毁影响9/名望3；盒只再加名望1。
    # 原农民数据费用4，原始供给实例在Recruit后进入新部队，故余额为0/5。
    for burn, influence, fame, remainder in [(False, 4, 3, 0), (True, 9, 4, 5)]:
        zone = "removed" if burn else "discard"
        used_zone = "removed" if burn else "played"
        ops = [dict(kind="UseMysticBox", enhanced=burn, option=0),
               dict(kind="Recruit", targetIndex=0, recruitLocation="Village"),
               dict(kind="EndTurn"), dict(kind="ProbeArtifactBottom")]
        final = {
            "Fame": fame, "hand": 0, "discard": 0 if burn else 1,
            "cards:removed": int(burn), "basic:zone0": zone, "box:boxZone": zone,
            "recruit:offer": "", "recruit:owned": "army_2_006,army_2_006",
            "units:ready": "False,True", "units:wounds": "0,0", "box:recruitedIdentity": True,
            "operation:1:unitId": "army_2_006", "operation:1:cost": 4,
            "operation:1:influence": remainder, "operation:1:units": 2, "operation:1:offer": "",
        }
        states = {
            0: {"Fame": fame, "InfluencePool": influence, "box:used": True,
                "box:revealedId": "items_010", "box:supplyCount": 1, "box:identity": True,
                "box:boxZone": used_zone, "basic:zone0": used_zone,
                "cards:played": int(not burn), "cards:removed": int(burn)},
            1: {"Fame": fame, "InfluencePool": remainder, "recruit:offer": "",
                "recruit:owned": "army_2_006,army_2_006", "units:ready": "False,True",
                "box:recruitedIdentity": True, "box:boxZone": used_zone, "box:supplyCount": 1},
            2: {"Fame": fame, "InfluencePool": 0, "cards:played": 0,
                "cards:removed": int(burn), "discard": 0 if burn else 1,
                "basic:zone0": zone, "box:boxZone": zone, "box:revealedId": "",
                "box:used": False, "box:supplyCount": 2, "box:identity": True},
        }
        add("借原金币包" + ("焚毁" if burn else "普通") + "实际招募，移动盒本体并归还原供应卡",
            "items_010", final, {"recruit:offer": "army_2_006", "recruit:owned": "army_2_006",
                                 "units:ready": "False"}, ops, states,
            recruitOffer=["army_2_006"], captureAfterOperations=[0, 1])

    add("借原勇气旌旗附盒，重整原农民并支付移动，回合末卸盒弃牌", "items_003", {
        "Fame": 1, "hand": 0, "discard": 1, "basic:zone0": "discard", "box:boxZone": "discard",
        "position:q": 1, "position:r": 0, "units:ready": "False", "units:wounds": "0",
    }, {"units:ready": "False", "position:q": 0, "position:r": 0}, [
        dict(kind="UseMysticBox", targetIndex=0, enhanced=False, option=0),
        dict(kind="UseCourage", targetIndex=0),
        dict(kind="UnitUse", targetIndex=0, option=2),
        dict(kind="Move", q=1, r=0),
        dict(kind="EndTurn"), dict(kind="ProbeArtifactBottom"),
    ], {
        0: {"Fame": 1, "box:used": True, "box:revealedId": "items_003", "box:supplyCount": 1,
            "box:boxZone": "attached", "box:bannerIdentity": True, "box:bannerEffectId": "items_003",
            "box:identity": True, "cards:played": 0, "cards:removed": 0, "discard": 0,
            "units:ready": "False", "MovementPool": 0},
        1: {"units:ready": "True", "box:bannerIdentity": True, "box:bannerEffectId": "items_003",
            "Fame": 1, "MovementPool": 0},
        2: {"units:ready": "False", "MovementPool": 2, "position:q": 0, "position:r": 0,
            "box:bannerIdentity": True, "Fame": 1},
        3: {"units:ready": "False", "MovementPool": 0, "position:q": 1, "position:r": 0,
            "box:boxZone": "attached", "box:bannerIdentity": True, "box:identity": True},
        4: {"Fame": 1, "cards:played": 0, "cards:removed": 0, "discard": 1,
            "basic:zone0": "discard", "box:boxZone": "discard", "box:revealedId": "",
            "box:used": False, "box:supplyCount": 2, "box:bannerIdentity": False,
            "box:bannerEffectId": "", "box:identity": True},
    }, captureAfterOperations=[0, 3])


add_mystic_box_basic_scenes(basic_acceptance_case)

def export():
    target = ROOT / 'Assets/Resources/CardVerification'
    target.mkdir(exist_ok=True)
    write_json(target / 'cases.json', {'cases': CASES})
    counts = {}
    for batch in ['basic_card', 'advanced_card', 'magic', 'items']:
        group = [c for c in CASES if c['batch'] == batch]
        counts[batch] = len(group)
        steps = []
        def expectation(key, value):
            return {'key': key, 'expected': str(value), 'rule': '界面操作完整性；效果通过与否以 effects.json 的独立断言为准'}
        for i, c in enumerate(group):
            if i:
                steps.append(dict(label=c['id'] + '_prepare', buttonPath='Canvas/UIRoot/Btn_Next',
                                  skipScreenshot=True,
                                  after=[expectation('caseId', c['id']), expectation('executed', False)]))
            steps.append(dict(label=c['id'] + '_select', buttonPath='Canvas/UIRoot/Btn_Select',
                              skipScreenshot=True,
                              before=[expectation('caseId', c['id'])], after=[expectation('selected', True)]))
            if c.get('combatPhase') or c.get('operations'):
                steps.append(dict(label=c['id'] + '_play', buttonPath='Canvas/UIRoot/Btn_Play',
                                  before=[expectation('art', c['cardId'])],
                                  after=[expectation('played', True), expectation('executed', False)]))
            for operation_index in range(max(0, len(c.get('operations', [])) - 1)):
                steps.append(dict(label=c['id'] + '_operation_' + str(operation_index), buttonPath='Canvas/UIRoot/Btn_Play',
                                  after=[expectation('operationCount', operation_index + 1), expectation('executed', False)]))
            steps.append(dict(label=c['id'] + '_execute', buttonPath='Canvas/UIRoot/Btn_Play',
                              before=[expectation('art', c['cardId'])], after=[expectation('executed', True), expectation('completed', i + 1)]))
        for step in steps:
            if step['label'] in ('items_009_00_execute','items_009_24_execute'):
                step.setdefault('after', []).append(expectation('ui:originalEnemyArt', True))
            if any(step['label'].startswith(c['id']+'_') for c in group if c.get('turnMachineStartPhase')):
                step.setdefault('after', []).append(expectation('ui:textFits', True))
            if step['label'].startswith(('advanced_card_024_', 'advanced_card_020_', 'advanced_card_030_', 'advanced_card_026_', 'advanced_card_027_', 'items_013_','items_014_','items_015_','items_001_','items_009_','items_010_','items_002_','items_003_','items_018_','items_000_','advanced_card_016_','magic_005_','magic_000_','magic_007_','magic_013_','magic_019_','magic_003_','magic_002_','magic_004_','magic_020_','items_017_')):
                step.setdefault('after', []).append(expectation('ui:textFits', True))
            if step['label'].startswith(('magic_014_', 'magic_011_', 'magic_021_', 'magic_023_', 'advanced_card_029_', 'advanced_card_025_', 'advanced_card_032_', 'items_008_', 'items_016_', 'items_023_', 'items_004_', 'items_005_', 'items_006_', 'items_007_', 'items_011_', 'items_012_', 'basic_card_001_', 'basic_card_004_', 'basic_card_005_', 'advanced_card_007_', 'advanced_card_015_', 'magic_001_', 'basic_card_021_')):
                step.setdefault('after', []).append(expectation('ui:textFits', True))
            if step['label'].startswith('items_013_') or (step['label'].startswith('advanced_card_027_') and next(c for c in group if step['label'].startswith(c['id']+'_')).get('playFromHand')):
                source=next(c for c in group if step['label'].startswith(c['id']+'_'))
                offer_art=source['wisdomOfferArt']
                if source.get('wisdomSecondOfferArt') and step['label'].endswith(('_operation_1','_execute')):
                    offer_art=source['wisdomSecondOfferArt']
                step.setdefault('after', []).extend([expectation('ui:materialArt',source['wisdomMaterialArt']),
                    expectation('ui:offerArt',offer_art)])
                if step['label'].endswith('_execute') and source.get('expectedException'):
                    step['after'].append(expectation('ui:status','按原规则拒绝：'+source['expectedException'].replace('InvalidOperationException: ','')))
            if step['label'].startswith('advanced_card_029_'):
                source=next(c for c in group if step['label'].startswith(c['id']+'_'))
                if source.get('playFromHand'):
                    step.setdefault('after', []).extend([expectation('ui:materialArt',source['ritualMaterialArt']),
                        expectation('ui:originalEnemyArt',True)])
            if step['label'].startswith('advanced_card_030_'):
                source=next(c for c in group if step['label'].startswith(c['id']+'_'))
                if source.get('playFromHand'):
                    index=source['option'] if source['enhanced'] else source['option']&255
                    offer=source['advancedOffer']
                    step.setdefault('after', []).append(expectation('ui:offerArt',offer[index] if 0<=index<len(offer) else ''))
            source=next(c for c in group if step['label'].startswith(c['id']+'_'))
            if source.get('basicAcceptance'):
                step.setdefault('after',[]).append(expectation('ui:textFits',True))
                if source['cardId']=='items_024':
                    top=source['artifactSupplyIds'][0]
                    if step['label'].endswith(('_play','_execute')) or '_operation_' in step['label']:
                        step['after'].append(expectation('ui:materialArt',top))
                    if step['label'].endswith('_execute'):
                        use=next((o for o in source['operations'] if o['kind']=='UseMysticBox'),None)
                        fame=0 if use is None else 1 if top=='items_003' else 4 if use.get('enhanced') else 3
                        mode='未使用' if use is None else '焚毁' if use.get('enhanced') else '普通'
                        step['after'].extend([expectation('ui:boxUse',f'借用方式：{mode}；实际名望{fame}'),
                            expectation('ui:boxSupply',f'供应顺序：items_004→{top}；原实例True')])
                        if source.get('recruitOffer'):
                            left=5 if use.get('enhanced') else 0
                            step['after'].extend([expectation('ui:offerArt','army_2_006'),
                                expectation('ui:boxRecruit',f'实际招募：农民；费用4；余额{left}；原供给True')])
                        elif top=='items_003':
                            step['after'].extend([expectation('ui:offerArt','army_2_006'),
                                expectation('ui:boxBanner','借旗：原勇气旗；盒仍附着False；部队就绪False')])
                if source['cardId']=='magic_016' and step['label'].endswith('_execute'):
                    step['after'].append(expectation('ui:warpPermission','扭曲许可：跨两格False；不激怒False'))
                    if source.get('useTurnEngine'):
                        step['after'].extend([expectation('ui:warpOrder','实际回合顺序：1→1→2'),
                            expectation('ui:warpAside','暂存至轮末：0张；手牌2 / 牌库4')])
                    if source.get('exploreTileIds'):
                        step['after'].append(expectation('ui:warpExplore','实际探索：[77]；原板块True；供给余0'))
                    if source.get('mapPlaces'):
                        step['after'].extend([expectation('ui:warpGuard','原肆虐敌：monster_032；在场False；强制战斗False'),
                            expectation('ui:warpWounds','肆虐实际承伤：2'),expectation('ui:enemyArtId','monster_032'),
                            expectation('ui:combatSummary','结算：印刷4 / 有效4 / 击杀1')])
                if source['cardId']=='items_022' and step['label'].endswith(('_play','_operation_0','_operation_1','_execute')):
                    dark=source['skillOfferIndices'][source['option']]==4
                    skill='暗火魔法' if dark else '黑暗道路'
                    step['after'].extend([expectation('ui:headbandChoice','头环选择：'+skill+'；选项0'),
                        expectation('ui:headbandSkillArt','hero_skill_pool_000:'+('04' if dark else '00')),
                        expectation('ui:headbandOwner','原技能归属：'+('本人技能' if source['enhanced'] else '公共供应')+'；实体守恒True')])
                    if dark and step['label'].endswith('_execute'):
                        step['after'].extend([expectation('ui:materialArt','basic_card_021'),expectation('ui:originalEnemyArt',True),
                            expectation('ui:combatSummary','结算：印刷4 / 有效4 / 击杀1')])

                if source['cardId']=='magic_018' and step['label'].endswith(('_play','_operation_0','_execute')):
                    steal='基础不适用' if not source['enhanced'] else ('放弃' if source['mindStealPlayerIndex']<0 else '玩家'+str(source['mindStealPlayerIndex']+1))
                    step['after'].extend([expectation('ui:mindChoice','思维选择：Green；偷取'+steal), expectation('ui:materialArt','basic_card_000')])
                    final=step['label'].endswith('_execute')
                    if not source['enhanced']:
                        players='玩家1选复原：玩家1弃牌|玩家2展示：狂怒、决心'
                    elif source['mindStealPlayerIndex']<0:
                        players='玩家1选行进：玩家1弃牌|玩家2选复原：玩家2弃牌'
                    else:
                        players='玩家1选宁静：玩家1'+('牌库' if final else '弃牌')+'|玩家2选行进：本人'+('牌库' if final else '已打出' if '_operation_' in step['label'] else '手牌')
                    step['after'].append(expectation('ui:mindPlayers',players))

                if source['cardId']=='advanced_card_038' and step['label'].endswith('_execute'):
                    step['after'].extend([expectation('ui:materialArt','army_2_006'),
                        expectation('ui:originalEnemyArt',True),
                        expectation('ui:combatSummary','结算：印刷4 / 有效4 / 击杀1')])
                if source['cardId']=='advanced_card_039' and step['label'].endswith('_execute'):
                    values={v['key']:v['value'] for v in source['expected']}
                    step['after'].append(expectation('ui:mountainRefill',
                        f"回合末：{values['CurrentTerrain']}；实际安全检查True；补牌后手牌{values['hand']}"))
                if source['cardId'] in ('advanced_card_035','advanced_card_037') and step['label'].endswith('_execute'):
                    values={v['key']:v['value'] for v in source['expected']}
                    if source.get('enemies'):
                        step['after'].append(expectation('ui:originalEnemyArt',True))
                        last=max(int(k.split(':')[1]) for k in values if k.startswith('operation:') and k.endswith(':printed'))
                        prefix=f'operation:{last}:'
                        step['after'].append(expectation('ui:combatSummary',
                            f"结算：印刷{values[prefix+'printed']} / 有效{values[prefix+'effective']} / 击杀{values[prefix+'kills']}"))
                    if source['cardId']=='advanced_card_035' and source['unitIds']==['army_003']:
                        step['after'].append(expectation('ui:materialArt','army_003'))
                    if source['cardId']=='advanced_card_037':
                        option=source['option']
                        value=values['played:'+['MovementPool','InfluencePool','MeleePool','BlockPool'][option]]
                        step['after'].append(expectation('ui:resolveChoice',f"不屈选择：{['移动','影响','近战','格挡'][option]}；原卡产出{value}"))
                if source['cardId'] in ('advanced_card_022','advanced_card_023') and step['label'].endswith('_execute'):
                    step['after'].extend([expectation('ui:materialArt','basic_card_021'),
                        expectation('ui:originalEnemyArt',True),
                        expectation('ui:combatSummary','结算：印刷4 / 有效4 / 击杀1')])
                    if source['cardId']=='advanced_card_023':
                        ranged_first='basic_card_007' in source['extraHandCards']
                        ranged=(5 if source['enhanced'] else 4) if ranged_first else 0
                        block=0 if ranged_first else (6 if source['enhanced'] else 4)
                        step['after'].append(expectation('ui:ambushFirst',f'伏击首效：远程{ranged} / 格挡{block}'))
                if source['cardId']=='advanced_card_043' and step['label'].endswith('_execute'):
                    if source['option']==0:
                        step['after'].append(expectation('ui:coldChoice','冷峻选择：影响力'+('5' if source['enhanced'] else '3')))
                    else:
                        target='monster_058' if source['enhanced'] else 'monster_001'
                        power=4 if source['enhanced'] else 8
                        profile='Physical1；去能力False；停攻True' if source['enhanced'] else 'Fire6；去能力True；停攻False'
                        step['after'].extend([expectation('ui:coldChoice',f'冷峻选择：{target} / 攻击1'),
                            expectation('ui:coldAttack','出牌后攻击：'+profile),
                            expectation('ui:enemyArtId',target), expectation('ui:materialArt','basic_card_021'),
                            expectation('ui:combatSummary',f'结算：印刷{power} / 有效{power} / 击杀1')])
                if source['cardId']=='advanced_card_042' and (step['label'].endswith('_operation_3') or step['label'].endswith('_execute')):
                    drawn=4 if source['enhanced'] else 3
                    removed=int(source['enhanced'])
                    step['after'].extend([expectation('ui:surgeDraw',f'新伤抽牌：已抽{drawn} / 余库{5-drawn}；移除新伤{removed}'),
                        expectation('ui:enemyArtId','monster_032')])
                    if step['label'].endswith('_execute'):
                        step['after'].extend([expectation('ui:materialArt','basic_card_016'),
                            expectation('ui:combatSummary','结算：印刷6 / 有效6 / 击杀2')])
                if source['cardId']=='advanced_card_041' and source['enhanced'] and step['label'].endswith('_operation_1'):
                    step['after'].extend([expectation('ui:enemyArtId','monster_027'),
                        expectation('ui:combatSummary','结算：印刷3 / 有效3 / 击杀1'),
                        expectation('ui:explosiveArmor','实际减甲：monster_027 5→4；剩余次数0')])
                if source['cardId']=='advanced_card_041' and step['label'].endswith('_execute'):
                    step['after'].extend([expectation('ui:materialArt','basic_card_021'),
                        expectation('ui:originalEnemyArt',True),
                        expectation('ui:combatSummary','结算：印刷4 / 有效4 / 击杀1')])
                    if source['enhanced']:
                        step['after'].append(expectation('ui:explosiveArmor','实际减甲：monster_027 5→4；剩余次数0'))
                if source['cardId']=='advanced_card_040' and step['label'].endswith('_execute'):
                    choices=['移动','治疗','抽牌']
                    bonus='/'.join(choices[i] for i in source.get('manaPowerBonusChoices',[])) or '无'
                    choice=(choices[source['option']]+'；奖励'+bonus+'；费用'+source['selectedActionManaColor']) if source['enhanced'] else '取得'+COLORS[source['option']]+'晶'
                    step['after'].append(expectation('ui:manaPowerChoice','魔晶力量选择：'+choice))
                    if source['enhanced'] and source['option']==1:
                        step['after'].append(expectation('ui:materialArt','army_2_006'))
                    elif source['option']==2:
                        step['after'].append(expectation('ui:materialArt','basic_card_000'))
                if source['cardId']=='advanced_card_017' and step['label'].endswith('_execute'):
                    recycle=next(o['option']==1 for o in source['operations'] if o['kind']=='SelectSteadyTempoReturn')
                    step['after'].append(expectation('ui:steadyChoice','回收选择：'+('回收' if recycle else '普通弃牌')))
                if source.get('crystalMasteryScenario'):
                    if source['cardId']!='advanced_card_021':
                        step['after'].append(expectation('ui:materialArt','advanced_card_021'))
                        step['after'].append(expectation('ui:originalEnemyArt',True))
                    if step['label'].endswith('_execute'):
                        step['after'].append(expectation('ui:crystalRefunds','0/0/0' if source['cardId']=='advanced_card_021' else '1/1/1|1/1/0'))
                if source['cardId']=='items_019':
                    step['after'].append(expectation('ui:originalEnemyArt',True))
                if source['cardId']=='basic_card_003':
                    count=len(source['operations']) if step['label'].endswith('_execute') else (
                        int(step['label'].rsplit('_',1)[1])+1 if '_operation_' in step['label'] else 0)
                    shown=[o for o in source['operations'][:count+1] if o['kind'] in ('PlayCard','MoveHarvest')][-1]
                    step['after'].append(expectation('ui:materialArt',shown['cardId']))
                    if source.get('enemies'):
                        step['after'].append(expectation('ui:originalEnemyArt',True))
                if source['cardId'] in ('advanced_card_012','advanced_card_013'):
                    if source.get('enemies'):
                        step['after'].append(expectation('ui:originalEnemyArt',True))
                    if source['cardId']=='advanced_card_012':
                        count=len(source['operations']) if step['label'].endswith('_execute') else (
                            int(step['label'].rsplit('_',1)[1])+1 if '_operation_' in step['label'] else 0)
                        shown=[o for o in source['operations'][:count+1] if o['kind']=='PlayCard']
                        if shown:
                            step['after'].append(expectation('ui:materialArt',shown[-1]['cardId']))
                        if step['label'].endswith('_execute'):
                            values={v['key']:v['value'] for v in source['expected']}
                            last=max(int(k.split(':')[1]) for k in values if k.startswith('operation:') and k.endswith(':printed'))
                            prefix=f'operation:{last}:'
                            step['after'].append(expectation('ui:combatSummary',
                                f"结算：印刷{values[prefix+'printed']} / 有效{values[prefix+'effective']} / 击杀{values[prefix+'kills']}"))
                            chosen='/'.join(source['bloodRitualColors']) if source['enhanced'] else COLORS[source['option']]
                            crystal=(source.get('bloodRitualCrystalColor') or '放弃') if source['enhanced'] else '基础红晶1'
                            step['after'].append(expectation('ui:bloodChoice',f'鲜血选色：{chosen}；转晶：{crystal}'))
            if 'decomposeMaterialArt' in source:
                step.setdefault('after', []).append(expectation('ui:materialArt',source['decomposeMaterialArt']))
            if 'overloadMaterialArt' in source:
                step.setdefault('after', []).append(expectation('ui:materialArt',source['overloadMaterialArt']))
                if source.get('combatPhase') == 'Block':
                    step.setdefault('after', []).append(expectation('ui:originalEnemyArt',True))
                if source.get('overloadStepwise'):
                    expected_values={v['key']:v['value'] for v in source['expected']}
                    if step['label'].endswith('_play'):
                        remaining=1 if source['enhanced'] else 2
                    elif '_operation_' in step['label']:
                        op=step['label'].rsplit('_',1)[1]
                        remaining=expected_values.get(f'operation:{op}:remaining',0)
                    else:
                        remaining=expected_values['overload:remaining'] if step['label'].endswith('_execute') else 0
                    step.setdefault('after',[]).append(expectation('ui:repeatRemaining',remaining))
            if 'overloadPermissionArt' in source and step['label'].endswith('_execute'):
                step.setdefault('after',[]).append(expectation('ui:offerArt',source['overloadPermissionArt']))
            if source['id']=='advanced_card_024_53' and step['label'].endswith(('_play','_operation_0')):
                step.setdefault('after',[]).append(expectation('ui:originalEnemyArt',True))
        config = dict(scenePath=f'Assets/Scenes/Verification/{batch}.unity',
                      screenshotsDirectory=f'AutomationOutputs/AllOriginalCards/20260910/{batch}/captures',
                      reportPath=f'AutomationOutputs/AllOriginalCards/20260910/{batch}/report.json', useRunSubfolder=False,
                      captureInitialView=True, initialDelaySeconds=1.5, defaultWaitAfterSeconds=0.05, maxRunSeconds=900, steps=steps)
        folder = ROOT / 'AutomationConfigs/AllOriginalCards'
        folder.mkdir(exist_ok=True)
        write_json(folder / (batch + '.json'), config)
    print(json.dumps({'cards': len(CARDS), 'cases': len(CASES), 'batches': counts}, ensure_ascii=False))


if __name__ == '__main__':
    export()
