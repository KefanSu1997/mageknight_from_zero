# Deck Ideal Local Art Generation Notes

## Context
- External text-to-image calls are blocked in the current environment.
- A local System.Drawing generator can produce candidate textures quickly for review.

## Key Points
- Use PowerShell + System.Drawing to generate PNGs with gradients, star noise, swirl curves, and emblem/rune rings.
- Avoid `Add-Type -AssemblyName System.Drawing.Drawing2D`; the namespace exists under System.Drawing and this call fails.
- Prefer `[System.Drawing.Rectangle]::new(...)` to avoid op_Addition/op_Subtraction runtime errors.
- Store candidates under `Assets/UI/Images/DeckTheme/Ideal/Candidates/*` and copy the selected variant into the final path referenced by code.

## Logging Tips
- Record prompt-style descriptions + exact color params + seeds to enable later Imdream re-generation.
