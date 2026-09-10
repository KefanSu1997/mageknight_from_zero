# MCP recovery when refresh_unity is unsupported

Timestamp: 2026-01-27 23:51:21

## Symptom
- MCP commands previously returned ping not answered / timeouts.
- refresh_unity tool call returns: Unknown or unsupported command type: refresh_unity.

## What worked
1. Use read_console with types=["error","warning","log"] and a small count to confirm MCP responsiveness.
2. Use debug_request_context to discover active instance string.
3. Call set_active_instance with the discovered instance.
4. Trigger a refresh via execute_menu_item: Assets/Refresh (Assets/Refersh is invalid in this project).
5. Collect compile snapshots via read_console types=["error"], count=1000, include_stacktrace=true.

## Notes
- Console errors may include MCP/tooling errors; they do not necessarily indicate C# compile failures.
- Avoid relying on refresh_unity in this environment; it is not supported by the server.
