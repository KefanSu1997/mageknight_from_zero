using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 跨编辑器与运行时共享的自动化执行状态。
    /// </summary>
    public static class SceneAutomationRuntimeState
    {
#if UNITY_EDITOR
        private const string SessionKey = "MageKnight.SceneAutomation.PendingRequest";
#endif

        static SceneAutomationRuntimeState()
        {
#if UNITY_EDITOR
            var json = SessionState.GetString(SessionKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                PendingRequest = JsonUtility.FromJson<SceneAutomationRequest>(json);
            }
#endif
        }

        public static SceneAutomationRequest PendingRequest { get; private set; }

        public static event Action<SceneAutomationReport> AutomationCompleted;

        public static void SetRequest(SceneAutomationRequest request)
        {
            PendingRequest = request;
#if UNITY_EDITOR
            if (request != null)
            {
                SessionState.SetString(SessionKey, JsonUtility.ToJson(request));
            }
            else
            {
                SessionState.SetString(SessionKey, string.Empty);
            }
#endif
        }

        public static void Clear()
        {
            SetRequest(null);
        }

        public static void ReportCompleted(SceneAutomationReport report)
        {
            AutomationCompleted?.Invoke(report);
        }
    }
}
