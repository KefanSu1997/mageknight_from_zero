"""UI-only selection regression; reuses original loadouts and does not rerun card rules."""
import json
from pathlib import Path

steps = []
def step(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
        waitAfterSeconds=.3, skipScreenshot=not capture,
        after=[dict(key=k, expected=str(v), rule='直接选择真实配置，预览和取消不重建玩家；边界无隐藏项或截断')
               for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

camp = 'CampShade/Camp/'
picker = 'CampShade/Picker/'
(step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '打开当前配置', {'loadout':0, 'draftLoadout':0, 'camp.open':'True'}))
step(camp+'Btn_Loadout', '浏览第一页原配置', {'picker.page':0, 'picker.hasPrevious':'False',
    'picker.visible':','.join(map(str,range(18)))}, True)
for page in range(1,4):
    step(picker+'Btn_NextPage', '直接翻页而不逐项切换', {'picker.page':page, 'draftLoadout':0, 'loadout':0})
step(picker+'Btn_PreviousPage', '上一页边界内返回', {'picker.page':2})
step(picker+'Btn_NextPage', '最后一页仅显示剩余原配置', {'picker.page':3, 'picker.hasNext':'False',
    'picker.visible':','.join(map(str,range(54,61)))}, True)
step(picker+'Btn_Select_60', '直接选择第61项农民与暴徒', {'draftLoadout':60, 'loadout':0, 'picker.kind':0})
step(camp+'Btn_Cancel', '取消预览保留当前玩家', {'camp.open':'False', 'loadout':0, 'units.count':0})
(step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '重新打开恢复实际配置', {'draftLoadout':0}))
step(camp+'Btn_Loadout', '再次浏览配置')
for page in range(1,4): step(picker+'Btn_NextPage', '翻页', {'picker.page':page})
step(picker+'Btn_Select_60', '选择原双部队', {'draftLoadout':60, 'loadout':0})
step(camp+'Btn_Encounter', '浏览全部原敌群', {'picker.kind':2, 'picker.page':0,
    'picker.hasPrevious':'False', 'picker.hasNext':'False', 'picker.visible':','.join(map(str,range(13)))}, True)
step(picker+'Btn_Select_12', '选择末尾敌群仅更新预览', {'draftEncounter':12, 'encounter':0})
step(camp+'Btn_Encounter', '重新浏览选中敌群')
step(picker+'Btn_Select_0', '返回原遗迹哨卫', {'draftEncounter':0})
step(camp+'Btn_Start', '确认后才创建所选原配置', {'loadout':60, 'encounter':0, 'phase':'Ranged',
    'source.units':'army_2_006,army_2_011', 'fame':8, 'units.count':2, 'camp.open':'False'}, True)
(step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '浏览入口定位选中项所在页'))
step(camp+'Btn_Loadout', '选中配置自动定位最后页', {'picker.page':3, 'draftLoadout':60})
step(picker+'Btn_Back', '浏览返回不改变选择', {'picker.kind':0, 'draftLoadout':60})
step(camp+'Btn_Cancel', '返回同一战斗玩家', {'loadout':60, 'phase':'Ranged','fame':8})
step('Inspector/Btn_Next', '所选配置进入实际格挡阶段', {'phase':'Block'})
step('Btn_Roster', '显示选中的两张原部队卡', {'ui.unitArtCount':2})
step('Units/Btn_Unit_0', '选择原农民')
step('Inspector/Btn_UnitAbility_0', '原农民实际格挡和耗竭', {'block':2,'units.ready':'False,True','reputation':0}, True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,
    initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=150,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps), 'UI steps;', sum(len(s['after']) for s in steps), 'checks')
