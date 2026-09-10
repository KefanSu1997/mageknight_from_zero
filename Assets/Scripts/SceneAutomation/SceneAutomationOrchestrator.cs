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
        private const int DeckIdealCaptureWidth = 1378;
        private const int DeckIdealCaptureHeight = 1204;
        private const string DeckIdealSceneSuffix = "Assets/Scenes/Part1/Part1_DeckIdeal.unity";

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

            Canvas.ForceUpdateCanvases();

            var stepIndex = _report.steps.Count;
            var fileName = BuildScreenshotFileName(label);
            var filePath = Path.Combine(_screenshotsDirectory, fileName);

            var png = CapturePng();
            if (png != null && png.Length > 0)
            {
                File.WriteAllBytes(filePath, png);
                WriteDeckIdealAliasesIfNeeded(stepIndex, png);
            }
            else
            {
                ScreenCapture.CaptureScreenshot(filePath);
            }
            record.screenshotPath = filePath;
            record.message = success ? "" : record.message;

            _report.steps.Add(record);
            Debug.Log($"[SceneAutomation] Step complete: {record.label} (success: {record.success})");

            yield return null;
        }

        private byte[] CapturePng()
        {
            var camera = FindCaptureCamera();
            if (camera == null)
            {
                Debug.LogWarning("[SceneAutomation] No camera found for deterministic capture; falling back to ScreenCapture.");
                return null;
            }

            var (width, height) = GetCaptureDimensions(camera);
            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning($"[SceneAutomation] Invalid capture size ({width}x{height}); falling back to ScreenCapture.");
                return null;
            }

            var renderTexture = new RenderTexture(width, height, 24);
            renderTexture.Create();

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousClearFlags = camera.clearFlags;

            camera.targetTexture = renderTexture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            var png = texture.EncodeToPNG();

            camera.targetTexture = previousTarget;
            camera.clearFlags = previousClearFlags;
            RenderTexture.active = previousActive;

            Destroy(texture);
            renderTexture.Release();
            Destroy(renderTexture);

            return png;
        }

        private (int width, int height) GetCaptureDimensions(Camera camera)
        {
            if (IsDeckIdealRequest())
            {
                return (DeckIdealCaptureWidth, DeckIdealCaptureHeight);
            }

            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            if (camera != null && camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
            }

            return (width, height);
        }

        private Camera FindCaptureCamera()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameras == null || cameras.Length == 0)
            {
                return null;
            }

            foreach (var cam in cameras)
            {
                if (cam != null && cam.enabled && string.Equals(cam.name, "DeckUICamera", StringComparison.Ordinal))
                {
                    return cam;
                }
            }

            if (Camera.main != null && Camera.main.enabled)
            {
                return Camera.main;
            }

            foreach (var cam in cameras)
            {
                if (cam != null && cam.enabled)
                {
                    return cam;
                }
            }

            return cameras[0];
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
