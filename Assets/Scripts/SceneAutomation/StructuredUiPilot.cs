using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageKnight.SceneAutomation
{
    // Explicit opt-in pilot. Rules stay in the existing Demo and button handlers.
    public sealed partial class StructuredUiPilot : MonoBehaviour
    {
        public const string SessionKey = "MageKnight.StructuredUiPilot.Directory";
        private string _directory;
        private bool _busy;
        private string _runtimeError;
        private string _lastRequest;

        [Serializable] public class Control
        {
            public string path, name, text, hitObject;
            public bool interactable, clickable;
            public float x, y, width, height;
        }
        [Serializable] public class Observation
        {
            public string version;
            public int screenWidth, screenHeight;
            public List<Control> controls = new();
            // Full state is verification evidence; Python actor uses an explicit public allowlist.
            public List<SceneAutomationStateValue> state = new();
        }
        [Serializable] public class Request
        {
            public string id, operation, path, expectedVersion, taskJson, prefixJson, paymentConsumerJson;
            public bool screenshot;
            public bool optimizedUi;
            public bool measure;
            public bool fastCapture;
            // Explicit evaluation only: never enters actor observations/version.
            public bool evaluationDiagnostics;
            public int sampleFrames;
            public bool customPoint;
            public float relativeX, relativeY;
            public float scrollY;
            public string nextControlPath;
        }
        [Serializable] public class Timing
        {
            public string method = "EventSystem dispatch to first completed Game View frame; software proxy, not display photon latency";
            public double handlerMilliseconds = -1;
            public double firstRenderedFrameMilliseconds = -1;
            public double nextControlReadyMilliseconds = -1;
            public double postFrameInputBlockedMilliseconds = -1;
            public bool nextControlTimedOut;
            public string nextControlPath;
        }
        [Serializable] public class PointProbe
        {
            public float relativeX, relativeY, x, y;
            public bool valid;
            public string hitObject;
        }
        [Serializable] public class Response
        {
            public string previousRuntimeError;
            public string id, error, screenshotPath;
            public bool success;
            public float elapsedSeconds;
            public Observation observation, before;
            public SceneAutomationReportStep click;
            public Timing timing;
            public float controlCenterX, controlCenterY;
            public List<PointProbe> raycastProbes;
            public EvaluationEvidence evaluation;
            public List<EvaluationFrame> evaluationFrames;
        }
        [Serializable] public class EvaluationFrame
        {
            public int frame;
            public float realtimeSeconds, unscaledDeltaMilliseconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR
            string directory = UnityEditor.SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(directory)) return;
            var host = new GameObject("StructuredUiPilot");
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<StructuredUiPilot>()._directory = directory;
#endif
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _runtimeError = message;
        }
        private void Update()
        {
            if (_busy || string.IsNullOrEmpty(_directory)) return;
            string path = Path.Combine(_directory, "request.json");
            if (!File.Exists(path)) return;
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                if (request == null || string.IsNullOrEmpty(request.id) || request.id == _lastRequest) return;
                _lastRequest = request.id;
                // Completed requests are immutable receipts, not pending work
                // after a scene/domain restart.
                if(File.Exists(Path.Combine(_directory,"responses",request.id+".json")))return;
                _busy = true;
                StartCoroutine(Execute(request));
            }
            // The Python atomic rename may briefly hold a Windows sharing lock.
            // Retry the read next frame; never attach that read failure to a prior request ID.
            catch (IOException) { _busy = false; }
            catch (UnauthorizedAccessException) { _busy = false; }
            catch (Exception ex) { Debug.LogException(ex); _busy = false; }
        }
        private static string ObjectPath(Transform transform)
        {
            var parts = new List<string>();
            for (var cursor = transform; cursor != null; cursor = cursor.parent) parts.Add(cursor.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
        private static Vector2 Center(Button button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }
        private static bool Hit(Button button, out Vector2 point, out string hitObject)
        {
            point = Center(button); return HitAt(button,point,out hitObject);
        }
        private static Vector2 RelativePoint(Button button,float u,float v)
        {
            var rect=(RectTransform)button.transform;
            var canvas=button.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(new Vector2(
                rect.rect.xMin+rect.rect.width*u,rect.rect.yMin+rect.rect.height*v)));
        }
        private static bool HitAt(Button button,Vector2 point,out string hitObject)
        {
            hitObject = "";
            if (EventSystem.current == null || !button.isActiveAndEnabled || !button.IsInteractable()
                || point.x < 0 || point.y < 0 || point.x >= Screen.width || point.y >= Screen.height) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0) return false;
            hitObject = hits[0].gameObject.name;
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject;
        }
        private static Observation Observe()
        {
            Canvas.ForceUpdateCanvases();
            var source = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<ISceneAutomationStateSource>().Single();
            var result = new Observation { screenWidth = Screen.width, screenHeight = Screen.height };
            result.state = source.ReadAutomationState().OrderBy(p => p.Key)
                .Select(p => new SceneAutomationStateValue { key = p.Key, value = p.Value }).ToList();
            if(source is MageKnight.Battle.PveBattleController costController)
            {
                var audit=costController.ReadCostFixtureAudit();
                if(!string.IsNullOrEmpty(audit))result.state.Add(new SceneAutomationStateValue { key="audit.costFixture",value=audit });
                var native=costController.ReadCostNativeState();
                if(!string.IsNullOrEmpty(native))result.state.Add(new SceneAutomationStateValue { key="audit.costNativeState",value=native });
            }
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy).OrderBy(b => ObjectPath(b.transform)))
            {
                bool clickable = Hit(button, out var point, out string hit);
                var rect = (RectTransform)button.transform;
                result.controls.Add(new Control { path = ObjectPath(button.transform), name = button.name,
                    text = string.Join(" | ", button.GetComponentsInChildren<TMP_Text>().Select(t => t.text)),
                    interactable = button.IsInteractable(), clickable = clickable, hitObject = hit,
                    x = point.x, y = point.y, width = rect.rect.width, height = rect.rect.height });
            }
            result.state.Add(new SceneAutomationStateValue { key="ui.hoverPreviewVisible",
                value=FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                    .Any(t=>t.name=="CardHoverPreview" && t.gameObject.activeInHierarchy).ToString() });
            using var sha = SHA256.Create();
            result.version = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(result))))
                .Replace("-", "").ToLowerInvariant();
            return result;
        }
        private IEnumerator Execute(Request request)
        {
            var response = new Response { id = request.id };
            float started = Time.realtimeSinceStartup;
            bool stop = request.operation == "stop";
            long inputTicks = 0;
            try
            {
                if(request.operation=="reset_task")
                {
                    // A fresh formal fixture must remain possible after a
                    // sampled UI state fails to render. Retain the old error.
                    response.previousRuntimeError=_runtimeError;_runtimeError=null;
                }
                else response.before = Observe();
                if (_runtimeError != null) throw new InvalidOperationException("Runtime error: " + _runtimeError);
                if (request.operation == "click" || request.operation == "probe" || request.operation=="hover" || request.operation=="unhover" || request.operation=="scroll")
                {
                    if (request.expectedVersion != response.before.version)
                        throw new InvalidOperationException("Stale observation: reobserve before selecting an action.");
                    var matches = FindObjectsByType<Button>(FindObjectsSortMode.None)
                        .Where(b => ObjectPath(b.transform) == request.path).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Control identity missing or ambiguous.");
                    var button = matches[0];
                    if(request.operation=="hover" || request.operation=="unhover")
                    {
                        var pointer=new PointerEventData(EventSystem.current){position=Center(button)};
                        if(request.operation=="hover")ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
                        else ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerExitHandler);
                    }
                    else if(request.operation=="scroll")
                    {
                        if(float.IsNaN(request.scrollY)||float.IsInfinity(request.scrollY)||Mathf.Abs(request.scrollY)>100)
                            throw new InvalidOperationException("Finite scroll delta within 100 required.");
                        var point=Center(button);
                        if(!HitAt(button,point,out string scrollHit))throw new InvalidOperationException("Scroll control disabled, offscreen or obscured.");
                        var pointer=new PointerEventData(EventSystem.current){position=point,scrollDelta=new Vector2(0,request.scrollY)};
                        if(!ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.scrollHandler))
                            throw new InvalidOperationException("Control has no scroll handler.");
                    }
                    else if(request.operation=="probe")
                    {
                        response.raycastProbes=new List<PointProbe>();
                        foreach(var uv in new[]{new Vector2(.5f,.5f),new Vector2(.5f,.1f),new Vector2(.5f,.9f),
                            new Vector2(.1f,.5f),new Vector2(.9f,.5f),new Vector2(.1f,.1f),new Vector2(.9f,.1f),
                            new Vector2(.1f,.9f),new Vector2(.9f,.9f),new Vector2(.5f,.02f),new Vector2(.5f,.98f)})
                        {
                            var point=RelativePoint(button,uv.x,uv.y);bool valid=HitAt(button,point,out string hit);
                            response.raycastProbes.Add(new PointProbe{relativeX=uv.x,relativeY=uv.y,x=point.x,y=point.y,valid=valid,hitObject=hit});
                        }
                    }
                    else
                    {
                    if(request.customPoint && (float.IsNaN(request.relativeX)||float.IsNaN(request.relativeY)||request.relativeX<0||request.relativeX>1||request.relativeY<0||request.relativeY>1))
                        throw new InvalidOperationException("Pointer position must stay within the actual control rectangle");
                    var point=request.customPoint?RelativePoint(button,request.relativeX,request.relativeY):Center(button);
                    if (!HitAt(button,point,out string hit))
                        throw new InvalidOperationException("Control disabled, offscreen or obscured.");
                    var center=Center(button);response.controlCenterX=center.x;response.controlCenterY=center.y;
                    response.click = new SceneAutomationReportStep { label = request.id, buttonPath = request.path,
                        clickX = point.x, clickY = point.y, hitObject = hit, beforeState = response.before.state };
                    var pointer = new PointerEventData(EventSystem.current)
                        { position = point, button = PointerEventData.InputButton.Left };
                    if (request.measure)
                    {
                        response.timing = new Timing { nextControlPath = request.nextControlPath };
                        inputTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    if (response.timing != null)
                        response.timing.handlerMilliseconds = MillisecondsSince(inputTicks);
                    }
                }
                else if (request.operation == "hover_info" || request.operation == "unhover_info" || request.operation == "scroll_info")
                {
                    if (request.expectedVersion != response.before.version)
                        throw new InvalidOperationException("Stale observation: reobserve before hover.");
                    var info = FindObjectsByType<MageKnight.Battle.BattleHoverInfo>(FindObjectsSortMode.None)
                        .Single(h => ObjectPath(h.transform) == request.path);
                    var rect = (RectTransform)info.transform;
                    var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                    var pointer = new PointerEventData(EventSystem.current) { position = point };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                    bool valid = hits.Count > 0 && (hits[0].gameObject == info.gameObject || hits[0].gameObject.transform.IsChildOf(info.transform));
                    if (request.operation != "unhover_info" && !valid)
                        throw new InvalidOperationException("Information surface is offscreen or obscured.");
                    response.raycastProbes = new List<PointProbe> { new PointProbe { relativeX=.5f,relativeY=.5f,x=point.x,y=point.y,valid=valid,hitObject=hits.Count>0?ObjectPath(hits[0].gameObject.transform):"" } };
                    if (request.operation == "hover_info") ExecuteEvents.Execute(info.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    else if (request.operation == "scroll_info")
                    {
                        if (float.IsNaN(request.scrollY) || float.IsInfinity(request.scrollY) || Mathf.Abs(request.scrollY) > 100)
                            throw new InvalidOperationException("Finite scroll delta within 100 required.");
                        pointer.scrollDelta = new Vector2(0, request.scrollY);
                        if (!ExecuteEvents.Execute(info.gameObject, pointer, ExecuteEvents.scrollHandler))
                            throw new InvalidOperationException("Information surface has no scroll handler.");
                    }
                    else ExecuteEvents.Execute(info.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                }
                else if (request.operation == "set_ui_design")
                {
                    FindObjectsByType<MageKnight.Battle.PveBattleController>(FindObjectsSortMode.None).Single().SetUiDesignPreview(request.optimizedUi);
                }
                else if (request.operation == "reset_task")
                {
                    var controller=FindObjectsByType<MageKnight.Battle.PveBattleController>(FindObjectsSortMode.None).Single();
                    controller.ResetCostFixture(request.taskJson);
                }
                else if (request.operation == "prepare_prefix")
                {
                    FindObjectsByType<MageKnight.Battle.PveBattleController>(FindObjectsSortMode.None).Single()
                        .PrepareCostPrefix(request.prefixJson,request.paymentConsumerJson);
                }
                else if (request.operation != "observe" && !stop)
                    throw new InvalidOperationException("Unsupported operation.");
            }
            catch (Exception ex) { response.error = ex.ToString(); }
            if (response.timing != null)
            {
                // Record before the pilot's 0.15 s stabilisation delay or screenshot encoding.
                yield return new WaitForEndOfFrame();
                response.timing.firstRenderedFrameMilliseconds = MillisecondsSince(inputTicks);
                if (!string.IsNullOrEmpty(request.nextControlPath))
                {
                    bool ready = false;
                    while (MillisecondsSince(inputTicks) < 3000)
                    {
                        var next = FindObjectsByType<Button>(FindObjectsSortMode.None)
                            .SingleOrDefault(b => ObjectPath(b.transform) == request.nextControlPath);
                        if (next != null && Hit(next, out _, out _)) { ready = true; break; }
                        yield return new WaitForEndOfFrame();
                    }
                    response.timing.nextControlTimedOut = !ready;
                    if (ready)
                    {
                        response.timing.nextControlReadyMilliseconds = MillisecondsSince(inputTicks);
                        response.timing.postFrameInputBlockedMilliseconds = Math.Max(0,
                            response.timing.nextControlReadyMilliseconds - response.timing.firstRenderedFrameMilliseconds);
                    }
                }
            }
            // UI controller redraws in LateUpdate; wait for the actual redraw before capturing.
            yield return null;
            if(!request.fastCapture)yield return new WaitForSecondsRealtime(0.15f);
            if (request.screenshot) yield return new WaitForEndOfFrame();
            if (request.evaluationDiagnostics && request.sampleFrames > 0)
            {
                response.evaluationFrames = new List<EvaluationFrame>();
                for (int i = 0; i < Math.Min(request.sampleFrames, 120); i++)
                {
                    yield return new WaitForEndOfFrame();
                    response.evaluationFrames.Add(new EvaluationFrame { frame = Time.frameCount,
                        realtimeSeconds = Time.realtimeSinceStartup,
                        unscaledDeltaMilliseconds = Time.unscaledDeltaTime * 1000f });
                }
            }
            try
            {
                response.observation = Observe();
                if (request.evaluationDiagnostics) response.evaluation = CaptureEvaluationEvidence();
                if (_runtimeError != null) throw new InvalidOperationException("Runtime error: " + _runtimeError);
                if (request.screenshot)
                {
                    string captures = Path.Combine(_directory, "captures"); Directory.CreateDirectory(captures);
                    response.screenshotPath = Path.Combine(captures, request.id + ".png");
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    if (texture == null) throw new InvalidOperationException("No Game View screenshot.");
                    try { File.WriteAllBytes(response.screenshotPath, texture.EncodeToPNG()); }
                    finally { Destroy(texture); }
                }
                response.success = string.IsNullOrEmpty(response.error);
                if (response.click != null)
                {
                    response.click.success = response.success;
                    response.click.afterState = response.observation.state;
                    response.click.runtimeRule = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .OfType<ISceneAutomationStateSource>().Single().LastRule;
                    response.click.screenshotPath = response.screenshotPath;
                }
            }
            catch (Exception ex) { response.error = ex.ToString(); response.success = false; }
            response.elapsedSeconds = Time.realtimeSinceStartup - started;
            Write(response); _busy = false;
            if (stop)
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.EraseString(SessionKey);
                UnityEditor.EditorApplication.ExitPlaymode();
#endif
            }
        }
        private static double MillisecondsSince(long ticks) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - ticks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        private void Write(Response response)
        {
            Directory.CreateDirectory(_directory);
            string responses = Path.Combine(_directory, "responses");
            Directory.CreateDirectory(responses);
            string temporary = Path.Combine(responses, response.id + ".tmp");
            File.WriteAllText(temporary, JsonUtility.ToJson(response, true));
            File.Move(temporary, Path.Combine(responses, response.id + ".json"));
        }
    }
}
