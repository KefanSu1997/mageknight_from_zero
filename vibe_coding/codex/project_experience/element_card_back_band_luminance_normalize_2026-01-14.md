# Element card back dark band removal (row luminance normalization)

## Issue
- element_card_back_imdream.png bottom rows were visibly darker, causing a horizontal band in flip screenshots.

## Fix
- Compute per-row mean luminance.
- Find the first row below a threshold (~120).
- Scale RGB values for rows in the band to match the mean luminance of the row above the band.
- Keep resolution and alpha intact; no crop/resize needed.

## Tools
- Python (py)
- Pillow
- numpy

## Files
- Assets/UI/Images/ElementCards/element_card_back_imdream.png
