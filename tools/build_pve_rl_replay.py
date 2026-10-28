"""Map saved PPO semantic actions to real UI buttons and real backend assertions."""
import argparse,json
from pathlib import Path
from build_pve_manual_play_acceptance import build
p=argparse.ArgumentParser();p.add_argument('replay');a=p.parse_args()
r=json.loads(Path(a.replay).read_text(encoding='utf-8'))
if r['scope']!='dual032_arythea_level2_two_original_cards_v1' or not r['result']['wins']:
 raise ValueError('Unsupported or unsuccessful PPO fixture; never manufacture a winning replay')
steps=build('attack');cut=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_JourneyEncounter_2'))
steps=steps[:cut+1]
mapped=[]
def state(d):
 keys=['phase','won','hero.id','fame','hand','deck.count','wounds','kills','ranged','siege','melee']
 out={k:d[k] for k in keys}
 for color in ['red','blue','green','white']:out[color]=d['crystals.'+color]
 out.update({k:v for k,v in d.items() if k.startswith('tokens.')})
 return out
def click(path,label,expected=None,shot=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,after=[dict(key=k,expected=str(v),rule='保存并重新载入PPO模型输出；原卡原技能正式操作前后状态一致') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
# The actual Unity fixture must start from the same visible combat observation.
steps[-1]['after'] += [dict(key=k,expected=v,rule='UI与RL初始真实战斗状态映射') for k,v in state(r['trace'][0]['before']).items()]
for i,t in enumerate(r['trace']):
 start=len(steps);m=t['action'];kind=m['Kind'];after=state(t['after'])
 if kind=='advance':click('Inspector/Btn_Next','PPO正式推进阶段',after,t['after']['won']=='True')
 elif kind in ('play','sideways'):
  click('Hand/Btn_Card_'+m['Card']+'_0','PPO选择实际原牌')
  if kind=='play':
   click('Inspector/Btn_Choice_'+m['Choice'],'PPO选择原效果层')
   click('Inspector/Btn_Play','PPO正式费用与出牌',after)
  else:click('Inspector/Btn_Sideways','PPO正式横置',after)
 elif kind=='skill':
  if m['Option']!=0:raise ValueError('Add actual option mapping before exposing this skill choice')
  click('Btn_Skills','PPO原技能');click('Skills/Btn_Skill_0','选择本人实际技能')
  click('Inspector/Btn_UseSkill','PPO正式技能消费',after);click('Btn_Skills','回到原手牌')
 elif kind=='attack':
  for target in range(2):
   if m['Targets']&(1<<target):click(f'Enemy_{target}/Btn_Target','PPO选择原敌实例 '+str(target))
  count=len(t['before']['attack.parts'].split(';')) if t['before']['attack.parts'] else 0
  if count>1:
   click('Inspector/Btn_AttackParts','PPO选择实际攻击贡献');click('AttackSelection/Btn_None','先保留全部实际来源')
   for part in range(count):
    if m['Parts']&(1<<part):click(f'AttackSelection/Btn_Part_{part}','PPO分配实际来源 '+str(part))
   click('AttackSelection/Btn_Done','完成来源分配')
  click('Inspector/Btn_Resolve','PPO实际结算原敌护甲与奖励',after,True)
 elif kind=='block':
  click(f'Enemy_{m["Targets"]}/Btn_Target','PPO格挡目标');click('Inspector/Btn_Resolve','正式格挡',after)
 else:raise ValueError(kind)
 mapped.append({'semantic_index':i,'action':m,'first_ui_step':start,'last_ui_step':len(steps)-1,'before':t['before'],'after':t['after']})
click('Btn_Menu','返回菜单');click('Btn_Camp','准备继续试玩')
out=Path('AutomationConfigs/pve_battle.json');out.write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
Path(a.replay).with_name('ui_mapping.json').write_text(json.dumps(mapped,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(r['trace']),'PPO actions;',len(steps),'UI operations;',sum(len(s['after']) for s in steps),'assertions')
