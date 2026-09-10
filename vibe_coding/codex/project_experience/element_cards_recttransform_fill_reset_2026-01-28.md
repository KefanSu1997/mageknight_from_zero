# ElementCards layout fix: reset child RectTransforms (2026-01-28)

## Context
- ElementCards front/back visuals were sometimes rendered tiny or offset after flipping.
- Scene hierarchy showed FrontRoot/BackRoot children with default anchors/sized 100x100.

## Fix
- In ElementCardFlipPresenter, reset RectTransform anchors/offsets/scale/rotation for:
  - BackRoot children (Img_Back, Img_Frame)
  - FrontRoot children (Img_FrontArt, Img_FrontTint, Img_FrontFrame)
  - FrontRoot rect itself
- This keeps the card face filling its parent and prevents tiny duplicates.

## Result
- Flipped cards render at correct size with no oversized remnants in screenshots.
