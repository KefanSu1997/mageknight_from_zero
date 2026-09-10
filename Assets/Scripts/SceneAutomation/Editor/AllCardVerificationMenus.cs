using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    [InitializeOnLoad]
    public static class AllCardVerificationMenus
    {
        private const string QueueKey = "MageKnight.AllCards.Queue";
        private const string StartKey = "MageKnight.AllCards.Start";
        private const string RootKey = "MageKnight.AllCards.OutputRoot";
        private static readonly string[] Batches = { "basic_card", "advanced_card", "magic", "items" };

        static AllCardVerificationMenus() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Tools/Mage Knight/Original Cards/Build Verification Scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building verification scenes.");
            const string folder = "Assets/Resources/CardVerification";
            const string path = folder + "/Catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CardVerificationCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardVerificationCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.cards = AssetDatabase.FindAssets("t:CardSO", new[] { "Assets/GameData/CardsAssets" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<CardSO>(p))
                .Where(c => c != null).Select(c => new CardVerificationCatalog.Entry
                { source = c, artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + c.Id + ".png") }).ToArray();
            if (catalog.cards.Length != 125) throw new InvalidOperationException("Expected 125 canonical deed card assets, got " + catalog.cards.Length);
            // The generator can update JSON while Unity is unfocused. Do not
            // run an old imported TextAsset against a new disk configuration.
            AssetDatabase.ImportAsset(folder + "/cases.json", ImportAssetOptions.ForceUpdate);
            catalog.cases = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/cases.json");
            if (catalog.cases == null) throw new InvalidOperationException("Run tools/build_all_card_verification.py first.");
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Verification")) AssetDatabase.CreateFolder("Assets/Scenes", "Verification");
            var original = SceneManager.GetActiveScene();
            foreach (string batch in Batches)
            {
                string scenePath = "Assets/Scenes/Verification/" + batch + ".unity";
                if (File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                new GameObject("OriginalCardVerification").AddComponent<OriginalCardVerificationController>().batch = batch;
                EditorSceneManager.SaveScene(scene, scenePath); EditorSceneManager.CloseScene(scene, true);
            }
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            Debug.Log("[AllCards] Built four verification scenes with 125 original card references.");
        }

        [MenuItem("Tools/Mage Knight/Original Cards/Run All Batches")]
        public static void RunAll() { BeginRun(0); RunAt(0); }

        [MenuItem("Tools/Mage Knight/Original Cards/Run Basic Cards")]
        public static void RunBasic() { BeginRun(-1); RunAt(0); }

        [MenuItem("Tools/Mage Knight/Original Cards/Run Advanced Cards")]
        public static void RunAdvanced() { BeginRun(-1); RunAt(1); }

        [MenuItem("Tools/Mage Knight/Original Cards/Run Spells")]
        public static void RunSpells() { BeginRun(-1); RunAt(2); }

        [MenuItem("Tools/Mage Knight/Original Cards/Run Artifacts")]
        public static void RunItems() { BeginRun(-1); RunAt(3); }

        private static void BeginRun(int queue)
        {
            Build();
            string root = "AutomationOutputs/AllOriginalCards/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(root);
            SessionState.SetString(RootKey, root);
            SessionState.SetInt(QueueKey, queue);
            Debug.Log("[AllCards] Output directory: " + root);
        }

        private static void RunAt(int index)
        {
            SessionState.SetString(StartKey, DateTime.UtcNow.ToString("o"));
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "OriginalCards_1920x1080");
            string root = SessionState.GetString(RootKey, "") + "/" + Batches[index];
            Directory.CreateDirectory(root);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(File.ReadAllText("AutomationConfigs/AllOriginalCards/" + Batches[index] + ".json"));
            request.reportPath = root + "/report.json";
            request.screenshotsDirectory = root + "/captures";
            string config = root + "/request.json";
            File.WriteAllText(config, JsonUtility.ToJson(request, true));
            SceneAutomationCommand.RunFromProjectRelativeConfig(config);
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            int index = SessionState.GetInt(QueueKey, -1);
            if (index < 0) return;
            string path = SessionState.GetString(RootKey, "") + "/" + Batches[index] + "/report.json";
            var report = File.Exists(path) ? JsonUtility.FromJson<SceneAutomationReport>(File.ReadAllText(path)) : null;
            bool fresh = report != null && DateTime.TryParse(report.startedAt, out var start)
                && DateTime.TryParse(SessionState.GetString(StartKey, ""), out var expected) && start >= expected;
            if (!fresh || report.status != "success")
            {
                SessionState.SetInt(QueueKey, -1);
                Debug.LogWarning("[AllCards] UI automation failed. Fix input/capture before resuming; effect mismatches are recorded separately.");
                return;
            }
            index++; SessionState.SetInt(QueueKey, index < Batches.Length ? index : -1);
            if (index < Batches.Length) EditorApplication.delayCall += () => RunAt(index);
            else Debug.Log("[AllCards] All scene operations completed. Read effects.json; UI success does not mean all card effects passed.");
        }
    }
}
