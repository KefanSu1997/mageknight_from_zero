"""Plan only by default: remaining Sorcerer/Scouts consumers in one real journey.

Reuse the proven loadout UI prefix, not production fixtures or injected state.
--write emits config; --from N replays a successful prefix without repeat evidence.
"""
import argparse
import copy
import importlib.util
import json
import re
from pathlib import Path


def build():
    spec = importlib.util.spec_from_file_location('unit_prefix', Path(__file__).with_name('build_pve_shock_forester_acceptance.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    old = module.build()
    boundary = next(i for i, s in enumerate(old) if s['buttonPath'].endswith('/HeroBuild/Btn_BuildUnits'))
    steps = copy.deepcopy(old[:boundary + 1])
    for step in steps:
        step['after'] = []
        step['skipScreenshot'] = True
        step['buttonPath'] = step['buttonPath'].replace('Btn_Level_6', 'Btn_Level_4')
        if step['buttonPath'].endswith('Btn_Level_4'):
            step['label'] = '原托瓦克等级4：名望15、两个真实指挥槽'
    rule = ('原巫师免费能力0远程3实际击杀；同斥候先以能力2移动2支付实际平原，'
            '正式跨轮同实体能力0攻城1加原蓝费利斧3击杀；不注入资源/重建玩家')

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k, v in {'ui.truncated': '', **(expected or {})}.items()]))

    scene = Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
    guids = re.findall(r'guid: ([0-9a-f]+)', scene.split('  unitCatalog:', 1)[1].split('--- !u!', 1)[0])
    ids = {re.search(r'guid: ([0-9a-f]+)', f.read_text()).group(1): f.name[:-11]
           for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
    catalog = [ids[g] for g in guids]
    page = 0
    for card in ['army_2_006', 'army_2_011', 'army_014', 'army_2_009']:
        target = catalog.index(card) // 18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点单位' if card in ['army_2_006', 'army_2_011'] else '合法选入原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '同玩家两个单位：巫师、斥候')
    click('CampShade/HeroBuild/Btn_Begin', '合法五原牌自定构筑开始；非标准开局',
          {'hero.level': 4, 'fame': 15, 'hero.skills': 0, 'units.count': 2,
           'units.ready': 'True,True', 'hand': 5, 'red': 1, 'blue': 1,
           'journey.samePlayer': 'True'})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '第一真实回合')
    click('Btn_JourneyHand', '原部队移动入口')
    click('JourneyHand/Btn_OwnedUnits', '正式自有部队')
    click('OwnedUnits/Btn_Unit_1', '原斥候')
    click('OwnedUnits/Btn_Ability_2', '原第三能力：移动2及探索许可')
    click('OwnedUnits/Btn_Use', '仅验证移动，不将许可当探索通过',
          {'movement': 2, 'units.ready': 'True,False', 'red': 1, 'blue': 1})
    click('OwnedUnits/Btn_Close', '保留同单位耗竭')
    click('JourneyHand/Btn_CloseHand', '实际位置消费')
    click('Btn_JourneyMap', '当前地图')
    click('JourneyMap/Btn_Hex_plains', '既有实际村庄0,1平原')
    click('JourneyMap/Btn_Move', '斥候移动2支付真实平原费用2',
          {'movement': 0, 'journey.position': '0,1', 'units.ready': 'True,False'}, True)
    click('JourneyMap/Btn_CloseMap', '同一玩家实际位置保留')

    def mark_unused(include_axe=False):
        click('Btn_JourneyHand', '正式回合末弃牌选择')
        for card in ['basic_card_000', 'basic_card_008', 'basic_card_016', 'basic_card_021'] + (['basic_card_011'] if include_axe else []):
            click('JourneyHand/@card=' + card, '实际未用原牌')
            click('JourneyHand/Btn_DiscardAtEnd', '标记实际回合末弃牌')
        click('JourneyHand/Btn_CloseHand', '同一实际手牌进入战斗')

    def axe(choice, expected):
        click('Hand/Btn_Card_basic_card_011_0', '实际利斧投掷')
        click('Inspector/Btn_Choice_' + choice, '原利斧' + choice)
        click('Inspector/Btn_Play', '原费用与远程贡献', expected)

    def end_battle(turn, ready, fame, blue, reputation):
        for _ in range(4):
            click('Inspector/Btn_Next', '远程清敌后完成正式剩余阶段')
        click('Inspector/Btn_EndTurn', '正式回合结束保留同玩家/单位/牌区',
              {'hand': 0, 'journey.discardCount': 5, 'wounds': 0,
               'units.ready': ready, 'units.wounds': '0,0', 'fame': fame,
               'red': 1, 'blue': blue, 'reputation': reputation,
               'journey.completedTurns': turn, 'journey.completedBattles': turn,
               'journey.position': '0,1', 'journey.samePlayer': 'True'})

    mark_unused(include_axe=True)
    click('Btn_JourneyEncounter_0', '既有原032甲3：免费远程3完整消费；非原地图探索')
    click('Btn_Roster', '原巫师免费能力')
    click('Units/Btn_Unit_0', '同巫师')
    click('Inspector/Btn_UnitAbility_0', '免费远程3，不选择白绿去除分支',
          {'ranged': 3, 'units.ready': 'False,False', 'red': 1, 'blue': 1})
    click('Enemy_0/Btn_Target', '实际原032护甲3')
    click('Inspector/Btn_Resolve', '仅巫师免费远程3真正击杀',
          {'kills': 1, 'fame': 17, 'ranged': 0, 'wounds': 0}, True)
    end_battle(1, 'False,False', 17, 1, 0)
    click('Btn_Announce', '空库正式宣布轮末')
    click('Btn_EndRound', '五原牌实际洗回，非重建出战状态',
          {'deck.count': 5, 'journey.dayPart': 'Night'})
    click('Btn_PrepareRound', '同两单位正式重整，旧位置保留',
          {'hand': 5, 'units.ready': 'True,True', 'units.wounds': '0,0',
           'journey.position': '0,1', 'journey.samePlayer': 'True'})
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '第二真实回合')
    mark_unused()
    click('Btn_JourneyEncounter_4', '另一原狼骑兵007甲4：攻城1与远程3缺一不可')
    click('Btn_Roster', '同斥候已正式重整')
    click('Units/Btn_Unit_1', '同实体原斥候')
    click('Inspector/Btn_UnitAbility_0', '原攻城1，无魔力费用',
          {'siege': 1, 'units.ready': 'True,False', 'blue': 1})
    click('Btn_Roster', '实际原牌补足攻击，不重复巫师能力')
    axe('strong', {'siege': 1, 'ranged': 3, 'blue': 0})
    click('Enemy_0/Btn_Target', '实际甲4狼骑兵')
    click('Inspector/Btn_Resolve', '攻城1加真实蓝费远程3击杀；原强化利斧名望+1',
          {'kills': 1, 'fame': 21, 'siege': 0, 'ranged': 0, 'reputation': 0}, True)
    end_battle(2, 'True,False', 21, 0, 0)
    return steps


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    if not 0 <= args.start <= len(steps):
        parser.error('--from must be within the generated operation count')
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False, initialDelaySeconds=2, defaultWaitAfterSeconds=.3,
            maxRunSeconds=420, useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
    print(len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions',
          sum(not s['skipScreenshot'] for s in steps), 'screenshots', 'written' if args.write else 'plan only')


if __name__ == '__main__':
    main()
