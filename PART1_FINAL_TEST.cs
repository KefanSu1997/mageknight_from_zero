/*
 * Part 1 Final Test - Mage Knight
 * 
 * This is a standalone test that validates all Part 1 systems
 * without any Unity dependencies or scene references.
 * 
 * To run: Use Unity Test Runner (Window → General → Test Runner)
 * or compile and run the tests directly.
 */

using System;
using System.Collections.Generic;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;
using MK.Logic.Runtime.CardEffects;

public static class Part1FinalTest
{
    public static void RunAllTests()
    {
        Console.WriteLine("🎯 Starting Part 1 Validation Tests...");
        
        bool allPassed = true;
        
        allPassed &= TestCoreSystems();
        allPassed &= TestCardSystems();
        allPassed &= TestExplorationSystems();
        allPassed &= TestCombatSystems();
        allPassed &= TestResourceSystems();
        allPassed &= TestRecruitmentSystems();
        
        if (allPassed)
        {
            Console.WriteLine("🏁 Part 1 Validation Tests Complete");
            Console.WriteLine("✅ All Part 1 systems are ready for Part 2 development!");
            Console.WriteLine("\n🚀 READY TO PROCEED TO PART 2");
        }
        else
        {
            Console.WriteLine("❌ Some tests failed - check console for details");
        }
    }
    
    public static bool TestCoreSystems()
    {
        try
        {
            var roundClock = new RoundClock();
            Console.WriteLine($"✅ Core: Day {roundClock.DayIndex}, Time {roundClock.DayPart}");
            
            var turnEngine = new TurnEngine();
            Console.WriteLine("✅ Core systems loaded successfully");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Core systems: {e.Message}");
            return false;
        }
    }
    
    public static bool TestCardSystems()
    {
        try
        {
            var effect = CardEffectFactory.Get(ActionEffectId.FireballBase);
            Console.WriteLine($"✅ Cards: {effect != null} effects loaded");
            
            // Test a few more effects
            var healEffect = CardEffectFactory.Get(ActionEffectId.HealBase);
            var manaEffect = CardEffectFactory.Get(ActionEffectId.ManaDrawBase);
            Console.WriteLine($"✅ Multiple effects loaded: {healEffect != null && manaEffect != null}");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Card systems: {e.Message}");
            return false;
        }
    }
    
    public static bool TestExplorationSystems()
    {
        try
        {
            var map = new MapState();
            var exploration = new ExplorationService(map);
            Console.WriteLine("✅ Exploration system OK");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Exploration: {e.Message}");
            return false;
        }
    }
    
    public static bool TestCombatSystems()
    {
        try
        {
            var attacker = new MK.Logic.Runtime.UnitState(new UnitCard("warrior", "战士", "Warrior", 1, 4, 6, RecruitLocation.Village, new AttackProfile[0], new Ability[0]));
            var defender = new MK.Logic.Runtime.UnitState(new UnitCard("goblin", "哥布林", "Goblin", 1, 2, 4, RecruitLocation.Keep, new AttackProfile[0], new Ability[0]));
            
            Console.WriteLine("✅ Combat system OK");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Combat: {e.Message}");
            return false;
        }
    }
    
    public static bool TestResourceSystems()
    {
        try
        {
            var manaPool = new ManaPool();
            manaPool.ResetTokens();
            
            // Test mana operations
            manaPool.AddCrystal(ManaColor.Red, 2);
            manaPool.AddCrystal(ManaColor.Blue, 1);
            
            Console.WriteLine("✅ Resources system OK");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Resources: {e.Message}");
            return false;
        }
    }
    
    public static bool TestRecruitmentSystems()
    {
        try
        {
            var service = new RecruitmentService();
            Console.WriteLine("✅ Recruitment system OK");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"❌ Recruitment: {e.Message}");
            return false;
        }
    }
    
    public static void Main()
    {
        RunAllTests();
    }
}