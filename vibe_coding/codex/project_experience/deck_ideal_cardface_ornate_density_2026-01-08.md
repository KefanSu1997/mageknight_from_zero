# DeckIdeal card face ornate density boost (2026-01-08)

## Goal
Make hand card faces read as "ornate dark fantasy" at small scale by increasing border contrast, rune density, and glow.

## Procedural approach (System.Drawing)
Script: vibe_coding/codex/_generate_deck_ideal_art.ps1

Key changes:
- Thicker metallic frame with extra bevel line.
- Larger corner ornaments for readability at hand scale.
- Edge runes along top/bottom borders.
- Center glow layer to emphasize magical core.
- Higher star/dust counts and more swirl arcs.

Suggested parameters (current values):
- Card face size: 1024x1024
- Frame inset: 44
- Starfield count: 520
- Dust count: 340
- Swirl arcs: 18
- Rune ring radius/count: 340 / 72
- Active emblem alpha: 230

## Outcome
The visible card faces now show metallic borders, rune ticks, and glow even in small hand thumbnails.
