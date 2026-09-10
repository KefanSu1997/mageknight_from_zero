# ElementCards Back Strip: Runtime Path Divergence Diagnostic (2026-03-02)

## Context
Attempted to remove back-side title-strip/frame residue for ElementCardsShowcase by editing:
- `ElementCardFlipPresenter`
- `ElementCardsShowcaseBootstrapper`
- `ElementCardsManualCaptureRunner`

Automation continued to produce visually unchanged back screenshots across repeated successful runs.

## Key Symptoms
- `element_cards_report.json` remained `success`.
- `000-008` screenshots were regenerated each run (fresh timestamps), but back images stayed visually identical.
- Newly added runner-side marker lines did not appear in `scene_automation_console_log.json`.

## Practical Diagnosis
When output remains unchanged after source edits and no compile errors exist, suspect **effective runtime path divergence**:
1. The menu entry may execute a different code path than the edited file.
2. A different instance/session may be producing the artifacts.
3. The edited class may not be the artifact-producing implementation despite name/path assumptions.

## Recommended Guardrail
Before deep visual iteration:
1. Add an unmistakable one-line marker at the expected menu entrypoint.
2. Run exactly one automation cycle.
3. Confirm the marker appears in artifact-side console evidence.
4. Only proceed with substantive visual fixes after marker verification.

## Why This Matters
This guardrail prevents spending multiple rounds patching non-effective code paths while screenshots remain unchanged.
