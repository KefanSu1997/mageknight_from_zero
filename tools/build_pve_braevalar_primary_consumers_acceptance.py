"""Braevalar: real conquered Tower interaction and original elemental damage.

Read-only reuse of the existing Tower route builder. Default plans only; --write
is explicit. No fixture/map edits, no synthetic conquest and no Unity dispatch.
"""
import argparse
import json
import runpy
from pathlib import Path


def build():
    # Loading definitions under a non-main name does not write the source config.
    tower = runpy.run_path(str(Path(__file__).with_name('build_pve_tower_recruitment_acceptance.py')))
    original = tower['build']()
    stop = next(i for i,s in enumerate(original) if s['label']=='原金币包实际支付能力')
    steps=[]
    rule=('布拉维拉原001:00/04；真实地道征服塔后原声誉-1、迷醉2、金币普通4与承诺2恰付魔像7；'
          '下回合同魔像参战，原寒冰攻击2由元素抗性2实际全免，原火焰箭矢3与魔像物理2经抗性击杀')

    def click(path,label,checks=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
            skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule=rule)
            for k,v in {'ui.truncated':'',**(checks or {})}.items()]))

    for step in original[:stop]:
        label=step['label']
        if label=='选择原许可黑魔力':continue
        if label=='白昼真实黑魔力及许可':
            click('JourneyHand/Btn_ArtifactBurn','焚毁原护符三独立颜色，保留库存红晶供后战')
            # Actual defaults are Red, Blue, White, not three Red slots.
            for slot,count,color in [(0,5,'黑'),(1,4,'黑'),(2,3,'红')]:
                for _ in range(count):click('JourneyHand/Btn_ArtifactColor_'+str(slot),'独立选择第'+str(slot+1)+'枚'+color+'色')
            click('JourneyHand/Btn_PlayOutside','原黑黑红实际标记与白昼许可',
                  {'tokens.black':2,'tokens.red':1,'red':1,'mana.allowBlackAtDay':'True','removed.ids':'items_015'})
            continue
        step=json.loads(json.dumps(step))
        if label=='明确五张原牌自定起手':step['label']='七张原牌自定构筑，非标准16牌开局'
        if label=='五张实际原牌':step['label']='保留七张真实原牌'
        if step['buttonPath'].endswith('/Btn_Hero_Tovak'):
            step['buttonPath']=step['buttonPath'].replace('Btn_Hero_Tovak','Btn_Hero_Braevalar');step['label']='原布拉维拉'
        for a in step['after']:
            a['rule']=rule
            if label=='同玩家进入已有演练世界，不冒充原七格地图完成' and a['key']=='hand':a['expected']='7'
            if label=='付款绿黑，实际原守军进入同一战斗' and a['key']=='tokens.black':a['expected']='1'
            if label=='正式出牌与贡献' and a['key']=='red':a['expected']='1'
            if label=='正式回合末领奖，保留位置/金币包/实际奖励牌' and a['key']=='hand':a['expected']='4'
        steps.append(step)
        if label=='清除旧待选牌':
            click('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_008','原承诺基础2补足实际招募费')
            click('CampShade/DeedPicker/Btn_DeedKind_1','原高级行动第一页')
            click('CampShade/DeedPicker/Btn_ChooseDeed_advanced_card_000','原火焰箭矢留给实际寒冰抗性敌人')
        if step['buttonPath'].endswith('/Btn_Level_6'):
            for i in [0,4]:click('CampShade/HeroBuild/Btn_Skill_'+str(i),'合法本人元素抗性/迷醉')
        if step['buttonPath'].endswith('/HeroBuild/Btn_Begin'):
            step['after'] += [dict(key=k,expected=str(v),rule=rule) for k,v in {'hero.id':'Braevalar','hero.skills':2,'upgrade.skillSources':'hero_skill_pool_001:00,hero_skill_pool_001:04'}.items()]

    click('Btn_JourneyHand','同一已征服塔的实际手牌')
    click('JourneyHand/@card=items_010','保留原金币包')
    click('JourneyHand/Btn_ArtifactNormal','普通影响4名望2，保留原物理神器')
    click('JourneyHand/Btn_PlayOutside','原负声誉已入账，金币4后净3',{'influence':3,'fame':42})
    click('JourneyHand/@card=basic_card_008','保留原承诺')
    click('JourneyHand/Btn_OutsideChoice_base','原基础影响2，不用白费')
    click('JourneyHand/Btn_PlayOutside','净影响5仍不足原魔像费用7',{'influence':5})
    click('JourneyHand/Btn_Support','实际本人迷醉入口')
    click('JourneySupport/Btn_OutsideSkill_1','原迷醉按当前实际城防地点取2')
    click('JourneySupport/Btn_UseOutsideSkill','迷醉贡献真实欠缺的2，恰好达到招募7',
          {'influence':7,'journey.outsideSkill':'hero_skill_pool_001:04','journey.outsideSkillUsed':'True',
           'journey.position':'-1,-1','world.conquered':'-1:-1'},True)
    click('JourneySupport/Btn_BackSupport','同次交涉持续')
    click('JourneyHand/Btn_CloseHand','付款实际供应单位')
    click('Btn_JourneyInteraction','真实法师塔供给')
    click('JourneyInteraction/Btn_Offer_army_2_001','实际原魔像，塔图标与印刷费用7')
    click('JourneyInteraction/Btn_Recruit','金币4+承诺2+迷醉2-声誉1，真实全部付款',
          {'influence':0,'units.count':1,'units.ready':'True','units.wounds':0,'journey.offerCount':2},True)
    click('JourneyInteraction/Btn_CloseInteraction','保留新招同实体')
    click('Btn_EndTurn','第二回合保持原箭矢/奖励与红晶1',
          {'journey.completedTurns':2,'hand':2,'red':1,'wounds':0,'fame':42,'removed.ids':'items_015'})
    click('Btn_BeginTurn','同英雄第三回合')
    click('Btn_JourneyEncounter_7','既有原寒冰巫妖004，寒冰2护甲4物理/寒冰抗性')
    for _ in range(2):click('Inspector/Btn_Next','推进正式元素承伤阶段')
    click('Btn_Skills','本人原元素抗性')
    click('Skills/Btn_Skill_0','原001:00，非借用')
    click('Inspector/Btn_SkillChoice_0','忽略下一次火焰或寒冰2')
    click('Inspector/Btn_UseSkill','正式登记原英雄元素减伤')
    click('Btn_Skills','返回同一实际手牌')
    click('Inspector/Btn_Next','原寒冰2实际承伤全免，英雄保持零伤',
          {'phase':'Melee','wounds':0,'skills.used':'True,False','units.ready':'True'},True)
    click('Btn_Roster','上一回合实际招募魔像')
    click('Units/Btn_Unit_0','同一真实魔像')
    click('Inspector/Btn_UnitAbility_0','原物理攻击2，冰巫妖物抗后有效1',{'melee':2,'units.ready':'False'})
    click('Btn_Roster','保留两回合的原箭矢')
    click('Hand/Btn_Card_advanced_card_000_0','实际火焰箭矢原图')
    click('Inspector/Btn_Choice_strong','红费远程火焰3可用于近战')
    click('Inspector/Btn_Play','支付一直保留的红晶1',{'red':0,'ranged':3,'wounds':0})
    click('Enemy_0/Btn_Target','同一原寒冰巫妖')
    click('Inspector/Btn_Resolve','火焰3加物抗后魔像1，实际击败护甲4',{'kills':1,'fame':47,'wounds':0},True)
    click('Inspector/Btn_Next','真实第二战完成')
    click('Inspector/Btn_EndTurn','三回合同英雄/同魔像与实际征服塔，零伤费用守恒',
          {'journey.completedTurns':3,'journey.completedBattles':2,'journey.samePlayer':'True','hero.id':'Braevalar',
           'hero.skills':2,'units.count':1,'units.ready':'False','units.wounds':0,'fame':47,'wounds':0,
           'red':0,'blue':1,'green':1,'world.conquered':'-1:-1','journey.position':'-1,-1','removed.ids':'items_015'},True)
    return steps


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write',action='store_true');parser.add_argument('--from',dest='start',type=int,default=0)
    parser.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    args=parser.parse_args();steps=build()
    if not 0<=args.start<=len(steps):parser.error('--from outside plan')
    for s in steps[:args.start]:s['after']=[];s['skipScreenshot']=True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,
            initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',sum(not s['skipScreenshot'] for s in steps),'screenshots;', 'written' if args.write else 'plan only')


if __name__=='__main__':main()
