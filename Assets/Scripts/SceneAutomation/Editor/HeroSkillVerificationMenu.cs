using System;
using System.IO;
using MageKnight.SceneAutomation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    public static class HeroSkillVerificationMenu
    {
        [MenuItem("Tools/Mage Knight/Original Cards/Run Hero Skills")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式");
            AssetDatabase.ImportAsset("Assets/Resources/CardVerification/skill_cases.json", ImportAssetOptions.ForceUpdate);
            const string scenePath = "Assets/Scenes/Verification/hero_skills.unity";
            if (!File.Exists(scenePath))
            {
                var original = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var controller = new GameObject("HeroSkillVerification").AddComponent<HeroSkillVerificationController>();
                controller.originalSheet = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/skill_000.png");
                if (controller.originalSheet == null) throw new InvalidOperationException("原始技能表素材缺失");
                EditorSceneManager.SaveScene(scene, scenePath); EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "OriginalCards_1920x1080");
            string root = "AutomationOutputs/HeroSkills/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(root);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(File.ReadAllText("AutomationConfigs/AllOriginalCards/hero_skills.json"));
            request.reportPath = root + "/report.json"; request.screenshotsDirectory = root + "/captures";
            string config = root + "/request.json"; File.WriteAllText(config, JsonUtility.ToJson(request, true));
            SceneAutomationCommand.RunFromProjectRelativeConfig(config);
        }
    }
}
