// Assets/Editor/SceneGen/SceneFromBlueprintBuilder.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class SceneFromBlueprintBuilder
{
    [MenuItem("Tools/SceneGen/Build From Blueprint (Selected)")]
    public static void BuildFromSelectedBlueprint()
    {
        var blueprint = Selection.activeObject as SceneBlueprint;
        if (blueprint == null)
        {
            EditorUtility.DisplayDialog("SceneGen", "请在 Project 中选中一个 SceneBlueprint 资产。", "OK");
            return;
        }
        BuildFromBlueprint(blueprint, out var path);
        Debug.Log($"[SceneGen] Built scene from blueprint -> {path}");
    }

    public static void BuildFromBlueprint(SceneBlueprint bp, out string savedPath)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureUIBasics();

        // 先创建所有对象，再做事件绑定
        var map = new Dictionary<string, GameObject>();
        foreach (var n in bp.nodes)
        {
            GameObject go = null;

            if (!string.IsNullOrEmpty(n.prefabGuid))
            {
                var p = AssetDatabase.GUIDToAssetPath(n.prefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null)
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            if (go == null)
                go = new GameObject(n.name);

            go.name = n.name;

            if (!string.IsNullOrEmpty(n.parentName) && map.TryGetValue(n.parentName, out var parent))
                go.transform.SetParent(parent.transform, false);

            go.transform.localPosition = n.position;
            go.transform.localRotation = Quaternion.Euler(n.rotation);
            go.transform.localScale = n.scale;

            map[n.name] = go;
        }

        // 事件绑定（目前示例：Button.onClick）
        foreach (var n in bp.nodes)
        {
            if (!map.TryGetValue(n.name, out var go)) continue;

            foreach (var e in n.events)
            {
                var compType = Type.GetType(e.componentType);
                if (compType == null)
                {
                    Debug.LogWarning($"[SceneGen] Type not found: {e.componentType}");
                    continue;
                }
                var comp = go.GetComponent(compType) ?? go.AddComponent(compType);

                if (e.eventName == "onClick" && comp is Button btn)
                {
                    // === 修正点 1：逐个移除持久化监听（替代 RemovePersistentListeners）===
                    for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    {
                        UnityEventTools.RemovePersistentListener(btn.onClick, i);
                    }

                    // 依次添加 calls
                    foreach (var call in e.calls)
                    {
                        if (!map.TryGetValue(call.targetNodeName, out var tgtGO))
                        {
                            Debug.LogWarning($"[SceneGen] Target node not found: {call.targetNodeName}");
                            continue;
                        }
                        var tgtType = ResolveType(call.targetComponentType);
                        if (tgtType == null)
                        {
                            Debug.LogWarning($"[SceneGen] Target type not found: {call.targetComponentType}");
                            continue;
                        }
                        var tgtComp = tgtGO.GetComponent(tgtType);
                        if (tgtComp == null)
                        {
                            tgtComp = tgtGO.AddComponent(tgtType);
                        }

                        // 找 public 无参方法
                        var mi = tgtType.GetMethod(call.methodName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod);
                        if (mi == null || mi.GetParameters().Length != 0)
                        {
                            Debug.LogWarning($"[SceneGen] Method not found or not parameterless: {call.targetComponentType}.{call.methodName}");
                            continue;
                        }

                        // 创建 UnityAction 委托并持久化加入
                        var del = Delegate.CreateDelegate(typeof(UnityAction), tgtComp, mi) as UnityAction;
                        if (del != null)
                        {
                            UnityEventTools.AddPersistentListener(btn.onClick, del);
                        }
                        else
                        {
                            Debug.LogWarning($"[SceneGen] Failed to create UnityAction delegate for {call.targetComponentType}.{call.methodName}");
                        }
                    }

                    // === 修正点 2：标记对象已修改（替代 DirtyPersistentCalls）===
                    EditorUtility.SetDirty(btn);
                }
            }
        }

        // 保存场景
        var dir = "Assets/Scenes/Generated";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var name = bp.name.Replace(" ", "_");
        savedPath = $"{dir}/{name}.unity";
        EditorSceneManager.SaveScene(scene, savedPath);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureUIBasics()
    {
        var canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }

    private static Type ResolveType(string name)
    {
        // 支持简名（如 GameController）或带命名空间的完整名
        var t = Type.GetType(name);
        if (t != null) return t;

        // 尝试从已加载程序集中查找
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(name);
            if (t != null) return t;
            t = asm.GetTypes().FirstOrDefault(x => x.Name == name);
            if (t != null) return t;
        }
        return null;
    }
}
