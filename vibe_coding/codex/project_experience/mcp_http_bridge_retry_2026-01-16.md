MCP HTTP bridge retry: auto-start can fail if server is down

Summary
- MCPForUnity HTTP transport does not auto-retry if StartAsync fails while the server is offline.
- Adding a lightweight EditorApplication.update retry loop in a project-side auto-start script allows the bridge to reconnect when the server comes up.

What I changed
- Added a retry loop in Assets/Editor/McpBridgeAutoStart.cs that periodically calls Bridge.StartAsync until it succeeds, then unregisters the update callback.

Why this matters
- Prevents manual intervention when the MCP server is restarted or temporarily unavailable.

Notes
- This relies on a domain reload to compile the editor script; if auto-refresh is disabled, a manual refresh is still needed.
