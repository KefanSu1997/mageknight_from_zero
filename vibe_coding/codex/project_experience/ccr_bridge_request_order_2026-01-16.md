# CCR Bridge 请求文件排序坑（2026-01-16）

## 现象
- `.ccr_bridge` 下新增请求文件后，Unity 迟迟不生成 `ack_` / `trace_`。
- 明明已写入新请求，但 CCR 一直在“忽略”。

## 根因
- `CcrCompileBridge` 通过 `Directory.GetFiles` 后 **按文件名字符串排序**，只取排序后的最后一个文件。
- 如果新的请求文件名在字母序上 **小于** 之前的请求（例如 `request_elementcards...` vs `request_mcp...`），就会被忽略。

## 解决方案
- 发送新请求前删除旧的 `request_*.json`，确保最新文件被选中。
- 或给新请求 id 加前缀（如 `zz_`），保证按字母序排在最后。
