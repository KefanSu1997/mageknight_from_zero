using UnityEditor;

public static class CIHooks
{
    public static void CompileAndQuit()
    {
        AssetDatabase.Refresh();
        EditorApplication.Exit(0);
    }
}
