# MCP Session Recovery - HTTP Default Instance (2026-01-27)

Problem:
- unity-mcp tools returned "no_unity_session" after server restart.

Resolution:
- Start mcp-for-unity.exe with "--transport http --http-host localhost --http-port 60022 --default-instance MageKnight_from_zero".
- Verified WebSocket ESTABLISHED in netstat and instances resource shows connected.
- Use batch_execute to call manage_gameobject get_components when find_gameobjects/get_gameobject is unsupported.

Notes:
- Unity plugin may fail to reconnect without default instance; restarting server with default instance resolved.
