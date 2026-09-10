// 自动把 cards 文件夹下的图片设为 Sprite 类型
using UnityEditor;

class CardsTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.StartsWith("Assets/GameData/cards"))
        {
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 当前导入会应用这些设置；预处理回调中再次导入会造成递归，并被 Unity 6 拒绝。
        }
    }
}
