# cycle02 v2 预声明补充（最终 after_v3 采集前）

v1 spec、说明和历史诊断保持原样。v2增加`Inspector/ResolutionViewport/ResolutionFullText`及`Inspector/Btn_ResolutionRecall/Label`，在source_consumed要求两者。实际展开回看时guidance优先完整详情，避免以后台Details冒充当前说明。凝结的必要文本包括同时打出另一行动卡、免费强力效果以及“移动力、影响力、格挡或任何类型的攻击”的完整加成范围。

主比率是114个有限角色的组件最低数、指定关键词、完整匹配生成字符、正renderer alpha及图片几何对齐的联合检查。它不是全部游戏信息的完整率、可见率、人类结果或rubric分数。真实可见字形覆盖保持null。ResolutionFullText生成完整，仍不能证明全部文字处在滚动viewport裁剪区域内；需要实际滚动首尾/中间截图和独立读图，或可靠像素遮挡/裁剪证据。

对被拒绝的cycle02 after_v2只做负例诊断：112/114。source_consumed回看按钮7个预期字形、0个生成；hand_page_two反馈22个预期非空白字符、11个匹配生成且TMP overflow。凝结详情87/87匹配生成仅是生成完整性证据，不能消除上述clip caveat。

```powershell
python -X utf8 tools/RsiGame/measure_required_ui_information.py --captures AutomationOutputs/RsiGame/evaluation_iterations/20261023_cycle02/after_v3/key_phases --spec AutomationConfigs/RsiGame/evaluation/required_ui_information_cycle02_v2.json --out AutomationOutputs/RsiGame/evaluation_iterations/20261023_cycle02/measurement_preflight/after_v3_required_info_v2.json
```
