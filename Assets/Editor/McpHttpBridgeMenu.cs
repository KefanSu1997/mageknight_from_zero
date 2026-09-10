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
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetBool("MCPForUnity.ResumeHttpAfterReload", true);
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:60022");

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
                Debug.LogWarning($"[MCP] Start HTTP Bridge failed: {ex.Message}");
            }
        }
    }
}
