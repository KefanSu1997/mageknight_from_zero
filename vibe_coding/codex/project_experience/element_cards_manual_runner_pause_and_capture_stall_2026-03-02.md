# ElementCards manual runner pause/capture stall notes (2026-03-02)

## Context
- Target menu: `Tools/Scene Automation/Run ElementCardsShowcase Automation`
- Expected output: one run should produce `element_cards_report.json` and `000-008` in `review_bundle/artifacts`.

## What was observed
- Multiple isolated runs reproduced a consistent pattern:
  - `000_Scene Start.png` updates to the current run timestamp.
  - `001-008` and `element_cards_report.json` remain unchanged in target artifacts.
- Incremental log slicing (pre-line-count -> post-run appended lines) is useful to avoid confusion from old logs.

## Useful mitigations that should stay
- Explicit pre-play editor preparation:
  - disable `kPauseOnPlay`
  - disable Console Error Pause
  - force `EditorApplication.isPaused = false`
- Frame-bound polling for screenshot file write should be preferred over realtime stopwatch waits.

## Remaining risk
- Even with pause guards and ScreenCapture polling, capture flow can still stall after scene start in this environment.
- Keep compile evidence independent from automation success so compile guardrails remain trustworthy.

## Practical next-step hint
- If this persists, instrument manual runner with explicit phase markers around:
  - pre-capture
  - post-capture
  - pre-loop
  - per-step capture enter/exit
  and persist them to a dedicated artifact file under the run folder for deterministic post-mortem.
