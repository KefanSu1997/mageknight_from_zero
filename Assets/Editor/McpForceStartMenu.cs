using UnityEditor;

namespace MageKnight.EditorTools
{
    [InitializeOnLoad]
    internal static class McpForceStartMenu
    {
        static McpForceStartMenu()
        {
            if (!EditorPrefs.GetBool("MageKnight.LegacyMcpHttpAutoStart.Enabled", false))
                return;

            EditorApplication.delayCall += TryStartBridgeMenu;
        }

        private static void TryStartBridgeMenu()
        {
            EditorApplication.ExecuteMenuItem("Tools/MCP/Start HTTP Bridge");
        }
    }
}
