using System;
using System.IO;
using System.Linq;
using MK.Logic.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    public static class UnitVerificationMenu
    {
        [MenuItem("Tools/Mage Knight/Original Cards/Run Units")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式");
            const string data = "Assets/Resources/CardVerification/unit_cases.json";
            AssetDatabase.ImportAsset(data, ImportAssetOptions.ForceUpdate);
            const string path = "Assets/Scenes/Verification/units.unity";
            if (!File.Exists(path))
            {
                var original = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var controller = new GameObject("UnitVerification").AddComponent<UnitVerificationController>();
                controller.artwork = UnitJsonLoader.SupportedIds.Select(id => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png")).ToArray();
                if (controller.artwork.Any(s => s == null)) throw new InvalidOperationException("原部队图片缺失");
                EditorSceneManager.SaveScene(scene, path); EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
            foreach (string generated in new[] { path, path + ".meta", data + ".meta" })
            {
                if (!File.Exists(generated)) continue;
                string normalized = string.Join("\n", File.ReadAllLines(generated).Select(line => line.TrimEnd())) + "\n";
                if (File.ReadAllText(generated) == normalized) continue;
                string temporary = generated + ".tmp";
                File.WriteAllText(temporary, normalized); File.Replace(temporary, generated, null);
            }
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "OriginalCards_1920x1080");
            string root = "AutomationOutputs/OriginalUnits/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(root);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(File.ReadAllText("AutomationConfigs/AllOriginalCards/units.json"));
            request.reportPath = root + "/report.json"; request.screenshotsDirectory = root + "/captures";
            string config = root + "/request.json"; File.WriteAllText(config, JsonUtility.ToJson(request, true));
            SceneAutomationCommand.RunFromProjectRelativeConfig(config);
        }
    }
}
