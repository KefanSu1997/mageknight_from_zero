"""Actual original Keep guard conquest, natural recruitment, formal round consumer.

Default is plan-only. Explicit --write saves a scene configuration; --from N
replays the successful prefix without assertions or screenshots. No fixture edits.
"""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = ('原025自身城防由实际攻城8击杀；真实要塞归属后次回合支付原图标招募；'
            '同魔像跨正式轮次付蓝格挡4及真实旧伤/焚毁牌区保留')
    recording = False

    def click(path, label, expected=None, capture=False):
        checks = {'ui.truncated': '', **(expected or {})} if recording else {}
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not (recording and capture),
            after=[dict(key=k, expected=str(v), rule=rule) for k, v in checks.items()]))

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '既有演练实际要塞，非原七格板块完成证明'))
    click('CampShade/Camp/Btn_Loadout', '复用HornGold原资源/种子0')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '下一页')
    click('CampShade/Picker/Btn_Select_39', '红蓝晶各一，无起始部队')
    click('CampShade/Camp/Btn_ChooseCards', '合法五原牌自定构筑')
    p = 'CampShade/DeedPicker/'
    click(p + 'Btn_ClearDeeds', '清空待选')
    click(p + 'Btn_DeedKind_2', '原法术')
    for card in ['magic_004', 'magic_009']:
        click(p + 'Btn_ChooseDeed_' + card, '原地道或火球')
    click(p + 'Btn_DeedKind_3', '原神器')
    for card in ['items_007', 'items_010']:
        click(p + 'Btn_ChooseDeed_' + card, '原绿戒或金币包')
    click(p + 'Btn_DeedNext', '神器第二页')
    click(p + 'Btn_ChooseDeed_items_015', '原黑暗护符')
    click(p + 'Btn_DoneDeeds', '五张实际原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '原英雄合法等级')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '托瓦克')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6甲3，三指挥槽，无技能')
    click('CampShade/HeroBuild/Btn_Begin', '同玩家开始正式旅程')
    click('Tactic_day_1/Btn_Tactic_day_1', '正式首日战术')
    click('Btn_BeginTurn', '第一实际回合')
    click('Btn_JourneyHand', '原手牌费用')
    click('JourneyHand/@card=items_007', '原绿宝石戒指')
    click('JourneyHand/Btn_ArtifactNormal', '正常绿晶和绿标')
    click('JourneyHand/Btn_PlayOutside', '共用已过资源前缀')
    # New scope starts before the different amulet burn and actual Keep path.
    recording = True
    click('JourneyHand/@card=items_015', '选原黑暗护符，核当前真实起点',
          {'hero.level': 6, 'hero.armor': 3, 'fame': 36, 'red': 1, 'blue': 1,
           'green': 1, 'tokens.green': 1, 'units.count': 0,
           'world.guards': '-1:-1=monster_002,-1:1=monster_025'})
    click('JourneyHand/Btn_ArtifactBurn', '原焚毁三颗独立颜色魔力')
    # ResetArtifactInputs starts Red(0), Blue(1), White(3); choose Black/Black/Red.
    for row, count in enumerate([5, 4, 3]):
        for _ in range(count):
            click(f'JourneyHand/Btn_ArtifactColor_{row}', '独立选择第' + str(row + 1) + ('颗黑魔力' if row < 2 else '颗红魔力'))
    click('JourneyHand/Btn_PlayOutside', '两黑一红实际标记、白昼许可、护符永久移除',
          {'tokens.black': 2, 'tokens.red': 1, 'mana.allowBlackAtDay': 'True', 'removed.ids': 'items_015'})
    click('JourneyHand/@card=magic_004', '实际原地道')
    click('JourneyHand/Btn_OutsideChoice_strong', '地道强化实际城防终点')
    click('JourneyHand/Btn_PlayOutside', '原路径输入')
    click('DeedInputs/Btn_Path_keep', '相邻实际要塞(-1,1)，不替换守军')
    click('DeedInputs/Btn_ConfirmInputs', '真实绿黑费用与同一原025守军',
          {'tokens.green': 0, 'tokens.black': 1, 'green': 1,
           'world.tunnel': 'True', 'world.ignoreSite': 'True',
           'enemies.fortified': 'True', 'enemies.armor': '7',
           'journey.position': '-1,1', 'reputation': -1})
    click('Hand/Btn_Card_magic_009_0', '原火焰风暴')
    click('Inspector/Btn_Choice_strong', '原攻城火焰8与自伤1')
    click('Inspector/Btn_Play', '真实红标优先支付与第二黑标，保留原红晶',
          {'siege': 8, 'red': 1, 'tokens.red': 0, 'tokens.black': 0, 'wounds': 1,
           'enemies.fortified': 'True', 'enemies.armor': '7'}, True)
    click('Enemy_0/Btn_Target', '同一甲7自身城防025')
    click('Inspector/Btn_Resolve', '实际攻城8允许自身城防并击杀', {'kills': 1, 'fame': 39})
    for _ in range(4): click('Inspector/Btn_Next', '正式完成空敌剩余阶段')
    click('Inspector/Btn_FinishSite', '真实登记本人要塞归属、弃置同025，无塔法术奖励',
          {'world.conquered': '-1:1', 'world.discard': 'monster_025',
           'world.rewardPending': 'False', 'world.tunnel': 'False'}, True)
    click('Inspector/Btn_EndTurn', '正式回合结束保留金币包和真实自伤',
          {'journey.completedTurns': 1, 'journey.completedBattles': 1,
           'hand': 2, 'wounds': 1, 'deck.count': 0, 'tokens.black': 0,
           'mana.allowBlackAtDay': 'False', 'journey.position': '-1,1'})
    click('Btn_BeginTurn', '本人刚征服要塞中的下一正式回合')
    click('Btn_JourneyInteraction', '自然地点交涉入口')
    click('JourneyInteraction/Btn_BeginInteraction', '正式按本人要塞归属授权，声誉只记一次',
          {'journey.siteRecruitLocation': 'Keep', 'journey.siteCanInteract': 'True',
           'journey.interacting': 'True', 'journey.siteShieldInfluence': 0,
           'influence': -1, 'reputation': -1})
    click('JourneyInteraction/Btn_CloseInteraction', '保留同次交涉')
    click('Btn_JourneyHand', '原金币包实际付款来源')
    click('JourneyHand/@card=items_010', '原金币包')
    click('JourneyHand/Btn_ArtifactBurn', '焚毁原影响9/名望3')
    click('JourneyHand/Btn_PlayOutside', '实际影响8与第二件神器永久移除',
          {'influence': 8, 'fame': 42, 'removed.ids': 'items_015,items_010'})
    click('JourneyHand/Btn_CloseHand', '同一要塞合法原图标供应')
    click('Btn_JourneyInteraction', '当前真实原供应')
    click('JourneyInteraction/Btn_Offer_army_2_001', '原城堡图标守护者魔像，印刷费用7')
    click('JourneyInteraction/Btn_Recruit', '实际支付7取得供应实体',
          {'influence': 1, 'units.count': 1, 'units.ready': 'True',
           'journey.freeSlots': 2, 'journey.offerCount': 2,
           'journey.siteRecruitLocation': 'Keep'}, True)
    click('JourneyInteraction/Btn_CloseInteraction', '同一新招魔像')
    click('Btn_EndTurn', '正式清影响、真实手伤保留，没有要塞奖励牌',
          {'journey.completedTurns': 2, 'hand': 1, 'wounds': 1,
           'influence': 0, 'units.ready': 'True', 'deck.count': 0})
    click('Btn_Announce', '实际空牌库声明轮末，避免伤牌空过')
    click('Btn_EndRound', '三张未移除原牌和旧伤正式回收',
          {'journey.round': 2, 'deck.count': 4, 'hand': 0,
           'removed.ids': 'items_015,items_010'})
    click('Btn_PrepareRound', '本人要塞同位置正式补回四实际牌与重整',
          {'hand': 4, 'wounds': 1, 'deck.count': 0, 'units.ready': 'True',
           'blue': 1, 'green': 1, 'red': 1, 'world.conquered': '-1:1'})
    click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
    click('Btn_BeginTurn', '下一轮第三实际行动回合')
    click('Btn_JourneyEncounter_0', '已有演练原032，非原地图导航完成声明')
    click('Inspector/Btn_Next', '正式格挡阶段')
    click('Btn_Roster', '同一实际要塞新招魔像')
    click('Units/Btn_Unit_0', '相同部队实体原卡')
    click('Inspector/Btn_UnitAbility_2', '另付实际蓝晶发动寒冰格挡4',
          {'blue': 0, 'block': 4, 'power.blocks': 'Ice:4',
           'units.ready': 'False', 'wounds': 1}, True)
    click('Enemy_0/Btn_Target', '原物理攻击4实际目标')
    click('Inspector/Btn_Resolve', '寒冰格挡4真实完整消费')
    click('Inspector/Btn_Next', '正式承伤阶段')
    click('Inspector/Btn_Next', '无新增伤，原自伤仍1',
          {'wounds': 1, 'attacks.successfullyBlocked': '0:0'})
    click('Btn_Roster', '正式换轮抽回的原法术消费者')
    click('Hand/Btn_Card_magic_009_0', '同一火球原牌')
    click('Inspector/Btn_Choice_base', '原火焰远程5可在近战阶段消费')
    click('Inspector/Btn_Play', '付第一回合由真实红标代付而保留的红晶',
          {'red': 0, 'ranged': 5, 'wounds': 1})
    click('Enemy_0/Btn_Target', '同一甲3原032')
    click('Inspector/Btn_Resolve', '真实火焰5完成击杀', {'kills': 1, 'fame': 44})
    click('Inspector/Btn_Next', '正式第二场战斗结束')
    click('Inspector/Btn_EndTurn', '三回合跨轮、本人要塞/同部队/旧伤/永久移除牌保留',
          {'journey.completedTurns': 3, 'journey.completedBattles': 2,
           'world.conquered': '-1:1', 'journey.position': '-1,1',
           'journey.samePlayer': 'True', 'units.count': 1, 'units.ready': 'False',
           'units.wounds': 0, 'wounds': 1, 'hand': 3, 'fame': 44,
           'blue': 0, 'red': 0, 'green': 1, 'removed.ids': 'items_015,items_010'})
    return steps


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    if not 0 <= args.start <= len(steps): parser.error('--from outside route')
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False, initialDelaySeconds=2, defaultWaitAfterSeconds=.3,
            maxRunSeconds=420, useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
    print(len(steps), 'steps;', sum(len(s['after']) for s in steps), 'assertions;',
          sum(not s['skipScreenshot'] for s in steps), 'captures;',
          sum(bool(s['after']) for s in steps), 'new verified steps;',
          'written' if args.write else 'plan only')


if __name__ == '__main__': main()
