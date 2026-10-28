"""One original Heroes colour pays the shared fee at an actual Mage Tower.

Default plan-only; --write emits config, --from replays without repeat evidence.
No fixture, scene or production edits. Backend gate: PveHeroesRealAssaultJourneyTests.
"""
import argparse
import copy
import importlib.util
import json
import re
from pathlib import Path


def build():
    spec = importlib.util.spec_from_file_location('tower_prefix', Path(__file__).with_name('build_pve_tower_recruitment_acceptance.py'))
    source = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(source)
    old = source.build()
    stop = next(i for i, s in enumerate(old) if s['buttonPath'].endswith('JourneyHand/@card=items_007'))
    steps = []
    rule = '真实法师塔突袭且忽略地点城防，原承诺影响2与白魔力1整笔支付，原白勇者远程7击杀同守军，正式征服/回合保持同单位'

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k, v in {'ui.truncated': '', **(expected or {})}.items()]))

    for original in old[:stop]:
        step = copy.deepcopy(original)
        path = step['buttonPath']
        if path.endswith(('Btn_ChooseDeed_magic_009', 'Btn_ChooseDeed_items_010')):
            continue
        step['after'] = []
        step['skipScreenshot'] = True
        step['buttonPath'] = path.replace('Btn_Level_6', 'Btn_Level_4')
        if path.endswith('Btn_Level_6'):
            step['label'] = '原托瓦克等级4、名望15、两个指挥槽'
        if path.endswith('HeroBuild/Btn_Begin'):
            click('CampShade/HeroBuild/Btn_BuildUnits', '合法单位选择，不模拟招募')
            scene = Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
            guids = re.findall(r'guid: ([0-9a-f]+)', scene.split('  unitCatalog:', 1)[1].split('--- !u!', 1)[0])
            ids = {re.search(r'guid: ([0-9a-f]+)', f.read_text()).group(1): f.name[:-11]
                   for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
            for _ in range([ids[g] for g in guids].index('army_011') // 18):
                click('CampShade/UnitPicker/Btn_NextPage', '原目录实际翻页')
            click('CampShade/UnitPicker/Btn_Unit_army_011', '原白勇者，同一共用突袭费用代表')
            click('CampShade/UnitPicker/Btn_Back', '一支III级单位使用合法指挥槽')
            click('CampShade/HeroBuild/Btn_Begin', '正式旅程合法自定五原牌',
                  {'hero.level': 4, 'fame': 15, 'units.count': 1, 'units.ready': 'True',
                   'hand': 5, 'red': 1, 'blue': 1})
            continue
        steps.append(step)
        if path.endswith('Btn_ClearDeeds'):
            click('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_008', '原承诺基础影响2')
            click('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_000', '第五张原行进保留，不伪造费用')

    click('JourneyHand/@card=items_007', '原绿宝石戒指')
    click('JourneyHand/Btn_ArtifactNormal', '正常绿晶和绿标')
    click('JourneyHand/Btn_PlayOutside', '原真实费用材料', {'green': 1, 'tokens.green': 1, 'fame': 16})
    click('JourneyHand/@card=items_015', '原黑暗护符')
    click('JourneyHand/Btn_ArtifactBurn', '原三颗独立颜色魔力')
    # Inputs reset to Red(0), Blue(1), White(3): Black/White/White = 5/2/0 clicks.
    for row, count in enumerate([5, 2, 0]):
        for _ in range(count):
            click(f'JourneyHand/Btn_ArtifactColor_{row}', '实际选择黑/白/白')
    click('JourneyHand/Btn_PlayOutside', '焚毁护符取得黑1白2及昼黑许可',
          {'tokens.black': 1, 'tokens.white': 2, 'removed.ids': 'items_015', 'mana.allowBlackAtDay': 'True'})
    click('JourneyHand/@card=magic_004', '实际原地道')
    click('JourneyHand/Btn_OutsideChoice_strong', '真实突袭终点')
    click('JourneyHand/Btn_PlayOutside', '原路径输入')
    click('DeedInputs/Btn_Path_mountain', '真实中间山脉0,-1')
    click('DeedInputs/Btn_Path_tower', '实际法师塔-1,-1')
    click('DeedInputs/Btn_ConfirmInputs', '绿黑费实际支付，同紫守军进入战斗',
          {'tokens.green': 0, 'tokens.black': 0, 'tokens.white': 2,
           'world.tunnel': 'True', 'world.ignoreSite': 'True', 'journey.position': '-1,-1',
           'reputation': -1, 'influence': 0})
    click('Hand/Btn_Card_basic_card_008_0', '真实手牌承诺')
    click('Inspector/Btn_Choice_base', '原基础影响2，无魔力费')
    click('Inspector/Btn_Play', '在真实突袭战斗中产生激活费用', {'influence': 2, 'tokens.white': 2})
    click('Btn_Roster', '原白勇者实际卡面/印刷额外费用')
    click('Units/Btn_Unit_0', '同一自有白勇者')
    click('Inspector/Btn_UnitAbility_1', '实际白魔力1和影响2整笔支付，地道不豁免费用',
          {'influence': 0, 'tokens.white': 1, 'ranged': 7, 'units.ready': 'False',
           'world.tunnel': 'True', 'world.ignoreSite': 'True'}, True)
    click('Enemy_0/Btn_Target', '同一实际法师塔原002守军甲5')
    click('Inspector/Btn_Resolve', '白勇者远程7真正击杀实际守军', {'kills': 1, 'fame': 20, 'wounds': 0})
    for _ in range(4):
        click('Inspector/Btn_Next', '正式完成剩余战斗阶段')
    click('Inspector/Btn_FinishSite', '正式征服与原守军弃置，不预设Conquered',
          {'world.conquered': '-1:-1', 'world.discard': 'monster_002', 'world.rewardPending': 'True'}, True)
    click('SiteReward/Btn_Reward_0', '选择真实当前法术供应奖励')
    click('Inspector/Btn_EndTurn', '正式回合末领奖并清临时魔力，保留同单位耗竭',
          {'journey.completedTurns': 1, 'journey.completedBattles': 1, 'hand': 2,
           'units.ready': 'False', 'wounds': 0, 'fame': 20, 'influence': 0,
           'tokens.white': 0, 'world.rewardPending': 'False', 'world.tunnel': 'False',
           'journey.position': '-1,-1', 'journey.samePlayer': 'True'})
    click('Btn_BeginTurn', '正式次回合同一玩家和同一勇者保留真实征服位置',
          {'units.ready': 'False', 'units.count': 1, 'journey.position': '-1,-1',
           'world.conquered': '-1:-1', 'journey.samePlayer': 'True'}, True)
    return steps


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    if not 0 <= args.start <= len(steps):
        parser.error('--from outside route')
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False, initialDelaySeconds=2, defaultWaitAfterSeconds=.3,
            maxRunSeconds=420, useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
    print(len(steps), 'operations', sum(len(s['after']) for s in steps), 'assertions',
          sum(not s['skipScreenshot'] for s in steps), 'captures', 'written' if args.write else 'plan only')


if __name__ == '__main__':
    main()
