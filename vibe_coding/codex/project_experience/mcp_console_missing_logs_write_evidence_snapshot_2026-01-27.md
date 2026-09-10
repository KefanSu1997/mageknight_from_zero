Issue: MCP read_console returned 0 entries for log/warning even though Unity Editor.log had the messages.
Impact: Evidence logs for ElementCards could not be captured via MCP console.
Resolution: Add an editor debug menu that uses Unity APIs to read scene state (sprites, sibling indices, active states, button wiring) and write an ASCII evidence snapshot file. Trigger the menu via MCP execute_menu_item, then read the file from disk for audit.
Notes:
- Keep output ASCII to avoid CLI encoding crashes.
- Snapshot file path can be under multi-agent-workspace/runs/<task>/ for review.
