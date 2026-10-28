"""Incremental original-unit catalog/UI smoke; not a full per-unit acceptance batch."""
import json
import sys
from pathlib import Path

steps = []
def step(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
        waitAfterSeconds=.3, skipScreenshot=not capture,
        after=[dict(key=k, expected=str(v), rule='原部队同源素材、实际指挥槽、预览不改玩家、强制原承伤消费者')
               for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

camp = 'CampShade/Camp/'
picker = 'CampShade/UnitPicker/'
(step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '打开部队预览', {'units.count':0, 'draft.commandSlots':1}))
step(camp+'Btn_ChooseUnits', '浏览统一原部队目录', {'picker.kind':3})
step(picker+'Btn_NextPage', '新增九种原部队均可点选', {'picker.page':1,
    'catalog.visibleUnits':'army_001,army_2_000,army_2_003,army_014,army_008,army_009,army_010,army_011,army_005,army_2_010'}, True)
step(picker+'Btn_Unit_army_2_010', '原奇袭队占用一个真实指挥槽', {'draft.units':'army_2_010', 'units.count':0})
step(picker+'Btn_Back', '预览选择不改变当前玩家')
step(camp+'Btn_Cancel', '取消原部队选择', {'units.count':0, 'camp.open':'False'})
(step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '重新打开恢复实际部队', {'draft.units':''}))
step(camp+'Btn_ChooseUnits', '重选原部队')
step(picker+'Btn_NextPage', '定位奇袭队')
step(picker+'Btn_Unit_army_2_010', '确认一个槽位的原部队', {'draft.units':'army_2_010'})
step(picker+'Btn_Back', '返回原配置预览')
step(camp+'Btn_Start', '应用原部队且不增加名望等级', {'source.units':'army_2_010','units.count':1,'fame':0,'phase':'Ranged'})
step('Inspector/Btn_Next', '进入正式格挡阶段', {'phase':'Block'})
step('Enemy_0/Btn_Target', '选择本场原兽人')
step('Btn_Roster', '显示所选原奇袭队卡面', {'ui.unitArtCount':1})
step('Units/Btn_Unit_0', '选择原奇袭队能力', capture=True)
step('Inspector/Btn_UnitAbility_2', '对实际攻击减三并绑定先承伤', {'units.ready':'False', 'units.wounds':'0',
    'enemies.attacks':1, 'units.forcedDamage':'强制先承伤：兽人骷髅战士 #1 → 奇袭队。\n'}, True)
step('Inspector/Btn_Next', '实际进入分配伤害', {'phase':'AssignDamage'})
step('Inspector/Btn_Next', '正式消费者先伤奇袭队再处理溢出', {'phase':'Melee','units.wounds':'1','wounds':0,'realWounds':0}, True)

# Replay only ordinary setup + the failed/changed catalog display and unresolved consumer.
# The first run's successful cancellation checks remain in its report; do not repeat them.
if '--retry' in sys.argv:
    steps = steps[:5] + steps[11:]
    if '--consumer-only' in sys.argv:
        for s in steps[:10]: s['skipScreenshot'] = True
request = dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=150, useRunSubfolder=False, steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request, ensure_ascii=False, indent=2), encoding='utf-8')
print(len(steps), 'steps;', sum(len(s['after']) for s in steps), 'checks')
