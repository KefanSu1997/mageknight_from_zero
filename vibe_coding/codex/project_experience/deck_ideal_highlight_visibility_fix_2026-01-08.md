# DeckIdeal highlight visibility fix (2026-01-08)

## Issue
DeckIdeal highlight overlays were invisible in 001/002/003_Highlight screenshots. Root cause:
- Highlight Image components were disabled.
- CanvasGroup alpha was forced to 0 and the image color alpha was set to 0 in the highlighter.

## Fix
- Keep highlight Image enabled only when visible, controlled via CanvasGroup alpha.
- Use a non-zero base alpha for highlight tint so the frame is readable when activated.
- Avoid forcing Image color alpha to 0 in the highlighter.

## Key settings
- DeckIdealSceneBuilder CreateHighlight:
  - Base image color alpha: 0.7 (tint readable)
  - CanvasGroup alpha: 0 by default
- DeckIdealHitboxHighlighter:
  - ActiveHighlightAlpha = 0.24
  - SetHighlightAlpha() toggles Image.enabled based on alpha, without overwriting image color.

## Verification
- Run DeckIdeal automation and confirm highlight frames are visible in 001/002/003_Highlight screenshots.
