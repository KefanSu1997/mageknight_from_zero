# MCP bridge auto-start on reload (2026-02-27)

## Problem
During SceneAutomation work, Unity MCP frequently dropped into no_unity_session / hint=retry windows after domain reload.
Manual bridge restarts were high-friction and easy to race with playmode transitions.

## Action
Added an InitializeOnLoad safety hook in `Assets/Editor/McpTransportPrefs.cs`:
- keep HTTP transport prefs pinned (`UseHttpTransport`, `ResumeHttpAfterReload`, `HttpUrl`)
- schedule a delayed `TryStartHttpBridgeOnce()` call that invokes MCP bridge `StartAsync` via reflection

## Why it helps
When scripts reload, Unity re-applies bridge prefs and proactively starts the bridge, reducing dead windows where automation and console checks cannot run.

## Guardrails
- Invocation is delayed and wrapped in try/catch.
- Missing MCP assemblies/services are treated as no-op.
- No package install/uninstall required.

## Follow-up
If session instability remains, prefer explicit artifact-write validation over trusting execute_menu_item success alone.
