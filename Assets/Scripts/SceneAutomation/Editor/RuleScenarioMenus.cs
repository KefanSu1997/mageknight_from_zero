using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;

namespace MageKnight.SceneAutomation.Editor
{
    [InitializeOnLoad]
    public static class RuleScenarioMenus
    {
        private const string QueueKey = "MageKnight.RulesSuite.Index";
        private const string StartKey = "MageKnight.RulesSuite.ExpectedStart";
        private const string RootKey = "MageKnight.RulesSuite.OutputRoot";
        private static readonly string[] Names = { "Combat", "Exploration", "Recruitment", "Journey" };
        private static TestRunnerApi _testRunner;

        static RuleScenarioMenus() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("Tools/Mage Knight/Rules/Create or Locate Scenes")]
        public static void EnsureScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before creating scenes.");
            var original = SceneManager.GetActiveScene();
            for (int i = 0; i < Names.Length; i++)
            {
                string path = "Assets/Scenes/Part1/Part1_" + Names[i] + "Rules.unity";
                if (File.Exists(path)) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                new GameObject("RulesScenario").AddComponent<RuleScenarioController>().Kind = (RuleScenarioKind)i;
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
            }
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Mage Knight/Rules/Run All Four")]
        [MenuItem("Tools/Mage Knight/Adventure/Run All Four")]
        public static void RunAll()
        {
            BeginRun(0); RunAt(0);
        }

        [MenuItem("Tools/Mage Knight/Rules/Run Combat")]
        public static void RunCombat() => RunSingle(0);
        [MenuItem("Tools/Mage Knight/Rules/Run Exploration")]
        public static void RunExploration() => RunSingle(1);
        [MenuItem("Tools/Mage Knight/Rules/Run Recruitment")]
        public static void RunRecruitment() => RunSingle(2);
        [MenuItem("Tools/Mage Knight/Rules/Run Journey")]
        public static void RunJourney() => RunSingle(3);

        private static void RunSingle(int index)
        {
            BeginRun(-1); RunAt(index);
        }

        private static string NewOutputDirectory(string category)
        {
            string directory = "AutomationOutputs/" + category + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(directory);
            Debug.Log("[RulesSuite] Output directory: " + directory);
            return directory;
        }

        private static void BeginRun(int queue)
        {
            EnsureScenes();
            SessionState.SetString(RootKey, NewOutputDirectory("AdventureRegression"));
            SessionState.SetInt(QueueKey, queue);
        }

        private static void RunAt(int index)
        {
            SessionState.SetString(StartKey, DateTime.UtcNow.ToString("o"));
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "Rules_1920x1080");
            string directory = SessionState.GetString(RootKey, "") + "/" + Names[index];
            Directory.CreateDirectory(directory);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(File.ReadAllText("AutomationConfigs/adventure_" + Names[index].ToLowerInvariant() + ".json"));
            request.reportPath = directory + "/report.json";
            request.screenshotsDirectory = directory + "/captures";
            string config = directory + "/request.json";
            File.WriteAllText(config, JsonUtility.ToJson(request, true));
            SceneAutomationCommand.RunFromProjectRelativeConfig(config);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            int index = SessionState.GetInt(QueueKey, -1);
            if (index < 0) return;
            string path = SessionState.GetString(RootKey, "") + "/" + Names[index] + "/report.json";
            var report = File.Exists(path) ? JsonUtility.FromJson<SceneAutomationReport>(File.ReadAllText(path)) : null;
            bool fresh = report != null && DateTime.TryParse(report.startedAt, out var start)
                && DateTime.TryParse(SessionState.GetString(StartKey, ""), out var expected) && start >= expected;
            if (!fresh || report.status != "success")
            {
                SessionState.SetInt(QueueKey, -1);
                Debug.LogWarning("[RulesSuite] Stopped at failed or interrupted scenario: " + Names[index]);
                return;
            }
            index++;
            SessionState.SetInt(QueueKey, index < Names.Length ? index : -1);
            if (index < Names.Length) EditorApplication.delayCall += () => RunAt(index);
            else Debug.Log("[RulesSuite] Four scenarios completed. Inspect reports and screenshots before visual acceptance.");
        }

        [MenuItem("Tools/Mage Knight/Rules/Run Rule Tests")]
        public static void RunRuleTests() => RunTests(false);
        [MenuItem("Tools/Mage Knight/Rules/Run All EditMode Tests")]
        [MenuItem("Tools/Mage Knight/Adventure/Run All EditMode Tests")]
        public static void RunAllTests() => RunTests(true);

        [MenuItem("Tools/Mage Knight/Rules/Verify Wrong Expectation Fails")]
        public static void VerifyWrongExpectation()
        {
            SessionState.SetInt(QueueKey, -1);
            GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "Rules_1920x1080");
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/adventure_expected_failure.json");
        }

        private static void RunTests(bool all)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before running tests.");
            _testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            _testRunner.RegisterCallbacks(new TestResults(NewOutputDirectory("EditMode")));
            var filter = new Filter { testMode = TestMode.EditMode };
            if (!all) filter.groupNames = new[] { "^MK.Tests.Rules.", "^MK.Tests.Adventure.", "^MK.Tests.map.TerrainCostTests" };
            _testRunner.Execute(new ExecutionSettings(filter));
        }

        private sealed class TestResults : ICallbacks
        {
            private readonly string _directory;
            public TestResults(string directory) => _directory = directory;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.Combine(_directory, "editmode-results.xml"));
                File.WriteAllText(Path.Combine(_directory, "editmode-summary.json"),
                    "{\"passed\":" + result.PassCount + ",\"failed\":" + result.FailCount + ",\"skipped\":" + result.SkipCount + "}");
                Debug.Log($"[RulesTests] Passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}.");
            }
        }
    }
}
