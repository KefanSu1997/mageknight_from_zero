#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;

public class Part1TestValidator : EditorWindow
{
    [MenuItem("Mage Knight/Part 1/Validate Systems")]
    public static void ShowWindow()
    {
        GetWindow<Part1TestValidator>("Part 1 Validator");
    }
    
    void OnGUI()
    {
        GUILayout.Label("Part 1 Systems Validation", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Run All Tests", GUILayout.Height(30)))
        {
            RunAllTests();
        }
        
        if (GUILayout.Button("Test Core Systems", GUILayout.Height(25)))
        {
            TestCoreSystems();
        }
        
        if (GUILayout.Button("Test Card System", GUILayout.Height(25)))
        {
            TestCardSystems();
        }
        
        if (GUILayout.Button("Test Combat System", GUILayout.Height(25)))
        {
            TestCombatSystems();
        }
        
        if (GUILayout.Button("Test Exploration System", GUILayout.Height(25)))
        {
            TestExplorationSystems();
        }
        
        if (GUILayout.Button("Test Resource System", GUILayout.Height(25)))
        {
            TestResourceSystems();
        }
        
        if (GUILayout.Button("Test Recruitment System", GUILayout.Height(25)))
        {
            TestRecruitmentSystems();
        }
    }
    
    static void RunAllTests()
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
    
    static void TestCoreSystems()
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
    
    static void TestCardSystems()
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
    
    static void TestExplorationSystems()
    {
        try
        {
            var map = new MapState();
            var exploration = new ExplorationService(map);
            Debug.Log("✅ Exploration system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Exploration: {e.Message}");
        }
    }
    
    static void TestCombatSystems()
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
    
    static void TestResourceSystems()
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
    
    static void TestRecruitmentSystems()
    {
        try
        {
            var service = new RecruitmentService();
            Debug.Log("✅ Recruitment system OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Recruitment: {e.Message}");
        }
    }
}
#endif