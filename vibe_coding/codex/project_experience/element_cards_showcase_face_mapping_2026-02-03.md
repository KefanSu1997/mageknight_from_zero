# ElementCardsShowcase face mapping: GameData card sprites + automation run

## Context
- Goal: show earth/water/wind/fire card fronts using GameData card art.
- Scene: Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity
- Binding via ElementCardsShowcaseBootstrapper + scene serialized frontSpritePath.

## Mapping
- Earth -> Assets/GameData/cards/magic_003.png
- Water -> Assets/GameData/cards/magic_013.png
- Wind -> Assets/GameData/cards/magic_023.png
- Fire -> Assets/GameData/cards/magic_009.png

## Back/Frame
- Back sprites remain in Assets/UI/Images/ElementCards/element_card_back_*_imdream.png
- Frame sprites remain in Assets/UI/Images/ElementCards/element_card_frame_*_imdream.png

## Automation
- Menu item: Tools/Scene Automation/Run ElementCardsShowcase Automation
- Config path: multi-agent-workspace/runs/T-20251028-020/scene_automation_element_cards.json
- Steps: Back View -> Front View (buttonPath: AutomationButton)
- Output: multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/screenshots
  and multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/element_cards_report.json
