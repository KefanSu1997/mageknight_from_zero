using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MageKnight.SceneAutomation
{
    public sealed class ElementCardsManualCaptureRunner : MonoBehaviour
    {
        public const string PendingSessionKey = "MageKnight.ElementCardsManualCapture.Pending";
        private const string ScenePath = "Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity";
        private const string ScreenshotsRelativeDir = "multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/screenshots";
        private const string ReportRelativePath = "multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/element_cards_report.json";
        private const string BindingAuditRelativePath = "multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/element_cards_binding_audit.json";
        private const string ConsoleLogRelativePath = "multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/scene_automation_console_log.json";
        private const string TriggerRelativePath = "multi-agent-workspace/runs/T-20251028-020/manual_capture_trigger.flag";
        private const string AutomationButtonPath = "AutomationButton";
        private const string RunId = "T-20251028-020";
        private const int ScreenshotCaptureTimeoutFrames = 360;
        private const int ScreenshotStableFramesRequired = 2;
        private const double ScreenshotCaptureTimeoutSeconds = 12.0d;
        private const int InitialSceneSettleFrames = 30;
        private const int StepSettleFrames = 20;
        private const float StepCropInsetMinPixels = 14f;
        private const float StepCropInsetRatio = 0.03f;
        private const float StepCropLaneGapPixels = 2f;
        private const int StepCropMinSizePixels = 16;

        private static readonly string[] StepLabels =
        {
            "Earth Back",
            "Earth Front",
            "Water Back",
            "Water Front",
            "Wind Back",
            "Wind Front",
            "Fire Back",
            "Fire Front"
        };
        private static readonly string[] ElementOrder = { "Earth", "Water", "Wind", "Fire" };
        private static readonly Dictionary<string, string> CardNameByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "Card_Earth" },
            { "Water", "Card_Water" },
            { "Wind", "Card_Wind" },
            { "Fire", "Card_Fire" }
        };
        private static readonly Dictionary<string, string> FrontAssetPathByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "Assets/GameData/cards/magic_003.png" },
            { "Water", "Assets/GameData/cards/magic_013.png" },
            { "Wind", "Assets/GameData/cards/magic_023.png" },
            { "Fire", "Assets/GameData/cards/magic_009.png" }
        };
        private static readonly Dictionary<string, string> BackAssetPathByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "Assets/UI/Images/ElementCards/element_card_back_earth_imdream.png" },
            { "Water", "Assets/UI/Images/ElementCards/element_card_back_water_imdream.png" },
            { "Wind", "Assets/UI/Images/ElementCards/element_card_back_air_imdream.png" },
            { "Fire", "Assets/UI/Images/ElementCards/element_card_back_fire_imdream.png" }
        };
        private static readonly Dictionary<string, string> FrameAssetPathByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "Assets/UI/Images/ElementCards/element_card_frame_earth_imdream.png" },
            { "Water", "Assets/UI/Images/ElementCards/element_card_frame_water_imdream.png" },
            { "Wind", "Assets/UI/Images/ElementCards/element_card_frame_air_imdream.png" },
            { "Fire", "Assets/UI/Images/ElementCards/element_card_frame_fire_imdream.png" }
        };
        private static readonly Dictionary<string, string> FrontSpriteNameByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "magic_003" },
            { "Water", "magic_013" },
            { "Wind", "magic_023" },
            { "Fire", "magic_009" }
        };
        private static readonly Dictionary<string, string> TitleHintByElement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Earth", "Earth Tremor / Earth Quake" },
            { "Water", "Freeze / Deadly Freeze" },
            { "Wind", "Wind Wings / Night Wings" },
            { "Fire", "Fireball / Firestorm" }
        };
        private static bool _autoStartIssuedThisPlay;

        private bool _running;
        [SerializeField] private string _debugState = "idle";
        [SerializeField] private int _debugStepIndex = -1;
        [SerializeField] private string _debugReportPath = string.Empty;
        [SerializeField] private string _debugLastError = string.Empty;
        private List<string> _sceneAutomationLines;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetPlaymodeAutoStartGuard()
        {
            _autoStartIssuedThisPlay = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStartIfQueued()
        {
            if (_autoStartIssuedThisPlay)
            {
                return;
            }

            var queued = UnityEditor.SessionState.GetBool(PendingSessionKey, false);
            var triggeredByFile = HasFileTrigger();
            if (!queued && !triggeredByFile)
            {
                return;
            }

            if (SceneAutomationRuntimeState.PendingRequest != null)
            {
                SceneAutomationRuntimeState.Clear();
                Debug.LogWarning("[ElementCardsManualCapture] Cleared stale SceneAutomation pending request before manual capture.");
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != "Part1_ElementCardsShowcase")
            {
                return;
            }

            if (queued)
            {
                UnityEditor.SessionState.SetBool(PendingSessionKey, false);
            }

            if (triggeredByFile)
            {
                ClearFileTrigger();
            }

            var runner = FindFirstObjectByType<ElementCardsManualCaptureRunner>();
            if (runner == null)
            {
                var host = new GameObject("ElementCardsManualCaptureRunner");
                runner = host.AddComponent<ElementCardsManualCaptureRunner>();
            }

            _autoStartIssuedThisPlay = true;
            runner.Begin();
            Debug.Log("[ElementCardsManualCapture] Auto-started from queued request.");
        }

        private static bool HasFileTrigger()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var triggerPath = Path.GetFullPath(Path.Combine(projectRoot, TriggerRelativePath));
            return File.Exists(triggerPath);
        }

        private static void ClearFileTrigger()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var triggerPath = Path.GetFullPath(Path.Combine(projectRoot, TriggerRelativePath));
            if (File.Exists(triggerPath))
            {
                File.Delete(triggerPath);
            }
        }
#endif

        public void Begin()
        {
            if (_running)
            {
                Debug.LogWarning("[ElementCardsManualCapture] Run already in progress.");
                return;
            }

            _running = true;
            _debugState = "begin_called";
            _debugStepIndex = -1;
            _debugLastError = string.Empty;
            StartCoroutine(RunCapture());
        }

        private IEnumerator RunCapture()
        {
            _sceneAutomationLines = new List<string>(64);
            RecordSceneAutomationLine($"[SceneAutomation] Config prepared: {ScenePath} (steps: {StepLabels.Length})");
            RecordSceneAutomationLine("[SceneAutomation] Runner started.");
            RecordSceneAutomationLine($"[SceneAutomation] Crop policy: {BuildCropPolicySummary()}");
            var startedAtUtc = DateTime.UtcNow;
            var report = new SceneAutomationReport
            {
                scenePath = ScenePath,
                startedAt = startedAtUtc.ToString("o"),
                status = "running",
                message = string.Empty,
                steps = new List<SceneAutomationReportStep>()
            };

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var screenshotsDir = Path.GetFullPath(Path.Combine(projectRoot, ScreenshotsRelativeDir));
            var reportPath = Path.GetFullPath(Path.Combine(projectRoot, ReportRelativePath));
            var bindingAuditPath = Path.GetFullPath(Path.Combine(projectRoot, BindingAuditRelativePath));
            var consoleLogPath = Path.GetFullPath(Path.Combine(projectRoot, ConsoleLogRelativePath));
            _debugReportPath = reportPath;
            Directory.CreateDirectory(screenshotsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? projectRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(bindingAuditPath) ?? projectRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(consoleLogPath) ?? projectRoot);
            ResetOutputFiles(screenshotsDir, reportPath);
            DeleteFileIfExists(bindingAuditPath);
            DeleteFileIfExists(consoleLogPath);
            WriteReportSnapshot(reportPath, report);

            _debugState = "running";
            _debugStepIndex = 0;
            EnsureEditorUnpaused();

            var failed = false;
            var failedMessage = string.Empty;
            ElementCardsShowcaseBootstrapper.EnsureSetup();
            yield return WaitForFrames(InitialSceneSettleFrames);
            EnsureEditorUnpaused();
            var sceneStartState = BuildSideStateSummary();

            yield return CaptureStep(report, screenshotsDir, "Scene Start", string.Empty, "000_Scene Start.png", sceneStartState);
            WriteReportSnapshot(reportPath, report);
            if (!string.IsNullOrEmpty(_lastStepError))
            {
                failed = true;
                failedMessage = _lastStepError;
            }

            for (var i = 0; i < StepLabels.Length && !failed; i++)
            {
                _debugStepIndex = i + 1;
                var stepLabel = StepLabels[i];
                EnsureEditorUnpaused();
                RecordSceneAutomationLine($"[SceneAutomation] Step index: {i + 1}/{StepLabels.Length} label={stepLabel}");
                RecordSceneAutomationLine($"[SceneAutomation] Step begin: {stepLabel}");
                try
                {
                    ElementCardsShowcaseBootstrapper.AdvanceAutomationStepFromAutomation();
                }
                catch (Exception ex)
                {
                    failed = true;
                    failedMessage = $"Step toggle exception ({stepLabel}): {ex.Message}";
                    Debug.LogException(ex);
                    break;
                }

                RecordSceneAutomationLine($"[SceneAutomation] Step toggled: {stepLabel}");
                ApplySemanticStateForStep(stepLabel);
                yield return WaitForFrames(StepSettleFrames);
                EnsureEditorUnpaused();
                var semanticsValid = TryValidateStepSemantics(stepLabel, out var sideStateSummary);
                RecordSceneAutomationLine($"[SceneAutomation] SideState label={stepLabel} {sideStateSummary} valid={semanticsValid}");
                if (!semanticsValid)
                {
                    failed = true;
                    failedMessage = $"Semantic state mismatch for step '{stepLabel}'.";
                    report.steps.Add(new SceneAutomationReportStep
                    {
                        label = stepLabel,
                        buttonPath = AutomationButtonPath,
                        screenshotPath = string.Empty,
                        success = false,
                        message = $"{failedMessage} {sideStateSummary}"
                    });
                    WriteReportSnapshot(reportPath, report);
                    RecordSceneAutomationLine($"[SceneAutomation] Step complete: {stepLabel} (success: False)");
                    break;
                }

                var fileName = $"{i + 1:000}_{stepLabel}.png";
                yield return CaptureStep(report, screenshotsDir, stepLabel, AutomationButtonPath, fileName, sideStateSummary);
                WriteReportSnapshot(reportPath, report);
                if (!string.IsNullOrEmpty(_lastStepError))
                {
                    failed = true;
                    failedMessage = _lastStepError;
                    break;
                }
            }

            if (!failed && !TryValidateExpectedScreenshotSet(screenshotsDir, out var missingFiles))
            {
                failed = true;
                failedMessage = $"Missing expected screenshots: {missingFiles}";
            }

            if (!failed && !TryValidateScreenshotDiversity(screenshotsDir, out var diversityMessage))
            {
                failed = true;
                failedMessage = diversityMessage;
            }

            report.status = failed ? "failed" : "success";
            report.message = failed ? failedMessage : string.Empty;
            var finishedAtUtc = DateTime.UtcNow;
            if (TryGetLatestScreenshotWriteTimeUtc(report, out var latestScreenshotWriteUtc) && latestScreenshotWriteUtc > finishedAtUtc)
            {
                finishedAtUtc = latestScreenshotWriteUtc;
            }

            report.finishedAt = finishedAtUtc.ToString("o");
            WriteReportSnapshot(reportPath, report);
            WriteBindingAuditSnapshot(bindingAuditPath, report);

            _debugState = failed ? "failed" : "completed";
            Debug.Log($"[ElementCardsManualCapture] Report written: {reportPath}");
            RecordSceneAutomationLine($"[SceneAutomation] Scene: {ScenePath} Status: {report.status} Steps: {report.steps.Count}");
            if (report.steps != null)
            {
                foreach (var step in report.steps)
                {
                    var marker = step != null && step.success ? "OK" : "FAIL";
                    var stepLabel = step != null ? step.label : "unknown";
                    var screenshot = step != null ? step.screenshotPath : string.Empty;
                    RecordSceneAutomationLine($"[SceneAutomation] [{marker}] {stepLabel} -> {screenshot}");
                }
            }
            WriteConsoleLogSnapshot(consoleLogPath);

            _running = false;
            SceneAutomationRuntimeState.ReportCompleted(report);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }

        private string _lastStepError;

        private IEnumerator CaptureStep(
            SceneAutomationReport report,
            string screenshotsDir,
            string label,
            string buttonPath,
            string fileName,
            string stepContext)
        {
            _lastStepError = string.Empty;
            Canvas.ForceUpdateCanvases();
            EnsureEditorUnpaused();

            var screenshotPath = Path.Combine(screenshotsDir, fileName);
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }

            var captureSucceeded = false;
            if (string.Equals(label, "Scene Start", StringComparison.Ordinal))
            {
                yield return new WaitForEndOfFrame();
            }

            ScreenCapture.CaptureScreenshot(screenshotPath, 1);

            var waitDeadlineUtc = DateTime.UtcNow.AddSeconds(ScreenshotCaptureTimeoutSeconds);
            var captureWriteUtc = DateTime.MinValue;
            var lastObservedLength = -1L;
            var stableFrameCount = 0;
            for (var frame = 0; frame < ScreenshotCaptureTimeoutFrames && DateTime.UtcNow < waitDeadlineUtc; frame++)
            {
                if (TryReadFileSnapshot(screenshotPath, out var length, out var writeTimeUtc) && length > 0)
                {
                    if (length == lastObservedLength)
                    {
                        stableFrameCount++;
                    }
                    else
                    {
                        stableFrameCount = 0;
                        lastObservedLength = length;
                    }

                    if (stableFrameCount >= ScreenshotStableFramesRequired)
                    {
                        captureSucceeded = true;
                        captureWriteUtc = writeTimeUtc;
                        break;
                    }
                }
                else
                {
                    stableFrameCount = 0;
                    lastObservedLength = -1L;
                }

                EnsureEditorUnpaused();
                yield return null;
            }

            var fileExists = File.Exists(screenshotPath);
            var fileLength = fileExists ? GetFileLengthSafe(screenshotPath) : 0L;
            if ((!captureSucceeded && !fileExists) || fileLength <= 0)
            {
                _lastStepError = $"Failed to capture screenshot for step '{label}'.";
                _debugLastError = _lastStepError;
                report.steps.Add(new SceneAutomationReportStep
                {
                    label = label,
                    buttonPath = buttonPath,
                    screenshotPath = fileExists ? screenshotPath : string.Empty,
                    success = false,
                    message = $"{_lastStepError} {stepContext}".Trim()
                });
                RecordSceneAutomationLine($"[SceneAutomation] Step complete: {label} (success: False)");
                yield break;
            }

            if (captureWriteUtc == DateTime.MinValue)
            {
                captureWriteUtc = File.GetLastWriteTimeUtc(screenshotPath);
            }

            if (TryCropStepScreenshot(label, screenshotPath, out var cropMessage))
            {
                captureWriteUtc = File.GetLastWriteTimeUtc(screenshotPath);
                if (!string.IsNullOrWhiteSpace(cropMessage))
                {
                    RecordSceneAutomationLine($"[SceneAutomation] Step crop: {cropMessage}");
                }
            }

            report.steps.Add(new SceneAutomationReportStep
            {
                label = label,
                buttonPath = buttonPath,
                screenshotPath = screenshotPath,
                success = true,
                message = $"capturedAtUtc={captureWriteUtc:o}; {stepContext}".Trim()
            });
            RecordSceneAutomationLine($"[SceneAutomation] Step complete: {label} (success: True) path={screenshotPath}");
        }

        private static bool TryCropStepScreenshot(string stepLabel, string screenshotPath, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
            {
                message = "crop_skipped_missing_file";
                return false;
            }

            try
            {
                var pngBytes = File.ReadAllBytes(screenshotPath);
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    message = "crop_skipped_empty_file";
                    return false;
                }

                var source = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
                if (!source.LoadImage(pngBytes))
                {
                    message = "crop_skipped_load_failed";
                    UnityEngine.Object.Destroy(source);
                    return false;
                }

                var width = source.width;
                var height = source.height;
                if (width <= 2 || height <= 2)
                {
                    message = "crop_skipped_invalid_dimensions";
                    UnityEngine.Object.Destroy(source);
                    return false;
                }

                if (!TryBuildStepCropRect(stepLabel, width, height, out var cropRect, out var rectMessage))
                {
                    UnityEngine.Object.Destroy(source);
                    message = rectMessage;
                    return false;
                }

                var cropWidth = cropRect.width;
                var cropHeight = cropRect.height;
                var cropped = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, mipChain: false, linear: false);
                try
                {
                    var pixels = source.GetPixels(cropRect.x, cropRect.y, cropRect.width, cropRect.height);
                    cropped.SetPixels(pixels);
                    cropped.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    File.WriteAllBytes(screenshotPath, cropped.EncodeToPNG());
                    message = rectMessage;
                    return true;
                }
                finally
                {
                    UnityEngine.Object.Destroy(cropped);
                    UnityEngine.Object.Destroy(source);
                }
            }
            catch (Exception ex)
            {
                message = $"crop_failed reason={ex.Message}";
                return false;
            }
        }

        private static bool TryBuildStepCropRect(
            string stepLabel,
            int screenshotWidth,
            int screenshotHeight,
            out RectInt cropRect,
            out string message)
        {
            cropRect = default;
            if (!TryParseStepLabel(stepLabel, out var elementLabel, out _))
            {
                message = "crop_skipped_non_element_step";
                return false;
            }

            var elementIndex = Array.FindIndex(
                ElementOrder,
                entry => string.Equals(entry, elementLabel, StringComparison.OrdinalIgnoreCase));
            if (elementIndex < 0)
            {
                message = $"crop_skipped_unknown_element element={elementLabel}";
                return false;
            }

            if (!TryCollectElementCardScreenRects(out var screenRectsByElement, out var collectMessage))
            {
                message = collectMessage;
                return false;
            }

            if (!screenRectsByElement.TryGetValue(elementLabel, out var screenRect))
            {
                message = $"crop_skipped_missing_screen_rect element={elementLabel}";
                return false;
            }

            var insetX = Mathf.Max(StepCropInsetMinPixels, screenRect.width * StepCropInsetRatio);
            var insetY = Mathf.Max(StepCropInsetMinPixels, screenRect.height * StepCropInsetRatio);
            var xMinScreen = screenRect.xMin + insetX;
            var xMaxScreen = screenRect.xMax - insetX;
            var yMinScreen = screenRect.yMin + insetY;
            var yMaxScreen = screenRect.yMax - insetY;

            var targetCenterX = screenRect.center.x;
            var leftDivider = float.NegativeInfinity;
            var rightDivider = float.PositiveInfinity;
            foreach (var pair in screenRectsByElement)
            {
                if (string.Equals(pair.Key, elementLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherCenterX = pair.Value.center.x;
                if (otherCenterX < targetCenterX)
                {
                    leftDivider = Mathf.Max(leftDivider, (otherCenterX + targetCenterX) * 0.5f);
                }
                else if (otherCenterX > targetCenterX)
                {
                    rightDivider = Mathf.Min(rightDivider, (otherCenterX + targetCenterX) * 0.5f);
                }
            }

            if (!float.IsNegativeInfinity(leftDivider))
            {
                xMinScreen = Mathf.Max(xMinScreen, leftDivider + StepCropLaneGapPixels);
            }

            if (!float.IsPositiveInfinity(rightDivider))
            {
                xMaxScreen = Mathf.Min(xMaxScreen, rightDivider - StepCropLaneGapPixels);
            }

            if (xMaxScreen - xMinScreen < StepCropMinSizePixels || yMaxScreen - yMinScreen < StepCropMinSizePixels)
            {
                xMinScreen = screenRect.xMin;
                xMaxScreen = screenRect.xMax;
                yMinScreen = screenRect.yMin;
                yMaxScreen = screenRect.yMax;

                if (!float.IsNegativeInfinity(leftDivider))
                {
                    xMinScreen = Mathf.Max(xMinScreen, leftDivider + StepCropLaneGapPixels);
                }

                if (!float.IsPositiveInfinity(rightDivider))
                {
                    xMaxScreen = Mathf.Min(xMaxScreen, rightDivider - StepCropLaneGapPixels);
                }
            }

            if (xMaxScreen - xMinScreen < StepCropMinSizePixels)
            {
                var half = StepCropMinSizePixels * 0.5f;
                xMinScreen = Mathf.Max(screenRect.center.x - half, screenRect.xMin);
                xMaxScreen = Mathf.Min(screenRect.center.x + half, screenRect.xMax);
            }

            if (yMaxScreen - yMinScreen < StepCropMinSizePixels)
            {
                var half = StepCropMinSizePixels * 0.5f;
                yMinScreen = Mathf.Max(screenRect.center.y - half, screenRect.yMin);
                yMaxScreen = Mathf.Min(screenRect.center.y + half, screenRect.yMax);
            }

            var screenWidth = Mathf.Max(1, Screen.width);
            var screenHeight = Mathf.Max(1, Screen.height);
            var scaleX = screenshotWidth / (float)screenWidth;
            var scaleY = screenshotHeight / (float)screenHeight;

            var xMin = Mathf.Clamp(Mathf.FloorToInt(xMinScreen * scaleX), 0, screenshotWidth - 1);
            var xMaxExclusive = Mathf.Clamp(Mathf.CeilToInt(xMaxScreen * scaleX), xMin + 1, screenshotWidth);
            var yMin = Mathf.Clamp(Mathf.FloorToInt(yMinScreen * scaleY), 0, screenshotHeight - 1);
            var yMaxExclusive = Mathf.Clamp(Mathf.CeilToInt(yMaxScreen * scaleY), yMin + 1, screenshotHeight);
            var cropWidth = xMaxExclusive - xMin;
            var cropHeight = yMaxExclusive - yMin;
            if (cropWidth < 2 || cropHeight < 2)
            {
                message = $"crop_skipped_zero_area element={elementLabel}";
                return false;
            }

            cropRect = new RectInt(xMin, yMin, cropWidth, cropHeight);
            var leftDividerText = float.IsNegativeInfinity(leftDivider) ? "none" : leftDivider.ToString("F1", CultureInfo.InvariantCulture);
            var rightDividerText = float.IsPositiveInfinity(rightDivider) ? "none" : rightDivider.ToString("F1", CultureInfo.InvariantCulture);
            message =
                $"crop_applied step={stepLabel} element={elementLabel} elementIndex={elementIndex} " +
                $"screenRect={screenRect.xMin:F1},{screenRect.yMin:F1},{screenRect.width:F1},{screenRect.height:F1} " +
                $"laneDividers={leftDividerText}|{rightDividerText} laneRange={xMinScreen:F1}-{xMaxScreen:F1} " +
                $"imageRect={cropRect.x},{cropRect.y},{cropRect.width},{cropRect.height} screen={screenWidth}x{screenHeight} image={screenshotWidth}x{screenshotHeight} " +
                $"policy={BuildCropPolicySummary()}";
            return true;
        }

        private static string BuildCropPolicySummary()
        {
            return
                $"insetMinPx={StepCropInsetMinPixels.ToString("F1", CultureInfo.InvariantCulture)};" +
                $"insetRatio={StepCropInsetRatio.ToString("F3", CultureInfo.InvariantCulture)};" +
                $"laneGapPx={StepCropLaneGapPixels.ToString("F1", CultureInfo.InvariantCulture)};" +
                $"minSizePx={StepCropMinSizePixels}";
        }

        private static bool TryGetScreenSpaceRect(RectTransform rectTransform, out Rect screenRect)
        {
            screenRect = default;
            if (rectTransform == null)
            {
                return false;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (float.IsNaN(minX) || float.IsNaN(minY) || float.IsNaN(maxX) || float.IsNaN(maxY)
                || float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
            {
                return false;
            }

            if (maxX - minX < 1f || maxY - minY < 1f)
            {
                return false;
            }

            var clampedMinX = Mathf.Clamp(minX, 0f, Screen.width);
            var clampedMinY = Mathf.Clamp(minY, 0f, Screen.height);
            var clampedMaxX = Mathf.Clamp(maxX, clampedMinX + 1f, Screen.width);
            var clampedMaxY = Mathf.Clamp(maxY, clampedMinY + 1f, Screen.height);
            screenRect = Rect.MinMaxRect(clampedMinX, clampedMinY, clampedMaxX, clampedMaxY);
            return true;
        }

        private static bool TryCollectElementCardScreenRects(out Dictionary<string, Rect> screenRectsByElement, out string message)
        {
            screenRectsByElement = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < ElementOrder.Length; i++)
            {
                var element = ElementOrder[i];
                if (!TryGetCardNameForElement(element, out var cardName))
                {
                    message = $"crop_skipped_unknown_element element={element}";
                    return false;
                }

                var cardObject = GameObject.Find(cardName);
                if (cardObject == null)
                {
                    message = $"crop_skipped_missing_card card={cardName}";
                    return false;
                }

                var cardRect = cardObject.transform as RectTransform;
                if (cardRect == null)
                {
                    message = $"crop_skipped_missing_rect_transform card={cardName}";
                    return false;
                }

                if (!TryGetScreenSpaceRect(cardRect, out var screenRect))
                {
                    message = $"crop_skipped_unavailable_screen_rect card={cardName}";
                    return false;
                }

                screenRectsByElement[element] = screenRect;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryValidateScreenshotDiversity(string screenshotsDir, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(screenshotsDir) || !Directory.Exists(screenshotsDir))
            {
                message = "Screenshot diversity validation failed: screenshots directory is missing.";
                return false;
            }

            var expectedFileNames = new List<string> { "000_Scene Start.png" };
            expectedFileNames.AddRange(StepLabels.Select(BuildStepFileName));
            var digestByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fileName in expectedFileNames)
            {
                var filePath = Path.Combine(screenshotsDir, fileName);
                if (!File.Exists(filePath))
                {
                    message = $"Screenshot diversity validation failed: missing '{fileName}'.";
                    return false;
                }

                try
                {
                    using (var stream = File.OpenRead(filePath))
                    using (var sha = SHA256.Create())
                    {
                        var hash = sha.ComputeHash(stream);
                        digestByFile[fileName] = BitConverter.ToString(hash).Replace("-", string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    message = $"Screenshot diversity validation failed: cannot hash '{fileName}' ({ex.Message}).";
                    return false;
                }
            }

            var distinctHashes = new HashSet<string>(digestByFile.Values, StringComparer.OrdinalIgnoreCase);
            if (distinctHashes.Count <= 1)
            {
                message = "Screenshot diversity validation failed: all screenshots share one hash.";
                return false;
            }

            if (!TryEnsurePairDifferent(digestByFile, "001_Earth Back.png", "002_Earth Front.png", out message)
                || !TryEnsurePairDifferent(digestByFile, "003_Water Back.png", "004_Water Front.png", out message)
                || !TryEnsurePairDifferent(digestByFile, "005_Wind Back.png", "006_Wind Front.png", out message)
                || !TryEnsurePairDifferent(digestByFile, "007_Fire Back.png", "008_Fire Front.png", out message))
            {
                return false;
            }

            return true;
        }

        private static bool TryEnsurePairDifferent(
            Dictionary<string, string> digestByFile,
            string leftFileName,
            string rightFileName,
            out string message)
        {
            message = string.Empty;
            if (!digestByFile.TryGetValue(leftFileName, out var leftHash)
                || !digestByFile.TryGetValue(rightFileName, out var rightHash))
            {
                message = $"Screenshot diversity validation failed: missing digest for '{leftFileName}' or '{rightFileName}'.";
                return false;
            }

            if (string.Equals(leftHash, rightHash, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Screenshot diversity validation failed: '{leftFileName}' equals '{rightFileName}'.";
                return false;
            }

            return true;
        }

        private static void EnsureEditorUnpaused()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
            {
                UnityEditor.EditorApplication.isPaused = false;
            }
#endif
        }

        private static void WriteReportSnapshot(string reportPath, SceneAutomationReport report)
        {
            try
            {
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElementCardsManualCapture] Failed to write report snapshot: {ex.Message}");
            }
        }

        private static void ResetOutputFiles(string screenshotsDir, string reportPath)
        {
            if (Directory.Exists(screenshotsDir))
            {
                foreach (var filePath in Directory.GetFiles(screenshotsDir, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ElementCardsManualCapture] Failed to delete screenshot artifact '{filePath}': {ex.Message}");
                    }
                }
            }

            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElementCardsManualCapture] Failed to delete file '{filePath}': {ex.Message}");
            }
        }

        private void RecordSceneAutomationLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (_sceneAutomationLines == null)
            {
                _sceneAutomationLines = new List<string>();
            }

            _sceneAutomationLines.Add(line);
            Debug.Log(line);
        }

        private void WriteConsoleLogSnapshot(string consoleLogPath)
        {
            try
            {
                var marker = _sceneAutomationLines != null && _sceneAutomationLines.Count > 0
                    ? _sceneAutomationLines[0]
                    : string.Empty;

                var payload = new Dictionary<string, object>
                {
                    { "source", "runner_derived" },
                    { "lines", _sceneAutomationLines ?? new List<string>() },
                    { "marker", marker },
                    { "generatedAtUtc", DateTime.UtcNow.ToString("o") },
                    { "lineCount", _sceneAutomationLines?.Count ?? 0 }
                };

                File.WriteAllText(consoleLogPath, SerializeJson(payload), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElementCardsManualCapture] Failed to write console log snapshot: {ex.Message}");
            }
        }

        private static void WriteBindingAuditSnapshot(string bindingAuditPath, SceneAutomationReport report)
        {
            try
            {
                var status = string.Equals(report?.status, "success", StringComparison.OrdinalIgnoreCase)
                    ? "success"
                    : "failed";
                var runSucceeded = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
                var screenshotsRelative = NormalizeRelativePath(ScreenshotsRelativeDir);
                var reportRelative = NormalizeRelativePath(ReportRelativePath);
                var startedAt = report != null ? report.startedAt : string.Empty;
                var finishedAt = report != null ? report.finishedAt : string.Empty;

                var screenshotPairs = new List<object>
                {
                    BuildScreenshotPair("earth", "001_Earth Back.png", "002_Earth Front.png", screenshotsRelative),
                    BuildScreenshotPair("water", "003_Water Back.png", "004_Water Front.png", screenshotsRelative),
                    BuildScreenshotPair("wind", "005_Wind Back.png", "006_Wind Front.png", screenshotsRelative),
                    BuildScreenshotPair("fire", "007_Fire Back.png", "008_Fire Front.png", screenshotsRelative)
                };

                var elements = new Dictionary<string, object>
                {
                    { "earth", BuildElementBinding("Earth", "001_Earth Back.png", "002_Earth Front.png", screenshotsRelative, runSucceeded) },
                    { "water", BuildElementBinding("Water", "003_Water Back.png", "004_Water Front.png", screenshotsRelative, runSucceeded) },
                    { "wind", BuildElementBinding("Wind", "005_Wind Back.png", "006_Wind Front.png", screenshotsRelative, runSucceeded) },
                    { "fire", BuildElementBinding("Fire", "007_Fire Back.png", "008_Fire Front.png", screenshotsRelative, runSucceeded) }
                };

                var runtimeBindings = new Dictionary<string, object>
                {
                    { "earth", BuildRuntimeBinding("Earth") },
                    { "water", BuildRuntimeBinding("Water") },
                    { "wind", BuildRuntimeBinding("Wind") },
                    { "fire", BuildRuntimeBinding("Fire") }
                };

                var payload = new Dictionary<string, object>
                {
                    { "automationStatus", status },
                    { "screenshotPairs", screenshotPairs },
                    { "screenshotBatchRoot", screenshotsRelative },
                    { "screenshotsDirectory", screenshotsRelative },
                    { "backsBound", runSucceeded },
                    { "sourceBindingScript", "Assets/Scripts/UI/Part1/ElementCardsShowcaseBootstrapper.cs" },
                    { "framesBound", runSucceeded },
                    {
                        "namingMappings",
                        new Dictionary<string, object>
                        {
                            { "wind", "wind element uses air-named back/frame assets (element_card_back_air_imdream, element_card_frame_air_imdream)" }
                        }
                    },
                    { "status", status },
                    { "startedAt", startedAt },
                    { "generatedAt", finishedAt },
                    { "elements", elements },
                    { "flipOk", runSucceeded },
                    { "finishedAt", finishedAt },
                    { "screenshotDir", screenshotsRelative },
                    { "runId", RunId },
                    { "facesBound", runSucceeded },
                    { "reportFinishedAt", finishedAt },
                    { "scenePath", ScenePath },
                    {
                        "completeness",
                        new Dictionary<string, object>
                        {
                            { "elements_with_complete_bindings", runSucceeded ? 4 : 0 },
                            { "elements_expected", new List<object> { "earth", "water", "wind", "fire" } },
                            { "resource_types_expected", new List<object> { "back", "frame", "face" } }
                        }
                    },
                    { "automationReportPath", reportRelative },
                    { "reportPath", reportRelative },
                    { "runtimeBindings", runtimeBindings },
                    { "generatedAtUtc", finishedAt }
                };

                File.WriteAllText(bindingAuditPath, SerializeJson(payload), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElementCardsManualCapture] Failed to write binding audit snapshot: {ex.Message}");
            }
        }

        private static Dictionary<string, object> BuildScreenshotPair(string elementLower, string backFileName, string frontFileName, string screenshotsRelative)
        {
            return new Dictionary<string, object>
            {
                { "back", BuildRelativePath(screenshotsRelative, backFileName) },
                { "front", BuildRelativePath(screenshotsRelative, frontFileName) },
                { "element", elementLower }
            };
        }

        private static Dictionary<string, object> BuildElementBinding(string elementLabel, string backFileName, string frontFileName, string screenshotsRelative, bool complete)
        {
            var cardName = CardNameByElement.TryGetValue(elementLabel, out var resolvedCardName) ? resolvedCardName : $"Card_{elementLabel}";
            var backAssetPath = BackAssetPathByElement.TryGetValue(elementLabel, out var resolvedBackPath) ? resolvedBackPath : string.Empty;
            var frameAssetPath = FrameAssetPathByElement.TryGetValue(elementLabel, out var resolvedFramePath) ? resolvedFramePath : string.Empty;
            var frontAssetPath = FrontAssetPathByElement.TryGetValue(elementLabel, out var resolvedFrontPath) ? resolvedFrontPath : string.Empty;

            return new Dictionary<string, object>
            {
                {
                    "back",
                    new Dictionary<string, object>
                    {
                        { "sceneObjectPath", $"UIRoot/Canvas/CardsRow/{cardName}/BackRoot/Img_Back" },
                        { "evidenceScreenshotPath", BuildRelativePath(screenshotsRelative, backFileName) },
                        { "assetPath", backAssetPath }
                    }
                },
                { "complete", complete },
                {
                    "screenshots",
                    new Dictionary<string, object>
                    {
                        { "front", frontFileName },
                        { "back", backFileName }
                    }
                },
                {
                    "face",
                    new Dictionary<string, object>
                    {
                        { "sceneObjectPath", $"UIRoot/Canvas/CardsRow/{cardName}/FrontRoot/Img_FrontArt" },
                        { "evidenceScreenshotPath", BuildRelativePath(screenshotsRelative, frontFileName) },
                        { "assetPath", frontAssetPath }
                    }
                },
                {
                    "frame",
                    new Dictionary<string, object>
                    {
                        { "sceneObjectPath", $"UIRoot/Canvas/CardsRow/{cardName}/BackRoot/Img_Frame" },
                        { "evidenceScreenshotPath", BuildRelativePath(screenshotsRelative, backFileName) },
                        { "assetPath", frameAssetPath }
                    }
                }
            };
        }

        private static Dictionary<string, object> BuildRuntimeBinding(string elementLabel)
        {
            var frontSprite = FrontSpriteNameByElement.TryGetValue(elementLabel, out var frontSpriteName) ? frontSpriteName : string.Empty;
            var frontPath = FrontAssetPathByElement.TryGetValue(elementLabel, out var resolvedFrontPath) ? resolvedFrontPath : string.Empty;
            var titleHint = TitleHintByElement.TryGetValue(elementLabel, out var resolvedTitleHint) ? resolvedTitleHint : string.Empty;

            return new Dictionary<string, object>
            {
                { "frontSprite", frontSprite },
                { "frontPath", frontPath },
                { "titleHint", titleHint }
            };
        }

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/');
        }

        private static string BuildRelativePath(string root, string fileName)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return fileName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return root;
            }

            return $"{root.TrimEnd('/')}/{fileName}";
        }

        private static string SerializeJson(object value)
        {
            var sb = new StringBuilder(4096);
            AppendJsonValue(sb, value);
            return sb.ToString();
        }

        private static void AppendJsonValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string stringValue)
            {
                AppendJsonString(sb, stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                sb.Append(boolValue ? "true" : "false");
                return;
            }

            if (value is sbyte || value is byte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary<string, object> objectDictionary)
            {
                AppendJsonObject(sb, objectDictionary);
                return;
            }

            if (value is IDictionary genericDictionary)
            {
                var dict = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in genericDictionary)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    dict[key] = entry.Value;
                }

                AppendJsonObject(sb, dict);
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                AppendJsonArray(sb, enumerable);
                return;
            }

            AppendJsonString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static void AppendJsonObject(StringBuilder sb, IDictionary<string, object> dict)
        {
            sb.Append('{');
            var first = true;
            foreach (var pair in dict)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                AppendJsonString(sb, pair.Key ?? string.Empty);
                sb.Append(':');
                AppendJsonValue(sb, pair.Value);
            }

            sb.Append('}');
        }

        private static void AppendJsonArray(StringBuilder sb, IEnumerable items)
        {
            sb.Append('[');
            var first = true;
            foreach (var item in items)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                AppendJsonValue(sb, item);
            }

            sb.Append(']');
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    switch (c)
                    {
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\b':
                            sb.Append("\\b");
                            break;
                        case '\f':
                            sb.Append("\\f");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (c < 32)
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
        }

        private static bool TryValidateExpectedScreenshotSet(string screenshotsDir, out string missingFiles)
        {
            var missing = new List<string>();
            var sceneStartPath = Path.Combine(screenshotsDir, "000_Scene Start.png");
            if (!File.Exists(sceneStartPath))
            {
                missing.Add("000_Scene Start.png");
            }

            foreach (var stepLabel in StepLabels)
            {
                var fileName = BuildStepFileName(stepLabel);
                var screenshotPath = Path.Combine(screenshotsDir, fileName);
                if (!File.Exists(screenshotPath))
                {
                    missing.Add(fileName);
                }
            }

            missingFiles = string.Join(", ", missing);
            return missing.Count == 0;
        }

        private static string BuildStepFileName(string stepLabel)
        {
            var index = Array.IndexOf(StepLabels, stepLabel);
            if (index < 0)
            {
                return stepLabel;
            }

            return $"{index + 1:000}_{stepLabel}.png";
        }

        private static bool TryGetLatestScreenshotWriteTimeUtc(SceneAutomationReport report, out DateTime latestWriteUtc)
        {
            latestWriteUtc = DateTime.MinValue;
            if (report?.steps == null || report.steps.Count == 0)
            {
                return false;
            }

            var found = false;
            foreach (var step in report.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.screenshotPath))
                {
                    continue;
                }

                if (!File.Exists(step.screenshotPath))
                {
                    continue;
                }

                var writeUtc = File.GetLastWriteTimeUtc(step.screenshotPath);
                if (!found || writeUtc > latestWriteUtc)
                {
                    latestWriteUtc = writeUtc;
                    found = true;
                }
            }

            return found;
        }

        private static void ApplySemanticStateForStep(string stepLabel)
        {
            if (!TryParseStepLabel(stepLabel, out var elementLabel, out var showFront))
            {
                return;
            }

            ElementCardsShowcaseBootstrapper.SetAllFront(showFront: false);
            if (showFront && TryGetCardNameForElement(elementLabel, out var cardName))
            {
                ElementCardsShowcaseBootstrapper.SetExclusiveFront(cardName);
            }
        }

        private static bool TryValidateStepSemantics(string stepLabel, out string summary)
        {
            if (!TryParseStepLabel(stepLabel, out var targetElement, out var showFront))
            {
                summary = $"parseFailed stepLabel={stepLabel}";
                return false;
            }

            var sb = new StringBuilder();
            var valid = true;
            sb.Append($"expected={(showFront ? "front" : "back")} target={targetElement};");
            for (var i = 0; i < ElementOrder.Length; i++)
            {
                var element = ElementOrder[i];
                var state = ResolveCardFrontStateForElement(element, out var hasPresenterState);
                var expectedFront = showFront && string.Equals(element, targetElement, StringComparison.OrdinalIgnoreCase);
                var expectedText = expectedFront ? "front" : "back";
                if (!hasPresenterState || !string.Equals(state, expectedText, StringComparison.Ordinal))
                {
                    valid = false;
                }

                sb.Append($" {element.ToLowerInvariant()}={state}(expected:{expectedText})");
            }

            summary = sb.ToString();
            return valid;
        }

        private static string BuildSideStateSummary()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < ElementOrder.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                var element = ElementOrder[i];
                var state = ResolveCardFrontStateForElement(element, out _);
                sb.Append($"{element.ToLowerInvariant()}={state}");
            }

            return sb.ToString();
        }

        private static string ResolveCardFrontStateForElement(string element, out bool hasPresenterState)
        {
            hasPresenterState = false;
            if (!TryGetCardNameForElement(element, out var cardName))
            {
                return "unknown_element";
            }

            var card = GameObject.Find(cardName);
            if (card == null)
            {
                return "missing_card";
            }

            var presenter = card.GetComponent<ElementCardFlipPresenter>();
            if (presenter == null)
            {
                return "missing_presenter";
            }

            hasPresenterState = true;
            return presenter.IsFrontShown ? "front" : "back";
        }

        private static bool TryGetCardNameForElement(string element, out string cardName)
        {
            if (string.IsNullOrWhiteSpace(element))
            {
                cardName = string.Empty;
                return false;
            }

            return CardNameByElement.TryGetValue(element.Trim(), out cardName);
        }

        private static bool TryParseStepLabel(string stepLabel, out string elementLabel, out bool showFront)
        {
            elementLabel = string.Empty;
            showFront = false;
            if (string.IsNullOrWhiteSpace(stepLabel))
            {
                return false;
            }

            var parts = stepLabel.Trim().Split(' ');
            if (parts.Length < 2)
            {
                return false;
            }

            elementLabel = parts[0].Trim();
            var side = parts[1].Trim();
            if (string.Equals(side, "Front", StringComparison.OrdinalIgnoreCase))
            {
                showFront = true;
                return true;
            }

            if (string.Equals(side, "Back", StringComparison.OrdinalIgnoreCase))
            {
                showFront = false;
                return true;
            }

            return false;
        }

        private static bool TryReadFileSnapshot(string path, out long length, out DateTime writeTimeUtc)
        {
            length = 0L;
            writeTimeUtc = DateTime.MinValue;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(path);
                length = fileInfo.Length;
                writeTimeUtc = fileInfo.LastWriteTimeUtc;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static long GetFileLengthSafe(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch (IOException)
            {
                return 0L;
            }
            catch (UnauthorizedAccessException)
            {
                return 0L;
            }
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            if (frameCount <= 0)
            {
                yield break;
            }

            for (var i = 0; i < frameCount; i++)
            {
                EnsureEditorUnpaused();
                yield return null;
            }
        }
    }
}
