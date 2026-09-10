# ElementCards: Crop Lane Boundaries + Back Edge Cleanup (2026-03-04)

## Context
- Reviewer rejected previous evidence because cropped screenshots leaked neighboring cards and showed white edges on back captures.
- Previous crop was based on target card rect only; if card rects overlap in layout, adjacent cards can still enter the crop.

## Root Cause
- Card screen-space rectangles for neighboring elements were horizontally overlapping in some states.
- Crop rectangle built from one target rect without inter-card boundary constraints produced overlap windows.
- Small inset values were insufficient to remove edge bleed near card borders.

## Fix Applied
- File: `Assets/Scripts/SceneAutomation/ElementCardsManualCaptureRunner.cs`
- Strategy:
- Keep target-based screen-space crop.
- Add lane dividers from centerline midpoints between target card and neighboring cards.
- Clamp crop X range to the target lane (`leftDivider/rightDivider`) with a safety gap.
- Increase inset values to remove border bleed (`StepCropInsetMinPixels`, `StepCropInsetRatio`).
- Keep fallback path for narrow rectangles but re-apply lane constraints.
- Emit audit-friendly crop logs:
- `laneDividers=...`
- `laneRange=...`
- `imageRect=...`

## Why This Works
- Even when card rects overlap, lane bounds force each step crop to stay in a non-overlapping horizontal partition.
- Increased inset trims remaining border artifacts so back captures do not include white side/bottom strips.

## Verification Checklist
- Single menu run produced fresh `000..008` screenshots with `report.status=success`.
- Crop log confirms disjoint lanes per element (Earth/Water/Wind/Fire).
- Visual check: no neighboring card leakage on backs/fronts.
- Compile guardrail dual scan: `types=[\"error\"], count=1000, include_stacktrace=true` => both scans 0.
- Structured `compile_status.json` dual-written to run root and artifacts.

## Reuse Guidance
- If layout spacing changes again, keep lane-divider logic and tune only:
- `StepCropInsetMinPixels`
- `StepCropInsetRatio`
- `StepCropLaneGapPixels`
- Prefer lane-bound crops over fixed normalized half-width constants for multi-card evidence scenes.
