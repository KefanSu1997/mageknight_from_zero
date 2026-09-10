Title: DeckIdeal highlight overlay uses border + glow layering
Date: 2026-01-08

Issue
- Highlight screenshots showed cards/frames nearly disappearing, leaving mostly background + count plaques.

Cause
- Highlight overlay used a full-rect highlight sprite (no sprite border), so the tinted fill washed out card detail when alpha increased.

Fix
- Build highlight overlays from a border sprite (sliced, fillCenter=false) plus a faint glow layer.
- Put the CanvasGroup on a root rect and enable/disable all child Image components in the highlighter.

Result
- Highlight state keeps cards/frames visible while adding a readable outline + glow.

Files
- Assets/Editor/DeckIdealSceneBuilder.cs
- Assets/Scripts/UI/Part1/DeckIdealHitboxHighlighter.cs
