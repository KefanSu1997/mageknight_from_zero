"""Original Trance glade 4 -> actual recruit; same red Familiar influence 3 heals battle wound."""
import argparse
import json
from pathlib import Path
from build_pve_familiar_consumers_acceptance import build as prior


def build():
    old, _ = prior('red-blue')
    cut = next(i for i,s in enumerate(old) if s['buttonPath'].endswith('/Btn_JourneyTools'))
    steps = old[:cut]
    for i,s in enumerate(steps):
        if s['buttonPath'].endswith('/Btn_Hero_Tovak'):
            s['buttonPath'] = s['buttonPath'].replace('Tovak','Braevalar')
            s['label'] = '原布拉瓦尔合法等级6与迷醉'
        if s['buttonPath'].endswith('HeroBuild/Btn_Begin'):
            added = dict(s, buttonPath='Canvas/BattleTable/CampShade/HeroBuild/Btn_Skill_4', label='原迷醉001:04')
            steps.insert(i, added)
            break
    prefix = len(steps)
    rule = '原迷醉林地影响4+承诺2真实招募6及红晶；同一红标魔宠基础影响3实际买治疗1并去除上一战旧伤'
    def c(path,label,expected=None,shot=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,
            after=[dict(key=k,expected=str(v),rule=rule) for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    def card(id):
        c('JourneyHand/@card='+id,'实际可见原牌')
        c('JourneyHand/Btn_OutsideChoice_base','原基础模式')
        c('JourneyHand/Btn_PlayOutside','正式出牌')
    c('Btn_JourneyHand','R5实际林地与原英雄',{'hero.id':'Braevalar','hero.skills':1,'journey.position':'1,-3','journey.round':5,'units.count':0,'red':1,'blue':1})
    for path in ['Btn_Support'] : c('JourneyHand/'+path,'正式战外技能')
    c('JourneySupport/Btn_OutsideSkill_0','原迷醉')
    c('JourneySupport/Btn_OutsideOption_0','实际所在林地决定数值')
    c('JourneySupport/Btn_UseOutsideSkill','原迷醉在林地影响4',{'influence':4,'skills.used':'True'},True)
    c('JourneySupport/Btn_BackSupport','回到原手牌')
    card('basic_card_008')
    c('JourneyHand/Btn_CloseHand','原承诺基础补足2',{'influence':6})
    c('Btn_JourneyMap','实际林地')
    c('JourneyMap/Btn_Cell_1_-3','当前林地')
    c('JourneyMap/Btn_CurrentGlade','原林地招募入口')
    c('JourneyInteraction/Btn_Offer_army_2_004','第五轮自然供应魔宠')
    c('JourneyInteraction/Btn_RecruitMana_Red','真实红晶调谐')
    c('JourneyInteraction/Btn_Recruit','影响6与红晶1正式支付',{'influence':0,'red':0,'blue':1,'units.ready':'True','units.attunements':'army_2_004:Red:False','units.count':1})
    c('JourneyInteraction/Btn_CloseInteraction','保留实际魔宠')
    c('Btn_EndTurn','招募回合正式结束')
    c('Btn_BeginTurn','同魔宠保持就绪',{'journey.completedTurns':2,'units.ready':'True','journey.samePlayer':'True'})
    c('Btn_JourneyMap','原邻格哨兵')
    c('JourneyMap/Btn_Cell_1_-4','真实原肆虐标记')
    c('JourneyMap/Btn_Challenge','正式挑战原032')
    for _ in range(3):c('Inspector/Btn_Next','原防御阶段与真实承伤')
    for n,id in enumerate(['basic_card_014','advanced_card_018','advanced_card_039'],1):
        c('Hand/Btn_Card_'+id+'_0','实际原牌横置')
        c('Inspector/Btn_Sideways','原近战横置1',{'melee':n,'wounds':2,'units.ready':'True'})
    c('Enemy_0/Btn_Target','同一原哨兵甲3')
    c('Inspector/Btn_Resolve','三张原牌横置实际击杀',{'kills':1,'fame':37})
    c('Inspector/Btn_Next','真实肆虐清除声誉+1',{'reputation':1})
    c('Inspector/Btn_EndTurn','正式回合末林地移除一伤')
    c('SiteEnd/Btn_Wound_0','选择实际战斗伤牌')
    c('SiteEnd/Btn_Confirm','保留另一张真实旧伤',{'wounds':1,'units.ready':'True','journey.completedTurns':3},True)
    c('Btn_BeginTurn','同玩家同魔宠下一回合')
    c('Btn_JourneyHand','实际未用原行进')
    card('basic_card_000')
    c('JourneyHand/Btn_CloseHand','原移动2',{'movement':2})
    c('Btn_JourneyMap','实际邻格村庄')
    c('JourneyMap/Btn_Cell_2_-3','原村庄平原')
    c('JourneyMap/Btn_Move','实际支付移动2',{'movement':0,'journey.position':'2,-3'})
    c('JourneyMap/Btn_CloseMap','正式村庄位置')
    c('Btn_JourneyHand','同一原魔宠')
    for path in ['Btn_OwnedUnits']:c('JourneyHand/'+path,'正式战外部队')
    c('OwnedUnits/Btn_Unit_0','原魔宠实际实体')
    c('OwnedUnits/Btn_Ability_2','原移动或影响能力')
    c('OwnedUnits/Btn_Option_1','选择影响；红色非白不加2')
    c('OwnedUnits/Btn_Use','原基础影响3并耗竭',{'influence':3,'units.ready':'False','units.attunements':'army_2_004:Red:False','wounds':1},True)
    c('OwnedUnits/Btn_Close','保留实际影响')
    c('JourneyHand/Btn_CloseHand','前往实际村庄交涉')
    c('Btn_JourneyMap','当前村庄')
    c('JourneyMap/Btn_Cell_2_-3','当前真实位置')
    c('JourneyMap/Btn_CurrentVillage','正式村庄入口')
    c('JourneyInteraction/Btn_BeginInteraction','实际声誉1修正0',{'influence':3})
    c('JourneyInteraction/Btn_BuyHealing','原影响3全部消费购买治疗1',{'influence':0,'healing':1,'wounds':1})
    c('JourneyInteraction/Btn_CloseInteraction','治疗真正旧伤')
    c('Btn_JourneyHand','实际上一战伤牌')
    c('JourneyHand/Btn_HealHero','真实旧伤去除',{'healing':0,'wounds':0,'units.ready':'False'},True)
    c('JourneyHand/Btn_CloseHand','保留同一玩家结果')
    c('Btn_EndTurn','正式末回合',{'journey.samePlayer':'True','journey.completedTurns':4,'journey.completedBattles':1,'wounds':0,'red':0,'blue':1,'units.attunements':'army_2_004:Red:False'})
    return steps,prefix


def main():
    p=argparse.ArgumentParser();p.add_argument('--write',action='store_true');p.add_argument('--from',dest='start',type=int,default=0);p.add_argument('--output',default='AutomationConfigs/pve_battle.json');a=p.parse_args()
    steps,prefix=build()
    if not 0<=a.start<=len(steps):p.error('invalid --from')
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=600,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',sum(not s['skipScreenshot'] for s in steps),'captures; prefix',prefix,';', 'written' if a.write else 'plan only')
if __name__=='__main__':main()

