#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace MageKnight.EditorTools
{
    /// <summary>Explicit local one-shot request; ordinary editor loads do nothing.</summary>
    [InitializeOnLoad]
    internal static class CodexLocalMcpStartup
    {
        static CodexLocalMcpStartup()
        {
            EditorApplication.delayCall += StartRequestedBridge;
        }

        private static void StartRequestedBridge()
        {
            string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string request = Path.Combine(project, "AutomationOutputs", "start_local_unity_mcp.request");
            if (!File.Exists(request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += StartRequestedBridge;
                return;
            }
            bool accepted = EditorApplication.ExecuteMenuItem("Tools/MCP/Start HTTP Bridge");
            if (accepted)
                File.Move(request, request + ".consumed");
            File.WriteAllText(request + ".receipt", "menuAccepted=" + accepted);
        }
    }
}
#endif
