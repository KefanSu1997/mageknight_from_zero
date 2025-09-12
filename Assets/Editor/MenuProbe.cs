using UnityEditor;
public class MenuProbe {
    [MenuItem("Tools/Probe/Log OK")]
    static void Ping() => UnityEngine.Debug.Log("Menu works!");
}