"""Original Sorcerer and white Heroes across three actual battles and a formal round; no asset mutations.

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
    rule = '原巫师白费去实际城防、绿费去实际抗性并贡献远程3；同部队正式跨轮，白勇者原白费远程7后续真实击杀；原牌、魔晶与伤牌持续保留'

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
    click(p + 'Btn_ChooseDeed_basic_card_008', '原承诺供正式回合末弃牌')
    click(p + 'Btn_DeedNext', '基本第二页')
    for card in ['016','021']:
        click(p + 'Btn_ChooseDeed_basic_card_' + card, '原决心或狂怒')
    click(p + 'Btn_DeedKind_1', '高级原卡')
    click(p + 'Btn_DeedNext', '高级第二页')
    for card in ['012']:
        click(p + 'Btn_ChooseDeed_advanced_card_' + card, '原鲜血仪式')
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
    for card in ['army_2_006','army_2_011','army_014','army_011','army_2_003']:
        target = catalog.index(card)//18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录实际翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点部队' if card in ['army_2_006','army_2_011'] else '选择原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '巫师、白勇者、幻术师同属真实玩家')
    click('CampShade/HeroBuild/Btn_Begin', '同一自定玩家开始正式旅程',
          {'hero.level':6, 'units.count':3, 'hero.skills':0, 'hand':5, 'fame':35})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合')
    click('Btn_JourneyHand', '首回合真实手牌')
    for card in ['basic_card_008','basic_card_016','basic_card_021','advanced_card_012']:
        discard(card)
    click('JourneyHand/Btn_OwnedUnits', '原幻术师白晶能力')
    click('OwnedUnits/Btn_Unit_2', '第三槽同一幻术师')
    click('OwnedUnits/Btn_Ability_2', '印刷白色魔晶')
    click('OwnedUnits/Btn_Use', '真实耗竭换白晶', {'white':1,'units.ready':'True,True,False'})
    click('OwnedUnits/Btn_Close', '保留真实单位')
    click('JourneyHand/Btn_CloseHand', '进入首战')
    click('Btn_JourneyEncounter_10', '原巨鹰017，原城防护甲4')
    click('Btn_Roster', '部队原卡')
    click('Units/Btn_Unit_0', '第一槽巫师')
    click('Enemy_0/Btn_Target', '去城防指向实际巨鹰')
    click('Inspector/Btn_UnitAbility_1', '白晶1变0，失去城防并贡献远程3',
          {'white':0,'ranged':3,'enemies.fortified':'False','units.ready':'False,True,False'}, True)
    click('Btn_Roster', '原手牌真实追加')
    play('basic_card_011','base', {'ranged':4})
    click('Enemy_0/Btn_Target', '去城防后普通远程允许目标')
    click('Inspector/Btn_Resolve', '远程4击败甲4巨鹰', {'kills':1,'fame':38,'ranged':0})
    for _ in range(4): click('Inspector/Btn_Next', '正式完成剩余战斗阶段')
    click('Inspector/Btn_EndTurn', '真实五原牌进入弃牌堆',
          {'hand':0,'journey.discardCount':5,'wounds':0,'journey.completedTurns':1})
    click('Btn_Announce', '空牌库正式宣布轮末')
    click('Btn_EndRound', '同五牌洗回原牌库', {'deck.count':5,'journey.dayPart':'Night'})
    click('Btn_PrepareRound', '正式新轮重整同三部队、抽回实际五牌',
          {'hand':5,'units.ready':'True,True,True','units.wounds':'0,0,0','journey.samePlayer':'True'},True)
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '实际第二回合')
    click('Btn_JourneyHand', '原牌鲜血仪式')
    outside('advanced_card_012')
    click('DeedInputs/Btn_InputColor_0_Green', '原基础红晶加1与绿标')
    click('DeedInputs/Btn_ConfirmInputs', '真实红晶1变2、手伤1、绿标1',
          {'red':2,'wounds':1,'tokens.green':1})
    discard('basic_card_008')
    click('JourneyHand/Btn_CloseHand', '保留两张原牌到下回合')
    click('Btn_JourneyEncounter_7', '既有冰巫妖004，实际寒冰抗性护甲4')
    click('Btn_Roster', '同一巫师正式已重整')
    click('Units/Btn_Unit_0', '同原部队实体')
    click('Enemy_0/Btn_Target', '去抗性指向真实冰巫妖')
    click('Inspector/Btn_UnitAbility_2', '绿标1变0，去除真实抗性并贡献远程3',
          {'tokens.green':0,'ranged':3,'enemies.resistancesRemoved':'0','units.ready':'False,True,True'}, True)
    click('Btn_Roster', '同原利斧真实再次出牌')
    play('basic_card_011','base', {'ranged':4})
    click('Enemy_0/Btn_Target', '原冰巫妖实际目标')
    click('Inspector/Btn_Resolve', '实际远程4击杀，名望38到43',{'kills':1,'fame':43,'wounds':1})
    for _ in range(4): click('Inspector/Btn_Next', '完成第二战正式阶段')
    click('Inspector/Btn_EndTurn', '保留同伤与两张尚未使用原牌',
          {'hand':3,'wounds':1,'journey.discardCount':3,'journey.completedTurns':2})
    click('Btn_BeginTurn', '同一轮正式第三回合')
    click('Btn_JourneyHand', '实际原手牌与幻术师')
    for card in ['basic_card_016','basic_card_021']: discard(card)
    click('JourneyHand/Btn_OwnedUnits', '同一幻术师此轮尚未使用')
    click('OwnedUnits/Btn_Unit_2', '真实第三槽')
    click('OwnedUnits/Btn_Ability_2', '第二轮原白晶')
    click('OwnedUnits/Btn_Use', '耗竭并产生白晶',{'white':1,'units.ready':'False,True,False'})
    click('OwnedUnits/Btn_Close', '保留真实耗竭')
    click('JourneyHand/Btn_CloseHand', '后续勇者消费')
    click('Btn_JourneyEncounter_0', '原032守卫')
    click('Btn_Roster', '白勇者原卡')
    click('Units/Btn_Unit_1', '原白勇者')
    click('Inspector/Btn_UnitAbility_1', '真实白晶1变0，原远程7',
          {'white':0,'ranged':7,'units.ready':'False,False,False'}, True)
    click('Enemy_0/Btn_Target', '原守卫实际消费者')
    click('Inspector/Btn_Resolve', '远程7真实击杀且旧手伤仍1', {'kills':1,'fame':45,'wounds':1})
    for _ in range(4): click('Inspector/Btn_Next', '完成第三战正式阶段')
    click('Inspector/Btn_EndTurn', '三场战斗同玩家同单位，原牌与伤牌实际守恒',
          {'hand':1,'wounds':1,'journey.discardCount':5,'red':2,'blue':1,
           'units.ready':'False,False,False','journey.completedTurns':3,
           'journey.completedBattles':3,'journey.samePlayer':'True','fame':45})
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
