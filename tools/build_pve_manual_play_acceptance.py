"""Focused manual-play regressions. Original UI actions, no state injection."""
import argparse
import json
from pathlib import Path


def build(case):
    steps = []
    def c(path, label, expected=None, shot=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not shot,
            after=[dict(key=k, expected=str(v), rule='真实原牌/牌区与攻击来源；仅补玩家反馈的操作路径')
                   for k,v in {'ui.truncated':'', **(expected or {})}.items()]))
    (c('Btn_Menu', '打开战斗菜单'), c('Btn_Camp', '打开起点配置'))
    if case in ('redraw', 'hero_demo_redraw'):
        c('CampShade/Camp/Btn_OriginalHero', '原版完整牌库')
        c('CampShade/HeroStart/Btn_Hero_'+('Arythea' if case=='hero_demo_redraw' else 'Tovak'), '原英雄')
        c('CampShade/HeroStart/'+('Btn_BattleHero' if case=='hero_demo_redraw' else 'Btn_StartHero'), '原16张牌洗混抽5', {'hand':5, 'deck.count':11, 'red':0, 'blue':0})
    else:
        c('CampShade/Camp/Btn_Loadout', '选择已有固定资源构筑')
        for _ in range(2): c('CampShade/Picker/Btn_NextPage', '原构筑翻页')
        c('CampShade/Picker/Btn_Select_39', '原HornGold，初始红蓝各1，无部队')
        if case == 'empty':
            c('CampShade/Camp/Btn_Journey', '明确小牌库演练', {'hand':5, 'deck.count':0})
        else:
            c('CampShade/Camp/Btn_ChooseCards', '设置原卡')
            p='CampShade/DeedPicker/'
            c(p+'Btn_ClearDeeds', '清空待选')
            c(p+'Btn_ChooseDeed_basic_card_000', '原行军供横置')
            c(p+'Btn_DeedNext', '基础第二页')
            c(p+'Btn_ChooseDeed_basic_card_021', '原怒火')
            c(p+'Btn_DoneDeeds', '两张真实原牌')
            c('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原技能构筑')
            p='CampShade/HeroBuild/'
            c(p+'Btn_Hero_Arythea', '原阿瑞希亚，与炽热剑术归属一致')
            c(p+'Btn_Level_2', '合法等级2一个技能')
            c(p+'Btn_Skill_2', '本人原炽热剑术攻击2')
            c(p+'Btn_Begin', '同一玩家连续旅程', {'hand':2,'red':1,'fame':3,'hero.id':'Arythea'})
    if case in ('redraw', 'hero_demo_redraw', 'empty'):
        c('Tactic_day_2/Btn_Tactic_day_2', '三思后行原战术')
        c('Btn_TacticCard_0', '选择第一张实际牌')
        c('Btn_TacticCard_1', '选择第二张实际牌',
          {'tactic.selected':2, 'tactic.replacementPreview':2 if case!='empty' else 0,
           'tactic.warningVisible':'False' if case!='empty' else 'True'}, True)
        c('Btn_ConfirmTactic', '正式先抽后洗，牌区守恒',
          {'hand':5 if case!='empty' else 3, 'deck.count':11 if case!='empty' else 2,
           'journey.tacticResolved':'True'}, True)
        if case=='hero_demo_redraw':
            c('Btn_BeginTurn', '换牌后真实回合', {'hand':5,'deck.count':11})
            c('Btn_JourneyEncounter_1', '兽人战团保留真实五张手牌', {'hand':5,'deck.count':11,'hero.id':'Arythea'}, True)
        return steps
    c('Tactic_day_1/Btn_Tactic_day_1', '正式选战术')
    c('Btn_BeginTurn', '正式回合')
    c('Btn_JourneyEncounter_2', '两个独立原032敌人')
    for _ in range(3): c('Inspector/Btn_Next', '正式阶段及承伤，不修改状态')
    c('Hand/Btn_Card_basic_card_021_0','选择原怒火')
    c('Inspector/Btn_Choice_strong','原红强化攻击4')
    c('Inspector/Btn_Play','真实付款红1',{'red':0,'melee':4})
    c('Hand/Btn_Card_basic_card_000_0','选择原行军')
    c('Inspector/Btn_Sideways','真实横置1',{'melee':5})
    c('Btn_Skills','原英雄技能')
    c('Skills/Btn_Skill_0','唯一实际技能')
    c('Inspector/Btn_UseSkill','正式技能攻击2',{'melee':7})
    c('Btn_Skills','回到手牌')
    c('Enemy_0/Btn_Target','第一只原032')
    c('Inspector/Btn_AttackParts','选择本次来源')
    c('AttackSelection/Btn_Part_1','保留横置牌')
    c('AttackSelection/Btn_Part_2','保留技能',{'attacks.selected':1,'attacks.reserved':2,'melee':7,'ui.attackSources':'狂怒,行进,炽热剑术'},True)
    c('AttackSelection/Btn_Done','回到战场，尚未消费')
    c('Inspector/Btn_Resolve','原怒火4仅杀第一怪',{'kills':1,'melee':3,'fame':5},True)
    c('Enemy_1/Btn_Target','第二只同名独立敌人')
    c('Inspector/Btn_Resolve','余下横置1加技能2杀第二怪',{'kills':2,'melee':0,'fame':7},True)
    c('Inspector/Btn_Next','真实战果',{'won':'True','kills':2})
    return steps


if __name__ == '__main__':
    p=argparse.ArgumentParser();p.add_argument('case',choices=['redraw','hero_demo_redraw','empty','attack']);p.add_argument('--write',action='store_true');p.add_argument('--from',dest='start',type=int,default=0);p.add_argument('--view-only',action='store_true');a=p.parse_args()
    steps=build(a.case)
    if a.view_only:
        if a.case!='attack':p.error('--view-only applies to attack')
        steps=steps[:35]
        # Seven existing original cards plus real battle wounds exercise readable hand pages.
        # This is a separate UI fixture; do not reuse its setup as the two-card combat evidence.
        import copy
        def extra(path,label,shot=False):
            return dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
                        skipScreenshot=not shot,after=[])
        first=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_ChooseDeed_basic_card_000'))
        steps[first+1:first+1]=[extra('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_'+id,'七张原牌显示构筑') for id in ['001','007','008','009']]
        second=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_ChooseDeed_basic_card_021'))
        steps.insert(second,extra('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_014','第七张原牌'))
        for s in steps[:-1]:s['after']=[];s['skipScreenshot']=True
        play=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Hand/Btn_Card_basic_card_021_0'))
        page=extra('Btn_HandPage','真实承伤后11手牌分页',True)
        page['after']=[dict(key='hand',expected='11',rule='七张真实原牌加正式承伤四张原伤牌，翻页不改牌区'),dict(key='ui.truncated',expected='',rule='底部分页不与技能、部队按钮重叠')]
        steps[play:play]=[page,extra('Btn_HandPage','返回原卡第一页')]
        a.start=0
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:
        Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(a.case,len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
