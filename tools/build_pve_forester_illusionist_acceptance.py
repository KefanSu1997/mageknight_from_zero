"""Original two-unit continuous route. --write explicitly writes acceptance config.
No asset/seed/resource mutation. Custom level-4 five-card build, not standard start.
"""
import argparse
import json
import re
from pathlib import Path

def build():
    steps=[]
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
            after=[dict(key=k,expected=str(v),rule='原护林人减费、幻术师白晶/影响力，真实原牌付款及同玩家旧伤治疗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    def outside(card,choice,expected=None):
        click('JourneyHand/@card='+card,'实际原牌 '+card)
        click('JourneyHand/Btn_OutsideChoice_'+choice,'原印刷效果')
        click('JourneyHand/Btn_PlayOutside','真实出牌及费用',expected)
    def unit(index,ability,expected):
        click('JourneyHand/Btn_OwnedUnits','实际部队原图')
        click('OwnedUnits/Btn_Unit_'+str(index),'同一自有部队')
        click('OwnedUnits/Btn_Ability_'+str(ability),'原能力选择')
        click('OwnedUnits/Btn_Use','实际激活及耗竭',expected)
        click('OwnedUnits/Btn_Close','保留真实部队状态')
    def move(id,position):
        click('JourneyHand/Btn_CloseHand','返回原地图')
        click('Btn_JourneyMap','实际邻格')
        click('JourneyMap/Btn_Hex_'+id,'选择实际地形')
        click('JourneyMap/Btn_Move','支付本格真实移动费用',{'movement':0,'journey.position':position},True)
        click('JourneyMap/Btn_CloseMap','保留同一位置')
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法自定演练构筑'))
    click('CampShade/Camp/Btn_Loadout','既有Commoners起点')
    for _ in range(3):click('CampShade/Picker/Btn_NextPage','下一页')
    click('CampShade/Picker/Btn_Select_60','红蓝各一晶，未修改资源')
    click('CampShade/Camp/Btn_ChooseUnits','两个合法指挥槽')
    # Resolve actual authored catalog pages without assuming card order.
    scene=Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
    guids=re.findall(r'guid: ([0-9a-f]+)',scene.split('  unitCatalog:',1)[1].split('--- !u!',1)[0])
    ids={re.search(r'guid: ([0-9a-f]+)',f.read_text()).group(1):f.name[:-11] for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
    catalog=[ids[g] for g in guids]
    page=0
    for id in ['army_2_006','army_2_011','army_2_000','army_2_003']:
        target=catalog.index(id)//18
        while page!=target:
            direction='Next' if target>page else 'Previous'
            click('CampShade/UnitPicker/Btn_'+direction+'Page','实际原部队目录翻页')
            page+=1 if target>page else -1
        click('CampShade/UnitPicker/Btn_Unit_'+id,('移除起点' if id in ['army_2_006','army_2_011'] else '选择原部队')+id)
    click('CampShade/UnitPicker/Btn_Back','护林人与幻术师')
    click('CampShade/Camp/Btn_ChooseCards','五张原牌')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds','清空待选')
    for id in ['000','008']:click(p+'Btn_ChooseDeed_basic_card_'+id,'原行进与承诺')
    click(p+'Btn_DeedNext','下一原基本页')
    for id in ['016','021']:click(p+'Btn_ChooseDeed_basic_card_'+id,'原决心与愤怒')
    click(p+'Btn_DeedKind_1','原高级行动')
    click(p+'Btn_DeedNext','高级第二页')
    click(p+'Btn_ChooseDeed_advanced_card_015','原再生基础治疗1并重整等级1或2')
    click(p+'Btn_DoneDeeds','五张真实原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法等级构筑')
    click('CampShade/HeroBuild/Btn_Hero_Tovak','托瓦克原身份')
    click('CampShade/HeroBuild/Btn_Level_4','等级4两槽，无额外技能')
    click('CampShade/HeroBuild/Btn_Begin','正式连续旅程',{'units.count':2,'hero.level':4,'hand':5})
    click('Tactic_day_1/Btn_Tactic_day_1','原白昼战术')
    click('Btn_BeginTurn','正式首回合')
    click('Btn_JourneyHand','同一原手牌')
    unit(0,0,{'movement':2,'units.ready':'False,True'})
    move('forest','1,0')
    click('Btn_JourneyHand','保留真实森林位置')
    unit(1,2,{'white':1,'tokens.white':0,'units.ready':'False,False'})
    click('JourneyHand/Btn_CloseHand','保留真实白晶')
    click('Btn_JourneyEncounter_0','实际原哨卫032')
    for _ in range(3):click('Inspector/Btn_Next','正式未格挡承伤进入近战')
    click('Hand/Btn_Card_basic_card_021_0','原愤怒')
    click('Inspector/Btn_Choice_attack','基础攻击')
    click('Inspector/Btn_Play','实际攻击2',{'melee':2,'wounds':2})
    click('Hand/Btn_Card_basic_card_016_0','原决心')
    click('Inspector/Btn_Sideways','实际横置攻击1',{'melee':3})
    click('Enemy_0/Btn_Target','原哨卫')
    click('Inspector/Btn_Resolve','真实击杀护甲3',{'kills':1,'fame':17,'wounds':2})
    click('Inspector/Btn_Next','结束原战斗')
    click('Inspector/Btn_EndTurn','正式保留白晶/旧伤/部队耗竭')
    click('Btn_BeginTurn','同一玩家下一回合',{'journey.samePlayer':'True','wounds':2,'white':1,'units.ready':'False,False'})
    click('Btn_JourneyHand','实际剩余原手牌')
    outside('basic_card_000','base',{'movement':2})
    move('plains','0,1')
    click('Btn_JourneyHand','移动后原交涉资源')
    outside('basic_card_008','strong',{'white':0,'influence':4})
    click('JourneyHand/@card=advanced_card_015','原再生')
    click('JourneyHand/Btn_OutsideChoice_base','原基础治疗1并重整等级1或2')
    click('JourneyHand/Btn_RecoveryUnit_1','同一耗竭等级2幻术师')
    click('JourneyHand/Btn_PlayOutside','原再生只重整不治疗英雄',{'healing':1,'wounds':2,'units.ready':'False,True'})
    unit(1,0,{'influence':8,'units.ready':'False,False'})
    click('JourneyHand/Btn_CloseHand','进入实际村庄交涉')
    click('Btn_JourneyInteraction','正式村庄入口')
    click('JourneyInteraction/Btn_BeginInteraction','原声誉0不加减',{'influence':8})
    click('JourneyInteraction/Btn_BuyHealing','实际支付3影响',{'influence':5,'healing':2})
    click('JourneyInteraction/Btn_BuyHealing','再次支付3，实际消费幻术师贡献',{'influence':2,'healing':3},True)
    click('JourneyInteraction/Btn_CloseInteraction','返回真实旧伤')
    click('Btn_JourneyHand','实际首战旧伤')
    click('JourneyHand/Btn_HealHero','治疗第一张真实伤',{'wounds':1,'healing':2})
    click('JourneyHand/Btn_HealHero','治疗第二张真实伤',{'wounds':0,'healing':1},True)
    click('JourneyHand/Btn_CloseHand','保留同一实体')
    click('Btn_EndTurn','正式第二回合结束清除临时资源',{'journey.completedTurns':2,'influence':0,'healing':0,'units.ready':'False,False','wounds':0})
    return steps

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--write',action='store_true')
    ap.add_argument('--from',dest='start',type=int,default=0)
    args=ap.parse_args()
    steps=build()
    for s in steps[:args.start]:s['after']=[];s['skipScreenshot']=True
    if args.write:
        Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks; written:',args.write)
if __name__=='__main__':main()
