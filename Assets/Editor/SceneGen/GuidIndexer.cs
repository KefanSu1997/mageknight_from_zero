using System.IO;
using UnityEditor;
using UnityEngine;

public static class GuidIndexer
{
    [MenuItem("Tools/SceneGen/Export Prefab GUID Map")]
    public static void ExportGuidMap()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        var outDir = "Assets/SceneGen/GuidMaps";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        var path = $"{outDir}/prefab_guid_map.json";
        using (var sw = new StreamWriter(path))
        {
            sw.WriteLine("{");
            for (int i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(assetPath).Replace("\"", "\\\"");
                sw.Write($"  \"{name}\": \"{guid}\"");
                sw.WriteLine(i == guids.Length - 1 ? "" : ",");
            }
            sw.WriteLine("}");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[GuidIndexer] Exported: {path}");
    }
}
