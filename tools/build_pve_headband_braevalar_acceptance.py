"""One real-level-reward suite for Shape/Fork (Braevalar) and Flight (Goldyx).

Invocation builds automation only. --prepare/--restore guard the replay seed;
--from N preserves a successful prefix without repeating its assertions/screenshots.
"""
import json
import sys
from pathlib import Path

args = sys.argv[1:]
variants = {
    'Shape': ('Braevalar', 29, 'hero_skill_pool_001:06'),  # choices 7,6; learn 7
    'Fork': ('Braevalar', 8, 'hero_skill_pool_001:05'),    # choices 0,5; learn 0
    'Flight': ('Goldyx', 29, 'hero_skill_pool_005:06'),    # choices 7,6; learn 7
}
mode = next((a for a in args if a in variants), 'Shape')
chosen_hero, chosen_seed, chosen_public = variants[mode]
base = Path('tools/build_pve_headband_target_acceptance.py')
source = base.read_text(encoding='utf-8').split("if hero != 'Norowas':", 1)[0]
start, end = source.index('VARIANTS = {'), source.index('\nhero = ')
source = source[:start] + 'VARIANTS = ' + repr({chosen_hero: (chosen_seed, chosen_public, 0)}) + source[end:]
source = source.replace('tmp/headband_target_HornGold.original',
                        'tmp/headband_braevalar_' + mode.lower() + '_HornGold.original')
sys.argv = [str(base), chosen_hero] + [a for a in args if a in ('--prepare', '--restore')]
exec(compile(source, str(base), 'exec'))
rule = ('真实原英雄等级1击杀升级，未选技能进入公共区，本人不拥有；'
        '原头环独立逐次材料/目标或一次原飞行，真实费用、贡献、位置与消费者相符')


def battle_headband():
    click('Hand/Btn_Card_items_022_0', '实际手牌启迪头环')
    click('Inspector/Btn_OpenSpecialArtifact', '按原神器选择')
    click('SpecialArtifact/Btn_PublicSkill_0', '真实升级未选的本英雄技能')
    click('SpecialArtifact/Btn_ConfirmSpecial', '原普通借用真实输入')


def target(index):
    click('Enemy_' + str(index) + '/Btn_Target', '本场实际敌人引用')


def sideways(card):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '保留的实际原基本牌')
    click('Inspector/Btn_Sideways', '原横置攻击1')


def finish_battle():
    click('Inspector/Btn_Next', '正式战斗完成')
    click('Inspector/Btn_EndTurn', '同一玩家实际回合结算',
          {'journey.completedTurns': 2, 'journey.completedBattles': 2,
           'hero.skills': 1, 'supply.skills': chosen_public}, True)


if mode == 'Shape':
    click('Btn_JourneyEncounter_0', '第二战真实哨卫')
    for _ in range(3):
        click('Inspector/Btn_Next', '正式阶段至近战，保留真实承伤')
    battle_headband()
    # Basic candidates after the first battle: March, Tranquility, Swiftness (007), Stamina (014).
    click('Shapeshift/Btn_Card_0', '原行进基本移动2')
    click('Shapeshift/Btn_Basic', '原基础效果')
    click('Shapeshift/Btn_Output_0', '选择原固定移动项')
    click('Shapeshift/Btn_Confirm', '第一次实际行进转换攻击2',
          {'melee': 2, 'movement': 0, 'blue': 1, 'headband.remaining': 1,
           'hero.skills': 1, 'supply.skills': chosen_public}, True)
    # Same live hand; March was consumed, so Stamina (014) is now candidate index 2.
    click('Shapeshift/Btn_Card_2', '第二张真实基本牌耐力')
    click('Shapeshift/Btn_Strong', '原蓝色强化费用')
    click('Shapeshift/Btn_Output_0', '耐力原固定移动4')
    click('Shapeshift/Btn_Confirm', '第二次蓝费转换攻击4，首笔2保留',
          {'melee': 6, 'movement': 0, 'blue': 0, 'headband.remaining': 0,
           'hero.skills': 1, 'supply.skills': chosen_public}, True)
    target(0)
    click('Inspector/Btn_Resolve', '实际转换攻击合6击杀原哨卫', {'kills': 1, 'melee': 0}, True)
    finish_battle()
elif mode == 'Fork':
    click('Btn_JourneyEncounter_2', '第二战两个同名原哨卫实体')
    for _ in range(3):
        click('Inspector/Btn_Next', '正式阶段至近战；两敌原攻击实际承伤')
    battle_headband()
    click('EnemyTokenSkill/Btn_Enemy_0', '第一次选择真实敌人A')
    click('EnemyTokenSkill/Btn_Enemy_1', '第一次选择不同真实敌人B')
    click('EnemyTokenSkill/Btn_Confirm', '第一次各绑定冰火远程1',
          {'power.boundAttacks': 'monster_032:ColdFire:1,monster_032:ColdFire:1',
           'headband.remaining': 1, 'hero.skills': 1}, True)
    click('EnemyTokenSkill/Btn_Enemy_0', '第二次重新选择，仅实际敌人A')
    click('EnemyTokenSkill/Btn_Confirm', '第二笔A1追加，保留第一次A1及B1',
          {'power.boundAttacks': 'monster_032:ColdFire:1,monster_032:ColdFire:1,monster_032:ColdFire:1',
           'headband.remaining': 0, 'hero.skills': 1, 'supply.skills': chosen_public}, True)
    sideways('000')
    target(0)
    click('Inspector/Btn_Resolve', '仅A绑定两点加实际横置1击杀A',
          {'kills': 1, 'power.boundAttacks': 'monster_032:ColdFire:1'}, True)
    sideways('001')
    sideways('007')
    target(1)
    click('Inspector/Btn_Resolve', 'B独立绑定1加两张横置2击杀B',
          {'kills': 2, 'power.boundAttacks': ''}, True)
    finish_battle()
else:
    hand_play(0, expect={'movement': 2})
    click('Btn_JourneyMap', '真实地图')
    click('JourneyMap/Btn_Hex_plains', '实际村庄平原')
    click('JourneyMap/Btn_Move', '原行进费用2移动至村庄',
          {'journey.position': '0,1', 'movement': 0})
    click('JourneyMap/Btn_CloseMap', '保留真实位置')
    hand_play(2, 'strong', {'movement': 4, 'blue': 0})  # Stamina after March consumed.
    open_headband(2)  # Tranquility, Swiftness, physical Headband, two real wounds.
    click('JourneyMap/Btn_Hex_desert', '距当前村庄两格的实际安全沙漠', capture=True)
    click('JourneyMap/Btn_Move', '一次原飞行实际支付2，保留余下2移动',
          {'journey.position': '1,-1', 'movement': 2, 'headband.remaining': 0,
           'hero.skills': 1, 'supply.skills': chosen_public}, True)
    # Ordinary following move must originate at the actual flown-to position.
    click('JourneyMap/Btn_Hex_camp', '从真实沙漠落点相邻返回营地')
    click('JourneyMap/Btn_Move', '正常平原费用2消费剩余移动，飞行不遗留免费许可',
          {'journey.position': '0,0', 'movement': 0}, True)
    click('JourneyMap/Btn_CloseMap', '保留真实位置和牌区')
    click('Btn_EndTurn', '正式第二回合结算',
          {'journey.completedTurns': 2, 'journey.completedBattles': 1,
           'hero.skills': 1, 'supply.skills': chosen_public}, True)

if '--from' in args:
    for step in steps[:int(args[args.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print(mode, len(steps), 'steps', sum(len(s['after']) for s in steps), 'checks')
