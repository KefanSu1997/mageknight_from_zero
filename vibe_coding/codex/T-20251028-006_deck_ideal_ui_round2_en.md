[TASK]
- ID: T-20251028-006
- Goal: Replace DeckIdeal "static motherboard + temporary labels" with real UI components (hand/deck/discard), while keeping the Ideal art style as the visual base.

[VISUAL BASE (MOTHER PLATES)]
- Table background: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_background.png`
- Table foreground/frame: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_foreground.png`
- Card back: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png`
- Hand card faces: `Assets/GameData/cards/ideal_card_01.png` .. `ideal_card_05.png`

[IMPLEMENTATION NOTES]
- Scene builder overlays real UI elements on top of the Ideal foreground so the visible cards/stacks are not "painted into" the motherboard.
- Hand: 5 independent card UI objects (shadow + plate + face + glow) in a stable bottom row.
- Deck: thick stacked card-backs + plaque showing count.
- Discard: semi-transparent messy pile (tilt/offset/alpha) + plaque showing count.

[HOW TO REPRODUCE]
1) Build and capture overview screenshot:
   - Unity menu: `Tools/Scene Builders/Build + Capture Deck Ideal`
   - Output: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/deck_ideal_overview.png`
2) Run playmode automation (captures "Scene Start"):
   - Unity menu: `Tools/Scene Automation/Run DeckIdeal Automation`
   - Outputs:
     - Report: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/deck_ideal_report.json`
     - Screenshots: `multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/Part1_DeckIdeal_*/000_Scene Start.png`

[KNOWN LIMITATIONS]
- Count plaques are procedural (sliced highlight sprite) because there is no dedicated Ideal nameplate sprite in the repo.
