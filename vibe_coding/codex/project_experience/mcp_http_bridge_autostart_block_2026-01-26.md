# MCP Http Bridge 自动启动卡死与 DeckIdeal 自动截图自启处理

## 问题
- Unity 启动时卡在 EditorApplication.update，提示等待 Assembly-CSharp-Editor 执行完成。
- 追踪到 McpHttpBridgeAutoStart 在 Editor 主线程同步等待 Task（GetAwaiter().GetResult），导致阻塞。
- DeckIdealAutoCaptureOnce 在存在哨兵文件时会自动执行截图流程，启动即触发。

## 处理方案
- McpHttpBridgeAutoStart 改为 async/await 调用 Start/Verify/Stop，不在主线程阻塞等待结果。
- 增加 5s 超时保护，避免外部服务无响应导致长期挂起。
- DeckIdealAutoCaptureOnce 增加 EditorPrefs 开关，默认关闭自动截图，仅在显式开启时响应哨兵文件。

## 关键点
- EditorApplication.update 内不要做同步等待 Task 的操作。
- 哨兵文件机制建议配合开关，避免意外触发自动化流程。

## 相关文件
- `Assets/Editor/McpHttpBridgeAutoStart.cs`
- `Assets/Editor/DeckIdealAutoCaptureOnce.cs`
