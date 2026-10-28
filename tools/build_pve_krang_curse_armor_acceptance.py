"""Krang original Curse armor branch and persistent original card/wound consumer."""
import argparse
import json
from pathlib import Path

def build():
    steps=[]
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
            skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原诅咒减甲4至3，实际攻击3击杀，正式次回合同牌移动与旧伤保留')
            for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','克朗原自身诅咒减甲'))
    click('CampShade/Camp/Btn_Loadout','既有零部队构筑')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage','构筑目录')
    click('CampShade/Picker/Btn_Select_39','原红蓝库存')
    p='CampShade/DeedPicker/'
    click('CampShade/Camp/Btn_ChooseCards','原卡自定构筑')
    click(p+'Btn_ClearDeeds','清空旧选择')
    for card in ['000','001','007','008']: click(p+'Btn_ChooseDeed_basic_card_'+card,'原卡')
    click(p+'Btn_DeedNext','原卡下一页')
    for card in ['014','021','022']: click(p+'Btn_ChooseDeed_basic_card_'+card,'原卡')
    click(p+'Btn_DoneDeeds','实际七原牌')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法自身技能')
    click('CampShade/HeroBuild/Btn_Hero_Krang','原克朗')
    click('CampShade/HeroBuild/Btn_Level_4','等级4')
    click('CampShade/HeroBuild/Btn_Skill_8','原诅咒')
    click('CampShade/HeroBuild/Btn_Begin','同一原玩家',{'hero.id':'Krang','hero.skills':1,'hero.deckTotal':7})
    click('Tactic_day_1/Btn_Tactic_day_1','正式昼战术')
    click('Btn_BeginTurn','正式第一回合')
    click('Btn_JourneyEncounter_4','原狼骑兵007甲4攻击3')
    for _ in range(3): click('Inspector/Btn_Next','正式阶段与承伤')
    click('Btn_Skills','原技能入口',{'wounds':1,'enemies.armor':'4'})
    click('Skills/Btn_Skill_0','本人诅咒')
    click('Inspector/Btn_SkillChoice_1','原护甲减1模式')
    click('Enemy_0/Btn_Target','同一实际原狼骑兵')
    click('Inspector/Btn_UseSkill','实际护甲4减至3',{'enemies.armor':'3','enemies.attacks':'3'},True)
    click('Btn_Skills','实际原手牌')
    click('Hand/Btn_Card_basic_card_021_0','原狂怒')
    click('Inspector/Btn_Choice_attack','原攻击2')
    click('Inspector/Btn_Play','正式原攻击池',{'melee':2})
    click('Hand/Btn_Card_basic_card_014_0','原耐力横置')
    click('Inspector/Btn_Sideways','实际物理攻击追加1',{'melee':3})
    click('Enemy_0/Btn_Target','被诅咒原敌人')
    click('Inspector/Btn_Resolve','攻击3击杀减甲后3，不足原甲4',{'kills':1,'fame':18,'wounds':1},True)
    click('Inspector/Btn_Next','正式战斗结束')
    click('Inspector/Btn_EndTurn','保留实际旧伤与原牌',{'journey.completedTurns':1,'wounds':1})
    click('Btn_BeginTurn','同玩家正式次回合')
    click('Btn_JourneyHand','真实保留原牌')
    click('JourneyHand/@card=basic_card_000','原行进')
    click('JourneyHand/Btn_OutsideChoice_base','原移动2')
    click('JourneyHand/Btn_PlayOutside','实际移动池',{'movement':2})
    click('JourneyHand/Btn_CloseHand','实际地图')
    click('Btn_JourneyMap','既有演练地形')
    click('JourneyMap/Btn_Hex_plains','邻接平原')
    click('JourneyMap/Btn_Move','实际付费2',{'movement':0,'journey.position':'0,1'},True)
    click('JourneyMap/Btn_CloseMap','保留真实位置')
    click('Btn_EndTurn','同玩家七原牌及一真伤',{'journey.completedTurns':2,'journey.completedBattles':1,
        'hero.deckTotal':8,'wounds':1,'journey.samePlayer':'True','fame':18})
    return steps

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--write',action='store_true');ap.add_argument('--from',dest='start',type=int,default=0)
    args=ap.parse_args();steps=build()
    if not 0<=args.start<=len(steps):ap.error('invalid start')
    for step in steps[:args.start]:step['after']=[];step['skipScreenshot']=True
    if args.write:
        Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions')
