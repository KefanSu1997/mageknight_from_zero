# Unity MCP 复现记录：ElementCards Showcase Automation 卡住（2026-01-27）

## 目标

按用户提供的最小复现步骤，验证在触发 Scene Automation 后是否出现 `ping not answered` / MCP 会话不可用。

## 操作步骤与结果

1. set_active_instance -> 成功
   - instance: MageKnight_from_zero@6dd7608139862331
2. execute_menu_item -> 成功返回（仅表示已尝试触发）
   - menu: Tools/Scene Automation/Run ElementCards Showcase Automation
3. read_console（types=["all"], count=200, include_stacktrace=false）
   - 第一次：返回 hint=retry，原因：no_unity_session
4. 按“重试前先 refresh_unity(wait_for_ready=true)”的标准流程尝试
   - refresh_unity 工具返回：Unknown or unsupported command type: refresh_unity
   - 说明：当前 MCP 服务端未实现/未暴露该命令
5. 采用指数退避 + 主动轮询替代方案（2s→4s→8s→16s）
   - 每轮：Start-Sleep -> manage_editor telemetry_ping/telemetry_status -> read_console
   - 结果：连续多轮 read_console 均报错：Unity session not ready for 'read_console' (ping not answered)

## 结论（当前状态）

- 已稳定复现：触发该自动化菜单后，MCP 端 read_console 持续返回 ping not answered。
- 标准建议的 refresh_unity(wait_for_ready=true) 在本环境不可用（命令未实现）。

## 下一步建议

- 需要用户在 Unity Editor 内确认：
  - 是否进入 Play Mode 且未卡在弹窗/编译中
  - Console 是否有红字/脚本编译失败
- 若允许，我可以继续：
  - 轮询更大窗口（count=1000, include_stacktrace=true, types=["error","warning","log"]）
  - 尝试其它只读 MCP 命令判断会话健康度（如 manage_scene.get_active / get_hierarchy）
