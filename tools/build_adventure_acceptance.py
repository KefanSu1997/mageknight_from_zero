"""生成固定预期的真实按钮验收；预期不从被测规则或运行快照推导。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = "AutomationOutputs/OfficialCards/20260910"
UI = "Canvas/UIRoot/"
CHECK_UI = {"hand", "fame", "wounds", "redCrystal"}


def checks(values, rule):
    result = []
    for key, value in values.items():
        result.append(dict(key=key, expected=str(value), rule=rule))
        if key in CHECK_UI:
            result.append(dict(key="ui:" + key, expected=str(value), rule="可见读数必须对应规则状态。" + rule))
    return result


class Scenario:
    def __init__(self, name):
        self.name, self.steps = name, []

    def click(self, path, label, after, rule, before=None):
        self.steps.append(dict(buttonPath=UI + path, label=label, waitAfterSeconds=.18,
                               before=checks(before or {}, rule), after=checks(after, rule)))
        return self

    def card(self, serial, card, hand, **extra):
        card = "basic_card_" + str(card).zfill(3)
        return self.click(f"Hand/Card_{serial}", f"选择_{card}_{serial}",
                          dict(selectedCard=card, hand=hand, accepted=1, **{f"ui:cardArt:{serial}":card}, **({"ui:printedEffect":"基础：" + {"basic_card_000":"移动力2。", "basic_card_014":"移动力2。", "basic_card_008":"影响力2。", "basic_card_016":"攻击或格挡2。", "basic_card_021":"攻击或格挡2。"}[card]} if not extra.get("sideways") else {}), **extra), "选牌不扣牌，确认才消耗。")

    def target(self, target, **extra):
        kind, name = target.split(":")
        path = {"enemy": "Enemy_", "site": "Site_", "offer": "Offer_"}[kind] + name
        return self.click("Stage/Targets/" + path, "点选_" + target.replace(":", "_"),
                          dict(target=target, accepted=1, **extra), "点选目标不扣费。")

    def confirm(self, label, after, rule):
        return self.click("Inspector/Btn_Confirm", label, after, rule)

    def reset(self):
        return self.click("Btn_Reset", "重新开始", dict(hand=5, played=0, wounds=0, redCrystal=1), "重开创建独立的新运行状态。")

    def write(self, file=None, folder=None):
        config = dict(scenePath=f"Assets/Scenes/Part1/Part1_{self.name}Rules.unity",
                      screenshotsDirectory=f"{OUT}/{folder or self.name}/captures",
                      reportPath=f"{OUT}/{folder or self.name}/report.json", useRunSubfolder=True,
                      captureInitialView=True, initialDelaySeconds=1.3, defaultWaitAfterSeconds=.18,
                      maxRunSeconds=180, steps=self.steps)
        path = ROOT / "AutomationConfigs" / (file or f"adventure_{self.name.lower()}.json")
        path.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")



def enhance(s, color, value):
    return s.click("Inspector/Btn_Enhance", "选择原卡强化", dict(enhanced=1, selectedValue=value, **{color + "Crystal":1, "ui:printedEffect":"强化：" + {"blue":"格挡5。", "green":"移动力4。", "white":"影响力4。", "red":"攻击4。"}[color]}), "选择强化不支付；必须使用原卡规定的耗色和数值。")

c = Scenario("Combat")
c.card(0,16,5,selectedValue=2).confirm("缺目标不得扣牌",dict(accepted=0,hand=5,played=0),"选择不是打出。")
c.target("enemy:0").confirm("决心基础格挡2",dict(hand=4,block0=2,blueCrystal=1,**{"ui:resource":"2 / 4"}),"原卡决心基础格挡2，不是自制格挡3。")
c.confirm("格挡不足承受完整攻击",dict(phase="Attack",wounds=2,hand=6,woundCards=2,actionCardTotal=10),"未达到4则承受完整攻击，4除护甲2产生2张伤牌。")
c.click("Hand/Card_10","伤牌不能打出",dict(accepted=0,hand=6),"伤牌不能横置或当攻击使用。")
c.card(2,21,6,selectedValue=2).target("enemy:0").confirm("狂怒基础攻击2",dict(attack0=2,hand=5,redCrystal=1),"狂怒基础攻击2。")
c.card(3,21,5).confirm("第二张狂怒基础合计4",dict(attack0=4,hand=4,**{"ui:resource":"4 / 4"}),"两张独立实例，2加2击破护甲4。")
c.confirm("结算且不重复受伤",dict(completed=1,fame=3,wounds=2),"只结算攻击和名望，不再次结算敌人伤害。")
c.reset().card(0,16,5).target("enemy:0")
enhance(c,"blue",5).confirm("蓝色强化决心格挡5",dict(block0=5,blueCrystal=0,redCrystal=1,hand=4),"决心强化是格挡5，消耗蓝色魔力。")
c.confirm("成功格挡免伤",dict(phase="Attack",wounds=0,hand=4),"5达到需求4。")
c.card(2,21,4).target("enemy:0")
enhance(c,"red",4).confirm("红色强化狂怒攻击4",dict(attack0=4,redCrystal=0,blueCrystal=0,hand=3),"原卡狂怒强化攻击4，不是6，也不是火焰攻击。")
c.card(3,21,3).click("Inspector/Btn_Enhance","再次声明强化",dict(enhanced=1,redCrystal=0,hand=3),"声明可失败，尚未出牌。")
c.confirm("缺红色魔力不降级打出",dict(accepted=0,redCrystal=0,hand=3,attack0=4,selectedCard="basic_card_021"),"魔力不足不得偷偷执行基础攻击2。")
c.click("Inspector/Btn_Cancel","取消未支付行动",dict(selectedCard="",hand=3,attack0=4),"取消不消耗牌。")
c.confirm("原卡强化获胜",dict(completed=1,fame=3,wounds=0),"攻击4对护甲4。")
c.click("Inspector/Btn_Rules","展开规则",{"ui:rulesVisible":1},"规则按需展开。")
c.click("Inspector/Btn_Rules","收起规则",{"ui:rulesVisible":0},"收起后不占用主操作区。")
c.click("Btn_Menu","切换遭遇",{},"场景与卡牌相互独立。")
c.click("AdventureMenu/Btn_wolf_encounter","复用原卡迎战灰狼",dict(scenario="wolf_encounter",requiredBlock0=6,hand=5),"相同卡牌资源面对不同规则需求。")
c.card(0,16,5).target("enemy:0")
enhance(c,"blue",5).confirm("决心强化5",dict(block0=5,hand=4,blueCrystal=0),"迅捷需要6，5还不够。")
c.card(4,0,4,sideways=1,selectedValue=1).confirm("行进横置格挡1",dict(block0=6,hand=3,greenCrystal=1),"非伤牌可横置普通格挡1，不发动移动也不耗绿魔力。")
c.confirm("横置补足迅捷格挡",dict(phase="Attack",wounds=0),"5加1达到6。")
c.card(2,21,3).target("enemy:0")
enhance(c,"red",4).confirm("狂怒强化4",dict(attack0=4,hand=2,redCrystal=0),"护甲5尚未被击破。")
c.card(1,16,2,selectedValue=2).confirm("决心基础改选攻击2",dict(attack0=6,hand=1),"决心基础攻击或格挡2，攻击阶段可选攻击。")
c.confirm("原卡组合战胜灰狼",dict(completed=1,fame=4,wounds=0),"4加2大于等于护甲5。")
c.write()

e=Scenario("Exploration")
e.target("site:forest").confirm("零移动不能进入",dict(accepted=0,movement=0,position="camp"),"白天森林需要3。")
e.card(0,0,5,selectedValue=2).target("site:forest")
enhance(e,"green",4).confirm("行进绿色强化4",dict(movement=4,greenCrystal=0,blueCrystal=1,hand=4),"行进强化4消耗绿色，不是移动6。")
e.confirm("白天森林费用3",dict(movement=1,position="forest",**{"ui:resource":"1"}),"4减3等于1。")
e.card(1,14,4).target("site:frontier").confirm("耐力基础移动2",dict(movement=3,hand=3,blueCrystal=1),"耐力基础不耗蓝色。")
e.confirm("探索只揭示不移动",dict(movement=1,position="forest",revealed=7),"探索2，进入另付地形费用。")
e.confirm("进入平原费用不足",dict(accepted=0,movement=1,position="forest"),"平原需要2。")
e.card(2,0,3).confirm("另一张行进基础2",dict(movement=3,hand=2),"同名卡实例分别使用。")
e.confirm("进入揭示平原",dict(movement=1,position="frontier",actionCardTotal=10),"3减2等于1。")
e.reset().target("site:lake").confirm("湖泊不可通行",dict(accepted=0,position="camp",movement=0),"不能进入湖泊。")
e.target("site:mountain").confirm("高山不可通行",dict(accepted=0,position="camp"),"不能进入高山。")
e.target("site:frontier").confirm("非相邻不能探索",dict(accepted=0,revealed=6),"禁止跳格。")
e.write()

r=Scenario("Recruitment")
r.target("offer:guard").confirm("影响力不足",dict(accepted=0,influence=0,units=0),"影响来自出牌，不预填免费影响力。")
r.card(0,8,5).target("offer:guard")
enhance(r,"white",4).confirm("承诺白色强化4",dict(influence=4,whiteCrystal=0,hand=4),"承诺强效影响4。")
r.confirm("四点还不足费用五",dict(accepted=0,influence=4,units=0),"不能少付费用。")
r.card(1,8,4).confirm("第二张承诺基础2",dict(influence=6,hand=3,**{"ui:resource":"6"}),"4加2等于6。")
r.confirm("招募扣五余一",dict(influence=1,units=1,ready=1,freeSlots=0),"占用指挥槽。")
r.target("offer:guard").confirm("不能重复招募",dict(accepted=0,influence=1,units=1),"同一供给单位只能招募一次。")
r.target("offer:monk").confirm("地点不符不得招募",dict(accepted=0,influence=1),"村庄不能招募修道院单位。")
r.click("Inspector/Btn_Cancel","取消目标",dict(target="",influence=1),"取消不付费。")
r.confirm("回合结束正常补牌",dict(hand=5,deck=3,discard=2,turn=2,influence=0,whiteCrystal=0,units=1,ready=1),"已打出两张承诺进弃牌，补两张，保留晶体和部队。")
r.write()

j=Scenario("Journey")
j.card(0,0,5).target("site:village")
enhance(j,"green",4).confirm("行进强化准备探索",dict(movement=4,hand=4,greenCrystal=0),"原卡绿色强化4。")
j.confirm("探索村庄扣二",dict(movement=2,position="camp",revealed=2),"探索不移动。")
j.confirm("进入村庄再扣二",dict(movement=0,position="village",phase="Interaction",background="village"),"平原进入费用2。")
j.card(1,8,4).target("offer:guard")
enhance(j,"white",4).confirm("承诺强化4",dict(influence=4,whiteCrystal=0,hand=3),"白色强化4。")
j.card(2,8,3).confirm("承诺基础补二",dict(influence=6,hand=2),"4加2。")
j.confirm("支付五招募守卫",dict(influence=1,units=1,ready=1),"招募占一槽。")
j.confirm("进入下一回合遭遇",dict(phase="Block",turn=2,hand=5,deck=2,discard=3,influence=0,movement=0,greenCrystal=0,whiteCrystal=0,cardSlot0="basic_card_021",cardSlot2="basic_card_016",requiredBlock0=6,background="village"),"只补三张，原卡实例和已付魔力跨回合保留。")
j.click("Units/Unit_guard","选择部队",dict(selectedUnit="guard",ready=1),"选择不耗竭。")
j.confirm("没目标部队不能使用",dict(accepted=0,ready=1,block0=0),"未指定敌人不消耗。")
j.target("enemy:0").confirm("守卫格挡三",dict(block0=3,ready=0),"确认才耗竭部队。")
j.click("Units/Unit_guard","耗竭单位不能重复",dict(accepted=0,block0=3,ready=0),"不能重复用能力。")
j.card(5,16,5).target("enemy:0")
enhance(j,"blue",5).confirm("决心强化与部队合计八",dict(block0=8,blueCrystal=0,hand=4,**{"ui:resource":"8 / 6"}),"3加5抵挡迅捷需求6。")
j.confirm("成功格挡没有伤牌",dict(phase="Attack",wounds=0,hand=4),"迅捷不把攻击伤害翻倍。")
j.card(3,21,4).target("enemy:0")
enhance(j,"red",4).confirm("狂怒强化四",dict(attack0=4,redCrystal=0,hand=3),"狂怒4还不足灰狼护甲5。")
j.card(4,21,3).confirm("狂怒基础合计六",dict(attack0=6,hand=2,**{"ui:resource":"6 / 5"}),"两张原卡4加2。")
j.confirm("完整旅程获胜",dict(completed=1,fame=4,wounds=0,turn=2,units=1,ready=0,actionCardTotal=10,redCrystal=0,blueCrystal=0,greenCrystal=0,whiteCrystal=0),"四种原卡强化各消费对应颜色，奖励只结算一次。")
j.write()

bad=Scenario("Combat")
bad.card(0,16,999)
bad.write("adventure_expected_failure.json","ExpectedFailure")
print(json.dumps({s.name:len(s.steps) for s in [c,e,r,j]}))
