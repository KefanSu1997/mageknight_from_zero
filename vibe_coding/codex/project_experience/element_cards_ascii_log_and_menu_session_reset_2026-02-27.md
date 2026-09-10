# ElementCards automation: ASCII log guard + menu-trigger session reset (2026-02-27)

## Problem
- Unity MCP sessions repeatedly reset after triggering `Tools/Scene Automation/Run ElementCardsShowcase Automation`.
- Session IDs changed frequently and `no_unity_session` appeared before report/screenshot artifacts were updated.
- In this environment, non-ASCII console output can destabilize CLI/mcp flows.

## What Helped
1. Convert high-frequency runtime automation logs to ASCII English in `SceneAutomationCommand`.
2. Verify menu registration via `mcpforunity://menu-items` instead of assuming compile import completed.
3. Rebind active instance from `mcpforunity://instances` after every `no_unity_session`.
4. Treat `execute_menu_item success` as non-authoritative; always verify artifact write timestamps directly.
5. Keep compile evidence independent from automation menu status by running dual `read_console` scans explicitly.

## Practical Guardrails
- Before retries on MCP instability:
  - `refresh_unity(wait_for_ready=true)` when possible, then rebind instance.
- After repeated transport failures on `60022`:
  - restart HTTP stack and validate `/health` before any scene automation calls.
- For reviewer gating:
  - never rely on a single automation invocation result; verify `report.json` timestamp + screenshot timestamps.

## Outcome
- Back/Front visibility logic was tightened in `ElementCardFlipPresenter`.
- Compile evidence dual-write completed with zero-error scans.
- Evidence mapping files were refreshed with current compile SHA.
