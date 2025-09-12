using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

#if UNITY_EDITOR
public class CreateDemoAssets : EditorWindow
{
    [MenuItem("Tools/Demo/Create Demo Assets")]
    static void CreateDemoAssetsMenu()
    {
        CreateHexTilePrefab();
        CreateDamageNumberPrefab();
        CreatePlayerTokenPrefab();
        CreateCardEffectPrefab();
        
        Debug.Log("Demo assets created successfully!");
    }
    
    static void CreateHexTilePrefab()
    {
        GameObject hexTile = new GameObject("HexTile");
        
        // 创建六边形网格
        MeshFilter meshFilter = hexTile.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = hexTile.AddComponent<MeshRenderer>();
        
        // 创建六边形网格
        Mesh hexMesh = new Mesh();
        
        Vector3[] vertices = new Vector3[7];
        vertices[0] = Vector3.zero;
        
        for (int i = 0; i < 6; i++)
        {
            float angle = 60 * i * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.5f;
        }
        
        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % 6 + 1;
        }
        
        hexMesh.vertices = vertices;
        hexMesh.triangles = triangles;
        hexMesh.RecalculateNormals();
        
        meshFilter.mesh = hexMesh;
        
        // 创建材质
        Material hexMaterial = new Material(Shader.Find("Standard"));
        hexMaterial.color = Color.gray;
        meshRenderer.material = hexMaterial;
        
        // 添加碰撞器
        hexTile.AddComponent<MeshCollider>();
        
        // 保存预制体
        PrefabUtility.SaveAsPrefabAsset(hexTile, "Assets/Demo/Prefabs/HexTile.prefab");
        DestroyImmediate(hexTile);
    }
    
    static void CreateDamageNumberPrefab()
    {
        GameObject damageNumber = new GameObject("DamageNumber");
        
        // 添加Canvas
        Canvas canvas = damageNumber.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        
        // 添加Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(damageNumber.transform);
        textObj.transform.localPosition = Vector3.zero;
        textObj.transform.localScale = Vector3.one * 0.01f;
        
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 50;
        text.color = Color.red;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "-5";
        
        RectTransform rectTransform = text.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 100);
        
        // 添加动画
        Animation animation = damageNumber.AddComponent<Animation>();
        
        // 保存预制体
        PrefabUtility.SaveAsPrefabAsset(damageNumber, "Assets/Demo/Prefabs/DamageNumber.prefab");
        DestroyImmediate(damageNumber);
    }
    
    static void CreatePlayerTokenPrefab()
    {
        GameObject playerToken = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerToken.name = "PlayerToken";
        playerToken.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
        
        // 设置材质
        Renderer renderer = playerToken.GetComponent<Renderer>();
        Material playerMaterial = new Material(Shader.Find("Standard"));
        playerMaterial.color = Color.blue;
        renderer.material = playerMaterial;
        
        // 保存预制体
        PrefabUtility.SaveAsPrefabAsset(playerToken, "Assets/Demo/Prefabs/PlayerToken.prefab");
        DestroyImmediate(playerToken);
    }
    
    static void CreateCardEffectPrefab()
    {
        GameObject cardEffect = new GameObject("CardEffect");
        
        // 添加Canvas
        Canvas canvas = cardEffect.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        
        // 添加背景
        GameObject background = new GameObject("Background");
        background.transform.SetParent(cardEffect.transform);
        background.transform.localPosition = Vector3.zero;
        
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = Color.white;
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(200, 300);
        
        // 添加标题
        GameObject title = new GameObject("Title");
        title.transform.SetParent(background.transform);
        title.transform.localPosition = new Vector3(0, 120, 0);
        
        Text titleText = title.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 24;
        titleText.color = Color.black;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "效果名称";
        
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(180, 40);
        
        // 添加描述
        GameObject description = new GameObject("Description");
        description.transform.SetParent(background.transform);
        description.transform.localPosition = new Vector3(0, 0, 0);
        
        Text descText = description.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        descText.fontSize = 16;
        descText.color = Color.black;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.text = "效果描述";
        
        RectTransform descRect = description.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(180, 200);
        
        // 设置整体缩放
        cardEffect.transform.localScale = Vector3.one * 0.01f;
        
        // 保存预制体
        PrefabUtility.SaveAsPrefabAsset(cardEffect, "Assets/Demo/Prefabs/CardEffect.prefab");
        DestroyImmediate(cardEffect);
    }
}
#endif