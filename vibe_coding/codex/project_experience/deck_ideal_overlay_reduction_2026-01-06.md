# DeckIdeal overlay reduction notes (2026-01-06)

## Context
- Goal: remove persistent gray overlays in DeckIdeal screenshots while keeping layout stable.

## Actions
- Lowered shelf base alpha to 0.10.
- Lowered bottom band base alpha to 0.08.
- Lowered card face base mask alpha to 0.12 (range 0.08-0.15).

## Expected result
- Top-left/right and bottom band rectangles should no longer appear as strong gray blocks.
- Card faces should read darker/cleaner without a heavy wash.

## Follow-up checklist
- Rebuild DeckIdeal scene via builder.
- Run DeckIdeal automation and confirm status=success.
- Verify screenshots show no persistent overlay blocks.
