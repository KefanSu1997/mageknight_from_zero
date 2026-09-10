# Element card texture import: max size + compression

## Problem
- Visible banding/ghosting after importing Imdream back/frame PNGs.

## Cause (suspected)
- Source PNGs exceed 2048 in one dimension, so Unity downsizes to maxTextureSize=2048.
- Resampling + compression can introduce faint horizontal artifacts in UI sprites.

## Fix
- Set maxTextureSize to 4096 for back/frame PNGs.
- Disable texture compression (textureCompression=0) to preserve alpha edges.

## Files
- Assets/UI/Images/ElementCards/element_card_back_imdream.png.meta
- Assets/UI/Images/ElementCards/element_card_frame_imdream.png.meta
