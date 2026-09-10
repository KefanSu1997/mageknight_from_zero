# ElementCards Front Art Sprite Import (2026-01-28)

## Context
- Element card front art uses `Assets/UI/Images/ElementCards/element_back_*_0.png` via `ElementCardsShowcaseBootstrapper`.
- These textures were imported as Default (spriteMode=0), so `AssetDatabase.LoadAssetAtPath<Sprite>` returned null.

## Fix
- Update the four `element_back_*_0.png.meta` files to:
  - `textureType: 8` (Sprite)
  - `spriteMode: 1`
  - `alphaIsTransparency: 1`
- Refresh assets in Unity so the sprites are reimported.

## Result
- Front art sprites load successfully in editor play mode, and `Img_FrontArt` renders visible content.
