using System.Collections.Generic;
using System.IO;
using MageKnight.SceneAutomation;
using UnityEditor;
using UnityEngine;

namespace MageKnight.SceneAutomation.Editor
{
    public static class SceneAutomationQuickMenus
    {
        private const string DeckIdealConfigPath = "multi-agent-workspace/runs/T-20251028-012/scene_automation_deckideal.json";

        [MenuItem("Tools/Scene Automation/Run DeckIdeal Automation")]
        private static void RunDeckIdealAutomation()
        {
            EnsureDeckIdealConfig();
            SceneAutomationCommand.RunFromProjectRelativeConfig(DeckIdealConfigPath);
        }

        private static void EnsureDeckIdealConfig()
        {
            var request = new SceneAutomationRequest
            {
                scenePath = "Assets/Scenes/Part1/Part1_DeckIdeal.unity",
                screenshotsDirectory = "multi-agent-workspace/runs/T-20251028-012/review_bundle/artifacts/screenshots",
                useRunSubfolder = false,
                reportPath = "multi-agent-workspace/runs/T-20251028-012/review_bundle/artifacts/deck_ideal_report.json",
                captureInitialView = true,
                initialDelaySeconds = 0.25f,
                defaultWaitAfterSeconds = 0.5f,
                steps = new List<SceneAutomationStep>()
            };

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, DeckIdealConfigPath));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(request, true));
            AssetDatabase.Refresh();
        }
    }
}
