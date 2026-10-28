"""Seven heroes through the normal skill-build entry, with stale rehearsal state."""
import json
from pathlib import Path
steps=[]
def c(path,label,expected=None,shot=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,
        after=[dict(key=k,expected=str(v),rule='正常构筑使用所选英雄原16牌；正式换牌与跨入口状态隔离') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
c('Btn_Menu','打开战斗菜单')
c('Btn_Camp','出战配置')
c('CampShade/Camp/Btn_Loadout','固定已有无部队资源配置')
for _ in range(2):c('CampShade/Picker/Btn_NextPage','翻页')
c('CampShade/Picker/Btn_Select_39','原红蓝各1，无部队')
c('CampShade/Camp/Btn_ChooseCards','先遗留旧演练手牌编辑状态')
c('CampShade/DeedPicker/Btn_ClearDeeds','空的手牌草稿不应影响完整英雄牌库')
c('CampShade/DeedPicker/Btn_DoneDeeds','保留空草稿返回')
c('CampShade/Camp/Btn_SmallDeckHeroBuild','先进入明确小牌库模式')
c('CampShade/HeroBuild/Btn_Back','离开小牌库模式')
for n,hero in enumerate(['Tovak','Arythea','Goldyx','Norowas','Wolfhawk','Krang','Braevalar']):
    if n:
        c('Btn_Menu','打开战斗菜单')
        c('Btn_Camp','从上一英雄战斗重新配置')
    c('CampShade/Camp/Btn_HeroBuild','正常原16牌技能构筑，不能继承旧小牌库')
    c('CampShade/HeroBuild/Btn_Hero_'+hero,'切换原英雄 '+hero)
    c('CampShade/HeroBuild/Btn_Level_4','与用户截图一致：名望15护甲3')
    if n==0:c('CampShade/HeroBuild/Btn_Skill_0','托瓦克本人的合法原技能')
    c('CampShade/HeroBuild/Btn_Begin','原完整牌库真实准备轮次 '+hero,
      {'hero.id':hero,'hero.level':4,'hero.armor':3,'fame':15,'hero.skills':1 if n==0 else 0,'hero.deckTotal':16,'hand':5,'deck.count':11,'red':1,'blue':1})
    c('Tactic_day_2/Btn_Tactic_day_2','三思后行 '+hero)
    c('Btn_TacticCard_0','选择第一张原牌')
    c('Btn_TacticCard_1','选择第二张原牌',{'tactic.selected':2,'tactic.replacementPreview':2,'tactic.warningVisible':'False'})
    c('Btn_ConfirmTactic','正式换2补2 '+hero,{'hand':5,'deck.count':11,'hero.deckTotal':16,'journey.tacticResolved':'True'},n==0)
    c('Btn_BeginTurn','实际开始回合',{'hand':5,'deck.count':11})
    c('Btn_JourneyEncounter_1','兽人战团 '+hero,{'hand':5,'deck.count':11,'hero.id':hero,'fame':15},n in (0,6))
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
