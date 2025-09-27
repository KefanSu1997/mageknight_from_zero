using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.SceneAutomation.Editor
{
    internal static class SceneAutomationQuickMenus
    {
        private const string DeckManaConfigRelativePath = "AutomationConfigs/part1_deckmana.json";
        private const string DeckButtonPath = "Canvas/Part1TestLayout/ControlColumn/Buttons/测试牌库系统Button";
        private const string ManaButtonPath = "Canvas/Part1TestLayout/ControlColumn/Buttons/测试魔力池Button";

        [MenuItem("Tools/Scene Automation/Run DeckMana Automation", priority = 2010)]
        private static void RunDeckManaAutomation()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig(DeckManaConfigRelativePath);
        }

        [MenuItem("Tools/Scene Automation/Debug/List Runtime Buttons", priority = 2090)]
        private static void ListRuntimeButtons()
        {
            var buttons = Object.FindObjectsOfType<Button>();
            if (buttons.Length == 0)
            {
                Debug.Log("[SceneAutomation] No Button components found in scene.");
                return;
            }

            foreach (var button in buttons)
            {
                Debug.Log($"[SceneAutomation] Button: {GetHierarchyPath(button.transform)}");
            }
        }

        [MenuItem("Tools/Scene Automation/Debug/Log Pending Request", priority = 2095)]
        private static void LogPendingRequest()
        {
            var request = SceneAutomationRuntimeState.PendingRequest;
            if (request == null)
            {
                Debug.Log("[SceneAutomation] Pending request: <none>");
                return;
            }

            Debug.Log($"[SceneAutomation] Pending request scene: {request.scenePath}, steps: {request.steps?.Count ?? 0}");
        }

        [MenuItem("Tools/Scene Automation/Debug/Test Deck Button Path", priority = 2096)]
        private static void TestDeckButtonPath()
        {
            var go = GameObject.Find(DeckButtonPath);
            Debug.Log(go != null
                ? $"[SceneAutomation] Found deck button path: {DeckButtonPath}"
                : $"[SceneAutomation] Deck button missing: {DeckButtonPath}");

            var mana = GameObject.Find(ManaButtonPath);
            Debug.Log(mana != null
                ? $"[SceneAutomation] Found mana button path: {ManaButtonPath}"
                : $"[SceneAutomation] Mana button missing: {ManaButtonPath}");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
