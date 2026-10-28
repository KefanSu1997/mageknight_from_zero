"""Actual Mage Tower conquest, next-turn site recruitment and same-unit combat consumer.

Default builds/prints the plan only. --write writes the acceptance configuration;
--from N replays passed operations without duplicate evidence. Reuses existing
backend proofs; this generator itself does not claim scene success.
"""
import argparse
import json
import re
from pathlib import Path


def build():
    steps = []
    rule = '原地道付绿黑征服实际法师塔守军并正式领奖；下一回合同一地点自然交涉付7招原魔像，下一战同实例真实攻击消费者'

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

    def outside(card, choice='base'):
        click('JourneyHand/@card=' + card, '实际原牌 ' + card)
        click('JourneyHand/Btn_OutsideChoice_' + choice, '原印刷模式 ' + choice)
        click('JourneyHand/Btn_PlayOutside', '展开原效果及实际输入')

    def discard(card):
        click('JourneyHand/@card=' + card, '实际原牌回合末选择')
        click('JourneyHand/Btn_DiscardAtEnd', '标记正式回合末弃牌')

    def play(card, choice=None, expected=None):
        click('Hand/Btn_Card_' + card + '_0', '实际原牌战斗消费')
        if choice is None:
            click('Inspector/Btn_Sideways', '原牌横置1', expected)
        else:
            click('Inspector/Btn_Choice_' + choice, '原印刷效果')
            click('Inspector/Btn_Play', '正式出牌与贡献', expected)

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '已存在演练实际法师塔连续路线'))
    click('CampShade/Camp/Btn_Loadout', '既有HornGold种子0，不修改fixture')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '下一页')
    click('CampShade/Picker/Btn_Select_39', '既有红蓝各1、无初始部队')
    click('CampShade/Camp/Btn_ChooseCards', '明确五张原牌自定起手')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds', '清除旧待选牌')
    click(p+'Btn_DeedKind_2', '原法术目录')
    for card in ['magic_004','magic_009']: click(p+'Btn_ChooseDeed_'+card, '原地道或火焰魔球')
    click(p+'Btn_DeedKind_3', '原神器目录')
    for card in ['items_007','items_010']: click(p+'Btn_ChooseDeed_'+card, '原绿戒或金币包')
    click(p+'Btn_DeedNext', '神器第二页')
    click(p+'Btn_ChooseDeed_items_015', '原黑暗护符')
    click(p+'Btn_DoneDeeds', '五张实际原牌')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄等级构筑')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35、护甲3和三指挥槽')
    click('CampShade/HeroBuild/Btn_Begin', '同玩家进入已有演练世界，不冒充原七格地图完成',
          {'fame':35,'hero.level':6,'hero.armor':3,'units.count':0,'hand':5,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合征服')
    click('Btn_JourneyHand', '原材料与实际原费用')
    click('JourneyHand/@card=items_007', '原绿宝石戒指')
    click('JourneyHand/Btn_ArtifactNormal', '正常绿晶和临时绿魔力')
    click('JourneyHand/Btn_PlayOutside', '分别记录库存与临时费用',{'green':1,'tokens.green':1,'fame':36})
    click('JourneyHand/@card=items_015', '原黑暗护符')
    for _ in range(5): click('JourneyHand/Btn_ArtifactColor_0', '选择原许可黑魔力')
    click('JourneyHand/Btn_PlayOutside', '白昼真实黑魔力及许可',{'tokens.black':1,'mana.allowBlackAtDay':'True'})
    outside('magic_004','strong')
    click('DeedInputs/Btn_Path_mountain', '实际经过山脉0,-1')
    click('DeedInputs/Btn_Path_tower', '终点实际法师塔-1,-1')
    click('DeedInputs/Btn_ConfirmInputs', '付款绿黑，实际原守军进入同一战斗',
          {'tokens.green':0,'tokens.black':0,'green':1,'world.tunnel':'True','world.ignoreSite':'True',
           'journey.position':'-1,-1','reputation':-1})
    play('magic_009','base',{'ranged':5,'red':0})
    click('Enemy_0/Btn_Target', '实际紫色守军002护甲5，地道忽略地点城防')
    click('Inspector/Btn_Resolve', '真实原火球5击杀守军',{'kills':1,'fame':40,'wounds':0})
    for _ in range(4): click('Inspector/Btn_Next', '正式完成其余空敌阶段')
    click('Inspector/Btn_FinishSite', '实际征服并把原守军弃置，未预置Conquered',
          {'world.conquered':'-1:-1','world.discard':'monster_002','world.rewardPending':'True'},True)
    click('SiteReward/Btn_Reward_0', '选择实际原供应首张法术奖励')
    click('Inspector/Btn_EndTurn', '正式回合末领奖，保留位置/金币包/实际奖励牌',
          {'journey.completedTurns':1,'journey.completedBattles':1,'hand':2,'deck.count':0,
           'world.conquered':'-1:-1','world.rewardPending':'False','world.tunnel':'False',
           'journey.position':'-1,-1','mana.allowBlackAtDay':'False','wounds':0})
    click('Btn_BeginTurn', '正式第二回合留在本人已征服法师塔')
    click('Btn_JourneyInteraction', '共用实际地点交涉入口')
    click('JourneyInteraction/Btn_BeginInteraction', 'BeginSiteInteraction按真实塔与归属授权，原负声誉修正一次',
          {'journey.interacting':'True','influence':-1,'reputation':-1,
           'journey.siteRecruitLocation':'MageTower','journey.siteShieldInfluence':0,'journey.siteCanInteract':'True'})
    click('JourneyInteraction/Btn_CloseInteraction', '保留同次自然交涉')
    click('Btn_JourneyHand', '原金币包实际支付能力')
    click('JourneyHand/@card=items_010', '原金币包')
    click('JourneyHand/Btn_ArtifactBurn', '原焚毁影响9名望3')
    click('JourneyHand/Btn_PlayOutside', '金币包永久移除并产生实际交涉费用',
          {'influence':8,'fame':43,'removed.ids':'items_010'})
    click('JourneyHand/Btn_CloseHand', '实际塔图标供应消费者')
    click('Btn_JourneyInteraction', '同塔实际供应')
    click('JourneyInteraction/Btn_Offer_army_2_001', '原守护者魔像，印刷魔法塔/城堡图标，费用7')
    click('JourneyInteraction/Btn_Recruit', '支付7实际招募同供应原魔像',
          {'influence':1,'units.count':1,'journey.freeSlots':2,'units.ready':'True','journey.offerCount':2},True)
    click('JourneyInteraction/Btn_CloseInteraction', '同新招魔像实体')
    click('Btn_EndTurn', '正式清余影响，保留原奖励法术和新招单位',
          {'journey.completedTurns':2,'journey.position':'-1,-1','world.conquered':'-1:-1',
           'hand':1,'influence':0,'units.count':1,'units.ready':'True','fame':43})
    click('Btn_BeginTurn', '第三正式回合使用同魔像及之前所得奖励')
    click('Btn_JourneyEncounter_0', '已有演练原032，不冒充地图其他地点')
    for _ in range(3): click('Inspector/Btn_Next', '推进正式格挡、承伤到近战')
    click('Btn_Roster', '实际原魔像卡面')
    click('Units/Btn_Unit_0', '同一塔内新招魔像')
    click('Inspector/Btn_UnitAbility_0', '原免费普通攻击2且同单位耗竭',
          {'melee':2,'units.ready':'False','units.wounds':'0','wounds':2},True)
    click('Btn_Roster', '真实取得的原奖励法术继续消费')
    click('Hand/#0', '保留于手的实际奖励法术，非添加新牌')
    click('Inspector/Btn_Sideways', '正式原奖励牌横置攻击1，合计3',{'melee':3})
    click('Enemy_0/Btn_Target', '实际原032护甲3')
    click('Inspector/Btn_Resolve', '真实新招部队2与奖励牌1共同击杀',{'kills':1,'fame':45})
    click('Inspector/Btn_Next', '真实后续战斗完成')
    click('Inspector/Btn_EndTurn', '三回合持续地点归属、新单位及原伤',
          {'journey.completedTurns':3,'journey.completedBattles':2,'units.count':1,'units.ready':'False',
           'wounds':2,'hand':2,'journey.samePlayer':'True','world.conquered':'-1:-1',
           'journey.position':'-1,-1','fame':45,'removed.ids':'items_010'},True)
    return steps

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    assert 0 <= args.start <= len(steps)
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,
            maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions',
          sum(not s['skipScreenshot'] for s in steps),'screenshots','written' if args.write else 'plan only')

if __name__ == '__main__': main()
