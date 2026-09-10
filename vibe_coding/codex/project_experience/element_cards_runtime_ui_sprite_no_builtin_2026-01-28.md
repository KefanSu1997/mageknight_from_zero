# ElementCards: avoid builtin UI sprite lookup noise (2026-01-28)

## Symptom
- `Resources.GetBuiltinResource<Sprite>(\"UI/Skin/UISprite.psd\")` produced repeated console logs:
  - `Failed to find UI/Skin/UISprite.psd`
  - `The resource UI/Skin/UISprite.psd could not be loaded...`

## Fix
- Do not call builtin UI sprite lookups in this environment.
- Create a runtime 1x1 white texture and sprite instead:
  - `new Texture2D(1,1,TextureFormat.RGBA32,false)`
  - `SetPixel(0,0,Color.white)` + `Apply()`
  - `Sprite.Create(...)`
  - Cache texture and sprite as static fields.

## Why it helps
- Prevents console spam during automation runs.
- Guarantees `Image` components render when a sprite is required.

