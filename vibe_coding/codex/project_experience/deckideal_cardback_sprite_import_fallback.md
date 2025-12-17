# DeckIdeal card back sprite import fallback

## Symptom
- `AssetDatabase.LoadAssetAtPath<Sprite>(path)` returns null for `*.png` even though the file exists and is visible in the Project window.
- Resulting UI shows white placeholder quads (UnityWhite texture) for deck/discard card backs.

## Root Cause
- The texture asset is imported as `Texture2D` (not `Sprite`), so direct Sprite load fails.
- Editor scripts that assume a Sprite exists (e.g. scene builders) then produce empty Image sprites.

## Fix Pattern
- In editor scripts, prefer a "loose" load:
  - Try `LoadAssetAtPath<Sprite>` first.
  - If that fails, `LoadAssetAtPath<Texture2D>` and create a runtime sprite via `Sprite.Create`.
- Optionally attempt to update the importer to `TextureImporterType.Sprite` + `SpriteImportMode.Single` for future loads, but keep the runtime fallback to avoid brittle pipelines.

## Where Applied
- `Assets/Editor/DeckIdealSceneBuilder.cs`: `LoadSpriteLoose(...)` + `EnsureSpriteImport(...)`.

