"""Original Heroes influence and Thugs outside interaction with real village consumers.

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
    rule = '勇者原影响5交涉声誉+1实际招募，暴徒原影响4声誉-1实际购买治疗；同玩家正式跨轮保留新招农民并实际攻防'

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
    click(p + 'Btn_ChooseDeed_basic_card_000', '原行军基础移动2')
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
    for card in ['army_2_006','army_008']:
        target = catalog.index(card)//18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录实际翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点部队' if card in ['army_2_006','army_2_011'] else '选择原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '保留原暴徒并选择原蓝勇者，第三槽留给真实招募')
    click('CampShade/HeroBuild/Btn_Begin', '同一自定玩家开始正式旅程',
          {'hero.level':6, 'units.count':2, 'hero.skills':0, 'hand':5, 'fame':35})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合')
    click('Btn_JourneyHand', '原牌实际移动与后续治疗材料')
    outside('basic_card_000')
    click('JourneyHand/Btn_CloseHand', '行军实际移动2')
    click('Btn_JourneyMap', '当前实际地图')
    click('JourneyMap/Btn_Hex_plains', '实际村庄落点')
    click('JourneyMap/Btn_Move', '支付原移动2进入真实村庄',{'movement':0,'journey.position':'0,1','journey.atVillage':'True'})
    click('JourneyMap/Btn_CloseMap', '保留真实位置')
    click('Btn_JourneyHand', '实际原伤牌来源')
    outside('advanced_card_012')
    click('DeedInputs/Btn_InputColor_0_Green', '原基础额外绿标')
    click('DeedInputs/Btn_ConfirmInputs', '真实手伤1和原红晶收益',{'red':2,'wounds':1,'tokens.green':1})
    for card in ['basic_card_008','basic_card_016','basic_card_021']: discard(card)
    click('JourneyHand/Btn_CloseHand', '原村庄交涉')
    click('Btn_JourneyInteraction', '实际可达村庄')
    click('JourneyInteraction/Btn_BeginInteraction', '正式冻结本次原声誉0',{'influence':0,'reputation':0,'journey.interacting':'True'})
    click('JourneyInteraction/Btn_CloseInteraction', '保留同一次交涉')
    click('Btn_JourneyHand', '实际已有部队')
    click('JourneyHand/Btn_OwnedUnits', '原部队战外入口')
    click('OwnedUnits/Btn_Unit_1', '蓝勇者实际原卡面')
    click('OwnedUnits/Btn_Ability_2', '原共用影响力5')
    click('OwnedUnits/Btn_Use', '真实影响5，交涉声誉+1且单位耗竭',
          {'influence':5,'reputation':1,'units.ready':'True,False'},True)
    click('OwnedUnits/Btn_Close', '保留实际交涉资源')
    click('JourneyHand/Btn_CloseHand', '选择实际供应消费者')
    click('Btn_JourneyInteraction', '原供应中的农民')
    click('JourneyInteraction/Btn_Offer_army_2_006', '同一真实原农民供应牌')
    click('JourneyInteraction/Btn_Recruit', '实际支付4占第三合法槽，勇者声誉变化不重算',
          {'influence':1,'units.count':3,'journey.freeSlots':0,'reputation':1,'units.ready':'True,False,True'},True)
    click('JourneyInteraction/Btn_CloseInteraction', '仍为同一次交涉')
    click('Btn_JourneyHand', '原暴徒影响消费者')
    click('JourneyHand/Btn_OwnedUnits', '原部队战外入口')
    click('OwnedUnits/Btn_Unit_0', '原暴徒')
    click('OwnedUnits/Btn_Ability_2', '原影响4声誉减1')
    click('OwnedUnits/Btn_Use', '真实影响1到5、声誉1到0、暴徒耗竭',
          {'influence':5,'reputation':0,'units.ready':'False,False,True'})
    click('OwnedUnits/Btn_Close', '保留同一玩家与原伤')
    click('JourneyHand/Btn_CloseHand', '实际村庄购买')
    click('Btn_JourneyInteraction', '原治疗费用3')
    click('JourneyInteraction/Btn_BuyHealing', '实际支付影响3得到治疗1',{'influence':2,'healing':1})
    click('JourneyInteraction/Btn_CloseInteraction', '实际旧伤消费者')
    click('Btn_JourneyHand', '同一张鲜血仪式伤牌')
    click('JourneyHand/Btn_HealHero', '治疗真实手伤归供应',{'wounds':0,'healing':0},True)
    click('JourneyHand/Btn_CloseHand', '正式结束回合')
    click('Btn_EndTurn', '清剩余影响和临时标记，正式弃原牌',
          {'hand':0,'journey.discardCount':5,'influence':0,'tokens.green':0,'journey.completedTurns':1,'units.ready':'False,False,True'})
    click('Btn_Announce', '空库正式宣布轮末')
    click('Btn_EndRound', '同五张原牌洗回',{'deck.count':5,'journey.dayPart':'Night'})
    click('Btn_PrepareRound', '同三单位真实新轮重整',{'hand':5,'units.ready':'True,True,True','journey.samePlayer':'True'})
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '同玩家后续真实战斗')
    click('Btn_JourneyHand', '本回合未用原牌末弃')
    for card in ['basic_card_000','basic_card_008','basic_card_021','advanced_card_012']: discard(card)
    click('JourneyHand/Btn_CloseHand', '新招农民后续消费者')
    click('Btn_JourneyEncounter_0', '原032攻击4护甲3')
    click('Inspector/Btn_Next', '正式格挡阶段')
    play('basic_card_016','block',{'block':2})
    click('Btn_Roster', '同一实际新招农民')
    click('Units/Btn_Unit_2', '原农民实体与原图')
    click('Inspector/Btn_UnitAbility_0', '原免费格挡2加到4',{'block':4,'units.ready':'True,True,False'})
    click('Enemy_0/Btn_Target', '实际原攻击4')
    click('Inspector/Btn_Resolve', '新招部队格挡实际消费')
    click('Inspector/Btn_Next', '真实承伤阶段')
    click('Inspector/Btn_Next', '完整防御，无新伤',{'wounds':0,'attacks.successfullyBlocked':'0:0'})
    click('Units/Btn_Unit_1', '同一上轮交涉勇者')
    click('Inspector/Btn_UnitAbility_0', '原免费普通攻击5',{'melee':5,'units.ready':'True,False,False'})
    click('Enemy_0/Btn_Target', '实际原甲3')
    click('Inspector/Btn_Resolve', '同勇者新轮真实攻击击杀',{'kills':1,'fame':37},True)
    click('Inspector/Btn_Next', '正式战斗结束')
    click('Inspector/Btn_EndTurn', '连续交涉新招治疗和后战全部保留',
          {'hand':0,'journey.discardCount':5,'units.count':3,'units.ready':'True,False,False','units.wounds':'0,0,0',
           'wounds':0,'reputation':0,'red':2,'blue':1,'journey.samePlayer':'True','journey.completedTurns':2,'journey.completedBattles':1})
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
