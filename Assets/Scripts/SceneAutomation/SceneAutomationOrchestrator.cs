using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
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
        private const string DeckIdealSceneSuffix = "Assets/Scenes/Part1/Part1_DeckIdeal.unity";

        private SceneAutomationRequest _request;
        private SceneAutomationReport _report;
        private string _screenshotsDirectory;
        private bool _hasError;
        private float _startedRealtime;
        private bool _finished;
        private string _runtimeError;

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnRuntimeLog;
            _startedRealtime = Time.realtimeSinceStartup;
        }

        private void OnDestroy() => Application.logMessageReceived -= OnRuntimeLog;

        private void OnRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _hasError = true;
            _runtimeError ??= condition;
        }

        private void Update()
        {
            if (_finished || _request == null || _report == null) return;
            if (Time.realtimeSinceStartup - _startedRealtime <= Mathf.Max(10, _request.maxRunSeconds)) return;
            StopAllCoroutines();
            _hasError = true; _finished = true;
            _report.status = "failed"; _report.finishedAt = DateTime.UtcNow.ToString("o");
            _report.message = "Automation timed out; last runtime error: " + (_runtimeError ?? "none");
            string path = ResolvePath(_request.reportPath); EnsureDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(_report, true));
            FinalizeAutomation();
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

            if (_request.useRunSubfolder)
            {
                var sceneName = SanitizeFileName(Path.GetFileNameWithoutExtension(_request.scenePath) ?? "Scene");
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var runFolderName = $"{sceneName}_{timestamp}";

                _screenshotsDirectory = Path.Combine(baseScreenshotsDirectory, runFolderName);
                EnsureDirectory(_screenshotsDirectory);
            }
            else
            {
                _screenshotsDirectory = baseScreenshotsDirectory;
            }

            if (_request.initialDelaySeconds > 0f)
            {
                Debug.Log($"[SceneAutomation] Waiting initial delay: {_request.initialDelaySeconds:F3}s");
                yield return new WaitForSecondsRealtime(_request.initialDelaySeconds);
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
                if (_hasError) break;
            }

            _report.status = _hasError ? "failed" : "success";
            if (_hasError && string.IsNullOrEmpty(_report.message))
            {
                _report.message = _runtimeError ?? "One or more steps failed.";
            }

            _report.finishedAt = DateTime.UtcNow.ToString("o");

            yield return WriteReport();
            _finished = true;
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
            var source = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<ISceneAutomationStateSource>().SingleOrDefault();
            var before = source?.ReadAutomationState() ?? new Dictionary<string, string>();
            record.beforeState = Snapshot(before);
            if (!CheckExpectations(step.before, before, "before", record))
            {
                _hasError = true;
                yield return CaptureAndRecordStep(label, step.buttonPath, false, record);
                yield break;
            }

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

            if (!TryPointerClick(button, record))
            {
                _hasError = true;
                yield return CaptureAndRecordStep(label, step.buttonPath, false, record);
                yield break;
            }
            Debug.Log($"[SceneAutomation] Clicked button: {label}");

            var waitSeconds = step.waitAfterSeconds >= 0f ? step.waitAfterSeconds : _request.defaultWaitAfterSeconds;
            if (waitSeconds < 0f)
            {
                waitSeconds = 0f;
            }

            if (waitSeconds > 0f)
            {
                Debug.Log($"[SceneAutomation] Waiting after step: {waitSeconds:F3}s");
                yield return new WaitForSecondsRealtime(waitSeconds);
            }


            yield return null;
            var after = source?.ReadAutomationState() ?? new Dictionary<string, string>();
            record.afterState = Snapshot(after);
            record.runtimeRule = source?.LastRule;
            bool passed = CheckExpectations(step.after, after, "after", record);
            if (!passed) _hasError = true;
            yield return CaptureAndRecordStep(label, step.buttonPath, passed, record, step.skipScreenshot && passed);
        }

        private static List<SceneAutomationStateValue> Snapshot(Dictionary<string, string> state) =>
            state.Select(pair => new SceneAutomationStateValue { key = pair.Key, value = pair.Value }).ToList();

        private static bool CheckExpectations(List<SceneAutomationExpectation> expected,
            Dictionary<string, string> state, string phase, SceneAutomationReportStep record)
        {
            bool allPassed = true;
            foreach (var item in expected ?? new List<SceneAutomationExpectation>())
            {
                string actual = state.TryGetValue(item.key, out var value) ? value : "<missing>";
                bool passed = actual == item.expected;
                record.assertions.Add(new SceneAutomationAssertionResult
                { phase = phase, key = item.key, expected = item.expected, actual = actual, rule = item.rule, passed = passed });
                if (!passed)
                {
                    allPassed = false;
                    record.message += $"{phase} {item.key}: expected {item.expected}, got {actual}. ";
                }
            }
            return allPassed;
        }

        private static bool TryPointerClick(Button button, SceneAutomationReportStep record)
        {
            try { return PointerClick(button, record); }
            catch (Exception ex)
            {
                record.message = $"Pointer input failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        private static bool PointerClick(Button button, SceneAutomationReportStep record)
        {
            if (EventSystem.current == null || !button.isActiveAndEnabled || !button.IsInteractable())
            { record.message = "No EventSystem, or button is inactive/disabled."; return false; }
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            if (point.x < 0 || point.y < 0 || point.x >= Screen.width || point.y >= Screen.height)
            { record.message = "Button center is outside Game View."; return false; }
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            record.clickX = point.x; record.clickY = point.y;
            if (hits.Count == 0) { record.message = "Pointer raycast missed the UI."; return false; }
            record.hitObject = hits[0].gameObject.name;
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (handler != button.gameObject)
            { record.message = $"Button is obscured by {record.hitObject}."; return false; }
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private IEnumerator CaptureAndRecordStep(string label, string buttonPath, bool success, SceneAutomationReportStep record = null, bool skipScreenshot = false)
        {
            record ??= new SceneAutomationReportStep
            {
                label = label,
                buttonPath = buttonPath
            };

            record.success = success;

            if (skipScreenshot)
            {
                _report.steps.Add(record);
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            var stepIndex = _report.steps.Count;
            var fileName = BuildScreenshotFileName(label);
            var filePath = Path.Combine(_screenshotsDirectory, fileName);

            try
            {
                var png = CapturePng(out var isBlank);
                File.WriteAllBytes(filePath, png);
                record.screenshotPath = filePath;
                if (isBlank)
                    throw new InvalidOperationException("Screenshot contains only a uniform color; visual verification failed.");

                WriteDeckIdealAliasesIfNeeded(stepIndex, png);
                if (record.success)
                    record.message = string.Empty;
            }
            catch (Exception ex)
            {
                record.success = false;
                record.message = string.IsNullOrEmpty(record.message)
                    ? $"Screenshot failed: {ex.Message}"
                    : $"{record.message}; Screenshot failed: {ex.Message}";
                _hasError = true;
                if (string.IsNullOrEmpty(_report.message))
                    _report.message = record.message;
                Debug.LogWarning($"[SceneAutomation] {record.message}");
            }

            _report.steps.Add(record);
            Debug.Log($"[SceneAutomation] Step complete: {record.label} (success: {record.success})");

            yield return null;
        }

        private byte[] CapturePng(out bool isBlank)
        {
            // 在帧渲染结束后捕获实际 Game View，包含 URP 和 Overlay Canvas。
            // Camera.Render() 在 SRP 中不能替代最终帧，也会漏掉屏幕空间 UI。
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            isBlank = true;
            try
            {
                if (texture == null || texture.width < 2 || texture.height < 2)
                    throw new InvalidOperationException("Game View did not produce a valid screenshot.");

                var pixels = texture.GetPixels32();
                var first = pixels[0];
                for (var i = 1; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    if (pixel.r != first.r || pixel.g != first.g || pixel.b != first.b)
                    {
                        isBlank = false;
                        break;
                    }
                }

                var png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("Screenshot PNG encoding produced no data.");
                return png;
            }
            finally
            {
                if (texture != null)
                    Destroy(texture);
            }
        }

        private bool IsDeckIdealRequest()
        {
            return _request != null
                && !string.IsNullOrWhiteSpace(_request.scenePath)
                && _request.scenePath.Replace('\\', '/').EndsWith(DeckIdealSceneSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private void WriteDeckIdealAliasesIfNeeded(int stepIndex, byte[] png)
        {
            if (!IsDeckIdealRequest() || png == null || png.Length == 0)
            {
                return;
            }

            if (stepIndex != 0)
            {
                return;
            }

            File.WriteAllBytes(Path.Combine(_screenshotsDirectory, "deck_ideal_overview.png"), png);
            File.WriteAllBytes(Path.Combine(_screenshotsDirectory, "deck_ideal_automation_000.png"), png);
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
