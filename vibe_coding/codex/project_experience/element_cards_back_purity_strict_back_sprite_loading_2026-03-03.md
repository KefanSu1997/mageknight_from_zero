# ElementCards Back Purity: Strict Back Sprite Loading + Disk Fallback (2026-03-03)

## Context
Back screenshots in ElementCardsShowcase still showed front-only title/header residue in strict review, even though side-state logic reported `back`.

## What worked
1. Remove unsafe cross-fallback between sides
- Do not allow `backSprite` to fall back to `frontSprite`.
- Keep front and back sprite sources independent, even when one side load fails.

2. Add robust disk fallback for sprite loading
- Keep `AssetDatabase.LoadAssetAtPath` as first choice.
- Add fallback: resolve `Assets/...` to `Application.dataPath` and load PNG bytes directly into `Texture2D`, then create `Sprite`.
- Apply this in both:
  - `ElementCardFlipPresenter` (runtime side switching)
  - `ElementCardsShowcaseBootstrapper` (setup-time binding)

3. Validate with one closed automation run
- `element_cards_report.json` must be `success` with 9 steps.
- Back screenshots (`001/003/005/007`) must show pure backs without front title/text.
- Front screenshots (`002/004/006/008`) must keep expected element-front mapping.

## Additional operational lesson
- Headless worker instances can emit `No graphic device is available...` and are unsuitable for screenshot validation.
- Use interactive instance for visual automation when worker is headless, while keeping code edits scene-safe and avoiding placeholder scene edits.
