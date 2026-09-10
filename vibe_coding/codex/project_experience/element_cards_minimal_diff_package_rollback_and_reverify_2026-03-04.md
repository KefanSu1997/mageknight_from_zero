# ElementCards Minimal-Diff Rollback and Re-Verify (2026-03-04)

## Problem
A prior workspace state mixed target changes with package path regression and unrelated edits, causing review rejection risk.

## Fix Strategy
- Restore package files and unrelated scripts/scenes to baseline.
- Keep only the ElementCards manual capture runner diff in code.
- Re-run strict compile gate and automation evidence generation.

## Key Steps
1. Reverted `Packages/manifest.json` and `Packages/packages-lock.json` changes.
2. Reverted unrelated editor/scene/script modifications.
3. Kept `Assets/Scripts/SceneAutomation/ElementCardsManualCaptureRunner.cs` (+ `.meta`) as the only code delta.
4. Removed `SceneAutomationReportStep.status` assignments to match current report schema.
5. Performed two compile scans (`types=["error"]`, `count=1000`, `include_stacktrace=true`) with zero errors.
6. Rewrote compile evidence JSON to both required output locations.
7. Re-ran ElementCards capture automation and confirmed `status=success` with refreshed 000-008 screenshots.

## Lessons
- In this project, reviewability depends on both minimal file scope and readable textual diffs.
- Package source drift (git tag -> local file path) is a hard blocker even when runtime output looks correct.
- After Unity playmode transitions, rebind the active MCP instance before compile checks.
