# DeckIdeal Theme Candidate Swap (2026-01-07)

## Context
- Task required a more ornate fantasy look without changing layout.
- Network access was restricted, so no external generation was used.

## Changes
- Updated `Assets/Editor/DeckIdealSceneBuilder.cs` theme asset paths to in-repo candidate art:
  - Board: `Assets/UI/Images/DeckTheme/Ideal/Candidates/Board/deck_ideal_board_candidate_20260104_f.png`
  - Card back: `Assets/UI/Images/DeckTheme/Ideal/Candidates/CardBack/deck_ideal_card_back_candidate_20260104_f.png`
  - Card faces: `Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_[a-e].png`

## Build Steps
- Exit Play Mode before running builder to avoid `EditorSceneManager.NewScene` error.
- Menu: `Tools/Scene Builders/Build + Capture Deck Ideal` to rebuild scene and save overview screenshot.
- Menu: `Tools/Scene Automation/Run DeckIdeal Automation` to capture runtime shots.

## Verification
- `review_bundle/artifacts/deck_ideal_report.json` status: success.
- Screenshots written under `review_bundle/artifacts/screenshots/`.
- Console error check (types=error) returned 0 twice after clearing logs.

## Notes
- If semi-transparent rectangles appear, rebuild the scene so highlight alpha fixes take effect.
