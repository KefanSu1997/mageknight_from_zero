"""Same Masters across two actual battles and a formal round; no asset mutations.

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
    rule = ('原大师四能力各自实际付费；白去城防和绿减甲供原远程击杀，'
            '蓝停一敌不算成功格挡，红只消灭另一实际完整格挡敌；'
            '蓝勇者冰火格挡实际消费，两战正式换轮保留同玩家/部队/伤牌')

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

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '合法三单位连续构筑'))
    click('CampShade/Camp/Btn_Loadout', '既有Commoners原资源起点')
    for _ in range(3): click('CampShade/Picker/Btn_NextPage', '下一页')
    click('CampShade/Picker/Btn_Select_60', '既有红蓝各一晶，不改资源/种子/敌群')
    click('CampShade/Camp/Btn_ChooseCards', '明确五牌自定快照，不是标准16牌')
    p = 'CampShade/DeedPicker/'
    click(p + 'Btn_ClearDeeds', '清除待选旧牌')
    click(p + 'Btn_ChooseDeed_basic_card_011', '原利斧投掷基础远程1')
    click(p + 'Btn_DeedNext', '基本第二页')
    for card in ['016','021']:
        click(p + 'Btn_ChooseDeed_basic_card_' + card, '原决心或狂怒')
    click(p + 'Btn_DeedKind_1', '高级原卡')
    click(p + 'Btn_DeedNext', '高级第二页')
    for card in ['012','021']:
        click(p + 'Btn_ChooseDeed_advanced_card_' + card, '原鲜血仪式或魔晶精通')
    click(p + 'Btn_DoneDeeds', '五张实际原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法等级及指挥槽')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35，三个真实指挥槽')
    click('CampShade/HeroBuild/Btn_BuildUnits', '实际原部队目录')
    scene = Path('Assets/Scenes/Part1/Part1_PveBattle.unity').read_text('utf-8-sig')
    guids = re.findall(r'guid: ([0-9a-f]+)', scene.split('  unitCatalog:',1)[1].split('--- !u!',1)[0])
    ids = {re.search(r'guid: ([0-9a-f]+)',f.read_text()).group(1):f.name[:-11]
           for f in Path('Assets/GameData/BattleDemo/Units').glob('*.asset.meta')}
    catalog = [ids[g] for g in guids]
    page = 0
    for card in ['army_2_006','army_2_011','army_005','army_008','army_2_003']:
        target = catalog.index(card)//18
        while target != page:
            direction = 'Next' if target > page else 'Previous'
            click('CampShade/UnitPicker/Btn_' + direction + 'Page', '原部队目录实际翻页')
            page += 1 if target > page else -1
        click('CampShade/UnitPicker/Btn_Unit_' + card,
              ('移除旧起点部队' if card in ['army_2_006','army_2_011'] else '选择原单位') + card)
    click('CampShade/UnitPicker/Btn_Back', '大师、蓝勇者、幻术师同属真实玩家')
    click('CampShade/HeroBuild/Btn_Begin', '同一自定玩家开始正式旅程',
          {'hero.level':6, 'units.count':3, 'hero.skills':0, 'hand':5, 'fame':35})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '实际第一回合')
    click('Btn_JourneyHand', '真实手牌供魔')
    outside('advanced_card_012')
    click('DeedInputs/Btn_InputColor_0_Green', '鲜血仪式基础：红晶与一绿标')
    click('DeedInputs/Btn_ConfirmInputs', '实际手伤1、红晶1变2、绿标1',
          {'wounds':1, 'red':2, 'tokens.green':1, 'green':0})
    outside('advanced_card_021')
    click('DeedInputs/Btn_InputColor_0_Blue', '复制已持有的实际蓝晶')
    click('DeedInputs/Btn_ConfirmInputs', '精通基础不付蓝费，蓝晶1变2', {'blue':2})
    click('JourneyHand/Btn_OwnedUnits', '实际幻术师原卡')
    click('OwnedUnits/Btn_Unit_2', '第三槽自有幻术师')
    click('OwnedUnits/Btn_Ability_2', '原白色魔晶能力')
    click('OwnedUnits/Btn_Use', '幻术师实际耗竭，产生白晶',
          {'white':1, 'tokens.white':0, 'units.ready':'True,True,False'})
    click('OwnedUnits/Btn_Close', '保留原单位实体')
    for card in ['basic_card_016','basic_card_021']:
        discard(card)
    click('JourneyHand/Btn_CloseHand', '保留真实魔晶与手伤')
    click('Btn_JourneyEncounter_10', '既有原巨鹰017，城防且护甲4')
    click('Enemy_0/Btn_Target', '实际原巨鹰')
    click('Btn_Roster', '大师原卡及能力')
    click('Units/Btn_Unit_0', '第一槽实际大师')
    click('Inspector/Btn_UnitAbility_3', '原白费去城防；大师耗竭后仍可用不同能力',
          {'white':0, 'enemies.fortified':'False', 'units.ready':'False,True,False'})
    click('Inspector/Btn_UnitAbility_2', '同大师绿费真实护甲4减到1',
          {'tokens.green':0, 'enemies.armor':'1', 'wounds':1}, True)
    click('Btn_Roster', '返回实际原牌')
    play('basic_card_011','base', {'ranged':1})
    click('Enemy_0/Btn_Target', '去城防后真实远程允许目标')
    click('Inspector/Btn_Resolve', '远程1击杀原护甲4巨鹰，名望35变38', {'kills':1, 'fame':38})
    for _ in range(4): click('Inspector/Btn_Next', '正式完成首战剩余阶段')
    click('Inspector/Btn_EndTurn', '正式弃完原牌，保留同一伤牌与单位耗竭',
          {'hand':1, 'wounds':1, 'journey.discardCount':5, 'units.ready':'False,True,False',
           'red':2, 'blue':2, 'journey.completedTurns':1})
    click('Btn_Announce', '真实空牌库宣布轮末')
    click('Btn_EndRound', '五原牌与一真实伤牌正式洗混', {'deck.count':6, 'hand':0, 'journey.dayPart':'Night'})
    click('Btn_PrepareRound', '等级6手限6，全部实际六牌抽回；原单位正式重整',
          {'hand':6, 'deck.count':0, 'wounds':1, 'units.ready':'True,True,True',
           'units.wounds':'0,0,0', 'blue':2, 'red':2, 'journey.samePlayer':'True'}, True)
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '同玩家同部队正式第二回合')
    click('Btn_JourneyHand', '随机手序按实际原卡选择')
    for card in ['basic_card_011','advanced_card_012','advanced_card_021']:
        discard(card)
    click('JourneyHand/Btn_CloseHand', '旧伤仍是首轮同一手牌')
    click('Btn_JourneyEncounter_2', '既有两个不同原032实例')
    click('Inspector/Btn_Next', '正式格挡阶段')
    click('Btn_Roster', '同一大师已正式重整')
    click('Enemy_0/Btn_Target', '第一实际敌人')
    click('Units/Btn_Unit_0', '同一第一槽大师')
    click('Inspector/Btn_UnitAbility_0', '蓝晶2变1：停止第一敌人攻击',
          {'blue':1, 'enemies.skipped':'0', 'attacks.successfullyBlocked':''})
    click('Enemy_1/Btn_Target', '第二实际敌人，不复用已停止目标')
    click('Units/Btn_Unit_0', '本场同大师继续不同能力')
    click('Inspector/Btn_UnitAbility_1', '红晶2变1：只绑定第二实际敌人', {'red':1, 'kills':0})
    click('Units/Btn_Unit_1', '同一原蓝勇者')
    click('Inspector/Btn_UnitAbility_1', '另付蓝晶1变0，原冰火格挡8',
          {'blue':0, 'block':8, 'power.blocks':'ColdFire:8', 'units.ready':'False,False,True'}, True)
    click('Enemy_1/Btn_Target', '只完整格挡第二实际攻击')
    click('Inspector/Btn_Resolve', '原冰火8实际消费，第一停攻未被算作格挡')
    click('Inspector/Btn_Next', '正式承伤阶段')
    click('Inspector/Btn_Next', '真正防御结算后红能力消灭第二敌，旧伤仍1',
          {'kills':1, 'fame':40, 'wounds':1, 'attacks.successfullyBlocked':'1:0', 'enemies.skipped':'0'}, True)
    click('Btn_Roster', '实际原牌完成剩余敌人')
    play('basic_card_021','attack', {'melee':2})
    play('basic_card_016',None, {'melee':3})
    click('Enemy_0/Btn_Target', '先前仅停攻的同一存活敌人')
    click('Inspector/Btn_Resolve', '真实攻击3击败甲3第一敌', {'kills':2, 'fame':42})
    click('Inspector/Btn_Next', '完成第二战')
    click('Inspector/Btn_EndTurn', '连续双战双回合，旧伤、单位及剩余红晶真实保留',
          {'hand':1, 'wounds':1, 'journey.discardCount':5, 'units.ready':'False,False,True',
           'units.wounds':'0,0,0', 'red':1, 'blue':0, 'fame':42,
           'journey.completedTurns':2, 'journey.completedBattles':2, 'journey.samePlayer':'True'})
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
