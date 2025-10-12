using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MageKnight.EditorTools
{
    internal static class DynamicFontGenerator
    {
        private const string SourceFontPath = "Assets/Fonts/msyh.ttc";
        private const string OutputFolder = "Assets/Resources/Fonts";
        private const string OutputAssetPath = OutputFolder + "/msyh TMP Dynamic.asset";

        [MenuItem("Tools/Fonts/Generate Dynamic MSYH Font", priority = 3000)]
        private static void GenerateDynamicFont()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[DynamicFontGenerator] 未找到字体资源：{SourceFontPath}");
                return;
            }

            EnsureOutputFolder();

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputAssetPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(OutputAssetPath);
            }

            var dynamicFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            dynamicFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            dynamicFont.isMultiAtlasTexturesEnabled = true;
            if (dynamicFont.fallbackFontAssetTable != null)
            {
                dynamicFont.fallbackFontAssetTable.Clear();
            }

            var atlasTexture = new Texture2D(1024, 1024, TextureFormat.Alpha8, false)
            {
                name = "msyh TMP Dynamic Atlas",
                hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
            };

            dynamicFont.atlasTextures = new[] { atlasTexture };

            var shader = Shader.Find("TextMeshPro/Distance Field");
            var material = new Material(shader)
            {
                name = "msyh TMP Dynamic Material",
                hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
            };
            material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
            dynamicFont.material = material;

            dynamicFont.name = "msyh TMP Dynamic";
            AssetDatabase.CreateAsset(dynamicFont, OutputAssetPath);

            AssetDatabase.AddObjectToAsset(atlasTexture, dynamicFont);
            AssetDatabase.AddObjectToAsset(material, dynamicFont);

            dynamicFont.ReadFontAssetDefinition();
            dynamicFont.atlasTextures = new[] { atlasTexture };
            dynamicFont.material = material;

            EditorUtility.SetDirty(dynamicFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(OutputAssetPath);

            Debug.Log("[DynamicFontGenerator] 动态字体已生成：" + OutputAssetPath);
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");
            }
        }
    }
}
