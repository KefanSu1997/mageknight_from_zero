# 村庄的盟友：逐步验收

16 次操作，58 条断言，全部通过。

数值来自本次 Unity 运行记录；`ui:` 字段来自实际界面的 TMP 文本或 Sprite 名称。
`操作被接受=0` 表示按规则拒绝操作；只要符合独立预期，这一步验收仍通过。

[原始 JSON 报告（含完整前后状态和命中坐标）](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/report.json>)

[初始截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/000_Scene Start.png>)

## 01 · 点选_offer_guard

操作：`Canvas/UIRoot/Stage/Targets/Offer_guard`；命中 `Offer_guard`，屏幕坐标 (378, 664.86)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | offer:guard | offer:guard | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/001_点选_offer_guard.png>)

## 02 · 影响力不足

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：招募失败不扣费；声望在交涉开始时只应用一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 影响力 | 0 | 0 | 0 | 通过 |
| 点击后 | 部队 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/002_影响力不足.png>)

## 03 · 选择_basic_card_008_0

操作：`Canvas/UIRoot/Hand/Card_0`；命中 `Card_0`，屏幕坐标 (248, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 5 | 5 | 5 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:0 | basic_card_008 | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 界面·ui:printedEffect | 招募费用 5 | 基础：影响力2。 | 基础：影响力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/003_选择_basic_card_008_0.png>)

## 04 · 点选_offer_guard

操作：`Canvas/UIRoot/Stage/Targets/Offer_guard`；命中 `Offer_guard`，屏幕坐标 (378, 664.86)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | offer:guard | offer:guard | offer:guard | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/004_点选_offer_guard.png>)

## 05 · 选择原卡强化

操作：`Canvas/UIRoot/Inspector/Btn_Enhance`；命中 `Btn_Enhance`，屏幕坐标 (1528.5, 392)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | enhanced | 0 | 1 | 1 | 通过 |
| 点击后 | selectedValue | 2 | 4 | 4 | 通过 |
| 点击后 | whiteCrystal | 1 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:printedEffect | 基础：影响力2。 | 强化：影响力4。 | 强化：影响力4。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/005_选择原卡强化.png>)

## 06 · 承诺白色强化4

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：影响力4。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 0 | 4 | 4 | 通过 |
| 点击后 | whiteCrystal | 1 | 0 | 0 | 通过 |
| 点击后 | 手牌 | 5 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 5 | 4 | 4 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/006_承诺白色强化4.png>)

## 07 · 四点还不足费用五

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：招募失败不扣费；声望在交涉开始时只应用一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 影响力 | 4 | 4 | 4 | 通过 |
| 点击后 | 部队 | 0 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/007_四点还不足费用五.png>)

## 08 · 选择_basic_card_008_1

操作：`Canvas/UIRoot/Hand/Card_1`；命中 `Card_1`，屏幕坐标 (363, 199)。

规则反馈：选择本身不消耗卡牌或魔力。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | selectedCard |  | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 界面·手牌 | 4 | 4 | 4 | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |
| 点击后 | 界面·ui:cardArt:1 | basic_card_008 | basic_card_008 | basic_card_008 | 通过 |
| 点击后 | 界面·ui:printedEffect | 招募费用 5 | 基础：影响力2。 | 基础：影响力2。 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/008_选择_basic_card_008_1.png>)

## 09 · 第二张承诺基础2

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：影响力2。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 4 | 6 | 6 | 通过 |
| 点击后 | 手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·手牌 | 4 | 3 | 3 | 通过 |
| 点击后 | 界面·ui:resource | 4 | 6 | 6 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/009_第二张承诺基础2.png>)

## 10 · 招募扣五余一

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：支付牌面费用，占用一个指挥槽；单位就绪加入。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 影响力 | 6 | 1 | 1 | 通过 |
| 点击后 | 部队 | 0 | 1 | 1 | 通过 |
| 点击后 | 就绪部队 | 0 | 1 | 1 | 通过 |
| 点击后 | 空闲指挥槽 | 1 | 0 | 0 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/010_招募扣五余一.png>)

## 11 · 点选_offer_guard

操作：`Canvas/UIRoot/Stage/Targets/Offer_guard`；命中 `Offer_guard`，屏幕坐标 (378, 664.86)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target |  | offer:guard | offer:guard | 通过 |
| 点击后 | 操作被接受 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/011_点选_offer_guard.png>)

## 12 · 不能重复招募

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 影响力 | 1 | 1 | 1 | 通过 |
| 点击后 | 部队 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/012_不能重复招募.png>)

## 13 · 点选_offer_monk

操作：`Canvas/UIRoot/Stage/Targets/Offer_monk`；命中 `Offer_monk`，屏幕坐标 (1117.2, 664.86)。

规则反馈：目标与行动条件会在确认时重新校验。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | offer:guard | offer:monk | offer:monk | 通过 |
| 点击后 | 操作被接受 | 0 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/013_点选_offer_monk.png>)

## 14 · 地点不符不得招募

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：招募失败不扣费；声望在交涉开始时只应用一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 操作被接受 | 1 | 0 | 0 | 通过 |
| 点击后 | 影响力 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/014_地点不符不得招募.png>)

## 15 · 取消目标

操作：`Canvas/UIRoot/Inspector/Btn_Cancel`；命中 `Btn_Cancel`，屏幕坐标 (1494, 136.5)。

规则反馈：招募失败不扣费；声望在交涉开始时只应用一次。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | target | offer:monk |  |  | 通过 |
| 点击后 | 影响力 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/015_取消目标.png>)

## 16 · 回合结束正常补牌

操作：`Canvas/UIRoot/Inspector/Btn_Confirm`；命中 `Btn_Confirm`，屏幕坐标 (1712, 118.5)。

规则反馈：已打出牌进入弃牌堆；补牌到5，临时移动力/影响力/魔力清零，水晶与部队保留。

| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| 点击后 | 手牌 | 3 | 5 | 5 | 通过 |
| 点击后 | 界面·手牌 | 3 | 5 | 5 | 通过 |
| 点击后 | 牌库 | 5 | 3 | 3 | 通过 |
| 点击后 | 弃牌 | 0 | 2 | 2 | 通过 |
| 点击后 | 回合 | 1 | 2 | 2 | 通过 |
| 点击后 | 影响力 | 1 | 0 | 0 | 通过 |
| 点击后 | whiteCrystal | 0 | 0 | 0 | 通过 |
| 点击后 | 部队 | 1 | 1 | 1 | 通过 |
| 点击后 | 就绪部队 | 1 | 1 | 1 | 通过 |

[本步骤截图](<D:/study_and_work/unity-MK-test/MageKnight_from_zero/AutomationOutputs/OfficialCards/20260910/Recruitment/captures/Part1_RecruitmentRules_20260910_104407/016_回合结束正常补牌.png>)
