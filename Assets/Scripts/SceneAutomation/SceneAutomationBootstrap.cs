using UnityEngine;
using System.IO;

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 播放模式加载后在场景中注入自动化执行器。
    /// </summary>
    public static class SceneAutomationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOrchestrator()
        {
            // Manual capture flow must not be preempted by SceneAutomationOrchestrator.
            if (IsManualCaptureRequested())
            {
                if (SceneAutomationRuntimeState.PendingRequest != null)
                {
                    SceneAutomationRuntimeState.Clear();
                    Debug.LogWarning("[SceneAutomationBootstrap] Cleared stale pending request for manual capture flow.");
                }

                return;
            }

            if (SceneAutomationRuntimeState.PendingRequest == null)
            {
                return;
            }

            var orchestrator = Object.FindObjectOfType<SceneAutomationOrchestrator>();
            if (orchestrator != null)
            {
                return;
            }

            var host = new GameObject("SceneAutomationOrchestrator");
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<SceneAutomationOrchestrator>();
        }

        private static bool IsManualCaptureRequested()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("MageKnight.ElementCardsManualCapture.Pending", false))
            {
                return true;
            }
#endif

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var triggerPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                "multi-agent-workspace/runs/T-20251028-020/manual_capture_trigger.flag"));
            return File.Exists(triggerPath);
        }
    }
}
