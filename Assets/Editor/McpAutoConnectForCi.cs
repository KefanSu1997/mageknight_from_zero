#if UNITY_EDITOR
using MCPForUnity.Editor;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class McpAutoConnectForCi
{
    static McpAutoConnectForCi()
    {
        try
        {
            McpCiBoot.StartStdioForCi();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[McpAutoConnectForCi] Failed to start stdio bridge: {ex.Message}");
        }
    }
}
#endif
