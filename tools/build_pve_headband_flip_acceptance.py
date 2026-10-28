"""Real Krang level rewards feed Headband flip-option basic consumers.

Only explicit invocation writes automation. --prepare/--restore use exact-byte
seed guards; --from replays passed operations without duplicate evidence.
"""
import json
import sys
from pathlib import Path
args = sys.argv[1:]
mode = next((a for a in args if a in ('Frenzy', 'Ritual', 'Disguise')), 'Frenzy')
# System.Random Fisher-Yates over 10 original skills: 10->[2,0],6->[0,3],2->[2,5].
seed, public_skill, reward = {'Frenzy': (10, 'hero_skill_pool_003:02', 1),
    'Ritual': (6, 'hero_skill_pool_003:03', 0),
    'Disguise': (2, 'hero_skill_pool_003:05', 0)}[mode]
base = Path('tools/build_pve_headband_target_acceptance.py')
source = base.read_text(encoding='utf-8').split("if hero != 'Norowas':", 1)[0]
a, b = source.index('VARIANTS = {'), source.index('\nhero = ')
source = source[:a] + 'VARIANTS = ' + repr({'Krang': (seed, public_skill, reward)}) + source[b:]
source = source.replace('tmp/headband_target_HornGold.original', 'tmp/headband_flip_' + mode.lower() + '_HornGold.original')
source = source.replace("first_page = ['000', '001', '008' if hero == 'Norowas' else '007']", "first_page = ['000']")
if mode == 'Disguise':
    source = source.replace("'014'", "'022'")
    source = source.replace("'hand': 7, 'deck.count': 1", "'hand': 5, 'deck.count': 1")
else:
    source = source.replace("battle_play('021', 'attack')\nbattle_play('016', 'attack')", "battle_play('021', 'strong')")
    source = source.replace("'red': 1, 'blue': 1", "'red': 0, 'blue': 1")
    source = source.replace("'hand': 7, 'deck.count': 1", "'hand': 6, 'deck.count': 1")
    source = source.replace('实际攻击2加2', '实际红费攻击4')
source = source.replace('七张', '五张')
sys.argv = [str(base), 'Krang'] + [a for a in args if a in ('--prepare', '--restore')]
exec(compile(source, str(base), 'exec'))
for step in steps:
    step['skipScreenshot'] = True  # Existing progression visuals already have evidence.
rule = '原克朗真实升级未选技能在公共供应；头环正面双用或翻面一次不翻个人技能；原手牌费用、实际消费者与正式回合清理对应'


def outside(card, choice='base', expected=None):
    hand_open()
    click('JourneyHand/@card=' + card, '选择实际原手牌 ' + card)
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原卡实际效果选项')
    click('JourneyHand/Btn_PlayOutside', '支付真实原费用并出牌', expected)
    hand_close()


def outside_headband():
    hand_open()
    click('JourneyHand/@card=items_022', '实际原头环')
    click('JourneyHand/Btn_PlayOutside', '原头环公共技能入口')
    click('SpecialArtifact/Btn_PublicSkill_0', '升级未选的实际公共技能')
    click('SpecialArtifact/Btn_ConfirmSpecial', '选择原正面或翻面效果')


def flip(option, expected):
    click('HeadbandFlip/Btn_Option_' + str(option), '明确选择本次原选项', capture=True)
    click('HeadbandFlip/Btn_Confirm', '执行后公共技能不转自有、不翻个人标记',
          {'headband.remaining': 0, 'hero.skills': 1, 'supply.skills': public_skill,
           'skills.flipped': '', **expected})


def move(site, expected):
    click('Btn_JourneyMap', '同一真实位置地图')
    click('JourneyMap/Btn_Hex_' + site, '实际相邻地形')
    click('JourneyMap/Btn_Move', '实际支付原地形费用', expected, True)
    click('JourneyMap/Btn_CloseMap', '保留真实位置')


if mode.startswith('Frenzy'):
    click('Btn_JourneyEncounter_0', '同一玩家第二战原哨卫')
    for _ in range(3):
        click('Inspector/Btn_Next', '正式阶段与实际承伤')
    click('Hand/Btn_Card_items_022_0', '真实原头环手牌')
    click('Inspector/Btn_OpenSpecialArtifact', '头环普通效果')
    click('SpecialArtifact/Btn_PublicSkill_0', '公共战斗狂乱')
    click('SpecialArtifact/Btn_ConfirmSpecial', '展开原选项')
    flip(0 if '--option0' in args else 1, {'melee': 4, 'wounds': 4})
    click('Enemy_0/Btn_Target', '实际哨卫目标')
    click('Inspector/Btn_Resolve', '原狂乱攻击4消费并击杀甲3', {'kills': 1, 'fame': 5, 'melee': 0}, True)
    click('Inspector/Btn_Next', '正式完成战斗')
    click('Inspector/Btn_EndTurn', '同玩家正式回合末',
          {'journey.completedTurns': 2, 'journey.completedBattles': 2,
           'skills.flipped': '', 'supply.skills': public_skill})
elif mode == 'Ritual':
    outside_headband()
    flip(2, {'tokens.green': 1, 'green': 0, 'wounds': 2})
    hand_close()
    outside('basic_card_000', 'strong', {'tokens.green': 0, 'green': 0, 'movement': 4})
    move('forest', {'journey.position': '1,0', 'movement': 1, 'tokens.green': 0})
    click('Btn_EndTurn', '实际移动后正式结束回合',
          {'journey.completedTurns': 2, 'movement': 0, 'skills.flipped': '', 'supply.skills': public_skill})
else:
    outside('basic_card_000', expected={'movement': 2})
    move('plains', {'journey.position': '0,1', 'movement': 0})
    outside('basic_card_022', 'strong', {'influence': 5, 'red': 0, 'reputation': -1})
    outside_headband()
    flip(1, {'skills.ignoreReputationTrack': 'True', 'influence': 5, 'reputation': -1})
    hand_close()
    click('Btn_JourneyInteraction', '真实村庄交涉')
    click('JourneyInteraction/Btn_BeginInteraction', '忽略实际负声誉轨扣减，保留声誉变化',
          {'influence': 5, 'reputation': -1, 'skills.ignoreReputationTrack': 'True'}, True)
    click('JourneyInteraction/Btn_BuyHealing', '真实影响5支付3买治疗1', {'influence': 2, 'healing': 1})
    click('JourneyInteraction/Btn_CloseInteraction', '实际治疗点')
    hand_open()
    click('JourneyHand/Btn_HealHero', '治疗首战遗留的真实伤牌', {'wounds': 1, 'healing': 0}, True)
    hand_close()
    click('Btn_EndTurn', '正式回合末结束忽略声誉期限',
          {'journey.completedTurns': 2, 'reputation': -1,
           'skills.ignoreReputationTrack': 'False', 'skills.flipped': '', 'supply.skills': public_skill})
click('Btn_BeginTurn', '同一真实玩家第三回合保留公共技能与个人翻面状态',
      {'journey.samePlayer': 'True', 'hero.id': 'Krang', 'hero.skills': 1,
       'skills.flipped': '', 'supply.skills': public_skill})
if '--from' in args:
    for step in steps[:int(args[args.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print(mode, len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions')
