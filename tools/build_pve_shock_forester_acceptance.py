"""Original Shock Troops / Foresters consumers across a real round; no asset mutations.

Default builds/prints the plan only. --write writes the acceptance configuration;
--from N replays passed operations without duplicate evidence. Reuses existing
backend proofs; this generator itself does not claim scene success.
"""
import argparse
import json
import re
from pathlib import Path


def build():
    steps = []
    rule = '奇袭原远程1及其他部队攻击+1排除自己和原牌；正式跨轮同单位减甲减攻各1，护林人格挡3与原狂怒攻击2实际消费'

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

    def outside(card, choice='base'):
        click('JourneyHand/@card=' + card, '实际原牌 ' + card)
        click('JourneyHand/Btn_OutsideChoice_' + choice, '原印刷模式 ' + choice)
        click('JourneyHand/Btn_PlayOutside', '展开原效果及实际输入')

    def discard(card):
        click('JourneyHand/@card=' + card, '实际原牌回合末选择')
        click('JourneyHand/Btn_DiscardAtEnd', '标记正式回合末弃牌')

    def play(card, choice=None, expected=None):
        click('Hand/Btn_Card_' + card + '_0', '实际原牌战斗消费')
        if choice is None:
            click('Inspector/Btn_Sideways', '原牌横置1', expected)
        else:
            click('Inspector/Btn_Choice_' + choice, '原印刷效果')
            click('Inspector/Btn_Play', '正式出牌与贡献', expected)

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '合法三单位连续构筑'))
    click('CampShade/Camp/Btn_Loadout', '既有Commoners原资源起点')
    for _ in range(3): click('CampShade/Picker/Btn_NextPage', '下一页')
    click('CampShade/Picker/Btn_Select_60', '既有红蓝各一晶，不改资源/种子/敌群')
    click('CampShade/Camp/Btn_ChooseCards', '明确五牌自定快照，不是标准16牌')
    p = 'CampShade/DeedPicker/'
    click(p + 'Btn_ClearDeeds', '清除待选旧牌')
    click(p + 'Btn_ChooseDeed_basic_card_011', '原利斧投掷基础远程1')
    click(p + 'Btn_ChooseDeed_basic_card_000', '原行军供正式弃牌')
    click(p + 'Btn_ChooseDeed_basic_card_008', '原承诺供正式回合末弃牌')
    click(p + 'Btn_DeedNext', '基本第二页')
    for card in ['016','021']:
        click(p + 'Btn_ChooseDeed_basic_card_' + card, '原决心或狂怒')
    click(p + 'Btn_DoneDeeds', '五张实际原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法等级及指挥槽')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35，三个真实指挥槽')
    click('CampShade/HeroBuild/Btn_BuildUnits', '实际原部队目录')
    scene = Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
    guids = re.findall(r'guid: ([0-9a-f]+)', scene.split('  unitCatalog:',1)[1].split('--- !u!',1)[0])
    ids = {re.search(r'guid: ([0-9a-f]+)',f.read_text()).group(1):f.name[:-11]
           for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
    catalog = [ids[g] for g in guids]
    page = 0
    for card in ['army_2_006','army_2_011','army_2_010','army_2_012','army_2_000']:
        target = catalog.index(card)//18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录实际翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点部队' if card in ['army_2_006','army_2_011'] else '选择原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '奇袭队、弩手、护林人同属真实玩家')
    click('CampShade/HeroBuild/Btn_Begin', '同一自定玩家开始正式旅程',
          {'hero.level':6, 'units.count':3, 'hero.skills':0, 'hand':5, 'fame':35})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合')
    click('Btn_JourneyHand', '标记四张实际未用原牌')
    for card in ['basic_card_000','basic_card_008','basic_card_016','basic_card_021']: discard(card)
    click('JourneyHand/Btn_CloseHand', '同玩家首战')
    click('Btn_JourneyEncounter_4', '原007狼骑兵护甲4无物抗，实际远程消费者')
    click('Btn_Roster', '实际自有部队')
    click('Units/Btn_Unit_0', '奇袭队原能力一')
    click('Inspector/Btn_UnitAbility_0', '原远程1和其他部队加值', {'ranged':1,'units.ready':'False,True,True'})
    click('Units/Btn_Unit_1', '真实弩手原远程2')
    click('Inspector/Btn_UnitAbility_1', '弩手2获原奇袭+1，合计4', {'ranged':4,'units.ready':'False,False,True'},True)
    click('Btn_Roster', '原卡独立来源')
    play('basic_card_011','base',{'ranged':5})
    click('Enemy_0/Btn_Target', '原狼骑兵护甲4')
    click('Inspector/Btn_Resolve', '真实远程5击杀，原牌仍只有1',{'kills':1,'fame':38,'ranged':0})
    for _ in range(4): click('Inspector/Btn_Next', '正式完成首战剩余阶段')
    click('Inspector/Btn_EndTurn', '同玩家五原牌正式弃入牌库循环',
          {'hand':0,'journey.discardCount':5,'wounds':0,'units.ready':'False,False,True','journey.completedTurns':1})
    click('Btn_Announce', '空库正式宣布轮末')
    click('Btn_EndRound', '真实五原牌洗回，非重建快照',{'deck.count':5,'journey.dayPart':'Night'})
    click('Btn_PrepareRound', '同三部队正式重整',{'hand':5,'units.ready':'True,True,True','units.wounds':'0,0,0','journey.samePlayer':'True'},True)
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '同玩家第二回合')
    click('Btn_JourneyHand', '标记实际未用原牌')
    for card in ['basic_card_000','basic_card_008','basic_card_011','basic_card_016']: discard(card)
    click('JourneyHand/Btn_CloseHand', '同三部队后续战斗')
    click('Btn_JourneyEncounter_0', '原032攻击4甲3')
    click('Inspector/Btn_Next', '正式格挡阶段')
    click('Btn_Roster', '同奇袭队跨轮能力二')
    click('Units/Btn_Unit_0', '真实奇袭队')
    click('Enemy_0/Btn_Target', '实际单敌单攻击槽')
    click('Inspector/Btn_UnitAbility_1', '原甲和一次攻击各减1',
          {'enemies.armor':'2','enemies.attacks':'3','units.ready':'False,True,True'},True)
    click('Units/Btn_Unit_2', '同护林人格挡能力')
    click('Inspector/Btn_UnitAbility_1', '原免费格挡3',{'block':3,'units.ready':'False,True,False'})
    click('Enemy_0/Btn_Target', '实际减后攻击3')
    click('Inspector/Btn_Resolve', '护林人格挡3实际消费')
    click('Inspector/Btn_Next', '正式承伤阶段')
    click('Inspector/Btn_Next', '实际防御结算，玩家及单位无新伤',
          {'attacks.successfullyBlocked':'0:0','wounds':0,'units.wounds':'0,0,0'})
    click('Btn_Roster', '原狂怒消费者')
    play('basic_card_021','attack',{'melee':2,'red':1,'blue':1})
    click('Enemy_0/Btn_Target', '减后实际甲2')
    click('Inspector/Btn_Resolve', '原基础攻击2击杀减甲敌',{'kills':1,'fame':40},True)
    click('Inspector/Btn_Next', '第二场真实战斗完成')
    click('Inspector/Btn_EndTurn', '实际两战跨轮保留同玩家与三单位',
          {'hand':0,'journey.discardCount':5,'wounds':0,'units.wounds':'0,0,0','units.ready':'False,True,False',
           'red':1,'blue':1,'fame':40,'journey.completedTurns':2,'journey.completedBattles':2,'journey.samePlayer':'True'})
    return steps

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    assert 0 <= args.start <= len(steps)
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,
            maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions',
          sum(not s['skipScreenshot'] for s in steps),'screenshots','written' if args.write else 'plan only')

if __name__ == '__main__': main()
