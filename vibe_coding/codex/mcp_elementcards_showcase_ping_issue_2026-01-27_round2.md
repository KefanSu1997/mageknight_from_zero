# Unity MCP 复现记录：ElementCards Showcase Automation（2026-01-27，二次复现）

## 按最小步骤复现（省略 git）
1. set_active_instance: MageKnight_from_zero@6dd7608139862331 -> success
2. execute_menu_item: Tools/Scene Automation/Run ElementCards Showcase Automation -> success(尝试触发)
3. read_console(count=200, types=["all"], include_stacktrace=false)
   - 首次：失败 -> ping not answered, hint=retry
4. 按规范尝试 refresh_unity(wait_for_ready=true)
   - MCP 返回：Unknown or unsupported command type: refresh_unity
5. 指数退避 + 主动轮询（2s 后 telemetry_ping/status）
   - read_console 恢复可用，但仅看到 MCP 自身错误：
     - MCP-FOR-UNITY: Unknown or unsupported command type: refresh_unity
6. 连续 read_console 5 次（每次间隔 1s）
   - 均返回同一条 MCP 自身错误日志

## 编译错误检查（标准流程）
- read_console(types=["error"], count=1000, include_stacktrace=true) -> 1 条
- 该 error 为 MCP 命令不支持（refresh_unity），不是 C# 编译错误

## 当前结论
- 触发自动化后，最初确实出现 ping not answered。
- 但在短退避后 Console 恢复响应，且未观察到新的 Unity 编译错误信息。
- 本环境的 MCP 服务端不支持 refresh_unity 命令。
