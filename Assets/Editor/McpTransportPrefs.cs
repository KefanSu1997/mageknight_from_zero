using System;
using UnityEditor;

namespace MageKnight.EditorTools
{
    [InitializeOnLoad]
    internal static class McpTransportPrefs
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string ResumeHttpAfterReloadKey = "MCPForUnity.ResumeHttpAfterReload";

        static McpTransportPrefs()
        {
            // Ensure MCPForUnity uses the default HTTP transport so the MCP tools can connect.
            EditorPrefs.SetBool(UseHttpTransportKey, true);
            EditorPrefs.SetBool(ResumeHttpAfterReloadKey, true);
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:60022");
        }
    }
}
