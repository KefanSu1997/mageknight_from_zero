# Deck Ideal local procedural art generation (2026-01-04)

## Problem
- Imdream API calls failed due to restricted network (no outbound HTTP).
- Needed candidate art for card back/card face/board in dark fantasy style.

## Solution
- Created local generator script using System.Drawing: tools/generate_deck_ideal_candidates.ps1.
- Adds gradients, starfield, sand streaks, arcane rings, rune ticks, celestial lines, ornate corners.
- Seeds control deterministic variants. No external references.
- When working offline, log per-image traceability fields (seed, size, style, command line, output path) in the worklog.

## Notes
- Keep outputs under Assets/UI/Images/DeckTheme/Ideal/Candidates/....
- Use seeds in logs for reproducibility.
- When network access returns, swap to Imdream API for higher-fidelity assets.
