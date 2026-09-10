"""Independent printed-rule fixtures. Never import engine output to set expectations.

This first audit separates immediate numerical checks from unverified downstream
rules. A partial case is never promoted to a fully verified card.
"""
from pathlib import Path
import json

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


def case(card_id, enhanced, title, expected, option=0, setup=None, limits=(), followup='', effect_choice=''):
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
    b(18, strong, '森林费用降至2', {'MovementPool': move, 'TerrainCostOverride:Forest': 2}, limits=['单格/整类地形范围还需移动场景结算'])
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
          limits=['原数据缺名称和效果且映射Unknown；本例预期直接来自现有本能卡面，未重写原卡资产'])

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


def export():
    target = ROOT / 'Assets/Resources/CardVerification'
    target.mkdir(exist_ok=True)
    (target / 'cases.json').write_text(json.dumps({'cases': CASES}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
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
            steps.append(dict(label=c['id'] + '_execute', buttonPath='Canvas/UIRoot/Btn_Play',
                              before=[expectation('art', c['cardId'])], after=[expectation('executed', True), expectation('completed', i + 1)]))
        config = dict(scenePath=f'Assets/Scenes/Verification/{batch}.unity',
                      screenshotsDirectory=f'AutomationOutputs/AllOriginalCards/20260910/{batch}/captures',
                      reportPath=f'AutomationOutputs/AllOriginalCards/20260910/{batch}/report.json', useRunSubfolder=False,
                      captureInitialView=True, initialDelaySeconds=1.5, defaultWaitAfterSeconds=0.05, maxRunSeconds=900, steps=steps)
        folder = ROOT / 'AutomationConfigs/AllOriginalCards'
        folder.mkdir(exist_ok=True)
        (folder / (batch + '.json')).write_text(json.dumps(config, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'cards': len(CARDS), 'cases': len(CASES), 'batches': counts}, ensure_ascii=False))


if __name__ == '__main__':
    export()
