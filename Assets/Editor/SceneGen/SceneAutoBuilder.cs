// Assets/Editor/SceneGen/SceneAutoBuilder.cs
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class SceneAutoBuilder
{
    [MenuItem("Tools/SceneGen/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1) 基础环境：相机 + 光源
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        camGO.tag = "MainCamera";
        cam.transform.position = new Vector3(0, 1.5f, -5f);
        cam.transform.LookAt(Vector3.zero);

        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // 2) 业务对象
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "TargetCube";

        // 3) 控制器脚本
        var controllerGO = new GameObject("GameController");
        var controller = controllerGO.AddComponent<GameController>();
        controller.targetToToggle = cube;

        // 4) UI：Canvas + Button + EventSystem
        var canvasGO = CreateCanvasIfMissing();
        var buttonGO = CreateUIButton(canvasGO.transform, "Toggle Cube", new Vector2(0, -80));
        var btn = buttonGO.GetComponent<Button>();

        // 5) 绑定 Button.onClick -> controller.OnButtonClicked（持久化监听）
        // 清空已有监听
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
        }

        // 添加新的持久化监听
        UnityAction action = controller.OnButtonClicked;
        UnityEventTools.AddPersistentListener(btn.onClick, action);

        // 标记按钮对象已修改，保证场景保存时持久化
        EditorUtility.SetDirty(btn);


        // 6) 保存场景
        var dir = "Assets/Scenes/Generated";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var path = $"{dir}/DemoAuto.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SceneAutoBuilder] Scene saved: {path}");
    }

    private static GameObject CreateCanvasIfMissing()
    {
        var canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas != null) return canvas.gameObject;

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = canvasGO.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;

        // EventSystem
        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
        return canvasGO;
    }

    private static GameObject CreateUIButton(Transform parent, string text, Vector2 anchoredPos)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 48);
        rt.anchoredPosition = anchoredPos;

        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        var txt = txtGO.GetComponent<Text>();
        txt.text = text;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.raycastTarget = false;

        return go;
    }
}
