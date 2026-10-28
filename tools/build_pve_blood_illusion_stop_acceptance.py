"""Blood Ritual strong -> actual unit stop/movement -> next-turn old-wound healing.
Five original cards, existing Commoners resources, legal Lv4 single unit.
Only --write writes the acceptance config. No assets, resource or RNG edits.
"""
import argparse
import json
import re
from pathlib import Path

def build():
    steps=[]
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
            after=[dict(key=k,expected=str(v),rule='鲜血仪式红印刷费、三色独立输入/蓝转晶；白费停攻非格挡；实际移动及旧伤跨回合治疗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    def outside(id,choice,expected=None):
        click('JourneyHand/@card='+id,'真实原卡 '+id)
        click('JourneyHand/Btn_OutsideChoice_'+choice,'原印刷分支')
        click('JourneyHand/Btn_PlayOutside','实际支付原费',expected)
    def move(id,coord,left):
        click('JourneyHand/Btn_CloseHand','返回实际地图')
        click('Btn_JourneyMap','实际邻格')
        click('JourneyMap/Btn_Hex_'+id,'原地形目标')
        click('JourneyMap/Btn_Move','真实地形费用消费',{'journey.position':coord,'movement':left})
        click('JourneyMap/Btn_CloseMap','保留同一位置')
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法五原牌单部队构筑'))
    click('CampShade/Camp/Btn_Loadout','既有红蓝各1晶起点')
    for _ in range(3):click('CampShade/Picker/Btn_NextPage','下一页')
    click('CampShade/Picker/Btn_Select_60','Commoners原资源不改资产')
    click('CampShade/Camp/Btn_ChooseUnits','实际指挥槽')
    scene=Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
    guids=re.findall(r'guid: ([0-9a-f]+)',scene.split('  unitCatalog:',1)[1].split('--- !u!',1)[0])
    ids={re.search(r'guid: ([0-9a-f]+)',f.read_text()).group(1):f.name[:-11] for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
    catalog=[ids[g] for g in guids];page=0
    for id in ['army_2_006','army_2_011','army_2_003']:
        target=catalog.index(id)//18
        while target!=page:
            click('CampShade/UnitPicker/Btn_'+('Next' if target>page else 'Previous')+'Page','原部队目录翻页')
            page+=1 if target>page else -1
        click('CampShade/UnitPicker/Btn_Unit_'+id,'移除旧单位/选择原幻术师 '+id)
    click('CampShade/UnitPicker/Btn_Back','保留一支真实幻术师')
    click('CampShade/Camp/Btn_ChooseCards','五张原卡足够真实消费')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds','清空待选')
    click(p+'Btn_ChooseDeed_basic_card_000','原行进')
    click(p+'Btn_DeedNext','基本第二页')
    click(p+'Btn_ChooseDeed_basic_card_014','原耐力')
    click(p+'Btn_DeedKind_1','原高级行动')
    click(p+'Btn_ChooseDeed_advanced_card_008','原胁迫基础攻击3/声誉−1')
    click(p+'Btn_DeedNext','高级第二页')
    for id in ['012','015']:click(p+'Btn_ChooseDeed_advanced_card_'+id,'原鲜血仪式/再生')
    click(p+'Btn_DoneDeeds','确认五张真实原牌')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法原英雄构筑')
    click('CampShade/HeroBuild/Btn_Hero_Tovak','托瓦克')
    click('CampShade/HeroBuild/Btn_Level_4','等级4，无技能，单部队合法')
    click('CampShade/HeroBuild/Btn_Begin','正式旅程非标准16牌起点',{'hero.level':4,'hand':5,'units.count':1,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1','原白昼战术')
    click('Btn_BeginTurn','正式首回合')
    click('Btn_JourneyHand','实际原手牌')
    outside('advanced_card_012','strong')
    for row,color in enumerate(['Green','Blue','White']):click('DeedInputs/Btn_InputColor_'+str(row)+'_'+color,'独立选择第'+str(row+1)+'色')
    click('DeedInputs/Btn_InputCrystal_Blue','可选把本次蓝魔力转库存晶',capture=True)
    click('DeedInputs/Btn_ConfirmInputs','付原红晶受真实伤，蓝转晶保留绿白标记',
        {'red':0,'blue':2,'tokens.blue':0,'tokens.green':1,'tokens.white':1,'wounds':1})
    outside('basic_card_000','strong',{'movement':4,'tokens.green':0,'green':0})
    move('forest','1,0',1)
    click('Btn_JourneyEncounter_0','实际非城防哨卫032')
    click('Inspector/Btn_Next','正式格挡阶段')
    click('Btn_Roster','实际原部队')
    click('Units/Btn_Unit_0','同一幻术师')
    click('Enemy_0/Btn_Target','实际非城防原敌')
    click('Inspector/Btn_UnitAbility_1','原白费停攻，非格挡',
        {'tokens.white':0,'white':0,'units.ready':'False','enemies.skipped':'0','block':0,'wounds':1,'attacks.successfullyBlocked':''},True)
    click('Inspector/Btn_Next','正式承伤阶段')
    click('Inspector/Btn_Next','停攻没有战斗新增伤，仍保留仪式旧伤',{'wounds':1,'block':0,'enemies.skipped':'0','attacks.successfullyBlocked':''})
    click('Btn_Roster','返回真实手牌')
    click('Hand/Btn_Card_advanced_card_008_0','原胁迫')
    click('Inspector/Btn_Choice_attack','原基础攻击选择')
    click('Inspector/Btn_Play','攻击3并保留原声誉代价',{'melee':3,'reputation':-1})
    click('Enemy_0/Btn_Target','实际哨卫')
    click('Inspector/Btn_Resolve','攻击3击杀原护甲3',{'kills':1,'fame':17,'wounds':1})
    click('Inspector/Btn_Next','正式战斗结束取消效果清理',{'enemies.skipped':''})
    click('Inspector/Btn_EndTurn','真实同玩家第一回合结束')
    click('Btn_BeginTurn','真实库存/旧伤/耗竭跨回合',{'journey.samePlayer':'True','blue':2,'wounds':1,'units.ready':'False'})
    click('Btn_JourneyHand','原耐力和再生仍在实际手牌')
    outside('basic_card_014','strong',{'blue':1,'movement':4})
    move('plains','0,1',2)
    click('Btn_JourneyHand','治疗上回合仪式的同一张伤')
    click('JourneyHand/@card=advanced_card_015','原再生')
    click('JourneyHand/Btn_OutsideChoice_base','治疗1并重整等级1或2')
    click('JourneyHand/Btn_RecoveryUnit_0','同一等级2耗竭幻术师')
    click('JourneyHand/Btn_PlayOutside','无绿费用产生治疗1并重整',{'healing':1,'wounds':1,'units.ready':'True'})
    click('JourneyHand/Btn_HealHero','原治疗消费真实仪式旧伤',{'healing':0,'wounds':0,'blue':1,'units.ready':'True'},True)
    click('JourneyHand/Btn_CloseHand','保留真实消费者结果')
    click('Btn_EndTurn','正式次回合结束，无重建玩家',{'journey.completedTurns':2,'wounds':0,'movement':0,'reputation':-1,'units.ready':'True'})
    return steps

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--write',action='store_true');ap.add_argument('--from',dest='start',type=int,default=0)
    args=ap.parse_args();steps=build()
    for s in steps[:args.start]:s['after']=[];s['skipScreenshot']=True
    if args.write:
        Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=270,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks; written:',args.write)
if __name__=='__main__':main()
