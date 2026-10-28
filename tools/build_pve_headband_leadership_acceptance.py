"""Real level reward, Loyalty recruitment and burned Command Banner feed Leadership.

Builds automation only when invoked; no Unity execution. Reuses the target suite's
real level-one opening. --prepare/--restore guard only the existing replay seed.
"""
import json
import sys
from pathlib import Path

args = sys.argv[1:]
base = Path('tools/build_pve_headband_target_acceptance.py')
source = base.read_text(encoding='utf-8').split("if hero != 'Norowas':", 1)[0]
source = source.replace("'Norowas': (2, 'hero_skill_pool_002:02', 1)",
                        "'Norowas': (29, 'hero_skill_pool_002:06', 0)")
source = source.replace('tmp/headband_target_HornGold.original',
                        'tmp/headband_leadership_HornGold.original')
source = source.replace("first_page = ['000', '001', '008' if hero == 'Norowas' else '007']",
                        "first_page = ['000', '001']")
source = source.replace("click(p + 'Btn_ChooseDeed_items_022', '原启迪头环')",
                        "click(p + 'Btn_ChooseDeed_items_017', '原指挥旌旗')\n"
                        "click(p + 'Btn_ChooseDeed_items_022', '原启迪头环')")
source = source.replace("+ ',items_022'", "+ ',items_017,items_022'")
sys.argv = [str(base), 'Norowas'] + [a for a in args if a in ('--prepare', '--restore')]
exec(compile(source, str(base), 'exec'))
rule = ('同一诺若威斯等级1真实击杀升级取得忠诚，领导留公共区；真实招募两部队，'
        '头环普通每次真实激活并耗竭，攻击加2，原贡献用于实际击杀，禁止假拥有技能')

hand_play(0, expect={'movement': 2})
click('Btn_JourneyMap', '实际地图')
click('JourneyMap/Btn_Hex_plains', '村庄平原')
click('JourneyMap/Btn_Move', '实际移动2进入村庄', {'journey.position': '0,1', 'movement': 0})
click('JourneyMap/Btn_CloseMap', '返回实际村庄')
click('Btn_JourneyInteraction', '当前真实供应')
click('JourneyInteraction/Btn_BeginInteraction', '正式村庄交涉')
click('JourneyInteraction/Btn_Offer_army_2_006', '原农民')
click('JourneyInteraction/Btn_LoyaltySlot', '选择实际取得的忠诚专属槽')
click('JourneyInteraction/Btn_Recruit', '原农民4减5至0实际招募',
      {'units.count': 1, 'units.ready': 'True', 'loyalty.bound': 'army_2_006',
       'command.ordinaryFree': 1, 'influence': 0}, True)
click('JourneyInteraction/Btn_CloseInteraction', '保留同一交涉及真实农民')
hand_open()
click('JourneyHand/Btn_Hand_2', '真实手牌指挥旌旗')
click('JourneyHand/Btn_PlayOutside', '选择原神器实际供应')
click('SpecialArtifact/Btn_SpecialBurn', '原焚毁免费招募')
click('SpecialArtifact/Btn_FreeOffer_1', '实际供应北境僧侣')
click('SpecialArtifact/Btn_ConfirmSpecial', '焚毁实体指挥旗取得僧侣，占普通槽',
      {'units.count': 2, 'units.ready': 'True,True', 'units.wounds': '0,0',
       'command.ordinaryFree': 0, 'removed.ids': 'items_017', 'fame': 5}, True)
hand_close()
click('Btn_EndTurn', '正式第二回合结束，保留实体头环与两支就绪部队',
      {'journey.completedTurns': 2, 'units.ready': 'True,True', 'hand': 5})
click('Btn_BeginTurn', '同一玩家第三回合')
click('Btn_JourneyEncounter_0', '原哨卫后续实际战斗')
for _ in range(3):
    click('Inspector/Btn_Next', '正式推进阶段，英雄承担原攻击，部队保留就绪')
click('Hand/Btn_Card_items_022_0', '同一实际手牌启迪头环')
click('Inspector/Btn_OpenSpecialArtifact', '原头环选择公共技能')
click('SpecialArtifact/Btn_PublicSkill_0', '真实升级未选的领导')
click('SpecialArtifact/Btn_ConfirmSpecial', '领导借用逐次真实激活')
click('Units/Btn_Unit_0', '真实就绪农民')
click('Inspector/Btn_UnitAbility_0', '农民原攻击2加领导2',
      {'melee': 4, 'units.ready': 'False,True', 'headband.remaining': 1,
       'hero.skills': 1, 'supply.skills': 'hero_skill_pool_002:06'}, True)
click('Units/Btn_Unit_1', '第二次独立实际就绪北境僧侣')
click('Inspector/Btn_UnitAbility_0', '僧侣原攻击3加领导2',
      {'melee': 9, 'units.ready': 'False,False', 'headband.remaining': 0,
       'hero.skills': 1, 'supply.skills': 'hero_skill_pool_002:06'}, True)
click('Enemy_0/Btn_Target', '实际哨卫目标')
click('Inspector/Btn_Resolve', '两笔真实攻击贡献9击杀哨卫', {'kills': 1}, True)
click('Inspector/Btn_Next', '实际战斗完成')
click('Inspector/Btn_EndTurn', '保留真实两战与两次耗竭结算',
      {'journey.completedTurns': 3, 'journey.completedBattles': 2,
       'units.ready': 'False,False', 'removed.ids': 'items_017'}, True)
if '--from' in args:
    for step in steps[:int(args[args.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print('HeadbandLeadership', len(steps), 'steps', sum(len(s['after']) for s in steps), 'checks')
