using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MK.Logic.Core;

#if UNITY_EDITOR
[InitializeOnLoad]
public class TestSceneSetup
{
    static TestSceneSetup()
    {
        EditorApplication.delayCall += SetupTestScenes;
    }
    
    static void SetupTestScenes()
    {
        // 确保Demo文件夹存在
        if (!AssetDatabase.IsValidFolder("Assets/Demo"))
        {
            AssetDatabase.CreateFolder("Assets", "Demo");
        }
        
        if (!AssetDatabase.IsValidFolder("Assets/Demo/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Demo", "Prefabs");
        }
        
        if (!AssetDatabase.IsValidFolder("Assets/Demo/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets/Demo", "Scenes");
        }
        
        // 创建场景构建配置
        CreateBuildSettings();
        
        Debug.Log("Test scenes setup completed. Use Tools/Demo/Create Demo Assets to generate prefabs.");
    }
    
    static void CreateBuildSettings()
    {
        // 获取当前构建场景
        var scenes = new List<EditorBuildSettingsScene>();
        
        // 添加现有场景
        var existingScenes = EditorBuildSettings.scenes;
        if (existingScenes != null)
        {
            scenes.AddRange(existingScenes);
        }
        
        // 添加测试场景
        string[] testScenes = {
            "Assets/Scenes/CombatDemo.unity",
            "Assets/Scenes/MapDemo.unity",
            "Assets/Scenes/CardEffectsDemo.unity",
            "Assets/Scenes/IntegrationDemo.unity"
        };
        
        foreach (var scenePath in testScenes)
        {
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
        }
        
        // 更新构建设置
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

public class DemoMenuItems
{
    [MenuItem("Tools/Demo/Open Combat Demo")]
    static void OpenCombatDemo()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CombatDemo.unity");
    }
    
    [MenuItem("Tools/Demo/Open Map Demo")]
    static void OpenMapDemo()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MapDemo.unity");
    }
    
    [MenuItem("Tools/Demo/Open Card Effects Demo")]
    static void OpenCardEffectsDemo()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CardEffectsDemo.unity");
    }
    
    [MenuItem("Tools/Demo/Open Integration Demo")]
    static void OpenIntegrationDemo()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/IntegrationDemo.unity");
    }
    
    [MenuItem("Tools/Demo/Quick Test All")]
    static void QuickTestAll()
    {
        // 运行所有测试
        Debug.Log("Starting quick test of all demo scenes...");
        
        // 这里可以添加自动化测试代码
        var testRunner = new DemoTestRunner();
        testRunner.RunAllTests();
    }
}

public class DemoTestRunner
{
    public void RunAllTests()
    {
        Debug.Log("=== 开始Demo测试 ===");
        
        // 测试战斗系统
        TestCombatSystem();
        
        // 测试地图系统
        TestMapSystem();
        
        // 测试卡牌效果
        TestCardEffects();
        
        // 测试集成场景
        TestIntegration();
        
        Debug.Log("=== Demo测试完成 ===");
    }
    
    void TestCombatSystem()
    {
        Debug.Log("测试战斗系统...");
        
        // 创建测试卡牌
        var attackerCard = new MK.Logic.Data.UnitCard("attacker", "攻击者", "测试攻击者", 10, 5, 0,
            MK.Logic.Core.RecruitLocation.Keep, 
            new MK.Logic.Data.AttackProfile[0], 
            new MK.Logic.Core.Ability[0]);
        var defenderCard = new MK.Logic.Data.UnitCard("defender", "防御者", "测试防御者", 8, 4, 0,
            MK.Logic.Core.RecruitLocation.Keep, 
            new MK.Logic.Data.AttackProfile[0], 
            new MK.Logic.Core.Ability[0]);
        
        var attacker = new MK.Logic.Runtime.UnitState(attackerCard);
        var defender = new MK.Logic.Runtime.UnitState(defenderCard);
        
        Debug.Log($"战斗测试: {attackerCard.NameEn} vs {defenderCard.NameEn}");
        Debug.Log($"攻击者状态: 等级{attackerCard.Level}, 护甲{attackerCard.Armor}");
        Debug.Log($"防御者状态: 等级{defenderCard.Level}, 护甲{defenderCard.Armor}");
        
        // 这里可以添加更多战斗测试逻辑
    }
    
    void TestMapSystem()
    {
        Debug.Log("测试地图系统...");
        
        var mapState = new MK.Logic.Runtime.Map.MapState();
        mapState.Countryside = new MK.Logic.Runtime.Map.TileDeck();
        mapState.Core = new MK.Logic.Runtime.Map.TileDeck();
        
        // 添加测试地块
        for (int i = 0; i < 3; i++)
        {
            var edges = new MK.Logic.Core.TerrainType[6];
            for (int j = 0; j < 6; j++)
                edges[j] = (MK.Logic.Core.TerrainType)((i + j) % 6);
            var tile = new MK.Logic.Runtime.Map.MapTile(MK.Logic.Core.TileSet.Core, i, edges);
            mapState.Countryside.Push(tile);
        }
        
        Debug.Log($"地图系统测试: 乡村地块{mapState.Countryside.Count}, 核心地块{mapState.Core.Count}");
    }
    
    void TestCardEffects()
    {
        Debug.Log("测试卡牌效果...");
        
        // 测试几个关键效果
        var effects = new System.Collections.Generic.List<MK.Logic.Runtime.CardEffects.ICardEffect>
        {
            new MK.Logic.Runtime.CardEffects.HealEffect(3),
            new MK.Logic.Runtime.CardEffects.FireballEffect(false),
            new MK.Logic.Runtime.CardEffects.ManaDrawEffect(false)
        };
        
        Debug.Log($"测试了 {effects.Count} 个卡牌效果");
    }
    
    void TestIntegration()
    {
        Debug.Log("测试集成场景...");
        
        // 创建完整的游戏实例
        var players = new System.Collections.Generic.List<MK.Logic.Runtime.PlayerState>();
        var player = new MK.Logic.Runtime.PlayerState(1, "测试玩家");
        players.Add(player);
        var gameEngine = new MK.Logic.Runtime.GameEngine(players, 6);
        
        Debug.Log($"集成测试: 游戏引擎已创建，玩家{player.Name}已加入");
    }
}
#endif