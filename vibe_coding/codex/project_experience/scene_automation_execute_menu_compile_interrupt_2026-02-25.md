# SceneAutomation execute_menu_item compile interruption (2026-02-25)

## Symptom
ElementCardsShowcase automation repeatedly updated only 000_Scene Start and then aborted with "play mode ended early" before writing the full report.

## Key observation
Editor.log showed: "[ScriptCompilation] Requested script compilation because: Assetdatabase observed changes in script compilation related files" during play mode, followed by domain reload and OnPlayModeStateChanged early-exit handling.

## Practical guidance
1. Avoid extra execute_menu_item calls once automation enters play mode.
2. Do not issue refresh-like commands during automation playback.
3. Poll output artifacts from shell while automation is running; avoid MCP calls unless recovery is required.
4. If MCP returns no_unity_session/hint=retry, rebind latest instance and run refresh_unity(wait_for_ready=true) before retry.
5. If repeated failures persist, restart MCP server + worker via orchestrated scripts.

## Code hardening made in this round
- Removed AssetDatabase.Refresh() in SceneAutomationQuickMenus config writers.
- Added synchronous ReadPixels fallback in SceneAutomationOrchestrator when camera capture bytes are unavailable.
