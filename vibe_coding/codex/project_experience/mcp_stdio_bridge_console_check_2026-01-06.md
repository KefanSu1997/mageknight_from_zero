# MCP stdio bridge: console check without WebSocket errors (2026-01-06)

## 现象
- Unity Console 出现 `MCP-FOR-UNITY: [WebSocket] Connection failed` error，导致 compile-clean 失败。
- MCP HTTP transport 无法建立时，`read_console` 工具无法使用。

## 处理方法
- 将 MCP 传输切换为 stdio（UseHttpTransport=false），避免 WebSocket 连接错误。
- 使用 stdio bridge（端口 6400）直接发送 MCP 命令：
  - `execute_menu_item` 触发 `Assets/Refresh`
  - `read_console` 执行 `action=clear` + 两次 `types=["error"]`
- 工具脚本：`tools/mcp_stdio_request.ps1`

## 要点
- stdio 协议使用 8 字节大端长度 + UTF-8 JSON payload。
- `read_console` 参数需走 `action=get`，`types` 传数组。
- 记录 compile_check 的时间与两次 error=0 结果。

## 结论
- stdio bridge 可绕开 WebSocket 连接失败问题，保证 compile-clean 流程稳定可执行。
