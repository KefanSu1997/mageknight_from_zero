# T-20251028-006 DeckIdeal UI - Round1

## Goal
- Unify Ideal Deck test scene visuals: hand (5 cards), deck stack + count, discard stack + count, center stage (magic circle / floating cards).
- Produce automation screenshots and ensure Unity compile errors are zero.

## Plan
- Update DeckIdeal scene builder to output to current run folder.
- Update card back to v2, ensure no white placeholders.
- Add semantic count badges for Deck/Discard.
- Run SceneAutomation for DeckIdeal to generate report + captures.

## Assets / Master Style Chain
- Table background: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_background.png`
- Foreground matte: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_foreground.png`
- Magic circle: `Assets/UI/Images/DeckTheme/deckui_magic_circle.jpg`
- Frame/border: `Assets/UI/Images/DeckTheme/deckui_border.jpg`
- Highlight/glow: `Assets/UI/Images/DeckTheme/deckui_card_highlight.jpg`
- Card back (v2): `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png`
- Card faces (placeholders): `Assets/GameData/cards/ideal_card_01.png` .. `ideal_card_05.png`

## Changes (Implementation Notes)
- `Assets/Editor/DeckIdealSceneBuilder.cs`
  - Screenshot output redirected to `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/deck_ideal_overview.png`.
  - Card back path switched to `deck_ideal_card_back_v2.png`.
  - Added sprite loading fallback (`LoadSpriteLoose`) so PNG imported as Texture2D still renders (avoids white placeholders).
  - Updated theme loading to prefer the 1920x1080 Ideal foreground master as background (sprite-create fallback) to match the reference full-scene composition.
  - Simplified layout to match the reference master (render master background, plus corner Deck/Discard labels); cards/stack overlays are now delegated to the master image.
- `AutomationConfigs/part1_deck_ideal.json`
  - screenshotsDirectory/reportPath updated to run `T-20251028-006`.
- `Assets/Scripts/SceneAutomation/Editor/SceneAutomationQuickMenus.cs`
  - Added menu entry `Tools/Scene Automation/Run DeckIdeal Automation`.

## Verification
- Unity console compile errors: 0 (checked via MCP `read_console types=["error"]`).
- Automation run: `Tools/Scene Automation/Run DeckIdeal Automation` => report status `success`.

## Outputs (Acceptance Artifacts)
- Overview screenshot: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/deck_ideal_overview.png`
- Automation capture: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/Part1_DeckIdeal_*/000_Scene Start.png`
- Report: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/deck_ideal_report.json`

## Seedream Prompts
- Not used in this round (used existing project assets, no new generation).
