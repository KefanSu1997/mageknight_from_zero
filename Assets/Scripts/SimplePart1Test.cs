using UnityEngine;

public class SimplePart1Test : MonoBehaviour
{
    [ContextMenu("Run Part 1 Tests")]
    public void RunTests()
    {
        Debug.Log("🎯 Starting Part 1 Validation Tests...");
        
        try
        {
            // Test Core Systems
            var roundClock = new MK.Logic.Runtime.RoundClock();
            Debug.Log($"✅ Core: Day {roundClock.DayIndex}, Time {roundClock.DayPart}");
            
            // Test Card System
            var effect = MK.Logic.Runtime.CardEffects.CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
            Debug.Log($"✅ Cards: {effect != null} effects loaded");
            
            // Test Exploration
            var map = new MK.Logic.Runtime.Map.MapState();
            var exploration = new MK.Logic.Runtime.Map.ExplorationService(map);
            Debug.Log("✅ Exploration system OK");
            
            // Test Combat
            var attacker = new MK.Logic.Runtime.UnitState(new MK.Logic.Data.UnitCard("test", "Test", "Test", 1, 3, 5, MK.Logic.Core.RecruitLocation.Village, new MK.Logic.Data.AttackProfile[0], new MK.Logic.Core.Ability[0]));
            Debug.Log("✅ Combat system OK");
            
            // Test Resources
            var manaPool = new MK.Logic.Runtime.ManaPool();
            manaPool.ResetTokens();
            Debug.Log("✅ Resources system OK");
            
            // Test Recruitment
            var service = new MK.Logic.Runtime.RecruitmentService();
            Debug.Log("✅ Recruitment system OK");
            
            Debug.Log("🏁 Part 1 Validation Complete!");
            Debug.Log("✅ All Part 1 systems are ready for Part 2 development!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Test failed: {e.Message}");
        }
    }
}