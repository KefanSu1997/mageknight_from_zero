using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MageKnight.EditorTools
{
    [InitializeOnLoad]
    internal static class McpHttpBridgeAutoRecover
    {
        static McpHttpBridgeAutoRecover()
        {
            if (!EditorPrefs.GetBool("MageKnight.LegacyMcpHttpAutoStart.Enabled", false))
                return;

            EditorApplication.delayCall += TryStartBridge;
        }

        private static void TryStartBridge()
        {
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetBool("MCPForUnity.ResumeHttpAfterReload", true);
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:60022");

                var locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor");
                if (locatorType == null)
                {
                    return;
                }

                var bridgeProperty = locatorType.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
                var bridge = bridgeProperty?.GetValue(null);
                if (bridge == null)
                {
                    return;
                }

                var bridgeType = bridge.GetType();
                var isRunningProperty = bridgeType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance);
                if (isRunningProperty != null)
                {
                    var isRunningValue = isRunningProperty.GetValue(bridge);
                    if (isRunningValue is bool running && running)
                    {
                        return;
                    }
                }

                var startAsync = bridgeType.GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
                _ = startAsync?.Invoke(bridge, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Auto recover failed: {ex.Message}");
            }
        }
    }
}
// reload trigger
