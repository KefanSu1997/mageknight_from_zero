using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MageKnight.SceneAutomation.Editor
{
    internal static class GameViewResolutionUtility
    {
        internal static void ApplyDeckIdealResolutionIfNeeded(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            if (!scenePath.EndsWith("Assets/Scenes/Part1/Part1_DeckIdeal.unity", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TrySetFixedResolution(1378, 1204, "DeckIdeal_1378x1204");
        }

        internal static bool TrySetFixedResolution(int width, int height, string label)
        {
            try
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;
                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
                var sizeType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                var sizeTypeClass = editorAssembly.GetType("UnityEditor.GameViewSize");

                if (gameViewType == null || sizesType == null || groupType == null || sizeType == null || sizeTypeClass == null)
                {
                    Debug.LogWarning("[SceneAutomation] GameView reflection types not found.");
                    return false;
                }

                var singletonType = editorAssembly.GetType("UnityEditor.ScriptableSingleton`1");
                if (singletonType == null)
                {
                    Debug.LogWarning("[SceneAutomation] ScriptableSingleton type not found.");
                    return false;
                }

                var sizesSingletonType = singletonType.MakeGenericType(sizesType);
                var instanceProperty = sizesSingletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                var sizesInstance = instanceProperty?.GetValue(null, null);
                if (sizesInstance == null)
                {
                    Debug.LogWarning("[SceneAutomation] GameViewSizes.instance not available.");
                    return false;
                }

                var standaloneGroup = Enum.Parse(groupType, "Standalone");
                var getGroupMethod = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
                var sizeGroup = getGroupMethod?.Invoke(sizesInstance, new[] { standaloneGroup });
                if (sizeGroup == null)
                {
                    Debug.LogWarning("[SceneAutomation] GameViewSizes.GetGroup returned null.");
                    return false;
                }

                var displayTextsMethod = sizeGroup.GetType().GetMethod("GetDisplayTexts", BindingFlags.Public | BindingFlags.Instance);
                var builtinCountMethod = sizeGroup.GetType().GetMethod("GetBuiltinCount", BindingFlags.Public | BindingFlags.Instance);
                var customCountMethod = sizeGroup.GetType().GetMethod("GetCustomCount", BindingFlags.Public | BindingFlags.Instance);
                var addCustomSizeMethod = sizeGroup.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);

                if (displayTextsMethod == null || builtinCountMethod == null || customCountMethod == null || addCustomSizeMethod == null)
                {
                    Debug.LogWarning("[SceneAutomation] GameViewSizeGroup reflection methods not found.");
                    return false;
                }

                var displayTexts = displayTextsMethod.Invoke(sizeGroup, null) as string[];
                var foundIndex = FindIndex(displayTexts, label);
                if (foundIndex < 0)
                {
                    var fixedResolution = Enum.Parse(sizeType, "FixedResolution");
                    var ctor = sizeTypeClass.GetConstructor(new[] { sizeType, typeof(int), typeof(int), typeof(string) });
                    if (ctor == null)
                    {
                        Debug.LogWarning("[SceneAutomation] GameViewSize constructor not found.");
                        return false;
                    }

                    var newSize = ctor.Invoke(new object[] { fixedResolution, width, height, label });
                    addCustomSizeMethod.Invoke(sizeGroup, new[] { newSize });

                    displayTexts = displayTextsMethod.Invoke(sizeGroup, null) as string[];
                    foundIndex = FindIndex(displayTexts, label);
                }

                if (foundIndex < 0)
                {
                    Debug.LogWarning("[SceneAutomation] Failed to register GameView size.");
                    return false;
                }

                var window = EditorWindow.GetWindow(gameViewType);
                if (window == null)
                {
                    Debug.LogWarning("[SceneAutomation] Failed to get GameView window.");
                    return false;
                }

                var selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selectedSizeIndex == null || !selectedSizeIndex.CanWrite)
                {
                    Debug.LogWarning("[SceneAutomation] GameView.selectedSizeIndex not found.");
                    return false;
                }

                selectedSizeIndex.SetValue(window, foundIndex);
                window.Repaint();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneAutomation] Failed to set GameView size: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static int FindIndex(string[] values, string match)
        {
            if (values == null || values.Length == 0)
            {
                return -1;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], match, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
