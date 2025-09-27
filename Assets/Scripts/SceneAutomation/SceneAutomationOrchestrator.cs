using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 在播放模式中执行按钮点击、截图与报告输出。
    /// </summary>
    public class SceneAutomationOrchestrator : MonoBehaviour
    {
        private SceneAutomationRequest _request;
        private SceneAutomationReport _report;
        private string _screenshotsDirectory;
        private bool _hasError;

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            _request = SceneAutomationRuntimeState.PendingRequest;
            if (_request == null)
            {
                Debug.LogWarning("[SceneAutomation] No pending request detected.");
                yield return null;
                Cleanup();
                yield break;
            }

            Debug.Log("[SceneAutomation] Runner started.");
            Debug.Log($"[SceneAutomation] Time scale at start: {Time.timeScale}");


            _report = new SceneAutomationReport
            {
                scenePath = _request.scenePath,
                startedAt = DateTime.UtcNow.ToString("o"),
                status = "running"
            };

            var baseScreenshotsDirectory = ResolveDirectory(_request.screenshotsDirectory);
            EnsureDirectory(baseScreenshotsDirectory);

            var sceneName = SanitizeFileName(Path.GetFileNameWithoutExtension(_request.scenePath) ?? "Scene");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var runFolderName = $"{sceneName}_{timestamp}";

            _screenshotsDirectory = Path.Combine(baseScreenshotsDirectory, runFolderName);
            EnsureDirectory(_screenshotsDirectory);

            if (_request.initialDelaySeconds > 0f)
            {
                Debug.LogWarning("[SceneAutomation] initialDelaySeconds is currently ignored; encode waits as explicit steps if needed.");
            }

            if (_request.captureInitialView)
            {
                yield return CaptureAndRecordStep("Scene Start", string.Empty, success: true);
            }

            Debug.Log($"[SceneAutomation] Total steps: {_request.steps?.Count ?? 0}");

            foreach (var step in _request.steps)
            {
                Debug.Log($"[SceneAutomation] Starting step: {step.label}");
                yield return ExecuteStep(step);
            }

            _report.status = _hasError ? "failed" : "success";
            if (_hasError && string.IsNullOrEmpty(_report.message))
            {
                _report.message = "One or more steps failed.";
            }

            _report.finishedAt = DateTime.UtcNow.ToString("o");

            yield return WriteReport();
            Debug.Log("[SceneAutomation] Automation finished.");
            FinalizeAutomation();
        }

        private IEnumerator ExecuteStep(SceneAutomationStep step)
        {
            var label = string.IsNullOrWhiteSpace(step.label) ? step.buttonPath : step.label;
            var record = new SceneAutomationReportStep
            {
                label = label,
                buttonPath = step.buttonPath
            };

            Debug.Log($"[SceneAutomation] Executing step: {label}");

            if (string.IsNullOrWhiteSpace(step.buttonPath))
            {
                record.success = false;
                record.message = "buttonPath is empty";
                if (string.IsNullOrEmpty(_report.message))
                {
                    _report.message = record.message;
                }

                _hasError = true;
                yield return CaptureAndRecordStep(label, step.buttonPath, false, record);
                yield break;
            }

            var button = FindButton(step.buttonPath);
            if (button == null)
            {
                record.success = false;
                record.message = "Button not found";
                Debug.LogWarning($"[SceneAutomation] Button not found: {step.buttonPath}");
                if (string.IsNullOrEmpty(_report.message))
                {
                    _report.message = record.message;
                }

                _hasError = true;
                yield return CaptureAndRecordStep(label, step.buttonPath, false, record);
                yield break;
            }

            yield return ExecuteClick(button);
            Debug.Log($"[SceneAutomation] Clicked button: {label}");

            var waitSeconds = step.waitAfterSeconds > 0f ? step.waitAfterSeconds : _request.defaultWaitAfterSeconds;
            if (waitSeconds > 0f)
            {
                Debug.Log($"[SceneAutomation] Step wait requested: {waitSeconds:F3}s (ignored)");
            }

            yield return null;
            yield return CaptureAndRecordStep(label, step.buttonPath, true, record);
        }

        private IEnumerator ExecuteClick(Button button)
        {
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            button.onClick?.Invoke();
            yield return null;
        }

        private IEnumerator CaptureAndRecordStep(string label, string buttonPath, bool success, SceneAutomationReportStep record = null)
        {
            record ??= new SceneAutomationReportStep
            {
                label = label,
                buttonPath = buttonPath
            };

            record.success = success;

            yield return new WaitForEndOfFrame();

            var fileName = BuildScreenshotFileName(label);
            var filePath = Path.Combine(_screenshotsDirectory, fileName);

            ScreenCapture.CaptureScreenshot(filePath);
            record.screenshotPath = filePath;
            record.message = success ? "" : record.message;

            _report.steps.Add(record);
            Debug.Log($"[SceneAutomation] Step complete: {record.label} (success: {record.success})");

            yield return null;
        }

        private string BuildScreenshotFileName(string label)
        {
            var index = _report.steps.Count;
            var safeLabel = SanitizeFileName(string.IsNullOrWhiteSpace(label) ? "step" : label);
            return $"{index:000}_{safeLabel}.png";
        }

        private static string SanitizeFileName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(Array.IndexOf(InvalidFileNameChars, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }

        private Button FindButton(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var go = GameObject.Find(path);
            if (go == null)
            {
                return null;
            }

            return go.GetComponent<Button>();
        }

        private IEnumerator WriteReport()
        {
            var reportPath = ResolvePath(_request.reportPath);
            EnsureDirectory(Path.GetDirectoryName(reportPath));

            var json = JsonUtility.ToJson(_report, true);
            File.WriteAllText(reportPath, json);
            yield return null;
        }

        private static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Application.dataPath, "../AutomationReports");
            }

            return ResolvePath(directory);
        }

        private static string ResolvePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return rawPath;
            }

            if (Path.IsPathRooted(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, rawPath));
        }

        private static void EnsureDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void FinalizeAutomation()
        {
            SceneAutomationRuntimeState.ReportCompleted(_report);
            Cleanup();

#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
            else
            {
                EditorApplication.ExitPlaymode();
            }
#endif
        }

        private void Cleanup()
        {
            SceneAutomationRuntimeState.Clear();
            Destroy(gameObject);
        }
    }
}
