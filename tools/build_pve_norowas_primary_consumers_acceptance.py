"""Own Full March + Rally healing, real recruited farmer and three formal turns.

Plan only by default; --write explicitly emits config after the backend gate.
No fixture changes. --from preserves accepted prefixes without recounting them.
"""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = ('原诺洛瓦斯等级4两自身技能；真实招募农民计数全军行进，付森林3；'
            '同农民承原哨卫攻击产生真实伤后，本人重整旗鼓治疗分支，不重整不治英雄；'
            '下一正式回合同单位实际移动与费用，保留原卡区和技能次数')

    def click(path, label, checks=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k,v in {'ui.truncated':'', **(checks or {})}.items()]))

    def hand(): click('Btn_JourneyHand', '当前实际原手牌')
    def close(): click('JourneyHand/Btn_CloseHand', '保留同一玩家状态返回地图')
    def outside(card, checks=None):
        click('JourneyHand/@card=' + card, '按原卡ID选择 ' + card)
        click('JourneyHand/Btn_OutsideChoice_base', '原基础印刷效果')
        click('JourneyHand/Btn_PlayOutside', '正式使用实际原牌', checks)
    def support(index):
        click('JourneyHand/Btn_Support', '本人正式已学技能')
        click('JourneySupport/Btn_OutsideSkill_' + str(index), '本人全军行进' if index==0 else '本人重整旗鼓')
    def back(): click('JourneySupport/Btn_BackSupport', '保留真实技能次数回到手牌')

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '诺洛瓦斯自身技能集中消费者'))
    click('CampShade/Camp/Btn_Loadout', '既有HornGold种子0，不改fixture')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '起点列表下一页')
    click('CampShade/Picker/Btn_Select_39', '零部队既有红蓝各一晶；本路线不使用库存')
    click('CampShade/Camp/Btn_ChooseCards', '六原牌可重放构筑，非标准16牌开局')
    p = 'CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds', '清除旧待选构筑')
    for c in ['000','008']: click(p+'Btn_ChooseDeed_basic_card_'+c, '原行进/承诺')
    click(p+'Btn_DeedNext', '第二页基本原卡')
    for c in ['014','016','021']: click(p+'Btn_ChooseDeed_basic_card_'+c, '原耐力/决心/狂怒')
    click(p+'Btn_DeedKind_3', '原神器第一页')
    click(p+'Btn_ChooseDeed_items_010', '原金币包普通真实招募来源')
    click(p+'Btn_DoneDeeds', '保留六张原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄构筑')
    click('CampShade/HeroBuild/Btn_Hero_Norowas', '诺洛瓦斯原技能池002')
    click('CampShade/HeroBuild/Btn_Level_4', '等级4名望15护甲3、两技能合法')
    for i in [0,2]: click('CampShade/HeroBuild/Btn_Skill_'+str(i), '选择本人原技能00/02')
    click('CampShade/HeroBuild/Btn_Begin', '真实六牌与两技能，不注入部队或创伤',
          {'hero.id':'Norowas','hero.level':4,'hero.armor':3,'hero.skills':2,
           'hand':6,'deck.count':0,'units.count':0,'wounds':0,'fame':15})
    click('Tactic_day_1/Btn_Tactic_day_1', '原白昼战术')
    click('Btn_BeginTurn', '正式第一回合')
    hand(); outside('basic_card_000', {'movement':2}); close()
    click('Btn_JourneyMap', '实际演练地图')
    click('JourneyMap/Btn_Hex_plains', '真实邻格村庄0,1')
    click('JourneyMap/Btn_Move', '行进2真实支付平原2', {'movement':0,'journey.position':'0,1'})
    click('JourneyMap/Btn_CloseMap', '在实际村庄交涉')
    click('Btn_JourneyInteraction', '实际村庄入口')
    click('JourneyInteraction/Btn_BeginInteraction', '本次正式交涉声誉0', {'influence':0})
    click('JourneyInteraction/Btn_CloseInteraction', '保留实际交涉')
    hand(); click('JourneyHand/@card=items_010', '实际原金币包')
    click('JourneyHand/Btn_PlayOutside', '普通影响4名望2，不焚毁', {'influence':4,'fame':17}); close()
    click('Btn_JourneyInteraction', '真实公共部队供应')
    click('JourneyInteraction/Btn_Offer_army_2_006', '选择真实原农民')
    click('JourneyInteraction/Btn_Recruit', '付款4，建立唯一实际农民实例',
          {'influence':0,'units.count':1,'units.ready':'True','units.wounds':0}, True)
    click('JourneyInteraction/Btn_CloseInteraction', '农民未发动，保留就绪')
    click('Btn_EndTurn', '正式第一回合弃行进与金币，保留四原牌',
          {'journey.completedTurns':1,'hand':4,'deck.count':0,'units.ready':'True','wounds':0})
    click('Btn_BeginTurn', '同英雄正式第二回合')
    hand(); support(0)
    click('JourneySupport/Btn_UseOutsideSkill', '仅一支真实已招就绪无伤农民，全军行进1',
          {'movement':1,'units.count':1,'units.ready':'True','units.wounds':0,'journey.outsideSkillUsed':'True'})
    back(); outside('basic_card_014', {'movement':3}); close()
    click('Btn_JourneyMap', '实际邻格森林')
    click('JourneyMap/Btn_Hex_forest', '白昼森林1,0')
    click('JourneyMap/Btn_Move', '原耐力2不足，加入全军1支付森林3',
          {'movement':0,'journey.position':'1,0','units.ready':'True'}, True)
    click('JourneyMap/Btn_CloseMap', '同一实际农民准备承伤')
    click('Btn_JourneyEncounter_0', '演练原032攻击4护甲3，不冒充原地图肆虐')
    for _ in range(2): click('Inspector/Btn_Next', '正式推进至伤害分配')
    click('Btn_Roster', '实际自有部队')
    click('Units/Btn_Unit_0', '同一刚招农民')
    click('Enemy_0/Btn_Target', '同一实际原032攻击')
    click('Inspector/Btn_AssignUnit', '原农民护甲3实际承攻击4')
    click('Inspector/Btn_Next', '正式伤害：部队1伤但仍就绪，余伤给英雄1',
          {'units.wounds':1,'units.ready':'True','units.canActivate':'False','wounds':1}, True)
    click('Btn_Roster', '返回实际战斗原手牌')
    for card in ['basic_card_021','basic_card_016']:
        click('Hand/Btn_Card_'+card+'_0', '原狂怒或决心')
        click('Inspector/Btn_Choice_attack', '原攻击2')
        click('Inspector/Btn_Play', '正式原攻击贡献')
    click('Enemy_0/Btn_Target', '原032护甲3')
    click('Inspector/Btn_Resolve', '真实攻击4击杀，保留实际部队伤和英雄伤',
          {'kills':1,'fame':19,'units.wounds':1,'wounds':1})
    click('Inspector/Btn_Next', '真实战斗结束后才可治疗')
    hand(); support(1)
    click('JourneySupport/Btn_SkillUnit_0', '选择同一受伤农民')
    click('JourneySupport/Btn_OutsideOption_1', '本人原治疗分支；不选择重整')
    click('JourneySupport/Btn_UseOutsideSkill', '原技能治真实部队一伤、英雄伤保留',
          {'units.wounds':0,'units.ready':'True','units.canActivate':'True','wounds':1,
           'journey.outsideSkillUsed':'True','hero.skills':2}, True)
    back(); close()
    click('Inspector/Btn_EndTurn', '第二正式回合，治疗后同部队状态保留',
          {'journey.completedTurns':2,'journey.completedBattles':1,'hand':2,'wounds':1,
           'units.ready':'True','units.wounds':0,'fame':19})
    click('Btn_BeginTurn', '第三正式回合，不重建英雄或部队')
    hand(); support(0)
    click('JourneySupport/Btn_UseOutsideSkill', '原每回合次数重置，已治疗部队再次贡献1', {'movement':1})
    back(); click('JourneyHand/Btn_OwnedUnits', '已治真实农民的后续消费者')
    click('OwnedUnits/Btn_Unit_0', '选择同一原单位')
    click('OwnedUnits/Btn_Ability_2', '原移动2')
    click('OwnedUnits/Btn_Use', '治疗后合法发动，原移动与全军合计3，正式耗竭',
          {'movement':3,'units.ready':'False','units.wounds':0,'wounds':1})
    click('OwnedUnits/Btn_Close', '保留部队真实耗竭')
    click('JourneyHand/@card=basic_card_008', '保留两回合的原承诺')
    click('JourneyHand/Btn_SidewaysMove', '实际横置1并满足正式回合出牌', {'movement':4})
    close(); click('Btn_JourneyMap', '返回真实村庄')
    click('JourneyMap/Btn_Hex_plains', '相邻平原0,1')
    click('JourneyMap/Btn_Move', '同农民实际移动2被正式付款消费',
          {'movement':2,'journey.position':'0,1','units.ready':'False','units.wounds':0})
    click('JourneyMap/Btn_CloseMap', '保留实际位置和英雄旧伤')
    click('Btn_EndTurn', '三正式回合一战，同原部队招募承伤治疗移动闭环',
          {'journey.completedTurns':3,'journey.completedBattles':1,'journey.samePlayer':'True',
           'units.count':1,'units.wounds':0,'units.ready':'False','wounds':1,'fame':19,
           'hero.skills':2,'journey.position':'0,1'}, True)
    return steps


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write',action='store_true')
    parser.add_argument('--from',dest='start',type=int,default=0)
    parser.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    args=parser.parse_args(); steps=build()
    if not 0<=args.start<=len(steps): parser.error('--from is outside the plan')
    for s in steps[:args.start]: s['after']=[]; s['skipScreenshot']=True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,
            useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',
          sum(not s['skipScreenshot'] for s in steps),'key screenshots;', 'written' if args.write else 'plan only')


if __name__=='__main__': main()
