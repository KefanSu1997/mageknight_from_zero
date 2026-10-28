"""Real box top/bottom supply: normal split shield, next-round burned wrath horn.

Seed 917 follows the existing formal shuffle (44 advanced, 24 spell, 24 artifact
cards after removing the owned box): items_020, items_009, items_006.
The horn legally chooses zero extra dice: the journey supply seed does not seed
card-effect randomness, so this route does not pretend to predict a random die.
Only --prepare changes the existing replay fixture; --restore guards exact bytes.
"""
import json
import sys
from pathlib import Path

SEED = 917
fixture = Path('Assets/GameData/BattleDemo/HornGold.asset')
backup = Path('tmp/box_weapon_HornGold.original')
if '--restore' in sys.argv:
    original = backup.read_bytes()
    expected = original.replace(b'effectRandomSeed: 0', f'effectRandomSeed: {SEED}'.encode())
    assert fixture.read_bytes() == expected, 'Replay fixture changed independently; refusing overwrite.'
    fixture.write_bytes(original)
    print('Original seed-0 fixture restored exactly')
    raise SystemExit
if '--prepare' in sys.argv:
    original = fixture.read_bytes()
    assert original.count(b'effectRandomSeed: 0') == 1, 'Restore the previous fixture first.'
    if backup.exists():
        assert backup.read_bytes() == original, 'Saved original differs; inspect before replacing.'
    else:
        backup.parent.mkdir(parents=True, exist_ok=True)
        backup.write_bytes(original)
    fixture.write_bytes(original.replace(b'effectRandomSeed: 0', f'effectRandomSeed: {SEED}'.encode()))

steps = []
RULE = '宝盒物理实体与真实揭示神器分离；原盾分次真实格挡，原号角焚毁合法零额外骰与攻城消费，借牌回合末归底'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def reveal(artifact, burn=False, capture=False):
    click('Hand/Btn_Card_items_024_0', '实际手牌宝盒本体')
    click('Inspector/Btn_OpenSpecialArtifact', '展开原宝盒')
    click('SpecialArtifact/Btn_ConfirmSpecial', '揭示真实神器牌库顶',
          {'box.revealed': artifact, 'box.used': 'False', 'supply.artifacts': 23}, capture)
    click('SpecialArtifact/Btn_SpecialBurn' if burn else 'SpecialArtifact/Btn_SpecialNormal',
          '借原焚毁效果' if burn else '借原普通效果')
    click('SpecialArtifact/Btn_ConfirmSpecial', '进入原武器Inspector，物理选牌仍为宝盒')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原宝盒两轮武器连续场景'))
click('CampShade/Camp/Btn_Loadout', '既有可重放起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '显式种子917与原红蓝库存')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选原牌')
for card_id in ('000', '007'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card_id, '原基本牌 ' + card_id)
click(p + 'Btn_DeedNext', '基础第二页')
for card_id in ('016', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card_id, '原基本牌 ' + card_id)
click(p + 'Btn_DeedKind_3', '原神器')
for _ in range(2):
    click(p + 'Btn_DeedNext', '神器下一页')
click(p + 'Btn_ChooseDeed_items_024', '唯一自有神器为原神秘宝盒')
click(p + 'Btn_DoneDeeds', '保存五原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄等级')
click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroBuild/Btn_Level_4', '等级4原护甲和名望')
click('CampShade/HeroBuild/Btn_Begin', '真实玩家和正式神器供应',
      {'hero.level': 4, 'fame': 15, 'units.count': 0, 'supply.artifacts': 24})
click('Tactic_day_1/Btn_Tactic_day_1', '正式首日战术')
click('Btn_BeginTurn', '第一实际回合')
click('Btn_JourneyEncounter_2', '两枚独立原哨卫各攻击4')
click('Inspector/Btn_Next', '原格挡阶段')
reveal('items_020', capture=True)
click('Inspector/Btn_ShieldMode', '选择原盾两次独立格挡4')
click('Inspector/Btn_ArtifactPlay', '普通借用真实宝盒，两个独立格挡额度',
      {'block': 8, 'box.used': 'True', 'removed.ids': '', 'played.ids': 'items_024'}, True)
for enemy, remaining in ((0, 4), (1, 0)):
    click(f'Enemy_{enemy}/Btn_Target', '选择该实际敌人的独立攻击')
    click('Inspector/Btn_Resolve', '原分次格挡4真正消费一次', {'block': remaining})
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '两攻击均格挡，没有真实伤牌', {'wounds': 0})
for card_id, choice, attack in (('021', 'strong', 4), ('016', 'attack', 6)):
    click('Hand/Btn_Card_basic_card_' + card_id + '_0', '原反击手牌')
    click('Inspector/Btn_Choice_' + choice, '原反击选择')
    click('Inspector/Btn_Play', '真实原费用及攻击', {'melee': attack})
click('Enemy_0/Btn_Target', '选择第一原哨卫')
click('Enemy_1/Btn_Target', '同时选择第二原哨卫')
click('Inspector/Btn_Resolve', '原攻击6击杀两枚原护甲3', {'kills': 2, 'fame': 20, 'red': 0})
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '盾实际归供应底，普通宝盒进入原弃牌',
      {'journey.completedTurns': 1, 'supply.artifacts': 24, 'box.revealed': '',
       'removed.ids': '', 'wounds': 0})
click('Btn_Announce', '五原牌实际库空声明轮末', {'deck.count': 0})
click('Btn_EndRound', '正式轮末把同一宝盒洗回', {'journey.round': 2})
click('Btn_PrepareRound', '正式补齐五原牌', {'hand': 5, 'deck.count': 0, 'supply.artifacts': 24})
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '同玩家第二实际回合')
click('Btn_JourneyEncounter_10', '原城防巨鹰护甲4')
reveal('items_009', burn=True, capture=True)
click('Inspector/Btn_ArtifactPlay', '真实焚毁宝盒本体，原合法选择零额外骰及攻城5',
      {'siege': 5, 'horn.bonus': 0, 'dice.faces': '', 'dice.count': 0, 'wounds': 0,
       'removed.ids': 'items_024', 'box.used': 'True', 'box.revealed': 'items_009'})
click('Enemy_0/Btn_Target', '实际城防巨鹰')
click('Inspector/Btn_Resolve', '原攻城5合法击杀城防护甲4', {'kills': 1, 'fame': 24})
for _ in range(4):
    click('Inspector/Btn_Next', '推进余下正式阶段至战斗结束')
click('Inspector/Btn_EndTurn', '原号角仍归底，永久移除仅宝盒本体',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'supply.artifacts': 24, 'box.revealed': '', 'removed.ids': 'items_024',
       'wounds': 0, 'fame': 24, 'journey.samePlayer': 'True'}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
