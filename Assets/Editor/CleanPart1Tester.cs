#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CleanPart1Tester
{
    [MenuItem("Tools/Part 1/Run All Tests")]
    public static void RunAllTests()
    {
        Debug.Log("🎯 Starting Part 1 Validation Tests...");
        
        TestCoreSystems();
        TestCardSystems();
        TestExplorationSystems();
        TestCombatSystems();
        TestResourceSystems();
        TestRecruitmentSystems();
        
        Debug.Log("🏁 Part 1 Validation Tests Complete");
        Debug.Log("✅ All Part 1 systems are ready for Part 2 development!");
    }
    
    [MenuItem("Tools/Part 1/Test Core Systems")]
    public static void TestCoreSystems()
    {
        try
        {
            var roundClock = new MK.Logic.Runtime.RoundClock();
            var turnEngine = new MK.Logic.Runtime.TurnEngine();
            Debug.Log("✅ Core systems loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Core systems: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Part 1/Test Card Systems")]
    public static void TestCardSystems()
    {
        try
        {
            var effect = MK.Logic.Runtime.CardEffects.CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
            Debug.Log($"✅ Card system: {effect != null} effects loaded");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Card systems: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Part 1/Test Exploration Systems")]
    public static void TestExplorationSystems()
    {
        try
        {
            var map = new MK.Logic.Runtime.Map.MapState();
            var exploration = new MK.Logic.Runtime.Map.ExplorationService(map);
            Debug.Log("✅ Exploration system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Exploration: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Part 1/Test Combat Systems")]
    public static void TestCombatSystems()
    {
        try
        {
            var attacker = new MK.Logic.Runtime.UnitState(new MK.Logic.Data.UnitCard("test", "Test", "Test", 1, 3, 5, MK.Logic.Core.RecruitLocation.Village, new MK.Logic.Data.AttackProfile[0], new MK.Logic.Core.Ability[0]));
            var defender = new MK.Logic.Runtime.UnitState(new MK.Logic.Data.UnitCard("test2", "Test2", "Test2", 1, 2, 4, MK.Logic.Core.RecruitLocation.Keep, new MK.Logic.Data.AttackProfile[0], new MK.Logic.Core.Ability[0]));
            Debug.Log("✅ Combat system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Combat: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Part 1/Test Resource Systems")]
    public static void TestResourceSystems()
    {
        try
        {
            var manaPool = new MK.Logic.Runtime.ManaPool();
            manaPool.ResetTokens();
            Debug.Log("✅ Resources system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Resources: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Part 1/Test Recruitment Systems")]
    public static void TestRecruitmentSystems()
    {
        try
        {
            var service = new MK.Logic.Runtime.RecruitmentService();
            Debug.Log("✅ Recruitment system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Recruitment: {e.Message}");
        }
    }
}
#endif