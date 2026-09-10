## ElementCards Front/Back Exclusive Roots (2026-01-27)

- Problem: The previous flip implementation layered a front overlay on top of the back image, which led to double-rendering, offsets, and unclear evidence of a true face swap.
- Key fix: Create explicit `BackRoot` and `FrontRoot` containers at runtime and toggle them with `SetActive` so only one face is active at any time.
- Click-chain stability: Keep the hitbox on the card root via a transparent `Image` + `Button`, and force all child graphics to `raycastTarget = false`.
- Practical notes:
  - Reparent existing `Img_Back` and `Img_Frame` into `BackRoot` at runtime.
  - Build the front face under `FrontRoot` and keep its layout stretched with a consistent padding.
  - Update automation to drive `SetExclusiveFront(cardName)` and include `front=Card_*` in report messages.

