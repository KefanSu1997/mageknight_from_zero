// Assets/Editor/CcrCompileBridge.cs
// Unity 2020+ 可用。作用：
// - 轮询 .ccr_bridge/request_*.json 是否有新请求
// - 收到后：若在 Playmode 则退出，强制 Refresh，触发脚本编译
// - 通过 CompilationPipeline 事件收集错误和警告
// - 将结果写入 .ccr_bridge/result_{id}.json，供外部读取
// - 【新增】完整打点：ack_ / trace_ / progress_ 文件，定位耗时阶段

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System.Text;

[InitializeOnLoad]
public static class CcrCompileBridge
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly string ProjectRoot =
        Directory.GetParent(Application.dataPath).FullName;
    private static readonly string BridgeDir =
        Path.Combine(ProjectRoot, ".ccr_bridge");

    private static readonly string LastIdKey = "CCR_LAST_REQ_ID";
    private static bool _isProcessing = false;
    private static string _currentId = null;
    private static DateTime _compileStartUtc;
    private static readonly List<CompilerMessage> _messages = new List<CompilerMessage>();

    // 进度打点（每隔 N 秒触碰一次 progress_<id>.txt）
    private static DateTime _nextProgressTickUtc;
    private const int ProgressTickSeconds = 5;

    static CcrCompileBridge()
    {
        // 避免重放半年前的请求及每帧写入巨大轨迹日志；旧外部循环按需启用。
        if (!EditorPrefs.GetBool("MageKnight.LegacyCcrBridge.Enabled", false))
            return;

        if (!Directory.Exists(BridgeDir))
            Directory.CreateDirectory(BridgeDir);

        EditorApplication.update += OnEditorUpdate;

        CompilationPipeline.compilationStarted += OnCompilationStarted;
#if UNITY_2020_2_OR_NEWER
        CompilationPipeline.compilationFinished += OnCompilationFinishedAll;
#endif
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        Debug.Log("[CCR Bridge] Initialized. Watching " + BridgeDir);
    }

    private static void OnEditorUpdate()
    {
        // 进行中时：若处于编译阶段，则定期更新 progress 文件，表示“还活着”
        if (_isProcessing && EditorApplication.isCompiling)
        {
            if (DateTime.UtcNow >= _nextProgressTickUtc && !string.IsNullOrEmpty(_currentId))
            {
                TouchProgress(_currentId, "compiling");
                _nextProgressTickUtc = DateTime.UtcNow.AddSeconds(ProgressTickSeconds);
            }
        }

        if (_isProcessing || EditorApplication.isCompiling) return;

        try
        {
            // 选择最新的 request_*.json
            var reqFiles = Directory.GetFiles(BridgeDir, "request_*.json");
            if (reqFiles.Length == 0) return;

            Array.Sort(reqFiles, StringComparer.OrdinalIgnoreCase);
            var latest = reqFiles.Last();

            Stamp(null, "poll_tick"); // 背景打点（可注掉）

            var json = File.ReadAllText(latest, Encoding.UTF8);
            var req = JsonUtility.FromJson<SimpleRequest>(json);
            if (req == null || string.IsNullOrEmpty(req.id)) return;

            var lastId = EditorPrefs.GetString(LastIdKey, "");
            if (lastId == req.id) return; // 已处理

            // 开始处理
            _isProcessing = true;
            _currentId = req.id;
            _messages.Clear();
            _compileStartUtc = DateTime.UtcNow;

            // ACK：Unity 已收到
            WriteAck(_currentId);
            Stamp(_currentId, "request_read");

            // 如在 Playmode，先退出
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[CCR Bridge] Exiting Play Mode for compilation.");
                EditorApplication.isPlaying = false;
                Stamp(_currentId, "exit_playmode");
            }

            // 强制导入刷新，确保外部改动被识别
            Stamp(_currentId, "before_refresh");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Stamp(_currentId, "after_refresh");

            // 请求脚本编译
            _nextProgressTickUtc = DateTime.UtcNow.AddSeconds(ProgressTickSeconds);
            Stamp(_currentId, "before_request_compile");
            CompilationPipeline.RequestScriptCompilation();
            // 等待 OnCompilationFinishedAll/OnAssemblyCompilationFinished 回写结果
        }
        catch (Exception e)
        {
            Debug.LogError("[CCR Bridge] Request handling error: " + e);
            Stamp(_currentId, "request_error:" + e.GetType().Name);
            // 避免卡死
            _isProcessing = false;
            _currentId = null;
            _messages.Clear();
        }
    }

    private static void OnCompilationStarted(object _)
    {
        if (_currentId != null)
        {
            Debug.Log("[CCR Bridge] Compilation started for request: " + _currentId);
            Stamp(_currentId, "compilation_started");
            TouchProgress(_currentId, "compiling");
            _nextProgressTickUtc = DateTime.UtcNow.AddSeconds(ProgressTickSeconds);
        }
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        if (_currentId == null) return;
        if (messages != null && messages.Length > 0)
        {
            _messages.AddRange(messages);
        }
    }

#if UNITY_2020_2_OR_NEWER
    private static void OnCompilationFinishedAll(object _)
    {
        if (_currentId == null) return;

        try
        {
            var finishedAtUtc = DateTime.UtcNow;

            Stamp(_currentId, "compiled");      // 编译完成时间点
            Stamp(_currentId, "collect_begin"); // 开始整理诊断

            var errors = new List<Diag>();
            var warnings = new List<Diag>();

            foreach (var m in _messages)
            {
                var diag = new Diag
                {
                    assembly = FindAssemblyName(m),
                    file = m.file,
                    line = m.line,
                    column = m.column,
                    message = m.message,
                    code = ExtractCsCode(m.message),
                    type = m.type.ToString()
                };

                if (m.type == CompilerMessageType.Error) errors.Add(diag);
                else if (m.type == CompilerMessageType.Warning) warnings.Add(diag);
            }

            Stamp(_currentId, "collect_end");

            var result = new CompileResult
            {
                id = _currentId,
                startedAtUtc = _compileStartUtc.ToString("o"),
                finishedAtUtc = finishedAtUtc.ToString("o"),
                success = errors.Count == 0,
                errors = errors.ToArray(),
                warnings = warnings.ToArray()
            };

            var outPath = Path.Combine(BridgeDir, $"result_{_currentId}.json");
            var json = JsonUtility.ToJson(result, prettyPrint: true);

            Stamp(_currentId, "before_write_result");
            File.WriteAllText(outPath, json, Utf8NoBom);
            Stamp(_currentId, "after_write_result");

            EditorPrefs.SetString(LastIdKey, _currentId);
            Debug.Log($"[CCR Bridge] Compilation finished. success={result.success}, errors={errors.Count}, warnings={warnings.Count}. Result -> {outPath}");
            Stamp(_currentId, $"done success={result.success} err={errors.Count} warn={warnings.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError("[CCR Bridge] Writing result failed: " + e);
            Stamp(_currentId, "write_result_error:" + e.GetType().Name);
        }
        finally
        {
            _isProcessing = false;
            _currentId = null;
            _messages.Clear();
        }
    }
#endif

    // ===== 辅助：打点/ACK/进度 =====

    private static void EnsureBridgeDir()
    {
        if (!Directory.Exists(BridgeDir))
            Directory.CreateDirectory(BridgeDir);
    }

    private static void WriteAck(string id)
    {
        try
        {
            EnsureBridgeDir();
            var ackPath = Path.Combine(BridgeDir, $"ack_{id}.json");
            File.WriteAllText(ackPath, "{\"ack\":true}",Utf8NoBom);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CCR Bridge] WriteAck failed: " + e.Message);
        }
    }

    private static void TouchProgress(string id, string note)
    {
        try
        {
            EnsureBridgeDir();
            var p = Path.Combine(BridgeDir, $"progress_{id}.txt");
            File.WriteAllText(p, $"{DateTime.UtcNow:O} {note}\n", Utf8NoBom);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CCR Bridge] TouchProgress failed: " + e.Message);
        }
    }

    private static void Stamp(string id, string stage)
    {
        try
        {
            EnsureBridgeDir();
            var tag = id ?? "global";
            var trace = Path.Combine(BridgeDir, $"trace_{tag}.log");
            File.AppendAllText(trace, $"{DateTime.UtcNow:O} {stage}\n", Encoding.UTF8);
        }
        catch { /* 忽略打点失败 */ }
    }

    // ===== 诊断字段提取 =====

    private static string FindAssemblyName(CompilerMessage m)
    {
        // Unity 没直接给 assembly 名，这里做一个大致推断
        try
        {
            if (!string.IsNullOrEmpty(m.file) && m.file.Contains("Editor"))
                return "Assembly-CSharp-Editor";
        }
        catch { }
        return "Assembly-CSharp";
    }

    private static string ExtractCsCode(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return "";
        var match = Regex.Match(msg, @"\bCS\d{4}\b");
        return match.Success ? match.Value : "";
    }

    // ===== 数据结构 =====

    [Serializable]
    private class SimpleRequest { public string id; public string action; public string source; }

    [Serializable]
    private class Diag
    {
        public string assembly;
        public string file;
        public int line;
        public int column;
        public string message;
        public string code;   // 例如 CS0103
        public string type;   // Error / Warning / Info
    }

    [Serializable]
    private class CompileResult
    {
        public string id;
        public string startedAtUtc;
        public string finishedAtUtc;
        public bool success;
        public Diag[] errors;
        public Diag[] warnings;
    }
}
#endif
