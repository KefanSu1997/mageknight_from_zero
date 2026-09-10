# MCP console blocked + missing script compile error (2026-01-22)

Symptom:
- mcp__unityMCP__read_console and execute_menu_item returned tool errors.
- Editor.log showed MCP websocket receive-loop errors and a compile error:
  CS2001 missing Assets/Editor/ElementCardsShowcaseBinder.cs.

Fix:
- Create Assets/Editor/ElementCardsShowcaseBinder.cs as a minimal placeholder to satisfy the missing file reference.

Notes:
- When MCP console is unavailable, Editor.log can reveal compile errors.
- After fixing missing scripts, re-try MCP calls to confirm console errors == 0.
