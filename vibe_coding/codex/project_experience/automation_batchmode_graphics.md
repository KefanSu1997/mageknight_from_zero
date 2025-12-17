# 自动化截图需保留图形设备的排查记录

## 背景

Agent A 的自动化流程需要在命令行下完成按钮点击与截图。早期在 `-batchmode -nographics -quit` 组合下运行 `SceneAutomationCommand.RunFromCommandLine`，Unity 会在场景载入后立即退出或卡在首个步骤，日志只剩 `PendingRequest stored` 等入口信息，截图与报告均不会生成。

## 排查过程

1. 给 `SceneAutomationOrchestrator` 增补生命周期日志、`PreExecutionSanityChecks`、协程异常捕获和按钮树 dump，确认 Play Mode 成功启动且按钮能被找到。
2. 在 `WaitForButton` 内持续打印可用按钮，并在 `ExecuteStep` 外围捕获所有异常，排除协程提前终止的可能。
3. 观察 `automation_run.log`，在 `-nographics` 环境下渲染设备固定为 `GraphicsDeviceType.Null`，随后 Unity 会输出 `No graphic device is available to initialize the view.` 并直接退出，Automation 报告也不会生成。
4. 改为仅使用 `-batchmode`（保留图形设备），命令：

   ```powershell
   "D:\Unity\Editor\2023.2.20f1c1\Editor\Unity.exe" \
     -batchmode -projectPath "D:\study and work\code\unity-MK-test\MageKnight_from_zero" \
     -executeMethod MageKnight.SceneAutomation.Editor.SceneAutomationCommand.RunFromCommandLine \
     --scene-automation-config "D:\study and work\code\unity-MK-test\MageKnight_from_zero\AutomationConfigs\agent_a_scene.json" \
     -logFile "D:\study and work\code\unity-MK-test\MageKnight_from_zero\AutomationOutputs\AgentATest\automation_run_batch.log"
   ```

   17 秒内即可完成全部 3 个步骤，生成 `report.json` 与三张截图。

## 结论与建议

- **截图流程必须拥有可用的图形设备。** 需要关闭 `-nographics`，否则 `SceneAutomationOrchestrator` 会停留在首个步骤前后，命令提前返回。
- 批处理环境下可以继续使用 `-batchmode` + `-quit`，前提是依赖 `SceneAutomationCommand` 在自动化完成后调用 `EditorApplication.Exit`。如需在 CI 中运行截图，请确保执行节点具备 GPU 或启用 WARP 渲染。
- 调试日志保留在 `AutomationOutputs/AgentATest/automation_run_batch.log`，可快速定位按钮查找、截图与报告生成的时间线。
- **多 worktree 协作提示：** 在 `wk-agentA` 等工作区运行时，先同步最新脚本并将 `AutomationConfigs/agent_a_scene.json` 的 `screenshotsDirectory`、`reportPath` 指向工作区私有目录（示例：`AutomationOutputs/AgentATest_wk/...`），再执行相同的 `Unity.exe -batchmode` 命令（可移除 `-nographics`）；这样各 agent 的日志、报告、截图都保存在各自 worktree，避免相互覆盖。

## 2025-11-29 更新：防挂死兜底

- `SceneAutomationCommand.RunWithConfig` 增加图形设备校验：在 batchmode 且 `GraphicsDeviceType.Null`（通常是 `-nographics`）时直接报错并以退出码 1 结束，避免 Play 模式长时间挂起。
- 若必须在无 GPU 的节点执行，请改用支持软件渲染/虚拟 GPU 的环境；当前自动化不再尝试在 Null 设备下进入 Play 模式。
