"""Actual conquered Mage Tower spell purchase and next-turn original spell consumer.

The first 60 real operations replay the already passed tower-conquest route;
no duplicate assertions or screenshots. New evidence begins at step 60.
No asset/fixture mutations; default plan only, --write, --from >= 60.
"""
import argparse
import json
from pathlib import Path
from build_pve_tower_recruitment_acceptance import build as tower_build


def build():
    steps = tower_build()[:60]
    assert steps[-1]['buttonPath'].endswith('JourneyHand/Btn_CloseHand')
    for step in steps:
        step['after'] = []
        step['skipScreenshot'] = True
    rule = '本人真实征服法师塔正式下回合交涉；付7影响与所选原法术同色魔力，原供应实例进牌库顶/正式抽回，后续真实寒冰远程消费者'

    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
            skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule=rule)
            for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

    click('Btn_JourneyInteraction','同一已征服塔交涉继续，不重建玩家',
          {'journey.siteRecruitLocation':'MageTower','journey.siteCanInteract':'True',
           'world.conquered':'-1:-1','journey.position':'-1,-1','influence':8,'blue':1,'fame':43,
           'hand.ids':'magic_011','supply.spells':'magic_001,magic_015,magic_000'})
    click('JourneyInteraction/Btn_SiteSpellShop','实际塔法术商店原卡供应')
    click('SiteSpellShop/Btn_Spell_magic_015','明确选择中间原蓝法术风雪交加，非默认第一张',capture=True)
    click('SiteSpellShop/Btn_BuySpell','整笔支付影响7及原蓝晶1，新原牌进牌库顶尚未入手',
          {'influence':1,'blue':0,'deck.count':1,'hand.ids':'magic_011','removed.ids':'items_010'},True)
    click('SiteSpellShop/Btn_Close','返回同一次实际交涉')
    click('JourneyInteraction/Btn_CloseInteraction','保留实际购入牌库顶与未用影响1')
    click('Btn_EndTurn','正式结束交涉并抽到实际新购原法术',
          {'journey.completedTurns':2,'hand':2,'hand.ids':'magic_011,magic_015','deck.count':0,
           'influence':0,'blue':0,'journey.samePlayer':'True','journey.position':'-1,-1'})
    click('Btn_BeginTurn','第三正式回合使用刚购入的实际原法术')
    click('Btn_JourneyHand','旧奖励法术保留实际实例')
    click('JourneyHand/@card=magic_011','原征服奖励献祭本回合不施放')
    click('JourneyHand/Btn_DiscardAtEnd','实际标记旧奖励回合末弃牌')
    click('JourneyHand/Btn_CloseHand','从真实源取得本回合印刷费用')
    click('Btn_JourneyTools','当前原魔力源',{'source.available':'White,Blue,White'})
    click('JourneyTools/Btn_Source_1','实际唯一蓝骰')
    click('JourneyTools/Btn_UseSource','正常额度取蓝魔力，库存蓝仍0',{'tokens.blue':1,'blue':0})
    click('JourneyTools/Btn_CloseTools','同玩家继续战斗')
    click('Btn_JourneyEncounter_0','已有演练原032护甲3，不冒充原地图导航')
    click('Hand/Btn_Card_magic_015_0','实际刚购买风雪交加原图')
    click('Inspector/Btn_Choice_base','原基础蓝费寒冰远程5')
    click('Inspector/Btn_Play','真实蓝token付款，产出原寒冰远程5',
          {'tokens.blue':0,'blue':0,'ranged':5,'power.attacks':'Ranged:Ice:5'},True)
    click('Enemy_0/Btn_Target','实际原032护甲3')
    click('Inspector/Btn_Resolve','消费新购原法术击杀，名望43到45',{'kills':1,'fame':45,'ranged':0,'wounds':0})
    for _ in range(4): click('Inspector/Btn_Next','正式推进剩余空敌阶段')
    click('Inspector/Btn_EndTurn','三正式回合同玩家，取得两原法术与焚毁去向守恒',
          {'journey.completedTurns':3,'journey.completedBattles':2,'hand':0,'journey.discardCount':6,
           'wounds':0,'fame':45,'blue':0,'green':1,'red':0,'removed.ids':'items_010',
           'world.conquered':'-1:-1','journey.position':'-1,-1','journey.samePlayer':'True'})
    return steps


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--write',action='store_true')
    p.add_argument('--from',dest='start',type=int,default=60)
    p.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    args=p.parse_args();steps=build()
    assert 60 <= args.start <= len(steps), 'Prefix 0..59 reuses 20260929_172048_1890cc47 evidence.'
    for step in steps[:args.start]:step['after']=[];step['skipScreenshot']=True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,
            useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'total operations;',len(steps)-args.start,'new evidence steps;',
          sum(len(s['after']) for s in steps),'assertions;',sum(not s['skipScreenshot'] for s in steps),
          'screenshots;','written' if args.write else 'plan only')

if __name__=='__main__':main()
