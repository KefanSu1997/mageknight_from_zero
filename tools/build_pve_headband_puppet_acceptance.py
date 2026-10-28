"""Real public Puppet through original level rewards, then same-battle keep/consume.
Explicit --prepare/--restore guards the original replay seed; never runs Unity.
"""
import json,sys
from pathlib import Path
args=sys.argv[1:]
base=Path('tools/build_pve_headband_target_acceptance.py')
source=base.read_text(encoding='utf-8').split("if hero != 'Norowas':",1)[0]
a,b=source.index('VARIANTS = {'),source.index('\nhero = ')
source=source[:a]+"VARIANTS = {'Krang': (29, 'hero_skill_pool_003:06', 0)}"+source[b:]
source=source.replace('tmp/headband_target_HornGold.original','tmp/headband_puppet_HornGold.original')
source=source.replace("battle_play('021', 'attack')\nbattle_play('016', 'attack')","battle_play('021', 'strong')")
source=source.replace("'red': 1, 'blue': 1","'red': 0, 'blue': 1")
source=source.replace("'hand': 7, 'deck.count': 1","'hand': 8, 'deck.count': 1")
source=source.replace('实际攻击2加2','实际红费攻击4')
source=source.replace("click('Btn_BeginTurn', '同一玩家第二回合')", "click('Btn_ChaosKeep', '原混乱大师未用，正式选择保持轮盘', {'skills.chaosPending':'False'})\nclick('Btn_BeginTurn', '同一玩家第二回合')")
sys.argv=[str(base),'Krang']+[a for a in args if a in ('--prepare','--restore')]
exec(compile(source,str(base),'exec'))
for step in steps: step['skipScreenshot']=True
rule='原克朗升级未选公共傀儡；真实头环两独立选择保存再消费原敌标，原贡献实际击杀另一敌；非永久取得且正式清理未留战果'
def target(i): click('Enemy_'+str(i)+'/Btn_Target','选择实际敌人实例 '+str(i))
def sideways(card,expected):
    click('Hand/Btn_Card_basic_card_'+card+'_0','选择原手牌 '+card)
    click('Inspector/Btn_Sideways','原普通横置1',expected)
click('Btn_JourneyEncounter_2','同玩家双哨卫两个独立标记')
click('Inspector/Btn_Next','正式格挡阶段')
battle_play('016','strong')
target(0)
click('Inspector/Btn_Resolve','蓝费决心5挡第一敌4',{'blue':0,'block':0})
click('Inspector/Btn_Next','正式承伤阶段')
click('Inspector/Btn_Next','第二敌实际造成2伤，旧伤仍保留',{'wounds':4})
for i,card in enumerate(['000','001','007']): sideways(card,{'melee':i+1})
target(0)
click('Inspector/Btn_Resolve','三张真实横置击杀第一敌',{'kills':1,'fame':5,'puppet.candidates':'monster_032'},True)
click('Hand/Btn_Card_items_022_0','真实原头环')
click('Inspector/Btn_OpenSpecialArtifact','打开公共技能')
click('SpecialArtifact/Btn_PublicSkill_0','实际公共傀儡大师')
click('SpecialArtifact/Btn_ConfirmSpecial','逐次真实标记输入')
click('EnemyTokenSkill/Btn_Enemy_0','选择本回合实际击败标记')
click('EnemyTokenSkill/Btn_Confirm','头环第一次保存实际敌标',{'headband.remaining':1,'puppet.stored':'monster_032','puppet.candidates':'','hero.skills':1,'supply.skills':'hero_skill_pool_003:06'},True)
click('EnemyTokenSkill/Btn_Mode_1','同次借用第二次选择消费作攻击')
click('EnemyTokenSkill/Btn_Enemy_0','同一个已保存原标记')
click('EnemyTokenSkill/Btn_Confirm','原物理攻击4一半向上取整2',{'headband.remaining':0,'puppet.stored':'','melee':2,'hero.skills':1,'supply.skills':'hero_skill_pool_003:06'},True)
sideways('014',{'melee':3})
target(1)
click('Inspector/Btn_Resolve','傀儡2加原横置1真实击杀第二敌',{'kills':2,'fame':7,'wounds':4},True)
click('Inspector/Btn_Next','结束同场战斗')
click('Inspector/Btn_EndTurn','正式回合末未保存候选释放，头环弃牌且公共归属不变',{'puppet.candidates':'','puppet.stored':'','headband.remaining':0,'hero.skills':1,'supply.skills':'hero_skill_pool_003:06','journey.completedTurns':2,'journey.completedBattles':2})
if '--from' in args:
    for s in steps[:int(args[args.index('--from')+1])]: s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions')
