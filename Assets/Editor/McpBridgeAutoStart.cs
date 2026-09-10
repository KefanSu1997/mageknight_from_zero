// Disabled to avoid auto-start side effects and missing dependency issues.
#if false
using System;
using MCPForUnity.Editor.Services;
using UnityEditor;

namespace MageKnight.EditorTools
{
    [InitializeOnLoad]
    internal static class McpBridgeAutoStart
    {
        static McpBridgeAutoStart()
        {
            EditorApplication.delayCall += TryStartBridge;
        }

        private static async void TryStartBridge()
        {
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetBool("MCPForUnity.ResumeHttpAfterReload", true);
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:60022");

                var bridge = MCPServiceLocator.Bridge;
                if (bridge.IsRunning)
                {
                    return;
                }

                await bridge.StartAsync();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[MCP] Auto-start failed: {ex.Message}");
            }
        }
    }
}
#endif
