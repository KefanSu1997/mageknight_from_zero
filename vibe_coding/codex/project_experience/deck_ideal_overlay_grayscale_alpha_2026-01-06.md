# DeckIdeal overlay grayscale-to-alpha fix (2026-01-06)

## Problem
DeckIdeal overlays based on JPG highlight/magic-circle textures can show faint rectangular tints if the background is not near-black.
This happens because the importer uses grayScaleToAlpha (alpha from luminance) and any non-black background becomes visible.

## Fix
Regenerate JPG overlays with a pure black background and bright strokes only. This keeps the alpha mask tight and removes
large rectangle tints in deck/discard/hand areas.

## Generator
Script: tools/generate_deck_ideal_overlays.ps1

Outputs:
- Assets/UI/Images/DeckTheme/deckui_card_highlight.jpg
- Assets/UI/Images/DeckTheme/deckui_magic_circle.jpg

Seeds:
- HighlightSeed=20260120
- MagicSeed=20260121
Size: 1024

## Notes
- Keep the background fully black; even dark gradients will produce alpha haze.
- Use white/gray strokes to control overlay strength via grayscale-to-alpha.
- Reimporting is not needed; Unity will refresh on asset change.
