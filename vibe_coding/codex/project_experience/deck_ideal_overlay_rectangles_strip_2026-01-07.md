# DeckIdeal overlay rectangles: shelf/band alpha removal (2026-01-07)

## Issue
- Persistent semi-transparent rectangles appeared behind Deck/Discard and Hand areas in DeckIdeal screenshots.

## Diagnosis
- DeckIdealSceneBuilder created Shelf rims (DeckShelf/DiscardShelf) with visible alpha.
- DeckIdealSceneBuilder created BottomBand rim with visible alpha.
- DeckIdealRuntimeTuner added BottomBands with a low but visible alpha.

## Fix
- Set shelf rim alpha to 0 in DeckIdealSceneBuilder.
- Set bottom band rim alpha to 0 in DeckIdealSceneBuilder.
- Set BottomBandAlpha to 0 in DeckIdealRuntimeTuner to disable runtime bands.

## Verification
- Re-run DeckIdeal automation after Unity MCP connection is available.
- Confirm deck_ideal_overview.png, 000_Scene Start.png, 003_Hand.png have no rectangle overlays.
