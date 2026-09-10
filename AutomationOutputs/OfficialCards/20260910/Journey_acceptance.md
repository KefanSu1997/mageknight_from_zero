# 从营地到并肩作战：逐步验收

31 次操作，141 条断言，全部通过。

数值来自本次 Unity 运行记录；`ui:` 字段来自实际界面的 TMP 文本或 Sprite 名称。
`操作被接受=0` 表示按规则拒绝操作；只要符合独立预期，这一步验收仍通过。

[原始 JSON 报告（含完整前后状态和命中坐标）](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/report.json>)

[初始截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/000_Scene Start.png>)

## 01 · 选择_basic_card_000_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_000 | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 界面·ui:printedEffect | 点击地图地点；需要移动力时，先选择移动牌。 | 基础：移动力2。 | 基础：移动力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/001_选择_basic_card_000_0.png>)

## 02 · 点选_site_village

操作：`Canvas/UIRoot/Stage/Targets/Site_village`；命中 `Site_village`，屏幕坐标 (754, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | site:village | site:village | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/002_点选_site_village.png>)

## 03 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | greenCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：移动力2。 | 强化：移动力4。 | 强化：移动力4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/003_选择原卡强化.png>)

## 04 · 行进强化准备探索

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：移动力4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 0 | 4 | 4 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | greenCrystal | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/004_行进强化准备探索.png>)

## 05 · 探索村庄扣二

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：探索费用2；进入揭示的地形还需另外支付地形费用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 4 | 2 | 2 | 通过 |
| 点击后 | position | camp | camp | camp | 通过 |
| 点击后 | 村庄已揭示 | 1 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/005_探索村庄扣二.png>)

## 06 · 进入村庄再扣二

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：按昼夜与底层地形支付费用；进入交涉阶段后不再移动。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 2 | 0 | 0 | 通过 |
| 点击后 | position | camp | village | village | 通过 |
| 点击后 | phase | Travel | Interaction | Interaction | 通过 |
| 点击后 | background | valley | village | village | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/006_进入村庄再扣二.png>)

## 07 · 选择_basic_card_008_1

操作：`Canvas/UIRoot/Hand/Card_1`；命中 `Card_1`，屏幕坐标 (363, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:1 | basic_card_008 | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 界面·ui:printedEffect | 点击想招募的伙伴；选择交涉牌，为招募积累影响力。 | 基础：影响力2。 | 基础：影响力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/007_选择_basic_card_008_1.png>)

## 08 · 点选_offer_guard

操作：`Canvas/UIRoot/Stage/Targets/Offer_guard`；命中 `Offer_guard`，屏幕坐标 (378, 664.86)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | offer:guard | offer:guard | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/008_点选_offer_guard.png>)

## 09 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | whiteCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：影响力2。 | 强化：影响力4。 | 强化：影响力4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/009_选择原卡强化.png>)

## 10 · 承诺强化4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：影响力4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 0 | 4 | 4 | 通过 |
| 点击后 | whiteCrystal | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/010_承诺强化4.png>)

## 11 · 选择_basic_card_008_2

操作：`Canvas/UIRoot/Hand/Card_2`；命中 `Card_2`，屏幕坐标 (478, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:2 | basic_card_008 | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 界面·ui:printedEffect | 招募费用 5 | 基础：影响力2。 | 基础：影响力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/011_选择_basic_card_008_2.png>)

## 12 · 承诺基础补二

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：影响力2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 4 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 界面·手牌 | 3 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/012_承诺基础补二.png>)

## 13 · 支付五招募守卫

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：支付牌面费用，占用一个指挥槽；单位就绪加入。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 6 | 1 | 1 | 通过 |
| 点击后 | 部队 | 0 | 1 | 1 | 通过 |
| 点击后 | 就绪部队 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/013_支付五招募守卫.png>)

## 14 · 进入下一回合遭遇

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：已打出牌进入弃牌堆；补牌到5，临时移动力/影响力/魔力清零，水晶与部队保留。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | phase | Interaction | Block | Block | 通过 |
| 点击后 | 回合 | 1 | 2 | 2 | 通过 |
| 点击后 | 手牌 | 2 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 2 | 5 | 5 | 通过 |
| 点击后 | 牌库 | 5 | 2 | 2 | 通过 |
| 点击后 | 弃牌 | 0 | 3 | 3 | 通过 |
| 点击后 | 影响力 | 1 | 0 | 0 | 通过 |
| 点击后 | 移动力 | 0 | 0 | 0 | 通过 |
| 点击后 | greenCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | whiteCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | cardSlot0 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | cardSlot2 | — | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | requiredBlock0 | — | 6 | 6 | 通过 |
| 点击后 | background | village | village | village | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/014_进入下一回合遭遇.png>)

## 15 · 选择部队

操作：`Canvas/UIRoot/Units/Unit_guard`；命中 `Unit_guard`，屏幕坐标 (802, 374)。

规则反馈：选择不消耗单位；确认后单位变为已用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedUnit |  | guard | guard | 通过 |
| 点击后 | 就绪部队 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/015_选择部队.png>)

## 16 · 没目标部队不能使用

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：选择不消耗单位；确认后单位变为已用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 就绪部队 | 1 | 1 | 1 | 通过 |
| 点击后 | block0 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/016_没目标部队不能使用.png>)

## 17 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/017_点选_enemy_0.png>)

## 18 · 守卫格挡三

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：部队能力来自单位定义；已用状态保留到整轮结束。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | block0 | 0 | 3 | 3 | 通过 |
| 点击后 | 就绪部队 | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/018_守卫格挡三.png>)

## 19 · 耗竭单位不能重复

操作：`Canvas/UIRoot/Units/Unit_guard`；命中 `Unit_guard`，屏幕坐标 (802, 374)。

规则反馈：部队能力来自单位定义；已用状态保留到整轮结束。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | block0 | 3 | 3 | 3 | 通过 |
| 点击后 | 就绪部队 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/019_耗竭单位不能重复.png>)

## 20 · 选择_basic_card_016_5

操作：`Canvas/UIRoot/Hand/Card_5`；命中 `Card_5`，屏幕坐标 (708, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:5 | basic_card_016 | basic_card_016 | basic_card_016 | 通过 |
| 点击后 | 界面·ui:printedEffect | 迅捷灰狼 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/020_选择_basic_card_016_5.png>)

## 21 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | enemy:0 | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/021_点选_enemy_0.png>)

## 22 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 5 | 5 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：格挡5。 | 强化：格挡5。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/022_选择原卡强化.png>)

## 23 · 决心强化与部队合计八

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：格挡5。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | block0 | 3 | 8 | 8 | 通过 |
| 点击后 | blueCrystal | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·ui:resource | 3 / 6 | 8 / 6 | 8 / 6 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/023_决心强化与部队合计八.png>)

## 24 · 成功格挡没有伤牌

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | phase | Block | Attack | Attack | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/024_成功格挡没有伤牌.png>)

## 25 · 选择_basic_card_021_3

操作：`Canvas/UIRoot/Hand/Card_3`；命中 `Card_3`，屏幕坐标 (363, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:3 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 选择攻击牌并指定敌人。攻击达到护甲时可以击败目标。 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/025_选择_basic_card_021_3.png>)

## 26 · 点选_enemy_0

操作：`Canvas/UIRoot/Stage/Targets/Enemy_0`；命中 `Enemy_0`，屏幕坐标 (1011.6, 656.02)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | enemy:0 | enemy:0 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/026_点选_enemy_0.png>)

## 27 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：攻击或格挡2。 | 强化：攻击4。 | 强化：攻击4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/027_选择原卡强化.png>)

## 28 · 狂怒强化四

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 0 | 4 | 4 | 通过 |
| 点击后 | 红色水晶 | 1 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/028_狂怒强化四.png>)

## 29 · 选择_basic_card_021_4

操作：`Canvas/UIRoot/Hand/Card_4`；命中 `Card_4`，屏幕坐标 (478, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:4 | basic_card_021 | basic_card_021 | basic_card_021 | 通过 |
| 点击后 | 界面·ui:printedEffect | 迅捷灰狼 | 基础：攻击或格挡2。 | 基础：攻击或格挡2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/029_选择_basic_card_021_4.png>)

## 30 · 狂怒基础合计六

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击或格挡2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | attack0 | 4 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 界面·手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 界面·ui:resource | 4 / 5 | 6 / 5 | 6 / 5 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/030_狂怒基础合计六.png>)

## 31 · 完整旅程获胜

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 任务完成 | 0 | 1 | 1 | 通过 |
| 点击后 | 名望 | 0 | 4 | 4 | 通过 |
| 点击后 | 界面·名望 | 0 | 4 | 4 | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 回合 | 2 | 2 | 2 | 通过 |
| 点击后 | 部队 | 1 | 1 | 1 | 通过 |
| 点击后 | 就绪部队 | 0 | 0 | 0 | 通过 |
| 点击后 | actionCardTotal | 10 | 10 | 10 | 通过 |
| 点击后 | 红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·红色水晶 | 0 | 0 | 0 | 通过 |
| 点击后 | blueCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | greenCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | whiteCrystal | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Journey/captures/Part1_JourneyRules_20260910_104419/031_完整旅程获胜.png>)
