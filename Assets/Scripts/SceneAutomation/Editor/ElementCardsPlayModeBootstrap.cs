using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    [InitializeOnLoad]
    public static class ElementCardsPlayModeBootstrap
    {
        private const string TargetSceneName = "Part1_ElementCardsShowcase";

        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions _)
        {
            EditorApplication.delayCall += TryBootstrap;
        }

        private static void TryBootstrap()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != TargetSceneName)
            {
                return;
            }

            ElementCardsShowcaseBootstrapper.EnsureSetup();
            Debug.Log("[ElementCardsPlayModeBootstrap] EnsureSetup invoked.");
        }
    }
}
