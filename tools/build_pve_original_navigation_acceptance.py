"""Focused original-map navigation; reuse the original seven-card setup."""
import contextlib,io,json,runpy,sys
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    old_argv=sys.argv;sys.argv=[old_argv[0]]
    setup=runpy.run_path('tools/build_pve_original_map_acceptance.py')
    sys.argv=old_argv
steps=setup['steps'];del steps[22:]
click=setup['click'];hand=setup['hand'];play=setup['play'];closehand=setup['closehand']
steps[-1]['skipScreenshot']=False
steps[-1]['after'].append(dict(key='ui.offMapBattles',expected='0',rule='原地图只能依实际位置挑战，不显示演练敌群'))
click('Btn_Nearby_0_-1','从邻格入口查看原地图',{'journey.position':'0,0','ui.offMapBattles':0})
click('JourneyMap/Btn_CloseMap','回到实际位置概览')
hand();play(0,'base',{'movement':2});play(1,'base',{'movement':4});play(2,'base',{'movement':6});closehand()
click('Btn_Nearby_0_-1','点选相邻原平原')
click('JourneyMap/Btn_Move','真实移动第一格',{'journey.position':'0,-1','movement':4})
for q,r,left in [(1,-2,2),(2,-3,0)]:
    click(f'JourneyMap/Btn_Cell_{q}_{r}','原相邻格')
    click('JourneyMap/Btn_Move','原平原费用2',{'journey.position':f'{q},{r}','movement':left})
click('JourneyMap/Btn_CurrentVillage','从实际所在村庄进入交涉',{'journey.atVillage':'True'},True)
click('JourneyInteraction/Btn_BeginInteraction','正式开始当前位置交涉',{'journey.interacting':'True'})
click('JourneyInteraction/Btn_EndInteraction','结束交涉仍保留地图玩家',{'journey.interacting':'False'})
click('JourneyInteraction/Btn_CloseInteraction','返回原村庄回合概览',{'ui.offMapBattles':0,'journey.position':'2,-3'},True)
click('Btn_EndTurn','正式结束实际移动交涉回合',{'journey.completedTurns':1,'journey.samePlayer':'True'})
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(step['after']) for step in steps),'assertions')
