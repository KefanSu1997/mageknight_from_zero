# 林间交锋与遭遇切换：逐步验收

45 次操作，202 条断言，全部通过。

数值来自本次 Unity 运行记录；`ui:` 字段来自实际界面的 TMP 文本或 Sprite 名称。
`操作被接受=0` 表示按规则拒绝操作；只要符合独立预期，这一步验收仍通过。

[原始 JSON 报告（含完整前后状态和命中坐标）](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/report.json>)

[初始截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/000_Scene Start.png>)

## 01 · 选择_basic_card_016_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_016 | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择格挡牌或就绪部队，再点选要抵挡的敌人。准备好后结束格挡。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |
| 点击后 | selectedValue | 0 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/001_选择_basic_card_016_0.png>)

## 02 · 缺目标不得扣牌

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：先选卡牌，再选地点、部队或敌人；未确认不消耗资源。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 已打出 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/002_缺目标不得扣牌.png>)

## 03 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/003_点选_enemy_0.png>)

## 04 · 决心基础格挡2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击或格挡2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | block0 | 0 | 2 | 2 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:resource | 0 / 4 | 2 / 4 | 2 / 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/004_决心基础格挡2.png>)

## 05 · 格挡不足承受完整攻击

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | phase | Block | Attack | Attack | 通过 |
| 点击后 | 伤口 | 0 | 2 | 2 | 通过 |
| 点击后 | 界面·伤口 | 0 | 2 | 2 | 通过 |
| 点击后 | 手牌 | 4 | 6 | 6 | 通过 |
| 点击后 | 界面·手牌 | 4 | 6 | 6 | 通过 |
| 点击后 | woundCards | 0 | 2 | 2 | 通过 |
| 点击后 | actionCardTotal | 10 | 10 | 10 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/005_格挡不足承受完整攻击.png>)

## 06 · 伤牌不能打出

操作：`Canvas/UIRoot/Hand/Card_10`；命中 `Card_10`，屏幕坐标 (1041, 199)。

规则反馈：只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 6 | 6 | 6 | 通过 |
| 点击后 | 界面·手牌 | 6 | 6 | 6 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/006_伤牌不能打出.png>)

## 07 · 选择_basic_card_021_2

操作：`Canvas/UIRoot/Hand/Card_2`；命中 `Card_2`，屏幕坐标 (375, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 6 | 6 | 6 | 通过 |
| 点击后 | 界面·手牌 | 6 | 6 | 6 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:2 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择攻击牌并指定敌人。攻击达到护甲时可以击败目标。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |
| 点击后 | selectedValue | 0 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/007_选择_basic_card_021_2.png>)

## 08 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/008_点选_enemy_0.png>)

## 09 · 狂怒基础攻击2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击或格挡2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 0 | 2 | 2 | 通过 |
| 点击后 | 手牌 | 6 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 6 | 5 | 5 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/009_狂怒基础攻击2.png>)

## 10 · 选择_basic_card_021_3

操作：`Canvas/UIRoot/Hand/Card_3`；命中 `Card_3`，屏幕坐标 (478, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:3 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 荒野兽人 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/010_选择_basic_card_021_3.png>)

## 11 · 第二张狂怒基础合计4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击或格挡2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 2 | 4 | 4 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·ui:resource | 2 / 4 | 4 / 4 | 4 / 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/011_第二张狂怒基础合计4.png>)

## 12 · 结算且不重复受伤

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 任务完成 | 0 | 1 | 1 | 通过 |
| 点击后 | 名望 | 0 | 3 | 3 | 通过 |
| 点击后 | 界面·名望 | 0 | 3 | 3 | 通过 |
| 点击后 | 伤口 | 2 | 2 | 2 | 通过 |
| 点击后 | 界面·伤口 | 2 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/012_结算且不重复受伤.png>)

## 13 · 重新开始

操作：`Canvas/UIRoot/Btn_Reset`；命中 `Btn_Reset`，屏幕坐标 (1762.5, 986.5)。

规则反馈：卡牌和部队提供行动；确认后才消耗资源。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 手牌 | 4 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 4 | 5 | 5 | 通过 |
| 点击后 | 已打出 | 3 | 0 | 0 | 通过 |
| 点击后 | 伤口 | 2 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 2 | 0 | 0 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/013_重新开始.png>)

## 14 · 选择_basic_card_016_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_016 | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择格挡牌或就绪部队，再点选要抵挡的敌人。准备好后结束格挡。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/014_选择_basic_card_016_0.png>)

## 15 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/015_点选_enemy_0.png>)

## 16 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 5 | 5 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：格挡5。 | 强化：格挡5。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/016_选择原卡强化.png>)

## 17 · 蓝色强化决心格挡5

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：格挡5。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | block0 | 0 | 5 | 5 | 通过 |
| 点击后 | blueCrystal | 1 | 0 | 0 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/017_蓝色强化决心格挡5.png>)

## 18 · 成功格挡免伤

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | phase | Block | Attack | Attack | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/018_成功格挡免伤.png>)

## 19 · 选择_basic_card_021_2

操作：`Canvas/UIRoot/Hand/Card_2`；命中 `Card_2`，屏幕坐标 (593, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:2 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择攻击牌并指定敌人。攻击达到护甲时可以击败目标。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/019_选择_basic_card_021_2.png>)

## 20 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/020_点选_enemy_0.png>)

## 21 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：攻击4。 | 强化：攻击4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/021_选择原卡强化.png>)

## 22 · 红色强化狂怒攻击4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 0 | 4 | 4 | 通过 |
| 点击后 | 红色水晶 | 1 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 0 | 0 | 通过 |
| 点击后 | blueCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/022_红色强化狂怒攻击4.png>)

## 23 · 选择_basic_card_021_3

操作：`Canvas/UIRoot/Hand/Card_3`；命中 `Card_3`，屏幕坐标 (708, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:3 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 荒野兽人 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/023_选择_basic_card_021_3.png>)

## 24 · 再次声明强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | 红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/024_再次声明强化.png>)

## 25 · 缺红色魔力不降级打出

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | attack0 | 4 | 4 | 4 | 通过 |
| 点击后 | selectedCard | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/025_缺红色魔力不降级打出.png>)

## 26 · 取消未支付行动

操作：`Canvas/UIRoot/Inspector/Btn_Cancel`；命中 `Btn_Cancel`，屏幕坐标 (1494, 136.5)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard | basic_card_021 |  |  | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | attack0 | 4 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/026_取消未支付行动.png>)

## 27 · 原卡强化获胜

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 任务完成 | 0 | 1 | 1 | 通过 |
| 点击后 | 名望 | 0 | 3 | 3 | 通过 |
| 点击后 | 界面·名望 | 0 | 3 | 3 | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/027_原卡强化获胜.png>)

## 28 · 展开规则

操作：`Canvas/UIRoot/Inspector/Btn_Rules`；命中 `Btn_Rules`，屏幕坐标 (1636, 333.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 界面·ui:rulesVisible | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/028_展开规则.png>)

## 29 · 收起规则

操作：`Canvas/UIRoot/Inspector/Btn_Rules`；命中 `Btn_Rules`，屏幕坐标 (1636, 333.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 界面·ui:rulesVisible | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/029_收起规则.png>)

## 30 · 切换遭遇

操作：`Canvas/UIRoot/Btn_Menu`；命中 `Btn_Menu`，屏幕坐标 (1542.5, 986.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/030_切换遭遇.png>)

## 31 · 复用原卡迎战灰狼

操作：`Canvas/UIRoot/AdventureMenu/Btn_wolf_encounter`；命中 `Btn_wolf_encounter`，屏幕坐标 (1634, 539.5)。

规则反馈：卡牌和部队提供行动；确认后才消耗资源。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | scenario | combat | wolf_encounter | wolf_encounter | 通过 |
| 点击后 | requiredBlock0 | 4 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 3 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 3 | 5 | 5 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/031_复用原卡迎战灰狼.png>)

## 32 · 选择_basic_card_016_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_016 | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择格挡牌或就绪部队，再点选要抵挡的敌人。准备好后结束格挡。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/032_选择_basic_card_016_0.png>)

## 33 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/033_点选_enemy_0.png>)

## 34 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 5 | 5 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：格挡5。 | 强化：格挡5。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/034_选择原卡强化.png>)

## 35 · 决心强化5

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：格挡5。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | block0 | 0 | 5 | 5 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | blueCrystal | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/035_决心强化5.png>)

## 36 · 选择_basic_card_000_4

操作：`Canvas/UIRoot/Hand/Card_4`；命中 `Card_4`，屏幕坐标 (1053, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:4 | basic_card_000 | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | sideways | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/036_选择_basic_card_000_4.png>)

## 37 · 行进横置格挡1

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：任意非伤牌可以横置提供移动、影响、格挡或普通攻击1。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | block0 | 5 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | greenCrystal | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/037_行进横置格挡1.png>)

## 38 · 横置补足迅捷格挡

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | phase | Block | Attack | Attack | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/038_横置补足迅捷格挡.png>)

## 39 · 选择_basic_card_021_2

操作：`Canvas/UIRoot/Hand/Card_2`；命中 `Card_2`，屏幕坐标 (708, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:2 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择攻击牌并指定敌人。攻击达到护甲时可以击败目标。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/039_选择_basic_card_021_2.png>)

## 40 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/040_点选_enemy_0.png>)

## 41 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：攻击4。 | 强化：攻击4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/041_选择原卡强化.png>)

## 42 · 狂怒强化4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 0 | 4 | 4 | 通过 |
| 点击后 | 手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 界面·手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 红色水晶 | 1 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/042_狂怒强化4.png>)

## 43 · 选择_basic_card_016_1

操作：`Canvas/UIRoot/Hand/Card_1`；命中 `Card_1`，屏幕坐标 (593, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 手牌 | 2 | 2 | 2 | 通过 |
| 点击后 | 界面·手牌 | 2 | 2 | 2 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:1 | basic_card_016 | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 界面·ui:printedEffect | 迅捷灰狼 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |
| 点击后 | selectedValue | 0 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/043_选择_basic_card_016_1.png>)

## 44 · 决心基础改选攻击2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击或格挡2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 4 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 2 | 1 | 1 | 通过 |
| 点击后 | 界面·手牌 | 2 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/044_决心基础改选攻击2.png>)

## 45 · 原卡组合战胜灰狼

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 任务完成 | 0 | 1 | 1 | 通过 |
| 点击后 | 名望 | 0 | 4 | 4 | 通过 |
| 点击后 | 界面·名望 | 0 | 4 | 4 | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Combat/captures/Part1_CombatRules_20260910_104327/045_原卡组合战胜灰狼.png>)
