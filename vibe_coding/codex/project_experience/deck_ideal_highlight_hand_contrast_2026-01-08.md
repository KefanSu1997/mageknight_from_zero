# DeckIdeal highlight visibility + hand card contrast (2026-01-08)

## Issue
- Highlight overlays were too faint because CanvasGroup alpha multiplied low border alpha.
- Hand card face looked washed out from large glow/filigree layers.

## Fix
- Increase active highlight alpha (0.75), raise highlight border base alpha, and boost glow alpha.
- Add a dark frame shadow ring and raise outer/inner frame contrast.
- Darken base tint, boost rune/sigil/sparkle alpha, and reduce glow/filigree wash.

## Notes
- If a menu item is missing and logs an error, clear the console before the final compile check.
- Always rerun Scene Automation to refresh report + screenshots after visual tweaks.
