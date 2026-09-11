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
    extra = {'BlockPool': 3 if strong else 2}
    if strong: extra['blockElement'] = 'Fire'
    b(4, strong, '选择格挡，不同时获得移动', extra, option=1)
    for opt, title, result in [(0, '治疗', {'Wounds': 1 if strong else 2, 'handWounds': 1 if strong else 2}),
                               (1, '抽牌', {'hand': 8 if strong else 7}),
                               (2, '绿色魔晶' if strong else '绿色魔力', {'crystal:Green': 2} if strong else {'token:Green': 3}),
                               (3, '重整耗竭的2级部队', {'unitReady': True})]:
        b(5, strong, title, result, option=opt)
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
    17: ({'MovementPool': 2, 'RecycleToBottom': True}, {'MovementPool': 4, 'RecycleToTop': True}),
    18: ({'InfluencePool': 2, 'InfluenceAsBlock': True}, {'InfluencePool': 4, 'InfluenceAsBlock': True}),
    19: ({'InfluencePool': 6}, {'InfluencePool': 11}),
    20: ({'crystal:Red': 3, 'hand': 5}, {'crystal:Blue': 2, 'crystal:Green': 2, 'crystal:White': 2, 'hand': 5}),
    21: ({'crystal:Red': 2}, {'ReturnSpentCrystals': True}),
    22: ({}, {'ExtraManaDice': 3, 'GoldManaWild': True, 'BlackManaWild': True}),
    23: ({'MovementPool': 2, 'AmbushAttackBonus': 1, 'AmbushBlockBonus': 2}, {'MovementPool': 4, 'AmbushAttackBonus': 2, 'AmbushBlockBonus': 4}),
    24: ({'MeleePool': 6, 'hand': 5}, {'MeleePool': 8, 'hand': 5}),
    25: ({'hand': 5}, {'spellOffer': 2, 'discard': 3}),
    26: ({'InfluencePool': 2}, {'InfluencePool': 4}),
    27: ({'hand': 5, 'discard': 3, 'advancedOffer': 2}, {'hand': 6, 'advancedOffer': 2}),
    28: ({'MeleePool': 2, 'AttackBonusPerBlock': 2}, {'MeleePool': 4, 'AttackBonusPerBlock': 3}),
    29: ({'MeleePool': 5, 'hand': 5}, {'MeleePool': 6, 'attackElement': 'Fire', 'hand': 5}),
    30: ({'Wounds': 4, 'handWounds': 4}, {'Wounds': 4, 'handWounds': 4}),
    31: ({'BlockPool': 3, 'SwiftBlockBonus': 3}, {'BlockPool': 5, 'SwiftBlockBonus': 5}),
    32: ({'TeleportRange': 1, 'NextDrawBonus': 1}, {'TeleportRange': 2, 'NextDrawBonus': 1}),
    33: ({'crystal:Red': 2}, {'crystal:Red': 2, 'crystal:Blue': 2}),
    34: ({'MeleePool': 3}, {'MeleePool': 6}),
    35: ({'InfluencePool': 3, 'HealInfluenceRate': 2}, {'InfluencePool': 6, 'HealInfluenceRate': 2, 'ReadyInfluencePerLevel': 2}),
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
        simple = n in [0, 1, 2, 3, 19, 20]
        limitations = [] if simple else ['首轮检验本分支的即时输出；目标范围、额外选择及回合/战斗后续结算须补专用案例']
        if n >= 44: limitations = ['原卡定义缺少名称与效果文本，不能把占位逻辑判为通过']
        kwargs = {}
        if n == 24: kwargs['effect_choice'] = 'FuryEnhanced' if strong else 'FuryBase'
        if n == 30 and strong:
            kwargs['effect_choice'] = 'FireBoltEnhanced'
            expected = {**expected, 'RangedPool': 3, 'attackElement': 'Fire'}
        if n == 33 and strong: kwargs['option'] = 256  # Red + Blue, two distinct offered spells.
        if n == 40 and not strong: kwargs['setup'] = {'crystal:Red': 0}
        if n == 25 and strong: kwargs['setup'] = {'CardToDiscard': '<none>'}
        case(f'advanced_card_{n:03}', strong, '主要印刷效果', expected, limits=limitations, **kwargs)
        if n == 4:
            case(f'advanced_card_{n:03}', strong, '主动承受创伤增强攻击', {'MeleePool': 9 if strong else 5, 'Wounds': 4, 'handWounds': 4}, option=1)
        if n == 7:
            case(f'advanced_card_{n:03}', strong, '战斗中不治疗', {'MovementPool': 4 if strong else 2, 'Wounds': 3, 'handWounds': 3}, setup={'InBattle': True})
        if n == 8:
            case(f'advanced_card_{n:03}', strong, '选择攻击', {'MeleePool': 7 if strong else 3, 'Reputation': -2 if strong else -1}, option=1)

SPELL = {
    0: ({'unitReady': True}, {'unitReady': True, 'unitWounds': 0}),
    1: ({'Wounds': 1, 'handWounds': 1, 'hand': 6}, {'BlockedArmorOne': True}),
    2: ({'discard': 0, 'NextDrawBonus': 2}, {'discard': 0, 'NextDrawBonus': 2}),
    3: ({'ArmorReduction:0': 3}, {'ArmorReduction:0': 3}),
    4: ({'TeleportRange': 3, 'TeleportMustEndSafe': True}, {'TeleportRange': 3, 'IgnoreFortified': True}),
    5: ({'Wounds': 0, 'handWounds': 0}, {'Wounds': 0, 'handWounds': 0, 'unitReady': True}),
    6: ({'Wounds': 3}, {'Wounds': 4, 'handWounds': 4, 'crystal:Red': 3}),
    7: ({'IgnoreFortified': True, 'ArmorReduction:0': 1}, {'KillEnemyIndex': 0}),
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
    22: ({'SkipAttackIndices:0': True}, {'KillEnemyIndex': 0}),
    23: ({'MovementPool': 2, 'TeleportRange': 1}, {'SkipAttackIndices:0': True}),
}
for n in range(24):
    for strong in [False, True]:
        kwargs = {'option': 1} if n == 23 else {}
        if n == 23 and not strong: kwargs['setup'] = {'MovementPool': 3}
        if n == 11 and strong: kwargs['option'] = 2  # Green + Red: siege fire, one crystal pair.
        case(f'magic_{n:03}', strong, '印刷效果及施法费用', SPELL[n][int(strong)], **kwargs,
             limits=['法术通过旧 ActionSystem 映射执行；所有目标选择、日夜合法性和后续结算尚未完整接通，不能判整卡通过'])

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
    16: ({'RangedPool': 4, 'hand': 4, 'discard': 4}, {'DoubleRangedAttack': True}),
    17: ({'InfluencePool': 4}, {'Fame': 2, 'FreeRecruit': True}),
    18: ({}, {'unitWounds': 0}),
    19: ({'MeleePool': 3, 'CrystalsPerKill': 1}, {'MeleePool': 8, 'CrystalsPerKill': 1}),
    20: ({'BlockPool': 6}, {'BlockPool': 8, 'blockElement': 'ColdFire'}),
    21: ({'hand': 5, 'unitReady': True}, {'unitReady': True, 'Wounds': 0, 'handWounds': 0}),
    22: ({'MovementPool': 2}, {'Skills:count': 1, 'skillOffer': 2}),
    23: ({'hand': 5, 'RangedPool': 5, 'attackElement': 'Fire'}, {'hand': 5, 'SiegePool': 8, 'attackElement': 'Fire', 'Wounds': 4, 'handWounds': 4}),
    24: ({}, {'Fame': 1}),
}
for n, color in zip(range(4, 8), ['Red', 'Blue', 'White', 'Green']):
    ITEM[n] = ({'token:' + color: 3, 'crystal:' + color: 2, 'Fame': 1}, {'InfiniteMana:' + color: True, 'InfiniteMana:Black': True})
for n in range(25):
    for strong in [False, True]:
        kwargs = {'option': 2 + (3 << 16) if strong else 2} if n == 21 else {}
        case(f'items_{n:03}', strong, '印刷效果', ITEM[n][int(strong)], **kwargs,
             limits=['复用已有CardEffectFactory模块；尚无按ItemCardData出牌的完整服务。分配、强化移除、限次、持续及随机效果保持待联验'])

# Healing boundaries use the same real hand as the Wounds property. A heal is
# capped by available wound cards and never draws for a nonexistent wound.
for strong in [False, True]:
    b(1, strong, '没有伤牌时不产生治疗或额外抽牌',
      {'Wounds': 0, 'handWounds': 0, 'hand': 3, 'discard': 2},
      setup={'player:Wounds': 0, 'DrawPerHeal': 1})
    b(1, strong, '治疗上限与每治疗一伤抽一牌',
      {'Wounds': 0, 'handWounds': 0, 'hand': 4, 'discard': 2},
      setup={'player:Wounds': 1, 'DrawPerHeal': 1})


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


def write_json(path, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    if path.exists() and path.read_bytes() == data:
        return
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes(data)
    os.replace(temporary, path)


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
            if step['label'].startswith(('magic_014_', 'magic_011_', 'magic_021_')):
                step.setdefault('after', []).append(expectation('ui:textFits', True))
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
