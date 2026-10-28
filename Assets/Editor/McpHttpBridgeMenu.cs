using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MageKnight.EditorTools
{
    internal static class McpHttpBridgeMenu
    {
        [MenuItem("Tools/MCP/Start HTTP Bridge")]
        private static void StartHttpBridge()
        {
            StartBridge(true);
        }

        [MenuItem("Tools/MCP/Start Local Stdio Bridge")]
        private static void StartLocalBridge()
        {
            StartBridge(false);
        }

        private static void StartBridge(bool useHttp)
        {
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", useHttp);
                EditorPrefs.SetBool("MCPForUnity.ResumeHttpAfterReload", useHttp);
                if (useHttp)
                    EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:60022");

                // The transport resolver uses a cache, not EditorPrefs directly.
                var cacheType = Type.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor");
                var cache = cacheType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                cacheType?.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance)?.Invoke(cache, null);

                var locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor");
                if (locatorType == null)
                {
                    Debug.LogWarning("[MCP] MCPForUnity.Editor not available. Install the package to use the HTTP bridge.");
                    return;
                }

                var bridgeProperty = locatorType.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
                var bridge = bridgeProperty?.GetValue(null);
                if (bridge == null)
                {
                    Debug.LogWarning("[MCP] MCPForUnity Bridge service not found.");
                    return;
                }

                var startAsync = bridge.GetType().GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
                if (startAsync == null)
                {
                    Debug.LogWarning("[MCP] MCPForUnity Bridge StartAsync method not found.");
                    return;
                }

                _ = startAsync.Invoke(bridge, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Start {(useHttp ? "HTTP" : "local Stdio")} Bridge failed: {ex.Message}");
            }
        }
    }
}
