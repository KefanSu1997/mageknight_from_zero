# ElementCards single-run evidence closure (2026-03-02)

## Context
ElementCardsShowcase automation repeatedly failed review because report/screenshot timestamps were not from one trustworthy run window, and Earth back/front semantics were occasionally inverted in evidence.

## What worked
1. Make report writing incremental during run (snapshot after each step), not only once at the end.
2. Reset output files before run (`000-008` + report) to prevent stale file contamination.
3. Enforce semantic state before each capture:
   - back step => all cards back
   - front step => only target element front, others back
4. Validate side-state before capture and log explicit per-element state.
5. Capture screenshot with frame-bound polling until file is non-zero and stable for multiple frames.
6. Set report finishedAt after final screenshot write is confirmed.
7. Extract `[SceneAutomation]` and `[OK] label -> path` from Editor.log when console session continuity is unstable.

## Pitfalls
1. PowerShell `ConvertFrom-Json` can auto-convert ISO date strings to local DateTime and lose timezone precision.
2. This can create false negatives in time-window checks and misaligned audit timestamps.

## Guardrails
1. Use `ConvertFrom-Json -DateKind String` when verifying report/audit timestamps.
2. During MCP `hint=retry` / session drop:
   - re-read `mcpforunity://instances`
   - set latest active instance
   - run `refresh_unity(wait_for_ready=true)` before retry.

## Reusable checklist
1. Trigger menu once.
2. Confirm report status success and 9 steps present.
3. Confirm screenshot `000-008` exist and write times are inside report window.
4. Confirm side-state logs exist for each step and `[OK]` summary lines exist.
5. Regenerate binding audit with generatedAt aligned to report finishedAt.
6. Run compile scan twice (`types=["error"], count=1000, include_stacktrace=true`) and dual-write compile_status.
