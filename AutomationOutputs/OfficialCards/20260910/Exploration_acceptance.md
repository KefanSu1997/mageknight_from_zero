# 穿越翡翠山谷：逐步验收

22 次操作，76 条断言，全部通过。

数值来自本次 Unity 运行记录；`ui:` 字段来自实际界面的 TMP 文本或 Sprite 名称。
`操作被接受=0` 表示按规则拒绝操作；只要符合独立预期，这一步验收仍通过。

[原始 JSON 报告（含完整前后状态和命中坐标）](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/report.json>)

[初始截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/000_Scene Start.png>)

## 01 · 点选_site_forest

操作：`Canvas/UIRoot/Stage/Targets/Site_forest`；命中 `Site_forest`，屏幕坐标 (754, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | site:forest | site:forest | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/001_点选_site_forest.png>)

## 02 · 零移动不能进入

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：先选择移动牌并确认打出，再支付目标费用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 移动力 | 0 | 0 | 0 | 通过 |
| 点击后 | position | camp | camp | camp | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/002_零移动不能进入.png>)

## 03 · 选择_basic_card_000_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_000 | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 界面·ui:printedEffect | 森林 · 进入费用3 | 基础：移动力2。 | 基础：移动力2。 | 通过 |
| 点击后 | selectedValue | 0 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/003_选择_basic_card_000_0.png>)

## 04 · 点选_site_forest

操作：`Canvas/UIRoot/Stage/Targets/Site_forest`；命中 `Site_forest`，屏幕坐标 (754, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | site:forest | site:forest | site:forest | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/004_点选_site_forest.png>)

## 05 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | greenCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：移动力2。 | 强化：移动力4。 | 强化：移动力4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/005_选择原卡强化.png>)

## 06 · 行进绿色强化4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：移动力4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 0 | 4 | 4 | 通过 |
| 点击后 | greenCrystal | 1 | 0 | 0 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/006_行进绿色强化4.png>)

## 07 · 白天森林费用3

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：按昼夜与底层地形支付费用；进入交涉阶段后不再移动。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 4 | 1 | 1 | 通过 |
| 点击后 | position | camp | forest | forest | 通过 |
| 点击后 | 界面·ui:resource | 4 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/007_白天森林费用3.png>)

## 08 · 选择_basic_card_014_1

操作：`Canvas/UIRoot/Hand/Card_1`；命中 `Card_1`，屏幕坐标 (363, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_014 | basic_card_014 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:1 | basic_card_014 | basic_card_014 | basic_card_014 | 通过 |
| 点击后 | 界面·ui:printedEffect | 点击地图地点；需要移动力时，先选择移动牌。 | 基础：移动力2。 | 基础：移动力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/008_选择_basic_card_014_1.png>)

## 09 · 点选_site_frontier

操作：`Canvas/UIRoot/Stage/Targets/Site_frontier`；命中 `Site_frontier`，屏幕坐标 (964, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | site:frontier | site:frontier | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/009_点选_site_frontier.png>)

## 10 · 耐力基础移动2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：移动力2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 1 | 3 | 3 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | blueCrystal | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/010_耐力基础移动2.png>)

## 11 · 探索只揭示不移动

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：探索费用2；进入揭示的地形还需另外支付地形费用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 3 | 1 | 1 | 通过 |
| 点击后 | position | forest | forest | forest | 通过 |
| 点击后 | 村庄已揭示 | 6 | 7 | 7 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/011_探索只揭示不移动.png>)

## 12 · 进入平原费用不足

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：先选择移动牌并确认打出，再支付目标费用。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 移动力 | 1 | 1 | 1 | 通过 |
| 点击后 | position | forest | forest | forest | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/012_进入平原费用不足.png>)

## 13 · 选择_basic_card_000_2

操作：`Canvas/UIRoot/Hand/Card_2`；命中 `Card_2`，屏幕坐标 (478, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 3 | 3 | 3 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:2 | basic_card_000 | basic_card_000 | basic_card_000 | 通过 |
| 点击后 | 界面·ui:printedEffect | 平原 · 进入费用2 | 基础：移动力2。 | 基础：移动力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/013_选择_basic_card_000_2.png>)

## 14 · 另一张行进基础2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：移动力2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 1 | 3 | 3 | 通过 |
| 点击后 | 手牌 | 3 | 2 | 2 | 通过 |
| 点击后 | 界面·手牌 | 3 | 2 | 2 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/014_另一张行进基础2.png>)

## 15 · 进入揭示平原

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：按昼夜与底层地形支付费用；进入交涉阶段后不再移动。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 移动力 | 3 | 1 | 1 | 通过 |
| 点击后 | position | forest | frontier | frontier | 通过 |
| 点击后 | actionCardTotal | 10 | 10 | 10 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/015_进入揭示平原.png>)

## 16 · 重新开始

操作：`Canvas/UIRoot/Btn_Reset`；命中 `Btn_Reset`，屏幕坐标 (1762.5, 986.5)。

规则反馈：卡牌和部队提供行动；确认后才消耗资源。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 手牌 | 2 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 2 | 5 | 5 | 通过 |
| 点击后 | 已打出 | 3 | 0 | 0 | 通过 |
| 点击后 | 伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 界面·伤口 | 0 | 0 | 0 | 通过 |
| 点击后 | 红色水晶 | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·红色水晶 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/016_重新开始.png>)

## 17 · 点选_site_lake

操作：`Canvas/UIRoot/Stage/Targets/Site_lake`；命中 `Site_lake`，屏幕坐标 (334, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | site:lake | site:lake | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/017_点选_site_lake.png>)

## 18 · 湖泊不可通行

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | position | camp | camp | camp | 通过 |
| 点击后 | 移动力 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/018_湖泊不可通行.png>)

## 19 · 点选_site_mountain

操作：`Canvas/UIRoot/Stage/Targets/Site_mountain`；命中 `Site_mountain`，屏幕坐标 (439, 748)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | site:lake | site:mountain | site:mountain | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/019_点选_site_mountain.png>)

## 20 · 高山不可通行

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | position | camp | camp | camp | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/020_高山不可通行.png>)

## 21 · 点选_site_frontier

操作：`Canvas/UIRoot/Stage/Targets/Site_frontier`；命中 `Site_frontier`，屏幕坐标 (964, 623)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | site:mountain | site:frontier | site:frontier | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/021_点选_site_frontier.png>)

## 22 · 非相邻不能探索

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 村庄已揭示 | 6 | 6 | 6 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Exploration/captures/Part1_ExplorationRules_20260910_104351/022_非相邻不能探索.png>)
