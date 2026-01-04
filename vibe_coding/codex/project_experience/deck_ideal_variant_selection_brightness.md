# Deck Ideal Variant Selection via Brightness Sampling

## Context
- Visual review was unavailable during selection.
- Candidate variants already existed from local generation.

## Approach
- Use System.Drawing to sample average brightness on a coarse grid (step=32) for each variant.
- Prefer darker outputs to match the "dark, glittering" fantasy direction.

## Result
- Variant A had the lowest average brightness across board, card back, and card face.
- Selected Variant A for the live assets while keeping B/C as review candidates.

## Notes
- Keep the same target asset names so scene builders do not change.
- If a future visual review contradicts this choice, switch by copying the candidate PNG again.
