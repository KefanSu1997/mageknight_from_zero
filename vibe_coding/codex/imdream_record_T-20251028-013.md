# Imdream Asset Record - T-20251028-013 Element Cards

## Purpose
- Provide audit trail for card back/frame images used by ElementCardView.

## Prompts
- Back: "fantasy card back, arcane sigil, elemental motif, ornate filigree, teal and gold, clean center emblem, high detail, ui asset"
- Frame: "fantasy card frame, carved metal and wood, elemental gems, transparent center, ornate corners, high detail, ui border"

## Commands (intended, not executed in this environment)
- Submit: tools/generate_imdream_image.ps1 "<prompt>" --size 1048576 --scale 0.6 --force-single
- Query (back): tools/imdream_query.ps1 <task_id> AutomationOutputs/Imdream --poll --interval 3 --timeout 600 --download-name element_card_back_imdream
- Query (frame): tools/imdream_query.ps1 <task_id> AutomationOutputs/Imdream --poll --interval 3 --timeout 600 --download-name element_card_frame_imdream

## Outputs (reused from existing files)
- Source outputs reused from earlier Imdream runs (no task id available due to restricted network in this environment):
  - AutomationOutputs/Imdream/imdream_result_0.png
  - AutomationOutputs/Imdream/imdream_result_1.png

## Reuse Mapping
- AutomationOutputs/Imdream/imdream_result_0.png -> Assets/UI/Images/ElementCards/element_card_back_imdream.png
- AutomationOutputs/Imdream/imdream_result_1.png -> Assets/UI/Images/ElementCards/element_card_frame_imdream.png

## Post-processing (frame)
- Added transparent center to the frame image so it works as an overlay border.
- Command (PowerShell/System.Drawing):
  - Add-Type -AssemblyName System.Drawing
  - $path = "D:\\study_and_work\\unity-MK-test\\MageKnight_from_zero\\Assets\\UI\\Images\\ElementCards\\element_card_frame_imdream.png"
  - $borderX = 150; $borderY = 150
  - Create 32bpp bitmap, draw source, then FillRectangle with ARGB(0,0,0,0) using SourceCopy

## Imported Assets
- Assets/UI/Images/ElementCards/element_card_back_imdream.png
- Assets/UI/Images/ElementCards/element_card_frame_imdream.png

## Usage
- Prefab: Assets/Prefabs/UI/ElementCardView.prefab
  - backSprite -> element_card_back_imdream.png
  - frameSprite -> element_card_frame_imdream.png
## Post-processing (back)
- Removed a dark bottom band by cropping rows below 0.7 * mean row luminance and resizing back to original size.
- Tool: Python + Pillow (local), no network access.
- Output: Assets/UI/Images/ElementCards/element_card_back_imdream.png

## Post-processing (back, luminance normalize)
- Normalized the last 82 rows to match the mean luminance of the row above the band to remove residual striping.
- Tool: Python + Pillow + numpy (local), no network access.
- Output: Assets/UI/Images/ElementCards/element_card_back_imdream.png

## Post-processing (resize to 2:3)
- Reason: Align card back/frame aspect ratio to the card prefab (2:3) and prevent stretching artifacts.
- Tool: PowerShell + System.Drawing (local), no network access.
- Back: center-crop to 2:3 and resize to 1600x2400.
- Frame: rotate 90 degrees, center-crop to 2:3, resize to 1600x2400.
- Outputs:
  - Assets/UI/Images/ElementCards/element_card_back_imdream.png
  - Assets/UI/Images/ElementCards/element_card_frame_imdream.png
