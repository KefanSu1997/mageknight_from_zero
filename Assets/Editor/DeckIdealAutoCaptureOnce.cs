#if UNITY_EDITOR
using System.IO;
using MageKnight.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 当存在哨兵文件时，自动触发 DeckIdeal 场景的重建与截图，避免 MCP 失联时无法手动点击菜单。
/// 运行成功后会删除哨兵文件，保持一次性执行。
/// </summary>
[InitializeOnLoad]
public static class DeckIdealAutoCaptureOnce
{
    private const string SentinelRelativePath = "multi-agent-workspace/compile/deckideal_autocapture.flag";

    static DeckIdealAutoCaptureOnce()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var sentinelPath = Path.Combine(projectRoot, SentinelRelativePath);
        if (!File.Exists(sentinelPath))
        {
            return;
        }

        // 在域重载后立刻建场景会触发 Editor 不可更新的断言，改为延迟到下一帧且等待编译完成。
        EditorApplication.delayCall += () => RunDeferredCapture(sentinelPath);
    }

    private static void RunDeferredCapture(string sentinelPath)
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // 再次排队到下一帧，直到 Editor 可安全执行。
            EditorApplication.delayCall += () => RunDeferredCapture(sentinelPath);
            return;
        }

        try
        {
            DeckIdealSceneBuilder.BuildAndCaptureMenu();
            Debug.Log($"[DeckIdealAutoCaptureOnce] Captured DeckIdeal screenshot via sentinel: {sentinelPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DeckIdealAutoCaptureOnce] Failed to capture DeckIdeal: {ex.Message}");
        }
        finally
        {
            try
            {
                File.Delete(sentinelPath);
            }
            catch
            {
                // ignore
            }
        }
    }
}
#endif
