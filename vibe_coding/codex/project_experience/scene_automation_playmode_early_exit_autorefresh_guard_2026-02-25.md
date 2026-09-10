# Scene Automation Early Exit Fix (2026-02-25)

## Symptom
- `Run ElementCardsShowcase Automation` repeatedly produced only `000_Scene Start.png`.
- Console/Editor log reported: play mode ended early, automation not completed.
- `element_cards_report.json` was missing in failed runs.

## Root Cause
- During automation, writing output files under project root can trigger editor refresh paths.
- Play mode transitions were interrupted before step execution completed.

## Effective Fix
- In `SceneAutomationCommand`:
  - Suppress auto refresh before `EditorApplication.EnterPlaymode()`.
  - Restore auto refresh in all cleanup paths (success, failure, recovery).
  - Track suppression state in `SessionState` with key `MageKnight.SceneAutomation.AutoRefreshSuppressed`.

## Validation
- Double compile check returned zero errors.
- ElementCardsShowcase automation report status became `success`.
- Required 9 screenshots were generated.

## Recovery Addendum (MCP)
- If `hint=retry` / session unavailable appears repeatedly:
  - Avoid rapid retries.
  - Rebind active instance from `mcpforunity://instances`.
  - If transport remains unhealthy, restart stack via `start-http-dual-instance-stack.ps1`.
