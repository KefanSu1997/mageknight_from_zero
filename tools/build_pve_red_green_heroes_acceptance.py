"""Original red/green Heroes with real damage, healing and a formal round; no asset mutations.

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
    rule = '红勇者红费冰火攻击6实际击杀物抗冰巫妖；绿勇者绿费治疗4按真实部队等级和旧伤消费且不重整；同单位正式跨轮再次格挡攻击'

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
    for card in ['army_2_006','army_2_011','army_009','army_010','army_2_003']:
        target = catalog.index(card)//18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录实际翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点部队' if card in ['army_2_006','army_2_011'] else '选择原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '红勇者、绿勇者、幻术师同属真实玩家')
    click('CampShade/HeroBuild/Btn_Begin', '同一自定玩家开始正式旅程',
          {'hero.level':6, 'units.count':3, 'hero.skills':0, 'hand':5, 'fame':35})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合')
    click('Btn_JourneyHand', '真实手牌与鲜血仪式')
    outside('advanced_card_012')
    click('DeedInputs/Btn_InputColor_0_Green', '原基础额外绿标')
    click('DeedInputs/Btn_ConfirmInputs', '真实手伤1、红晶1变2、绿标1',
          {'red':2,'wounds':1,'tokens.green':1})
    for card in ['basic_card_008','basic_card_011','basic_card_016','basic_card_021']: discard(card)
    click('JourneyHand/Btn_OwnedUnits', '同幻术师先产生原白晶并耗竭')
    click('OwnedUnits/Btn_Unit_2', '真实第三槽幻术师')
    click('OwnedUnits/Btn_Ability_2', '原印刷白晶能力')
    click('OwnedUnits/Btn_Use', '真实白晶和耗竭',{'white':1,'units.ready':'True,True,False'})
    click('OwnedUnits/Btn_Close', '保留同一耗竭幻术师')
    click('JourneyHand/Btn_CloseHand', '原冰巫妖战斗')
    click('Btn_JourneyEncounter_7', '原004寒冰攻击2，护甲4、冰抗和物抗')
    click('Inspector/Btn_Next', '真实格挡阶段')
    click('Inspector/Btn_Next', '实际未格挡寒冰攻击')
    click('Btn_Roster', '同耗竭幻术师承伤')
    click('Units/Btn_Unit_2', '真实护甲2、无寒冰抗性')
    click('Enemy_0/Btn_Target', '实际冰攻击2')
    click('Inspector/Btn_AssignUnit', '将真实幻术师加入实际承伤计划',{'units.plan':'0:0:army_2_003'})
    click('Inspector/Btn_Next', '真正承伤：幻术师创伤1，旧手伤仍1',
          {'units.wounds':'0,0,1','units.ready':'True,True,False','wounds':1},True)
    click('Units/Btn_Unit_0', '原红勇者')
    click('Inspector/Btn_UnitAbility_1', '实际红晶2变1，原冰火攻击6',
          {'red':1,'melee':6,'power.attacks':'Melee:ColdFire:6','units.ready':'False,True,False'},True)
    click('Enemy_0/Btn_Target', '有物抗和冰抗的原004')
    click('Inspector/Btn_Resolve', '真实冰火攻击不被单冰抗或物抗减半',{'kills':1,'fame':40})
    click('Inspector/Btn_Next', '正式战后治疗窗口')
    click('Units/Btn_Unit_1', '原绿勇者')
    click('Inspector/Btn_UnitAbility_1', '实际绿标1变0，原治疗4',
          {'tokens.green':0,'healing':4,'units.ready':'False,False,False'})
    for _ in range(3): click('Inspector/Btn_UnitSupportTarget', '选实际第三槽受伤幻术师')
    click('Inspector/Btn_HealWound', '按真实等级2支付，治疗不重整',
          {'units.wounds':'0,0,0','units.ready':'False,False,False','healing':2,'wounds':1},True)
    click('Inspector/Btn_UnitSupportTarget', '回到英雄真实手伤')
    click('Inspector/Btn_HealWound', '消费1点治疗鲜血仪式同一伤牌',{'healing':1,'wounds':0})
    click('Inspector/Btn_EndTurn', '正式弃牌并清未用治疗点',
          {'hand':0,'journey.discardCount':5,'healing':0,'journey.completedTurns':1,'red':1,'white':1})
    click('Btn_Announce', '空库真实宣布轮末')
    click('Btn_EndRound', '同五张原牌洗回', {'deck.count':5,'journey.dayPart':'Night'})
    click('Btn_PrepareRound', '同三单位正式重整、无伤状态保留',
          {'hand':5,'units.wounds':'0,0,0','units.ready':'True,True,True','journey.samePlayer':'True'},True)
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '同玩家第二回合')
    click('Btn_JourneyHand', '将真实未用原牌标记回合末弃牌')
    for card in ['basic_card_008','basic_card_011','basic_card_016','basic_card_021','advanced_card_012']: discard(card)
    click('JourneyHand/Btn_CloseHand', '同部队第二次战斗')
    click('Btn_JourneyEncounter_0', '原032攻击4护甲3')
    click('Inspector/Btn_Next', '真实格挡阶段')
    click('Btn_Roster', '同绿勇者实际已重整')
    click('Units/Btn_Unit_1', '普通能力实际免费格挡5')
    click('Inspector/Btn_UnitAbility_0', '原格挡5不消耗魔晶',{'block':5,'units.ready':'True,False,True','red':1})
    click('Enemy_0/Btn_Target', '实际原攻击4')
    click('Inspector/Btn_Resolve', '原格挡5实际覆盖4')
    click('Inspector/Btn_Next', '真实承伤阶段')
    click('Inspector/Btn_Next', '正式防御结算未新增伤',{'wounds':0,'attacks.successfullyBlocked':'0:0'})
    click('Units/Btn_Unit_0', '同红勇者普通攻击能力')
    click('Inspector/Btn_UnitAbility_0', '原免费攻击5',{'melee':5,'red':1,'units.ready':'False,False,True'})
    click('Enemy_0/Btn_Target', '真实甲3原敌')
    click('Inspector/Btn_Resolve', '实际普通5击杀，名望40到42',{'kills':1,'fame':42})
    click('Inspector/Btn_Next', '第二战正式完成')
    click('Inspector/Btn_EndTurn', '同单位跨轮两战持续状态',
          {'hand':0,'journey.discardCount':5,'wounds':0,'units.wounds':'0,0,0',
           'units.ready':'False,False,True','red':1,'white':1,'blue':1,'fame':42,
           'journey.completedTurns':2,'journey.completedBattles':2,'journey.samePlayer':'True'})
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
