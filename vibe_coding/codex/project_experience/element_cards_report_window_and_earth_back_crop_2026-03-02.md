# ElementCards Report Window + Earth Back Crop (2026-03-02)

## Context
- Reviewer rejected a round for two hard reasons:
  - report file write-time was outside `startedAt~finishedAt`.
  - Earth back image had a visible outer white edge versus other elements.

## What worked
1. Report window closure fix
- Do not finalize `finishedAt` before final report write.
- Use a two-stage close:
  - write status/message snapshot first (without `finishedAt`);
  - compute `finishedAt` after checking latest screenshot/report write times;
  - write final report with a small safety margin (`+2s`) to keep report write-time inside the window.
- Add deterministic frame waits per step so the run window reflects real execution time (avoid very short ~3s windows).

2. Earth white-edge mitigation
- Add `RectMask2D` to `BackRoot`.
- Apply Earth-only back overscan crop via `RectTransform` offsets (`18px`) on `Img_Back`.
- Keep other elements at zero crop to avoid unintended style drift.

3. Evidence consistency closure
- Rebuild `binding_audit`, `scene_automation_console_log`, and `artifact_presence_check` from the current run.
- Ensure `generatedAtUtc == reportFinishedAt == report.finishedAt` for audit metadata alignment.
- Keep compile evidence dual-written and hash-matched between run root and artifacts copy.

## Guardrails
- If MCP returns `hint=retry` / session loss:
  - rebind active Unity instance first;
  - run `refresh_unity(wait_for_ready=true)` before retrying calls.
- For time-window checks in PowerShell, avoid accidental local-time conversion from JSON datetime auto-parsing; compare ISO UTC text directly when needed.
