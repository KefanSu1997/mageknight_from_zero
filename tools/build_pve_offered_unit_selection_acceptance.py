"""One legal Norowas journey: real fourth offer and nonzero enemy attack slot.

Owned Loyalty adds two actual regular cards once at acquisition. Later formal
rounds rotate the same supply into Shock Troops. No fake offer/unit/resource.
Four key captures; --from supports evidence after a verified replay prefix.
"""
import json
import sys
from pathlib import Path

steps = []
RULE = '原忠诚取得真实供应追加、号召原白费与一次许可；第四张分页和奇袭第二攻击槽按真实目标消费，供应不入队'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def summon(expected):
    click('Btn_JourneyHand', '同玩家真实原手牌')
    click('JourneyHand/@card=magic_020', '实际原战斗号召')
    click('JourneyHand/Btn_OutsideChoice_base', '原白费普通号召')
    click('JourneyHand/Btn_PlayOutside', '原法术必要输入')
    click('DeedInputs/Btn_ConfirmInputs', '真实支付白费取得本回合一次许可', expected)
    click('JourneyHand/Btn_CloseHand', '保留许可进入正式战斗')


def card(card_id, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card_id + '_0', '真实原基本牌 ' + card_id)
    click('Inspector/Btn_Choice_' + choice, '原印刷选择 ' + choice)
    click('Inspector/Btn_Play', '正式出牌', expected)


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '供应分页与真实攻击槽连续场景'))
click('CampShade/Camp/Btn_Loadout', '已有合法原起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '既有红蓝库存各一')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选牌')
click(p + 'Btn_ChooseDeed_basic_card_007', '原敏捷')
click(p + 'Btn_DeedNext', '基本第二页')
for card_id in ('016', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card_id, '原基本牌 ' + card_id)
click(p + 'Btn_DeedKind_2', '原法术')
click(p + 'Btn_DeedNext', '法术第二页')
click(p + 'Btn_ChooseDeed_magic_020', '原战斗号召')
click(p + 'Btn_DeedKind_3', '原神器')
click(p + 'Btn_ChooseDeed_items_006', '原白宝石戒指')
click(p + 'Btn_DoneDeeds', '保存五原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '原英雄合法自身技能')
click('CampShade/HeroBuild/Btn_Hero_Norowas', '原诺若威斯')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4与原护甲3')
click('CampShade/HeroBuild/Btn_Skill_7', '原忠诚契约取得时追加真实常规供应')
click('CampShade/HeroBuild/Btn_Begin', '同一真实玩家开局',
      {'hero.id': 'Norowas', 'hero.level': 4, 'hero.skills': 1, 'units.count': 0,
       'offer.ids': 'army_2_001,army_2_005,army_2_006,army_2_007,army_2_008'})
click('Tactic_day_1/Btn_Tactic_day_1', '正式首日战术')
click('Btn_BeginTurn', '第一实际回合')
click('Btn_JourneyHand', '原白戒实际产魔')
click('JourneyHand/@card=items_006', '真实白宝石戒指')
click('JourneyHand/Btn_ArtifactNormal', '原普通效果')
click('JourneyHand/Btn_PlayOutside', '原白token及白库存各一，名望1',
      {'tokens.white': 1, 'white': 1, 'fame': 16})
click('JourneyHand/Btn_CloseHand', '返回')
summon({'tokens.white': 0, 'white': 1, 'offer.uses': 1})
click('Btn_JourneyEncounter_0', '原哨卫032')
click('Inspector/Btn_Next', '正式格挡阶段')
card('016', 'strong', {'block': 5, 'blue': 0})
click('Enemy_0/Btn_Target', '真实攻击4')
click('Inspector/Btn_Resolve', '原蓝费格挡5完整抵挡4', {'block': 0})
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '真实无伤近战', {'wounds': 0})
click('Btn_OfferedUnits', '真实号召许可的供应面板')
click('OfferedUnits/Btn_OfferNext', '原供应第二页')
click('OfferedUnits/Btn_Offer_3', '实际第四张原赤袍僧侣',
      {'offer.page': 1, 'offer.selected': 'army_2_007', 'offer.uses': 1, 'units.count': 0}, True)
click('OfferedUnits/Btn_OfferPrevious', '翻回第一页仍保留实际选择',
      {'offer.page': 0, 'offer.selected': 'army_2_007'})
click('OfferedUnits/Btn_OfferNext', '返回第二页原选择保留',
      {'offer.page': 1, 'offer.selected': 'army_2_007'})
click('OfferedUnits/Btn_Ability_0', '原免费攻击或格挡3')
click('OfferedUnits/Btn_Option_0', '实际攻击3')
click('OfferedUnits/Btn_Use', '消费号召一次许可，供应卡不入队',
      {'offer.uses': 0, 'melee': 3, 'units.count': 0,
       'offer.ids': 'army_2_001,army_2_005,army_2_006,army_2_007,army_2_008'})
click('Enemy_0/Btn_Target', '原护甲3哨卫')
click('Inspector/Btn_Resolve', '第四张实际贡献击杀', {'kills': 1, 'melee': 0, 'fame': 18}, True)
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '真实第一回合结算',
      {'journey.completedTurns': 1, 'wounds': 0, 'white': 1})

offers = {
    2: 'army_2_012,army_2_014,army_2_011',
    3: 'army_2_013,army_2_002,army_2_000',
    4: 'army_2_003,army_2_010,army_2_004',
}
for round_number, offer in offers.items():
    click('Btn_Announce', '真实牌库空，单人合法宣轮', {'deck.count': 0})
    click('Btn_EndRound', '正式轮末回收同一原牌', {'journey.round': round_number})
    click('Btn_PrepareRound', '真实供应整列轮换', {'offer.ids': offer, 'hand': 5, 'white': 1})
    tactic = 'night_1' if round_number % 2 == 0 else 'day_1'
    click(f'Tactic_{tactic}/Btn_Tactic_{tactic}', '正式昼夜战术')

click('Btn_BeginTurn', '第四轮实际第二回合')
summon({'white': 0, 'tokens.white': 0, 'offer.uses': 1})
click('Btn_JourneyEncounter_8', '原女巫015火冰两个真实攻击槽')
click('Inspector/Btn_Next', '正式格挡阶段', {'enemies.attacks': '3,3', 'enemies.armor': 4})
click('Btn_OfferedUnits', '真实号召当前供应')
click('OfferedUnits/Btn_Offer_1', '原奇袭队实际供应实体', {'offer.selected': 'army_2_010'})
click('OfferedUnits/Btn_Ability_1', '合法供应减甲及一次攻击减1，非强制承伤')
click('OfferedUnits/Btn_Target_0', '实际女巫目标')
click('OfferedUnits/Btn_AttackSlot_1', '明确第二次冰攻击', {'offer.attackSlot': 1})
click('OfferedUnits/Btn_Use', '只对第二攻击减1，第一火攻击不变',
      {'offer.uses': 0, 'units.count': 0, 'reduction.pending': '0:1=1',
       'enemies.attacks': '3,2', 'enemies.armor': 3,
       'offer.ids': 'army_2_003,army_2_010,army_2_004'}, True)
click('Inspector/Btn_Next', '正式真实承伤阶段')
click('Inspector/Btn_Next', '两次攻击按英雄护甲3各一真实伤牌', {'wounds': 2})
card('016', 'attack', {'melee': 2})
card('021', 'attack', {'melee': 4})
click('Enemy_0/Btn_Target', '减甲后的同一女巫')
click('Inspector/Btn_Resolve', '原攻击4实际击杀护甲3', {'kills': 1, 'fame': 21})
click('Inspector/Btn_Next', '正式战斗结束清攻击槽状态')
click('Inspector/Btn_EndTurn', '同玩家两场结算，供应未被移除或入队',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'units.count': 0, 'white': 0, 'wounds': 2, 'fame': 21,
       'journey.samePlayer': 'True', 'offer.ids': 'army_2_003,army_2_010,army_2_004'}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
