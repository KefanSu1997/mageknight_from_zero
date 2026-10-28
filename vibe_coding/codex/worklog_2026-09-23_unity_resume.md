# Unity恢复与合并定向验收（2026-09-23）

- 用户02:17明确允许立即使用Unity，覆盖此前游戏期间的时间限制。只通过Hub打开6000.6.0f1；一次性提醒已停用，避免重复启动。
- 39个方法/类过滤项展开143例。首跑142通过、1失败，证据：`AutomationOutputs/RulesState/20260923_021816_212ab687`。失败为大师夹具把原愤怒基础攻击选项写成`base`，正式选项是`attack`；仅改夹具，并仅补失败方法，`20260923_021930_708b3d12`通过1/1。保留首轮失败证据，不重跑整批。
- Unity启动暴露程序集边界：`PveDelphanaMastersTests.cs:157`直接设置`ActionContext.OfferedUnitUses`，其setter为internal，CLI共编译可访问但Unity测试程序集不可访问。改为调用正式`BattleSummonsEffect(false).Execute`建立许可，不扩大生产接口权限。只补受影响的招募/供应许可用例，`AutomationOutputs/RulesState/20260923_022411_29b6250b`通过1/1。
- Unity进入Safe Mode后执行一次Assets/Refresh，编译与导入完成。MCP尚未初始化时连接拒绝，未快速重试；就绪后按标准流程连续两次读取Console error，均0条，`tmp/pve_20260923_console_compile.json`。场景完成后再次双0，`tmp/pve_20260923_console_after_selection.json`。
- 直接选择场景报告：`AutomationOutputs/PveBattle/20260923_022607_919c19f0/report.json`，28步/86断言全部通过。实际检查5张关键截图：第一页18项、末页7项、13种敌群单页、确认后切换农民与暴徒原配置、农民实际格挡2且耗竭；无布局截断或遮挡。保留原卡、状态前后值与鼠标实际命中证据。
- 此次后台证据不增加部队场景通过数。当前19/30部队、10/25神器、17/70技能有限场景基线保持；连续控制器及新增单位合法构筑入口仍待完成。
