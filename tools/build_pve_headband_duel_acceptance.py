"""Real Wolfhawk level reward -> public Duel -> Headband double-use consumer.

Generates configuration only; never starts Unity.  --prepare/--restore change
only HornGold's replay seed with exact-byte checks. --from N replays accepted
prefix operations without repeating their assertions or screenshots.
"""
import json
import sys
from pathlib import Path

args = sys.argv[1:]
base = Path('tools/build_pve_headband_target_acceptance.py')
source = base.read_text(encoding='utf-8').split("if hero != 'Norowas':", 1)[0]
start, end = source.index('VARIANTS = {'), source.index('\nhero = ')
# Established Fisher-Yates seed 29 flips skill indices 7 and 6. Learn Taunt (6);
# the actual unselected Duel (7) enters the public offer through HeroProgression.
source = source[:start] + 'VARIANTS = ' + repr({'Wolfhawk': (29, 'hero_skill_pool_004:07', 1)}) + source[end:]
source = source.replace('tmp/headband_target_HornGold.original',
                        'tmp/headband_duel_HornGold.original')
# Five actual original cards: red Fury kills the first enemy alone; retain
# Determination for the second block, March for the final attack consumer.
source = source.replace("first_page = ['000', '001', '008' if hero == 'Norowas' else '007']", "first_page = ['000']")
source = source.replace("battle_play('021', 'attack')\nbattle_play('016', 'attack')", "battle_play('021', 'strong')")
source = source.replace("'red': 1, 'blue': 1", "'red': 0, 'blue': 1")
source = source.replace("'hand': 7, 'deck.count': 1", "'hand': 6, 'deck.count': 1")
source = source.replace('七张', '五张').replace('实际攻击2加2', '实际红费攻击4')
sys.argv = [str(base), 'Wolfhawk'] + [a for a in args if a in ('--prepare', '--restore')]
exec(compile(source, str(base), 'exec'))
rule = ('原狼鹰实际击杀升级；未选决斗留在公共供应；原头环普通效果同敌同攻击双用，'
        '格挡2、近战2、无部队介入奖励2；同玩家原手牌横置消费及正式跨回合牌区保留')


def target():
    click('Enemy_0/Btn_Target', '同一个真实哨卫标记')


def sideways(card, expected):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '实际保留的原基本牌')
    click('Inspector/Btn_Sideways', '原基本牌真实横置1', expected)


click('Btn_JourneyEncounter_0', '同一玩家第二战原哨卫032')
click('Inspector/Btn_Next', '正式格挡阶段')
click('Hand/Btn_Card_items_022_0', '首回合保留的真实原启迪头环')
click('Inspector/Btn_OpenSpecialArtifact', '原头环普通借用入口')
click('SpecialArtifact/Btn_PublicSkill_0', '选择真实升级未选的公共决斗')
click('SpecialArtifact/Btn_ConfirmSpecial', '展开原双用同敌同攻击输入')
click('HeadbandDuel/Btn_Enemy_0', '选择本场真实存活哨卫')
click('HeadbandDuel/Btn_Attack_0', '选择该敌实际尚未处理的攻击槽0')
click('HeadbandDuel/Btn_Confirm', '一次完成同敌双用，公共技能未转为自有',
      {'block': 2, 'duel.target': 'monster_032', 'duel.unitAffected': 'False',
       'headband.remaining': 0, 'hero.skills': 1,
       'supply.skills': 'hero_skill_pool_004:07', 'wounds': 2}, True)
battle_play('016', 'block')
target()
click('Inspector/Btn_Resolve', '头环格挡2加原决心基础格挡2合4，完整格挡原攻击4',
      {'block': 0}, True)
click('Inspector/Btn_Next', '正式防御阶段')
click('Inspector/Btn_Next', '完整格挡无新伤，同敌获得原决斗双用攻击2',
      {'melee': 2, 'wounds': 2, 'duel.attackGranted': 'True',
       'duel.unitAffected': 'False'}, True)
sideways('000', {'melee': 3})
target()
click('Inspector/Btn_Resolve', '限定同敌攻击2加横置1击杀甲3；原名望2加双用奖励2',
      {'kills': 1, 'fame': 7, 'melee': 0, 'duel.unitAffected': 'False',
       'supply.skills': 'hero_skill_pool_004:07', 'hero.skills': 1}, True)
click('Inspector/Btn_Next', '完成同一场真实战斗')
click('Inspector/Btn_EndTurn', '正式回合末头环与实际出牌进入弃牌区',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'headband.remaining': 0, 'hero.skills': 1, 'fame': 7,
       'supply.skills': 'hero_skill_pool_004:07'})
click('Btn_BeginTurn', '同一个真实玩家进入第三回合，保留伤牌及公共技能归属',
      {'journey.samePlayer': 'True', 'hero.id': 'Wolfhawk', 'hero.level': 2,
       'wounds': 2, 'hero.skills': 1, 'fame': 7,
       'supply.skills': 'hero_skill_pool_004:07'})

if '--from' in args:
    for step in steps[:int(args[args.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print('HeadbandDuel', len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions')
