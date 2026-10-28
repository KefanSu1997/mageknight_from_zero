# 付款批次与离线 UI 图谱

## 当前已核验基线

固定 1,009 Kind 组合、5-cap 4,083 样本不变。当前 3,430 实测、165 独立不兼容、488 未解析；758 组全实测、862 组全部解析。完整总体成本仍未就绪，未知不填零。

付款主批 `H:/MageKnight_RL_Backups/UiFluency_future/20261013_payment_remaining`：134 固定样本，124 成功，通过 248 个独立完整效果快照，结果 SHA `a41e192fe2b6264c63fb62fc6be37e729f3ef84d9e070021b9a0ee9a4eaede2c`，已入库。

修正已打出的 A 卡不能再作 B 付款目标，回退真实剩余非材料卡或未毁部队；按原固定失败 ID 重测 `20261013_payment_retry10`，4 成功/6 失败，通过 8 个独立效果快照，SHA `372ac666a31e180510bc8e06e628276842acd101331accbc0c30d25932d0b176`，已入库。

`20261013_payment_incompat6` 独立证实 4 条当前 UI 无入口：承伤阶段出牌 3 条、神器横置 1 条。证明 SHA `5b9627ce4a3efc6e72845e43e6beab1c54d12e7e200df99c1674237822605228` 已入库。余下 episode978/step26 原模式入口、episode5138/step21 宝典参数仍待修复，不判不兼容。

## 离线方案的实际进展和边界

用户指出成本可以离线规划，实景逐例复现过重。已实现 `tools/BattleRl/build_ui_cost_atlas.py`：从入库成本绑定的不可变结果 SHA 提取布局、公开 UI 状态、真实点击边和固定意图路径，离线重新计算点击数、视图切换、控件中心距离。不调用 Unity，不预测未观察路径，不回填 pending。

图谱 `H:/MageKnight_RL_Backups/UiFluency_future/20261013_offline_atlas1/atlas.sqlite`/`summary.json` 固定在当时 3,302 实测快照：26,359 点击、11,521 布局、27,975 个公开 UI 观察节点，三项几何成本与入库全部一致，status passed。这证明已知路径可离线重算，不证明泛化或自动规划所有未观察意图。

`render_ui_cost_atlas.py` 已生成 D 盘 `AutomationOutputs/UiFluency/full_cost_progress/offline_atlas.html`，23 类视图的代表几何示意（不是截图），保留控件原标签、路径、坐标和可用状态。SQL 每视图重复连接太慢，已改为单次遍历节点/边计数；只结束本任务已核对 PID116884 的只读 renderer 后重跑成功。主报告已链接图谱。HTML 数据检查通过，但 Browser Use 拒绝 file: URL，未绕过政策、未完成浏览器视觉核验。

后续应建立可校准的离线导航/路径模型，仅新布局、分支及差异补实景验证。不能把 23 类代表图当作所有参数布局，也不能把离线几何重算标为未知路径预测。延迟仍用实测软件代理，运行期机器负载影响另行解释。

## 运行状态

本次开始时权威进程/窗口显示 Unity Editor 和 MCP 服务已退出，不是仅观察超时。已通过 Unity Hub 原项目恢复：Editor PID206140，创建时间2026-10-13 22:11:22；本地既有 MCP server CLI 后台启动 PID114920，创建时间22:12:59，日志 `20261013_mcp_restore.stdout.log`/`.stderr.log` 在 H future 目录。Unity Tools/MCP/Start HTTP Bridge 通过 @oai/sky 恢复，实际两次 Console 零错误。

UnityMCP deferred namespace 当前移除，可用现有 `local_unity_mcp.py` 访问端口60022，resources/read 检查 editor/state。普通窗口按已读 Windows Computer Use 技能自行处理。

所有上述 measurement、proof、atlas、renderer 都已 terminal exit0 并完成相应入库。当前 owned pilot `H:/MageKnight_RL_Backups/UiFluency_future/20261013_payment_gate4/live` 由 proof keep-live 留在空闲状态；后续必须 fresh observe attach，无需重复菜单启动。没有活测量 worker。C# 新付款代码已编译并实景核验；本轮末只改 Python 和报告，没有新增待编译 C#。

Goal 保持 active，完整 4,083 条与最终优化分析尚未完成。不得动其他训练进程、权重或 RNG。
