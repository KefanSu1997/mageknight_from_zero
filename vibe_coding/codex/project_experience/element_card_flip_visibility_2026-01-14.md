# Element Card Flip Visibility Fix (2026-01-14)

## Issue
- Back-face screenshots showed horizontal dark bands matching front text regions.
- Frame visibility was not clearly on top after flip.

## Cause
- Front/back visibility relied on GameObject active state alone.
- Frame ordering was not enforced at runtime.

## Fix
- Enforce strict visibility per layer:
  - Set active state and alpha 0/1 on front/back images.
  - Cull hidden layers via CanvasRenderer.
  - Ensure frame GameObject is active and set as last sibling each update.

## Reference
- Script: Assets/Scripts/UI/ElementCardView.cs
