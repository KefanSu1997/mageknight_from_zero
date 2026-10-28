using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageKnight.SceneAutomation.Editor
{
    public static class StructuredUiPilotMenu
    {
        [MenuItem("Tools/Mage Knight/Battle/Start Structured UI Pilot")]
        public static void StartPilot()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            // Python writes the desired run directory before invoking this explicit menu.
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = File.ReadAllText(Path.Combine(project, "AutomationOutputs", "structured_ui_pilot_directory.txt")).Trim();
            if (!Path.IsPathRooted(directory)) throw new System.InvalidOperationException("Absolute output directory required.");
            Directory.CreateDirectory(directory);
            SessionState.SetString(StructuredUiPilot.SessionKey, directory);
            SceneAutomationRuntimeState.Clear();
            EditorSceneManager.OpenScene("Assets/Scenes/Part1/Part1_PveBattle.unity", OpenSceneMode.Single);
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "StructuredUiPilot_1920x1080");
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null) EditorWindow.GetWindow(gameViewType).Focus();
            EditorApplication.isPaused = false;
            EditorApplication.EnterPlaymode();
        }
    }
}
