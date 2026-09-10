#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AutoCreateBallCylinderScene
{
    private const string ScenePath = "Assets/Scenes/BallCylinderScene.unity";
    private const string PurpleMatPath = "Assets/Materials/Purple.mat";
    private const string YellowMatPath = "Assets/Materials/Yellow.mat";

    [MenuItem("Tools/Setup/Create Ball Cylinder Scene")]
    public static void CreateOrUpdateScene()
    {
        EnsureFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "BallCylinderScene";

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "PlayerBall";
        sphere.transform.position = new Vector3(0f, 0.5f, 0f);

        var rb = sphere.GetComponent<Rigidbody>();
        if (rb == null) rb = sphere.AddComponent<Rigidbody>();
        rb.useGravity = true;

        if (sphere.GetComponent<BallController>() == null)
            sphere.AddComponent<BallController>();

        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "TargetCylinder";
        cylinder.transform.position = new Vector3(3f, 1f, 0f);

        var purple = LoadOrCreateMaterial(PurpleMatPath, new Color(0.55f, 0.2f, 0.8f));
        var yellow = LoadOrCreateMaterial(YellowMatPath, new Color(1f, 0.86f, 0.2f));

        var sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null) sphereRenderer.sharedMaterial = purple;

        var cylinderRenderer = cylinder.GetComponent<Renderer>();
        if (cylinderRenderer != null) cylinderRenderer.sharedMaterial = yellow;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 8f, -10f);
            cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Scene created/updated: {ScenePath}");
    }

    [InitializeOnLoadMethod]
    private static void CreateOnceIfMissing()
    {
        if (!File.Exists(ScenePath))
        {
            CreateOrUpdateScene();
        }
    }

    private static Material LoadOrCreateMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");

        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");
    }
}
#endif
