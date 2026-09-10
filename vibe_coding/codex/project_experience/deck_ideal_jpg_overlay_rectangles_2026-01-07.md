# DeckIdeal JPG Overlay Rectangles (2026-01-07)

## Context
- DeckIdeal screenshots showed large semi-transparent rectangles in top-left, top-right, center, and bottom zones.
- The overlays used `deckui_magic_circle.jpg` and `deckui_card_highlight.jpg` (JPG has no alpha channel).

## Root Cause
- JPG textures are fully opaque; tinting them with low alpha still renders full rectangles.
- In edit-mode captures, `CanvasGroup.alpha` did not reliably hide highlight images, so their Image alpha still showed as boxes.

## Fix
- Set `MagicCircleOverlay` / `MagicSwirl` image color alpha to 0.
- Set Deck/Discard/Hand highlight image color alpha to 0.
- Keep `fillCenter=false` on sliced images, but do not rely on it when sprite borders are zero.

## Verification Tips
- Rebuild the DeckIdeal scene and regenerate screenshots.
- Inspect `deck_ideal_overview.png`, `000_Scene Start.png`, and `003_Hand.png` to ensure no large rectangular tint remains.
