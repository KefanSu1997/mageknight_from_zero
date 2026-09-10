#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AutoCreateCatDogBoardScene
{
    private const string ScenePath = "Assets/Scenes/CatDogBoardScene.unity";
    private const string MaterialsDir = "Assets/Materials";
    private const string TexturesDir = "Assets/Textures";

    [MenuItem("Tools/Setup/Create Cat Dog Board Scene")]
    public static void CreateOrUpdateScene()
    {
        EnsureFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";

        var board = GameObject.CreatePrimitive(PrimitiveType.Quad);
        board.name = "WhiteBoard";
        board.transform.position = new Vector3(0f, 2f, 5f);
        board.transform.localScale = new Vector3(4f, 3f, 1f);

        var boardMat = CreateMaterialWithTexture("CatBoard.mat", CreateLabelTexture("CAT", new Color(0.95f, 0.95f, 0.95f), new Color(0.2f, 0.2f, 0.2f), "cat_texture.png"));
        board.GetComponent<Renderer>().sharedMaterial = boardMat;

        var cubeRoot = new GameObject("DogCube");
        cubeRoot.transform.position = new Vector3(0f, 0.5f, 0f);
        var rb = cubeRoot.AddComponent<Rigidbody>();
        rb.useGravity = true;
        cubeRoot.AddComponent<BoxCollider>();
        cubeRoot.AddComponent<CubeWASDController>();

        CreateDogFace(cubeRoot.transform, "Front",  new Vector3(0f, 0f, 0.5f), Quaternion.identity, "DOG1", new Color(0.95f,0.75f,0.55f), "dog1_texture.png");
        CreateDogFace(cubeRoot.transform, "Back",   new Vector3(0f, 0f, -0.5f), Quaternion.Euler(0,180,0), "DOG2", new Color(0.75f,0.85f,0.95f), "dog2_texture.png");
        CreateDogFace(cubeRoot.transform, "Right",  new Vector3(0.5f, 0f, 0f), Quaternion.Euler(0,90,0), "DOG3", new Color(0.85f,0.95f,0.75f), "dog3_texture.png");
        CreateDogFace(cubeRoot.transform, "Left",   new Vector3(-0.5f, 0f, 0f), Quaternion.Euler(0,-90,0), "DOG4", new Color(0.95f,0.85f,0.75f), "dog4_texture.png");
        CreateDogFace(cubeRoot.transform, "Top",    new Vector3(0f, 0.5f, 0f), Quaternion.Euler(-90,0,0), "DOG5", new Color(0.85f,0.75f,0.95f), "dog5_texture.png");
        CreateDogFace(cubeRoot.transform, "Bottom", new Vector3(0f, -0.5f, 0f), Quaternion.Euler(90,0,0), "DOG6", new Color(0.75f,0.95f,0.95f), "dog6_texture.png");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 5f, -9f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CatDogBoardScene created.");
    }

    private static void CreateDogFace(Transform parent, string name, Vector3 localPos, Quaternion localRot, string label, Color bg, string texName)
    {
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = name;
        face.transform.SetParent(parent, false);
        face.transform.localPosition = localPos;
        face.transform.localRotation = localRot;
        var mat = CreateMaterialWithTexture(name + ".mat", CreateLabelTexture(label, bg, Color.black, texName));
        face.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Texture2D CreateLabelTexture(string label, Color bg, Color fg, string fileName)
    {
        var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var pixels = new Color[256 * 256];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
        tex.SetPixels(pixels);

        for (int y = 96; y < 160; y++)
        {
            for (int x = 32; x < 224; x++)
            {
                if ((x + y) % 11 < 2) tex.SetPixel(x, y, fg);
            }
        }

        tex.Apply();

        var bytes = tex.EncodeToPNG();
        var path = TexturesDir + "/" + fileName;
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path);
        var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return imported != null ? imported : tex;
    }

    private static Material CreateMaterialWithTexture(string matName, Texture tex)
    {
        var path = MaterialsDir + "/" + matName;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.mainTexture = tex;
        return mat;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(TexturesDir)) AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts")) AssetDatabase.CreateFolder("Assets", "Scripts");
    }

    [InitializeOnLoadMethod]
    private static void CreateOnceIfMissing()
    {
        if (!File.Exists(ScenePath)) CreateOrUpdateScene();
    }
}
#endif
