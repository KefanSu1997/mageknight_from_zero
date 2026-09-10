# 规则教学场景与数值验收，2026-09-09

## 一次操作需要三类证据

固定配置中的预期、运行时规则状态、实际界面显示不能混为一份数据。LessonSession只计算状态，不生成预期或passed；配置脚本不导入游戏实现。运行器读取按钮实际位置并射线命中，发送PointerDown/Up/Click，再分别获取逻辑值和TMP文本。报告必须记录点击前、预期、点击后、规则解释和截图。汇总器还要验证每条配置断言确实出现在报告里。

本轮正式结果为85次操作、431条断言、89张截图。故意把block预期改成999（实际1），报告应失败；这是避免验收器无条件通过的必要反证。

## 规则资料也可能错

旧摘要把森林/沙漠昼夜费用、山脉可通行、初始指挥槽写错；不能为了绿测试保留错误规则。核对WizKids官方MKUE昼夜板（Walkthrough p.6）、Rulebook p.3/7/8/9-10后修复实现和错误预期。

要重点测失败动作的资源守恒：移动不可邻接/不可通行、空地块牌堆、招募地点不匹配/槽满、重复使用单位/卡牌/奖励、多个颜色魔力支付失败。先验证所有付款条件再扣费，避免部分资源已消失。

## 长任务必须有失败出口

协程抛异常可能既没有report又不退出Play Mode。运行器应捕获RuntimeError/Exception/Assert，并在独立Update中设置看门狗超时；错误写报告、停止后续步骤、退出Play。四场景队列要验证report.startedAt属于本次执行，禁止读取旧success继续下一场。

MCP只负责菜单触发和Console读取，步骤执行、断言、截图与落盘由Unity内的执行器完成。本轮没有启动外部multi-agent loop，也没有调用Unity.exe/batchmode。

## uGUI自绘与画面问题

- 自定义MaskableGraphic要显式RequireComponent(CanvasRenderer)。缺失时会出现MissingComponentException并使验收协程卡住；修复后重新编译并重跑，不拿此前图当成功。
- 半透明面板使用Outline会重复覆盖边缘颜色，整块容易发黄。细边框使用四条独立Image；六边形用外边框图形+不透明内层，内层关闭raycast。
- TMP中文标题高度要留足；标题31像素容器曾截字，42像素后正常。地图节点不能碰到下方数值卡边界。
- 最终不仅看ScreenCapture文件，还用原生鼠标验证顶部导航与实质行动。在相同1920×1080 Game View下核对数值。

## 旧测试失败的处理

全量测试第一轮发现8个失败：拆分实现问题与测试前提错误，逐条处理而不删测试。探索测试先放起始地块；类型实例用IsInstanceOf；高级行动展示区始终3；初始指挥槽1；魔力源归还后不能残留占用。最终73项全部通过。

## Git合并状态

当前工程在本轮前已有4097项历史合并索引。即使git diff只列出少量本轮修改，直接git commit仍会把历史索引一起提交。先查MERGE_HEAD、索引树、HEAD并备份，禁止git add -A。保留本轮独立清单/patch，基线归并应作为独立版本控制工作处理。

## 重现

1. `python tools/build_rules_acceptance.py`
2. Unity菜单 `Tools/Mage Knight/Rules/Run All EditMode Tests`
3. Unity菜单 `Tools/Mage Knight/Rules/Verify Wrong Expectation Fails`
4. Unity菜单 `Tools/Mage Knight/Rules/Run All Four`
5. Console两次error扫描保存为`console_final_1.json`/`console_final_2.json`。
6. `python tools/summarize_rules_acceptance.py`

入口：`AutomationOutputs/RulesScenarios/20260909/验收总览.md`。
