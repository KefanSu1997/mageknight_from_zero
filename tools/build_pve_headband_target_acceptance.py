"""Actual wolf-rider wounds and level reward feed independent headband target inputs.

Writes the acceptance config only when explicitly invoked. --prepare changes only the
existing HornGold replay seed, with an exact-byte --restore guard. No scene is run.
"""
import json
import sys
from pathlib import Path


VARIANTS = {
    # HeroProgression Fisher-Yates on ten original skills; choose the other skill.
    'Braevalar': (1, 'hero_skill_pool_001:08', 0),  # choices 6,8
    'Krang': (0, 'hero_skill_pool_003:04', 0),      # choices 0,4
    'Tovak': (2, 'hero_skill_pool_007:05', 0),     # choices 2,5
    'Norowas': (2, 'hero_skill_pool_002:02', 1),   # choices 2,5
}
hero = next((x for x in sys.argv[1:] if x in VARIANTS), 'Braevalar')
seed, public_skill, reward_choice = VARIANTS[hero]
fixture = Path('Assets/GameData/BattleDemo/HornGold.asset')
backup = Path('tmp/headband_target_HornGold.original')

if '--restore' in sys.argv:
    original = backup.read_bytes()
    expected = original.replace(b'effectRandomSeed: 0', f'effectRandomSeed: {seed}'.encode())
    assert fixture.read_bytes() == expected, 'Replay fixture changed independently; do not overwrite it.'
    fixture.write_bytes(original)
    print('Original seed-0 asset restored')
    raise SystemExit

if '--prepare' in sys.argv:
    original = fixture.read_bytes()
    assert b'effectRandomSeed: 0' in original, 'Restore the previous fixture first.'
    if backup.exists():
        assert backup.read_bytes() == original
    else:
        backup.parent.mkdir(parents=True, exist_ok=True)
        backup.write_bytes(original)
    fixture.write_bytes(original.replace(b'effectRandomSeed: 0', f'effectRandomSeed: {seed}'.encode()))

steps = []
rule = ('头环来自真实手牌，借用升级未选的实际公共技能；伤牌来自真实狼骑兵攻击，'
        '每次独立伤牌/颜色/部队，正常两次与重整一次分离；原费用、牌区、正式回合与消费者对应')


def click(path, label, expect=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=rule)
                             for k, v in {'ui.truncated': '', **(expect or {})}.items()]))


def battle_play(card, choice):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '实际原手牌')
    click('Inspector/Btn_Choice_' + choice, '原卡效果选择')
    click('Inspector/Btn_Play', '真实出牌')


def hand_open():
    click('Btn_JourneyHand', '同一玩家实际手牌')


def hand_close():
    click('JourneyHand/Btn_CloseHand', '返回同一连续旅程')


def hand_play(index, choice='base', expect=None):
    hand_open()
    click('JourneyHand/Btn_Hand_' + str(index), '选择实际原手牌实例')
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原卡基础效果')
    click('JourneyHand/Btn_PlayOutside', '原费用与效果', expect)
    hand_close()


def open_headband(index):
    hand_open()
    click('JourneyHand/Btn_Hand_' + str(index), '实际原启迪头环')
    click('JourneyHand/Btn_PlayOutside', '展开实际公共技能')
    click('SpecialArtifact/Btn_PublicSkill_0', '实际升级未选原技能')
    click('SpecialArtifact/Btn_ConfirmSpecial', '逐次选择真实输入', capture=True)


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '配置原英雄连续旅程'))
click('CampShade/Camp/Btn_Loadout', '已有可重放原起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '原红蓝各一晶与明确重放种子')
click('CampShade/Camp/Btn_ChooseCards', '配置七张原卡')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空旧构筑')
# Extra actual unplayed cards keep the level-reward card in the draw pile.
# ConfiguredPlayer clears its draw pile; no artificial draw cards are injected.
first_page = ['000', '001', '008' if hero == 'Norowas' else '007']
for card in first_page:
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '选择原基本牌')
click(p + 'Btn_DeedNext', '第二页原基本牌')
for card in ['014', '016', '021']:
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '选择原基本牌')
click(p + 'Btn_DeedKind_3', '原神器')
click(p + 'Btn_DeedNext', '神器第二页')
click(p + 'Btn_ChooseDeed_items_022', '原启迪头环')
click(p + 'Btn_DoneDeeds', '确认七张原牌')
click('CampShade/Camp/Btn_OriginalHero', '原英雄')
click('CampShade/HeroStart/Btn_Hero_' + hero, '选择原英雄')
opening = ','.join('basic_card_' + c for c in first_page + ['014', '016', '021']) + ',items_022'
click('CampShade/HeroStart/Btn_ConfiguredHero', '等级1无技能无部队的合法构筑',
      {'hero.id': hero, 'hero.level': 1, 'hero.skills': 0, 'units.count': 0, 'hand.ids': opening})
click('Tactic_day_1/Btn_Tactic_day_1', '原捷足先登')
click('Btn_BeginTurn', '正式第一回合')
click('Btn_JourneyEncounter_4', '原狼骑兵产生真实伤牌')
for _ in range(3):
    click('Inspector/Btn_Next', '正式战斗阶段')
battle_play('021', 'attack')
battle_play('016', 'attack')
click('Enemy_0/Btn_Target', '实际原狼骑兵')
click('Inspector/Btn_Resolve', '实际攻击2加2击杀与真实两张创伤',
      {'kills': 1, 'fame': 3, 'wounds': 2, 'red': 1, 'blue': 1}, True)
click('Inspector/Btn_Next', '完成实际战斗')
click('Inspector/Btn_EndTurn', '正式回合末升级', {'upgrade.choices': 2})
click('LevelReward/Btn_RewardSkill_' + str(reward_choice), '取得另一个合法原技能')
click('LevelReward/Btn_RewardAction_0', '实际高级行动奖励')
click('LevelReward/Btn_ConfirmReward', '目标原技能进入公共供应，奖励留在实际牌库',
      {'hero.level': 2, 'hero.skills': 1, 'supply.skills': public_skill,
       'wounds': 2, 'hand': 7, 'deck.count': 1}, True)
click('Btn_BeginTurn', '同一玩家第二回合')

if hero != 'Norowas':
    # Hand: four unplayed basics, physical headband, two actual wounds.
    open_headband(4)
    for use in range(2):
        click('HeadbandTarget/Btn_Wound_0', '本次实时手牌中的实际伤牌')
        if hero != 'Tovak':
            click('HeadbandTarget/Btn_Color_' + str(use), '独立支付红色' if use == 0 else '独立支付蓝色')
        checks = {'headband.remaining': 1 - use, 'wounds': 1 - use,
                  'hero.skills': 1, 'supply.skills': public_skill, 'deck.count': 0,
                  'hand': 6 - use}
        if hero != 'Tovak':
            checks.update(red=0, blue=1 - use)
        else:
            checks.update({'journey.discardCount': 3 + use, 'red': 1, 'blue': 1})
        click('HeadbandTarget/Btn_Confirm', '第一次实际抽取升级奖励' if use == 0 else
              '第二次独立处理伤牌；实际牌库为空，不制造抽牌', checks, True)
    # Both single-player regeneration uses satisfy the current actual strict-lowest
    # predicate (All over no other players); only one physical reward card exists.
    # Same final hand in all wound variants: four basics followed by the drawn reward.
    click('JourneyHand/Btn_Hand_4', '刚从真实牌库抽到的升级高级行动')
    click('JourneyHand/Btn_SidewaysMove', '新抽原牌真实横置移动1', {'movement': 1, 'hand': 4}, True)
    click('JourneyHand/Btn_Hand_0', '原行进')
    click('JourneyHand/Btn_OutsideChoice_base', '原移动2')
    click('JourneyHand/Btn_PlayOutside', '与刚抽原牌合计移动3', {'movement': 3})
    hand_close()
    click('Btn_JourneyMap', '实际地图消费')
    click('JourneyMap/Btn_Hex_forest', '白昼森林目标')
    click('JourneyMap/Btn_Move', '正式森林费用3消费刚抽原牌贡献',
          {'journey.position': '1,0', 'movement': 0, 'wounds': 0}, True)
    click('JourneyMap/Btn_CloseMap', '保留真实位置和牌区')
else:
    # ConfiguredHero correctly starts with no units. Recruit a real offered farmer.
    hand_play(0, expect={'movement': 2})
    click('Btn_JourneyMap', '实际地图')
    click('JourneyMap/Btn_Hex_plains', '实际村庄')
    click('JourneyMap/Btn_Move', '行进支付2进入村庄', {'journey.position': '0,1', 'movement': 0})
    click('JourneyMap/Btn_CloseMap', '返回村庄')
    click('Btn_JourneyInteraction', '原村庄交涉')
    click('JourneyInteraction/Btn_BeginInteraction', '正式交涉')
    click('JourneyInteraction/Btn_CloseInteraction', '保留同一次交涉')
    hand_play(1, expect={'influence': 2})  # Original Promise, no mana paid.
    hand_open()
    for _ in range(2):
        click('JourneyHand/Btn_Hand_0', '实际宁静或耐力原手牌')
        click('JourneyHand/Btn_SidewaysInfluence', '实际原牌横置影响力1')
    hand_close()
    click('Btn_JourneyInteraction', '实际部队供应')
    click('JourneyInteraction/Btn_Offer_army_2_006', '原农民')
    click('JourneyInteraction/Btn_Recruit', '实际影响力4招募原农民',
          {'units.count': 1, 'units.ready': 'True', 'units.wounds': '0', 'influence': 0}, True)
    click('JourneyInteraction/Btn_CloseInteraction', '同一部队保留')

    def farmer_influence(expected):
        hand_open()
        click('JourneyHand/Btn_OwnedUnits', '实际自有部队')
        click('OwnedUnits/Btn_Unit_0', '同一实际农民')
        click('OwnedUnits/Btn_Ability_1', '原影响力2能力')
        click('OwnedUnits/Btn_Use', '正式激活并耗竭原农民',
              {'units.ready': 'False', 'units.wounds': '0', 'influence': expected}, True)
        click('OwnedUnits/Btn_Close', '返回手牌')
        hand_close()

    farmer_influence(2)
    open_headband(0)
    click('HeadbandTarget/Btn_SkillUnit_0', '实际耗竭且未受伤的同一农民')
    click('HeadbandTarget/Btn_Ready', '原重整选项')
    click('HeadbandTarget/Btn_Confirm', '每轮技能仅借用一次，保留技能在供应',
          {'headband.remaining': 0, 'units.ready': 'True', 'units.wounds': '0',
           'hero.skills': 1, 'supply.skills': public_skill, 'influence': 2}, True)
    hand_close()
    farmer_influence(4)
    click('Btn_JourneyInteraction', '实际消费重整后部队影响力')
    click('JourneyInteraction/Btn_BuyHealing', '原村庄支付3影响力购买治疗1',
          {'influence': 1, 'healing': 1}, True)
    click('JourneyInteraction/Btn_CloseInteraction', '实际治疗点')
    hand_open()
    click('JourneyHand/Btn_HealHero', '治疗第一战遗留的真实伤牌',
          {'healing': 0, 'wounds': 1, 'units.ready': 'False', 'units.wounds': '0'}, True)
    hand_close()

click('Btn_EndTurn', '正式回合末头环与实际出牌弃牌',
      {'journey.completedTurns': 2, 'headband.remaining': 0, 'hero.skills': 1,
       'supply.skills': public_skill, 'journey.completedBattles': 1}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True

Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=300,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print(hero, len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions')
