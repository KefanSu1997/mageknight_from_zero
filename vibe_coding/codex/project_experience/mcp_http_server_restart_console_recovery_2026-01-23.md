# MCP HTTP server restart to recover tool timeouts (2026-01-23)

Issue
- Unity MCP tools timed out (read_console/manage_scene/editor_state) with empty error messages.
- Server log at ~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log showed "Tool ... failed:" with no detail (timeout).

Fix attempt
- Restarted the mcp-for-unity HTTP server on port 60022.
  - Stop listener PID for 60022.
  - Start: mcp-for-unity.exe --transport http --http-host localhost --http-port 60022
- Reconnect Unity (verify unity://instances returns session).
- After restart, read_console succeeded once and returned a stale MCP connection error; clearing console then returned 0 errors.

Notes
- After Assets/Refresh, Unity plugin may disconnect/reconnect; tool calls can time out until the plugin is fully registered.
- Server log is useful for confirming timeouts and session disconnects.
