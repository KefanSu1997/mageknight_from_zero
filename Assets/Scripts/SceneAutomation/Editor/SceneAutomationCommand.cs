using System;
using System.IO;
using MageKnight.SceneAutomation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageKnight.SceneAutomation.Editor
{
    /// <summary>
    /// 提供命令行与菜单入口以执行场景自动化流程。
    /// </summary>
    public static class SceneAutomationCommand
    {
        private const string ConfigArg = "--scene-automation-config";
        private const string SceneArg = "--scene";

        private static bool _isRunning;
        private static bool _completed;

        private const string SessionRunningKey = "MageKnight.SceneAutomation.IsRunning";

        [InitializeOnLoadMethod]
        private static void RestoreSession()
        {
            if (!SessionState.GetBool(SessionRunningKey, false))
            {
                return;
            }

            if (SceneAutomationRuntimeState.PendingRequest == null)
            {
                SessionState.SetBool(SessionRunningKey, false);
                return;
            }

            SceneAutomationRuntimeState.AutomationCompleted += OnAutomationCompleted;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _isRunning = true;
            _completed = false;
        }

        [MenuItem("Tools/Scene Automation/Run From Config...")]
        private static void RunFromMenu()
        {
            var configPath = EditorUtility.OpenFilePanel("选择自动化配置", Application.dataPath, "json");
            if (string.IsNullOrEmpty(configPath))
            {
                return;
            }

            RunWithConfig(configPath, Environment.GetCommandLineArgs());
        }

        /// <summary>
        /// 命令行入口，使用 -executeMethod 调用。
        /// </summary>
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var configPath = GetOptionValue(args, ConfigArg);
            if (string.IsNullOrEmpty(configPath))
            {
                Debug.LogError($"缺少配置文件参数 {ConfigArg}");
                ExitWithCode(1);
                return;
            }

            RunWithConfig(configPath, args);
        }

        public static void RunFromProjectRelativeConfig(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                Debug.LogError("缺少配置文件路径");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));

            RunWithConfig(fullPath, Environment.GetCommandLineArgs());
        }

        private static void RunWithConfig(string configPath, string[] args)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("请在编辑模式下启动自动化执行");
                ExitWithCode(1);
                return;
            }

            try
            {
                var request = LoadRequest(configPath);
                var sceneOverride = GetOptionValue(args, SceneArg);
                if (!string.IsNullOrEmpty(sceneOverride))
                {
                    request.scenePath = sceneOverride;
                }

                var scenePath = ValidateScenePath(request.scenePath);
                if (string.IsNullOrEmpty(scenePath))
                {
                    throw new InvalidOperationException($"场景路径无效：{request.scenePath}");
                }

                request.scenePath = scenePath;

                SceneAutomationRuntimeState.SetRequest(request);
                Debug.Log($"[SceneAutomation] Config prepared: {scenePath} (steps: {request.steps.Count})");
                SceneAutomationRuntimeState.AutomationCompleted += OnAutomationCompleted;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

                EditorPrefs.SetBool("kPauseOnPlay", false);
                EditorApplication.isPaused = false;
                _isRunning = true;
                _completed = false;
                SessionState.SetBool(SessionRunningKey, true);

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex)
            {
                CleanupHandlers();
                SceneAutomationRuntimeState.Clear();
                Debug.LogException(ex);
                ExitWithCode(1);
            }
        }

        private static SceneAutomationRequest LoadRequest(string configPath)
        {
            var fullPath = ResolvePath(configPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("未找到配置文件", fullPath);
            }

            var json = File.ReadAllText(fullPath);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(json);
            if (request == null)
            {
                throw new InvalidDataException("配置文件解析失败");
            }

            if (request.steps == null || request.steps.Count == 0)
            {
                Debug.LogWarning("配置未包含任何步骤，将仅执行初始截图（如启用）");
                request.steps = new();
            }

            if (string.IsNullOrWhiteSpace(request.reportPath))
            {
                request.reportPath = Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, "scene-automation-report.json");
            }

            return request;
        }

        private static string ResolvePath(string rawPath)
        {
            if (Path.IsPathRooted(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, rawPath));
        }

        private static string ValidateScenePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return null;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var scenePath = rawPath.Replace("\\", "/");

            if (!scenePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolvePath(rawPath);
                if (resolved.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = resolved.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    scenePath = relative.Replace("\\", "/");
                }
                else
                {
                    scenePath = resolved.Replace("\\", "/");
                }
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            return sceneAsset == null ? null : scenePath;
        }

        private static void OnAutomationCompleted(SceneAutomationReport report)
        {
            _completed = true;
            CleanupHandlers();
            SceneAutomationRuntimeState.Clear();

            LogReportSummary(report);
            var exitCode = report != null && string.Equals(report.status, "success", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            ExitWithCode(exitCode);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!_isRunning)
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode && !_completed)
            {
                Debug.LogError("播放模式提前结束，自动化执行未完成");
                CleanupHandlers();
                SceneAutomationRuntimeState.Clear();
                ExitWithCode(1);
            }
        }

        private static void CleanupHandlers()
        {
            SceneAutomationRuntimeState.AutomationCompleted -= OnAutomationCompleted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _isRunning = false;
            SessionState.SetBool(SessionRunningKey, false);
        }

        private static void LogReportSummary(SceneAutomationReport report)
        {
            if (report == null)
            {
                Debug.LogError("未收到自动化报告");
                return;
            }

            Debug.Log($"[SceneAutomation] 场景：{report.scenePath} 状态：{report.status} 步骤数：{report.steps.Count}");
            foreach (var step in report.steps)
            {
                var status = step.success ? "[OK]" : "[FAIL]";
                var message = string.IsNullOrWhiteSpace(step.message) ? string.Empty : $" {step.message}";
                Debug.Log($"  {status} {step.label} -> {step.screenshotPath}{message}");
            }
        }

        private static string GetOptionValue(string[] args, string option)
        {
            if (args == null || args.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], option, StringComparison.Ordinal))
                {
                    continue;
                }

                var nextIndex = i + 1;
                if (nextIndex < args.Length)
                {
                    return args[nextIndex];
                }
            }

            return null;
        }

        private static void ExitWithCode(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
