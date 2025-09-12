// Assets/Editor/SceneGen/SceneGenJsonImporter.cs
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SceneGenJsonImporter
{
    [System.Serializable] private class CallDTO { public string targetNodeName, targetComponentType, methodName; }
    [System.Serializable] private class EventDTO { public string componentType, eventName = "onClick"; public CallDTO[] calls; }
    [System.Serializable] private class NodeDTO
    {
        public string name, prefabGuid, parentName;
        public Vector3 position, rotation, scale = Vector3.one;
        public EventDTO[] events;
    }
    [System.Serializable] private class BlueprintDTO { public NodeDTO[] nodes; }

    [MenuItem("Tools/SceneGen/Import JSON to Blueprint")]
    public static void Import()
    {
        var path = EditorUtility.OpenFilePanel("Select SceneGen JSON", "Assets/SceneGen/Tasks", "json");
        if (string.IsNullOrEmpty(path)) return;

        var json = File.ReadAllText(path);
        var dto = JsonUtility.FromJson<BlueprintDTO>(json);
        if (dto == null || dto.nodes == null)
        {
            EditorUtility.DisplayDialog("SceneGen", "JSON 格式不正确。", "OK");
            return;
        }

        var asset = ScriptableObject.CreateInstance<SceneBlueprint>();
        foreach (var n in dto.nodes)
        {
            var node = new SceneBlueprint.Node
            {
                name = n.name,
                prefabGuid = n.prefabGuid,
                parentName = n.parentName,
                position = n.position,
                rotation = n.rotation,
                scale = n.scale
            };
            if (n.events != null)
            {
                foreach (var e in n.events)
                {
                    var eb = new SceneBlueprint.EventBinding
                    {
                        componentType = e.componentType,
                        eventName = string.IsNullOrEmpty(e.eventName) ? "onClick" : e.eventName
                    };
                    if (e.calls != null)
                    {
                        foreach (var c in e.calls)
                        {
                            eb.calls.Add(new SceneBlueprint.Call
                            {
                                targetNodeName = c.targetNodeName,
                                targetComponentType = c.targetComponentType,
                                methodName = c.methodName
                            });
                        }
                    }
                    node.events.Add(eb);
                }
            }
            asset.nodes.Add(node);
        }

        var outDir = "Assets/SceneGen/Blueprints/Imported";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        var outPath = $"{outDir}/{Path.GetFileNameWithoutExtension(path)}.asset";
        AssetDatabase.CreateAsset(asset, outPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
        Debug.Log($"[SceneGen] Imported JSON -> {outPath}");
    }
}
