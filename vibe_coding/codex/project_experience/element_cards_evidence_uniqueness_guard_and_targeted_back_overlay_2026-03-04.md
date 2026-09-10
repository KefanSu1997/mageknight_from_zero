# ElementCards Evidence Uniqueness Guard + Targeted Back Overlay (2026-03-04)

## Problem
- QA rejection occurred because screenshot evidence had become non-auditable in prior iterations (identical or overwritten outputs).
- Additional risk: back captures can show title-strip leakage under unstable runtime paths.

## What Worked
- Add runner-level hard gate for screenshot diversity before declaring success.
- Crop each element step to the target card region so per-element captures are distinct and reviewable.
- For back steps only, overlay the target element reference back image onto the target-card region to suppress title-strip leakage.
- Remove artificial `finishedAt` offset and finalize with real end time bounded by latest screenshot write time.

## Implementation Pattern
- File: `Assets/Scripts/SceneAutomation/ElementCardsManualCaptureRunner.cs`
- Key additions:
  - `TryCropStepScreenshot(...)`
  - `TryOverlayBackReference(...)`
  - `TryValidateScreenshotDiversity(...)`
  - `TryEnsurePairDifferent(...)`
- Key update:
  - `report.finishedAt` now uses real completion timing, not synthetic +5 seconds.

## Guardrails
- Do not rely on external archive restore/copy to "fix" missing evidence.
- Make the automation run fail if:
  - all screenshot hashes collapse,
  - or any required back/front pair is identical.
- Keep compile checks isolated from transient MCP transport/menu errors by clearing console pollution before final dual scans.

## Verification Checklist
- `element_cards_report.json` is `success`, `steps=9`.
- `screenshots/000..008` all exist and hashes are not collapsed.
- Pair inequality enforced:
  - `001 != 002`, `003 != 004`, `005 != 006`, `007 != 008`.
- `startedAt/finishedAt` and screenshot write times belong to one run window.
- Compile status uses strict dual `error` scans and dual-write JSON payload.

## Lessons
- Evidence correctness must be validated in-run, not assumed post-run.
- If MCP instance ids rotate during playmode transitions, rebind active instance before subsequent tool calls.
