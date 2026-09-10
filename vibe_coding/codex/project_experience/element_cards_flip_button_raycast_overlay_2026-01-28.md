# ElementCards flip: Button hitbox + raycast routing + front overlay (2026-01-28)

## Problem
- Cards had `IPointerClickHandler` on the root, but clicks were intercepted by child `Image` components.
- Automation had no steps, so there was no front/back evidence.

## Approach
1. Route clicks to the card root:
   - Add a transparent root `Image` and a root `Button`.
   - Set `Img_Back.raycastTarget = false` and `Img_Frame.raycastTarget = false`.
   - Guard `OnPointerClick` to avoid double toggles when `Button` exists.
2. Make the front state visually distinct and consistent:
   - Build a runtime `FrontOverlay` that includes:
     - `Img_FrontArt` with `AspectRatioFitter(EnvelopeParent)`
     - `Img_FrontFrame`
     - Existing sigil and label
   - Hide the back image while front is active.
3. Provide automation evidence:
   - Populate flip steps in `SceneAutomationQuickMenus`.

## Why This Works
- Unity UI events target the topmost raycastable `Graphic`. Disabling raycast on child images forces the event to hit the root `Button`.
- The overlay-based front content guarantees consistent margins and aspect behavior without directly editing the `.unity` file.

## Evidence
- Report: `multi-agent-workspace/runs/T-20251028-017/review_bundle/artifacts/element_cards_showcase_report.json`
- Screenshots: `multi-agent-workspace/runs/T-20251028-017/review_bundle/artifacts/screenshots/`

## Extra Notes (automation pitfalls)
- The orchestrator originally only called `Button.onClick`, which makes IPointerClickHandler-only targets appear to "work" but not actually flip.
- The report writer used to clear `record.message` on success, erasing valuable evidence like `front=Card_Earth`.
