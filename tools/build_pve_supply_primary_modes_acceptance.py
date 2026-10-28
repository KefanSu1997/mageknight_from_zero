"""Two original-card continuous routes; backend-first, no Unity calls or asset edits.

The seven/five-card configured hero snapshots are NOT a standard 16-card start.
--from N replays the passed prefix without duplicate assertions/screenshots.
--prepare backs up the previous automation config, --restore restores exact bytes
only if the current config is the one this generator last wrote.
"""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('route', choices=['learning', 'practice'], nargs='?', default='learning')
parser.add_argument('--from', dest='start', type=int, default=0)
parser.add_argument('--prepare', action='store_true')
parser.add_argument('--restore', action='store_true')
parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
args = parser.parse_args()
config = Path(args.output)
backup = Path('tmp/supply_primary_previous_config.original')
stamp = Path('tmp/supply_primary_generated_config.sha256')
if args.restore:
    assert backup.exists() and stamp.exists(), 'No saved configuration exists.'
    assert hashlib.sha256(config.read_bytes()).hexdigest() == stamp.read_text().strip(), 'Configuration changed independently; refusing to overwrite.'
    config.write_bytes(backup.read_bytes())
    backup.unlink()
    stamp.unlink()
    print('Previous acceptance configuration restored byte-for-byte; no Unity assets changed.')
    raise SystemExit
if args.prepare:
    assert not backup.exists(), 'Restore the previous saved configuration first.'
    backup.parent.mkdir(exist_ok=True)
    backup.write_bytes(config.read_bytes())

steps = []
rule = ('原定义/卡面与正式效果；强化熔炉两张实际法术留供应、学习白费与9影响力入手、'
        '风暴实际源骰取晶并消费；修行基础同色材料永久移除、取得牌入弃，正式跨轮抽回攻防')


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=rule)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def hand():
    click('Btn_JourneyHand', '同一玩家实际手牌')


def close():
    click('JourneyHand/Btn_CloseHand', '返回同一实际旅程')


def outside(card, choice, expected=None):
    click('JourneyHand/@card=' + card, '选择实际原牌 ' + card)
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原牌印刷模式 ' + choice)
    click('JourneyHand/Btn_PlayOutside', '实际出牌或打开原输入', expected)


def discard(card):
    click('JourneyHand/@card=' + card, '保留原牌实例选择回合末弃牌')
    click('JourneyHand/Btn_DiscardAtEnd', '正式回合末弃牌选择')


def battle(card, choice=None, expected=None):
    click('Hand/Btn_Card_' + card + '_0', '原实体战斗手牌 ' + card)
    if choice is None:
        click('Inspector/Btn_Sideways', '原牌横置贡献', expected)
    else:
        click('Inspector/Btn_Choice_' + choice, '原卡效果 ' + choice)
        click('Inspector/Btn_Play', '原费用与实际贡献', expected)


# Three owned advanced cards are intentional: supplies exclude owned unique cards
# before shuffling. Omitting one changes seed-0 offers and is a different fixture.
opening = (['basic_card_000', 'basic_card_008', 'basic_card_014', 'basic_card_022']
           if args.route == 'learning' else ['basic_card_014', 'basic_card_021'])
opening += ['advanced_card_026', 'advanced_card_027', 'advanced_card_033']
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原卡主要模式的连续消费路线'))
click('CampShade/Camp/Btn_Loadout', '选择既有种子0原红蓝各一晶快照')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页原构筑')
click('CampShade/Picker/Btn_Select_39', 'HornGold既有种子0，不修改资源或资产')
click('CampShade/Camp/Btn_ChooseCards', '自定七或五原牌，不冒充标准16牌开局')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清除待选旧卡')
if args.route == 'learning':
    for card in ['000', '008']:
        click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基础牌 ' + card)
click(p + 'Btn_DeedNext', '基础第二页')
for card in (['014', '022'] if args.route == 'learning' else ['014', '021']):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基础牌 ' + card)
click(p + 'Btn_DeedKind_1', '原高级行动')
for _ in range(2):
    click(p + 'Btn_DeedNext', '下一页高级行动')
for card in ['026', '027', '033']:
    click(p + 'Btn_ChooseDeed_advanced_card_' + card, '原高级行动 ' + card)
click(p + 'Btn_DoneDeeds', '确认原牌实例构筑')
click('CampShade/Camp/Btn_OriginalHero', '原英雄身份')
click('CampShade/HeroStart/Btn_Hero_Arythea', '艾瑞希亚')
click('CampShade/HeroStart/Btn_ConfiguredHero', '等级1无技能无部队自定快照',
      {'hero.id': 'Arythea', 'hero.level': 1, 'hero.skills': 0, 'units.count': 0,
       'hero.standardStart': 'False', 'hand.ids': ','.join(opening)})
click('Tactic_day_1/Btn_Tactic_day_1', '原昼战术')
click('Btn_BeginTurn', '正式第一回合',
      {'supply.advanced': 'advanced_card_005,advanced_card_022,advanced_card_003',
       'supply.spells': 'magic_004,magic_023,magic_000', 'source.faces': 'White,Blue,White',
       'red': 1, 'blue': 1, 'green': 0, 'white': 0})
hand()
if args.route == 'learning':
    click('JourneyHand/Btn_NextHand', '翻到真实第七张原牌；出牌后自动收回首页')
    outside('advanced_card_033', 'strong')
    click('DeedInputs/Btn_InputOffer_0', '第一张实际绿色法术')
    click('DeedInputs/Btn_InputOffer_1', '第二张不同实际白色法术', capture=True)
    click('DeedInputs/Btn_ConfirmInputs', '支付蓝晶，绿白各一晶；两法术留原供应',
          {'blue': 0, 'green': 1, 'white': 1, 'supply.spells': 'magic_004,magic_023,magic_000'})
    outside('advanced_card_026', 'strong', {'white': 0, 'influence': 4, 'learning.used': 'False'})
    close()
    click('Btn_JourneyTools', '实际普通源骰额度')
    click('JourneyTools/Btn_Source_0', '实际白骰')
    click('JourneyTools/Btn_UseSource', '取白标支付下一张原承诺', {'tokens.white': 1, 'source.faces': 'Blue,White'})
    click('JourneyTools/Btn_CloseTools', '返回实际手牌')
    hand()
    outside('basic_card_008', 'strong', {'tokens.white': 0, 'influence': 8})
    outside('basic_card_022', 'base', {'influence': 10})
    click('JourneyHand/Btn_LearningSupply', '强化学习的独立实际购牌入口')
    click('LearningSupply/Btn_LearnStrong', '明确9影响力加入手牌')
    click('LearningSupply/Btn_InputOffer_1', '选择实际魔力风暴', capture=True)
    click('LearningSupply/Btn_BuyLearning', '支付9余1，真实风暴加入手牌',
          {'influence': 1, 'learning.used': 'True', 'hand': 4})
    outside('advanced_card_022', 'base')
    click('DeedInputs/Btn_InputDie_0', '实际蓝色源骰，独立于已取白骰')
    click('DeedInputs/Btn_ConfirmInputs', '新取得风暴得蓝晶并重掷同骰归源', {'blue': 1, 'source.claims': 1})
    outside('basic_card_000', 'strong', {'green': 0, 'movement': 4})
    outside('basic_card_014', 'strong', {'blue': 0, 'movement': 8})
    close()
    click('Btn_JourneyMap', '所得两色魔晶已支付真实移动牌')
    click('JourneyMap/Btn_Hex_plains', '实际相邻平原村庄')
    click('JourneyMap/Btn_Move', '从实际位置支付平原2移动，8余6',
          {'journey.position': '0,1', 'movement': 6, 'green': 0, 'blue': 0}, True)
    click('JourneyMap/Btn_CloseMap', '保留实际移动位置')
    hand()
    discard('advanced_card_027')
    close()
    click('Btn_EndTurn', '正式清理本回合移动、学习许可与实际牌区',
          {'hand': 0, 'journey.discardCount': 8, 'movement': 0, 'journey.completedTurns': 1,
           'journey.position': '0,1', 'journey.samePlayer': 'True'})
else:
    outside('advanced_card_027', 'base')
    click('DeedInputs/Btn_InputMaterial_0', '实际蓝色耐力材料永久移除')
    click('DeedInputs/Btn_InputOffer_0', '选择同色实际寒冰之盾', capture=True)
    click('DeedInputs/Btn_ConfirmInputs', '不付绿费，原盾入弃、材料永久去除',
          {'green': 0, 'blue': 1, 'removed.ids': 'basic_card_014', 'discard.ids': 'advanced_card_005', 'hand': 3})
    for card in ['basic_card_021', 'advanced_card_026', 'advanced_card_033']:
        discard(card)
    close()
    click('Btn_EndTurn', '正式弃牌保留五张真实原牌、原材料不回来',
          {'hand': 0, 'journey.discardCount': 5, 'removed.ids': 'basic_card_014'})
    click('Btn_Announce', '真实空库宣布轮末')
    click('Btn_EndRound', '正式洗混五张原牌并进入夜晚', {'deck.count': 5, 'journey.dayPart': 'Night'})
    click('Btn_PrepareRound', '正式抽回全部五张，随机顺序按原卡引用选择',
          {'hand': 5, 'deck.count': 0, 'removed.ids': 'basic_card_014', 'journey.discardCount': 0})
    click('Tactic_night_1/Btn_Tactic_night_1', '原夜战术')
    click('Btn_BeginTurn', '同一玩家第二回合保留原盾及永久区', {'journey.samePlayer': 'True'})
    hand()
    for card in ['advanced_card_026', 'advanced_card_027']:
        discard(card)
    close()
    click('Btn_JourneyEncounter_0', '同一旅程实际原哨卫')
    click('Inspector/Btn_Next', '正式格挡阶段')
    battle('advanced_card_005', 'strong', {'blue': 0, 'block': 3})
    battle('advanced_card_033', None, {'block': 4})
    click('Enemy_0/Btn_Target', '真实攻击4')
    click('Inspector/Btn_Resolve', '实际消费寒冰格挡与横置贡献')
    click('Inspector/Btn_Next', '正式承伤阶段')
    click('Inspector/Btn_Next', '原盾成功格挡后减甲，旧玩家无伤',
          {'wounds': 0, 'enemies.armor': '1', 'removed.ids': 'basic_card_014'}, True)
    battle('basic_card_021', 'strong', {'red': 0, 'melee': 4})
    click('Enemy_0/Btn_Target', '原盾实际减甲后的同一敌人')
    click('Inspector/Btn_Resolve', '红费狂怒击杀原敌', {'kills': 1, 'fame': 2})
    click('Inspector/Btn_Next', '完成实际战斗')
    click('Inspector/Btn_EndTurn', '同一原盾回弃牌，原材料永久去除且真实两回合完成',
          {'hand': 0, 'journey.discardCount': 5, 'removed.ids': 'basic_card_014',
           'journey.completedTurns': 2, 'journey.completedBattles': 1, 'journey.samePlayer': 'True',
           'journey.discardSelected': 0}, True)

assert 0 <= args.start <= len(steps)
for step in steps[:args.start]:
    step['after'] = []
    step['skipScreenshot'] = True
payload = json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
    captureInitialView=False, initialDelaySeconds=2, defaultWaitAfterSeconds=.3,
    maxRunSeconds=360, useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2).encode('utf-8')
config.write_bytes(payload)
if backup.exists():
    stamp.write_text(hashlib.sha256(payload).hexdigest())
print(args.route, len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions',
      sum(not s['skipScreenshot'] for s in steps), 'screenshots')

