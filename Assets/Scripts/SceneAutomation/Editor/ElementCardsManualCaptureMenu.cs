using MageKnight.SceneAutomation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    public static class ElementCardsManualCaptureMenu
    {
        private const string TargetSceneName = "Part1_ElementCardsShowcase";

        [MenuItem("Tools/Scene Automation/Queue ElementCards Manual Capture")]
        private static void QueueRun()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ElementCardsManualCapture] Queue from Edit Mode only.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != TargetSceneName)
            {
                Debug.LogError($"[ElementCardsManualCapture] Load scene '{TargetSceneName}' before queueing.");
                return;
            }

            SessionState.SetBool(ElementCardsManualCaptureRunner.PendingSessionKey, true);
            Debug.Log("[ElementCardsManualCapture] Queued. Enter Play Mode to run.");
        }

        [MenuItem("Tools/Scene Automation/Run ElementCards Manual Capture (PlayMode)")]
        private static void RunInPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[ElementCardsManualCapture] Enter Play Mode before running manual capture.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != TargetSceneName)
            {
                Debug.LogError($"[ElementCardsManualCapture] Active scene must be '{TargetSceneName}'.");
                return;
            }

            var runner = Object.FindFirstObjectByType<ElementCardsManualCaptureRunner>();
            if (runner == null)
            {
                var host = new GameObject("ElementCardsManualCaptureRunner");
                runner = host.AddComponent<ElementCardsManualCaptureRunner>();
            }

            runner.Begin();
            Debug.Log("[ElementCardsManualCapture] Started.");
        }
    }
}
