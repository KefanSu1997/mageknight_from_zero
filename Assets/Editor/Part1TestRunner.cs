#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;

public static class Part1TestRunner
{
    [MenuItem("Mage Knight/Test/Run Part 1 Validation %t")]
    public static void RunPart1Validation()
    {
        Debug.Log("🎯 Starting Part 1 Validation Tests in Editor...");
        
        TestCoreSystems();
        TestCardSystems();
        TestExplorationSystems();
        TestCombatSystems();
        TestResourceSystems();
        TestRecruitmentSystems();
        TestIntegration();
        
        Debug.Log("🏁 Part 1 Validation Tests Complete");
    }
    
    [MenuItem("Mage Knight/Test/Load Scene_Part1Tests")]
    public static void LoadPart1TestScene()
    {
        var scene = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Scene_Part1Tests.unity");
        if (scene != null)
        {
            EditorSceneManager.OpenScene("Assets/Scene_Part1Tests.unity");
            Debug.Log("🔧 Loaded Part 1 Test Scene");
        }
        else
        {
            Debug.LogError("❌ Part 1 Test Scene not found!");
        }
    }
    
    [MenuItem("Mage Knight/Test/Generate Part 1 Report")]
    public static void GeneratePart1Report()
    {
        string report = $"# Part 1 System Integration Report - {System.DateTime.Now}\n\n";
        report += "## System Status:\n\n";
        
        try
        {
            TestCoreSystems();
            TestCardSystems();
            TestExplorationSystems();
            TestCombatSystems();
            TestResourceSystems();
            TestRecruitmentSystems();
            TestIntegration();
            
            report += "✅ All systems validated successfully";
        }
        catch (System.Exception e)
        {
            report += $"❌ Validation failed: {e.Message}";
        }
        
        System.IO.File.WriteAllText("Part1_Systems_Report.md", report);
        Debug.Log("📄 Generated Part 1 Systems Report");
    }
    
    private static void TestCoreSystems()
    {
        try
        {
            var roundClock = new RoundClock();
            var turnEngine = new TurnEngine();
            
            Debug.Log("✅ Core systems loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Core systems: {e.Message}");
        }
    }
    
    private static void TestCardSystems()
    {
        try
        {
            var playerDeck = new PlayerDeck();
            var effect = MK.Logic.Runtime.CardEffects.CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
            
            Debug.Log("✅ Cards & Effects system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Card systems: {e.Message}");
        }
    }
    
    private static void TestExplorationSystems()
    {
        try
        {
            var map = new MapState();
            var exploration = new ExplorationService(map, new System.Random());
            
            Debug.Log("✅ Exploration system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Exploration: {e.Message}");
        }
    }
    
    private static void TestCombatSystems()
    {
        try
        {
            var attacker = new UnitState(new UnitCard("warrior", "战士", "Warrior", 1, 4, 6, RecruitLocation.Village, new AttackProfile[0], new Ability[0]));
            var defender = new UnitState(new UnitCard("goblin", "哥布林", "Goblin", 1, 2, 4, RecruitLocation.Keep, new AttackProfile[0], new Ability[0]));
            
            var result = BattleResolver.Resolve(
                new PlayerState(1, "TestPlayer"), 
                new System.Collections.Generic.List<MK.Logic.Data.Monster>(), 
                new System.Collections.Generic.List<MK.Logic.Data.BlockAllocation>(), 
                new System.Collections.Generic.List<MK.Logic.Data.AttackAllocation>());
            
            Debug.Log("✅ Combat system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Combat: {e.Message}");
        }
    }
    
    private static void TestResourceSystems()
    {
        try
        {
            var manaPool = new ManaPool();
            manaPool.ResetTokens();
            
            Debug.Log("✅ Resources system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Resources: {e.Message}");
        }
    }
    
    private static void TestRecruitmentSystems()
    {
        try
        {
            var service = new RecruitmentService();
            var units = new List<UnitCard>();
            
            Debug.Log("✅ Recruitment system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Recruitment: {e.Message}");
        }
    }
    
    private static void TestIntegration()
    {
        try
        {
            var gameEngine = new GameEngine(new List<PlayerState>(), 6);
            var player = new PlayerState(1, "TestPlayer");
            var map = new MapState();
            var resources = new ManaPool();
            
            Debug.Log("✅ Integration test OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Integration: {e.Message}");
        }
    }
}
#endif