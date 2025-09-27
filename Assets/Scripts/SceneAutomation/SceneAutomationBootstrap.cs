using UnityEngine;

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
    }
}
