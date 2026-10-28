import json
from pathlib import Path
heroes={'Arythea':'013','Tovak':'020','Goldyx':'019','Norowas':'012','Wolfhawk':'010','Krang':'026','Braevalar':'004'}
steps=[]
def click(path,label,expect=None,capture=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='按真实Player.HeroId复用该英雄原专属牌插画，不改变牌库与规则') for k,v in {'ui.truncated':'',**(expect or {})}.items()]))
for hero,source in list(heroes.items())+[('Tovak','020')]:
 standard=len(steps)==49
 (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','配置原英雄显示'))
 click('CampShade/Camp/Btn_OriginalHero','原英雄入口')
 click('CampShade/HeroStart/Btn_Hero_'+hero,'实际选择'+hero)
 click('CampShade/HeroStart/'+('Btn_StartHero' if standard else 'Btn_ConfiguredHero'),'标准原牌库' if standard else '自定原英雄构筑')
 click('Tactic_day_1/Btn_Tactic_day_1','原战术')
 click('Btn_BeginTurn','正式回合')
 click('Btn_JourneyEncounter_0','实际英雄战斗头像',{'hero.id':hero,'art.hero.source':'basic_card_'+source,'art.hero.visible':'True'},True)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'display-only operations; 8 captures')
