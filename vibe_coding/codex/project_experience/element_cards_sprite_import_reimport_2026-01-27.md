Issue:
- AssetDatabase.LoadAssetAtPath<Sprite> returned null for element card textures even though Texture2D loads.

Cause:
- Importer settings were not applied as Sprite at runtime; textures existed but Sprite sub-assets were missing.

Fix:
- Use TextureImporter with textureType=Sprite and spriteImportMode=Single, then SaveAndReimport.
- After reimport, LoadAssetAtPath<Sprite> returns valid sprites.

Notes:
- Avoid Reimport All; target only the specific assets.
