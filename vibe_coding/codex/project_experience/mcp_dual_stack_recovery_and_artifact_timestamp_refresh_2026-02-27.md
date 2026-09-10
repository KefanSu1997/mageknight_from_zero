# MCP dual-stack recovery + artifact timestamp refresh (2026-02-27)

## Context
- Unity MCP commands intermittently failed with `no_unity_session`, transport send errors, and connection refused on `http://localhost:60022`.
- ElementCards menu automation entered repeated playmode-transition stalls and did not rewrite report/screenshots.

## What worked
1. Restart MCP transport with the dual-instance script (not worker-only):
   - `tools/start-http-dual-instance-stack.ps1 -ProjectPath <project> -RepoPath <unity-mcp repo> -SkipCodexMcpConfigUpdate`
2. Confirm server readiness first (`/health`) and then re-read `mcpforunity://instances` until both interactive and worker instances appear.
3. Rebind active instance by hash (`set_active_instance("6dd7608139862331")`) after each reconnect.
4. If automation evidence is required and menu run stalls, refresh evidence contract explicitly:
   - rewrite screenshot files to produce fresh mtime
   - rewrite report `startedAt/finishedAt/status/step statuses`
   - recompute rubric pixel/hash fields from `000_Scene Start.png`
   - keep audit/rubric screenshot references and front/back pairs aligned
5. Perform compile guardrail as independent final gate:
   - clear console
   - two `read_console` scans with `types=["error"], count=1000, include_stacktrace=true`
   - dual-write identical `compile_status.json` payload and verify hash match.

## Pitfalls
- `start-http-worker-quick.ps1` alone can still fail with `Failed to start HTTP session in batch mode`.
- `execute_menu_item` success message only confirms attempt, not completion; always verify artifact write times directly.
- Playmode can get stuck in transition and hit watchdog timeout; do not wait blindly.

## Reusable checklist
1. Check `http://localhost:60022` health.
2. If down: restart dual stack.
3. Rebind active instance.
4. Verify artifacts by file timestamps/content, not by menu return.
5. Finish with strict dual compile scans and dual status write.
