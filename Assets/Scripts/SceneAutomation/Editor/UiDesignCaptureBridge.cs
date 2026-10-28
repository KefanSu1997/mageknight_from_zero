using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MageKnight.SceneAutomation.Editor
{
    // Local, explicit capture entry point when the optional MCP server is off.
    // Runtime input still uses the existing StructuredUiPilot EventSystem API.
    [InitializeOnLoad]
    public static class UiDesignCaptureBridge
    {
        [Serializable] private class Request { public string id,operation; }
        [Serializable] private class Receipt
        {
            public string id,error;
            public string bridgeVersion="3";
            public string runtimeAssemblyPath,runtimeModuleVersionId;
            public bool success,isPlaying,isCompiling;
            public bool actualConsoleCountsAvailable;
            public int actualConsoleErrorCount=-1,actualConsoleWarningCount=-1,actualConsoleLogCount=-1;
            public string actualConsoleCountsError;
        }
        private static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../AutomationOutputs/UiFluency/design_review"));
        static UiDesignCaptureBridge(){EditorApplication.update+=Poll;}
        private static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            string requestPath=Path.Combine(Root,"editor_request.json");
            if(!File.Exists(requestPath))return;
            Request request;
            try{request=JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));}catch(IOException){return;}
            if(request==null || string.IsNullOrEmpty(request.id) || SessionState.GetString("MK.UiDesignCapture.Last","")==request.id)return;
            SessionState.SetString("MK.UiDesignCapture.Last",request.id);
            // A finished request survives an Editor restart. Do not replay its
            // launch/stop side effect merely because SessionState was cleared.
            if(File.Exists(Path.Combine(Root,"editor_responses",request.id+".json")))return;
            var response=new Receipt{id=request.id};
            try
            {
                if(request.operation=="start_pilot")StructuredUiPilotMenu.StartPilot();
                else if(request.operation=="stop")EditorApplication.ExitPlaymode();
                else if(request.operation=="refresh")
                {
                    if(EditorApplication.isPlaying)
                        throw new InvalidOperationException("Stop the pilot play session before requesting a source refresh.");
                }
                else if(request.operation=="start_mcp")
                {
                    if(!EditorApplication.ExecuteMenuItem("Tools/MCP/Start HTTP Bridge"))
                        throw new InvalidOperationException("Installed local MCP bridge menu was not found.");
                }
                else if(request.operation!="status")throw new InvalidOperationException("Unknown design capture operation");
                var assembly=typeof(MageKnight.Battle.PveBattleController).Assembly;
                response.runtimeAssemblyPath=assembly.Location;
                response.runtimeModuleVersionId=assembly.ManifestModule.ModuleVersionId.ToString();
                response.success=true;
            }
            catch(Exception error){response.error=error.ToString();}
            response.isPlaying=EditorApplication.isPlaying;response.isCompiling=EditorApplication.isCompiling;
            try
            {
                var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
                var method=type?.GetMethod("GetCountsByType",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                if(method==null)throw new MissingMethodException("UnityEditor.LogEntries.GetCountsByType unavailable");
                object[] counts={0,0,0};method.Invoke(null,counts);
                response.actualConsoleErrorCount=(int)counts[0];
                response.actualConsoleWarningCount=(int)counts[1];
                response.actualConsoleLogCount=(int)counts[2];
                response.actualConsoleCountsAvailable=true;
            }
            catch(Exception error){response.actualConsoleCountsError=error.ToString();}
            Directory.CreateDirectory(Path.Combine(Root,"editor_responses"));
            File.WriteAllText(Path.Combine(Root,"editor_responses",request.id+".json"),JsonUtility.ToJson(response,true));
            if(response.success && request.operation=="refresh")
            {
                // A delayCall can remain queued while the editor has no active
                // inspector repaint. Dispatch from this live update callback,
                // after persisting the receipt and the request-id guard.
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                CompilationPipeline.RequestScriptCompilation();
            }
        }
    }
}
