"""Reuse the verified recruitment prefix, then heal the same wounded exhausted unit."""
import json,runpy,sys
from pathlib import Path
args=sys.argv[1:]
sys.argv=['tools/build_pve_headband_target_acceptance.py','Norowas']+([x for x in args if x in ('--prepare','--restore')])
runpy.run_path(sys.argv[0],run_name='__main__')
p=Path('AutomationConfigs/pve_battle.json')
config=json.loads(p.read_text(encoding='utf-8'))
end=next(i for i,s in enumerate(config['steps']) if s['label']=='正式激活并耗竭原农民')+3
steps=config['steps'][:end]
for s in steps:s['after']=[];s['skipScreenshot']=True
def click(path,label,checks=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
      after=[dict(key=k,expected=str(v),rule='同一真实农民正式承伤、头环借重整治疗仅一次且不重整，正式跨轮恢复后原移动实际消费') for k,v in {'ui.truncated':'',**(checks or {})}.items()]))
click('Btn_EndTurn','保留头环和耗竭农民至第三回合',{'journey.completedTurns':2,'hand':4,'deck.count':0,'units.ready':'False','units.wounds':0})
click('Btn_BeginTurn','同一玩家第三回合');click('Btn_JourneyEncounter_0','实际哨卫攻击4')
for _ in range(2):click('Inspector/Btn_Next','正式阶段到承伤')
click('Btn_Roster','选择实际自有部队');click('Units/Btn_Unit_0','耗竭农民也可承伤');click('Enemy_0/Btn_Target','实际攻击目标')
click('Inspector/Btn_AssignUnit','农民护甲3承担实际攻击')
click('Inspector/Btn_Next','实际部队一伤与英雄剩余一伤',{'units.ready':'False','units.wounds':1,'units.canActivate':'False','wounds':3},True)
click('Inspector/Btn_Next','实际未击败敌人战后',{'phase':'End','won':'False'})
click('Btn_JourneyHand','实际保留手牌');click('JourneyHand/Btn_Hand_0','保留的原头环');click('JourneyHand/Btn_PlayOutside','原头环借用')
click('SpecialArtifact/Btn_PublicSkill_0','实际公共重整旗鼓');click('SpecialArtifact/Btn_ConfirmSpecial','选择真实部队输入')
click('HeadbandTarget/Btn_SkillUnit_0','同一受伤且耗竭农民');click('HeadbandTarget/Btn_Heal','原治疗分支',capture=True)
click('HeadbandTarget/Btn_Confirm','治疗仅去伤且保持耗竭',{'headband.remaining':0,'units.wounds':0,'units.ready':'False','units.canActivate':'False','wounds':3,'hero.skills':1,'supply.skills':'hero_skill_pool_002:02'},True)
click('JourneyHand/Btn_CloseHand','返回实际战果');click('Inspector/Btn_EndTurn','正式第三回合结束',{'journey.completedTurns':3,'journey.completedBattles':2,'units.ready':'False','units.wounds':0,'deck.count':0},True)
click('Btn_Announce','实际空牌库声明');click('Btn_EndRound','正式结束本轮');click('Btn_PrepareRound','同一玩家正式准备夜晚')
click('Tactic_night_1/Btn_Tactic_night_1','原夜晚战术');click('Btn_BeginTurn','正式新轮恢复同一部队',{'journey.round':2,'journey.dayPart':'Night','units.ready':'True','units.wounds':0,'units.canActivate':'True','units.count':1},True)
click('Btn_JourneyHand','真实新轮手牌');click('JourneyHand/Btn_OwnedUnits','同一农民');click('OwnedUnits/Btn_Unit_0','选择已治疗部队')
click('OwnedUnits/Btn_Ability_2','农民原移动2');click('OwnedUnits/Btn_Use','真实激活治疗后的同一农民',{'movement':2,'units.ready':'False','units.wounds':0},True)
click('OwnedUnits/Btn_Close','返回手牌');click('JourneyHand/Btn_CloseHand','实际地图');click('Btn_JourneyMap','相邻平原')
click('JourneyMap/Btn_Hex_dungeon','地下城所在平原格');click('JourneyMap/Btn_Move','原平原费用2消费部队移动',{'journey.position':'1,1','movement':0,'units.wounds':0},True)
click('JourneyMap/Btn_CloseMap','保留实际位置')
if '--from' in args:
    for s in steps[:int(args[args.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
config['steps']=steps
p.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print('RallyHeal',len(steps),'steps; verified prefix',end,'incremental assertions',sum(len(s['after']) for s in steps))
