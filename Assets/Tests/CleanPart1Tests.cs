using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;
using MK.Logic.Runtime.CardEffects;

namespace MageKnight.Tests.Clean
{
    public class CleanPart1Tests
    {
        [Test]
        public void CoreSystems_LoadSuccessfully()
        {
            Assert.DoesNotThrow(() => {
                var roundClock = new RoundClock();
                var turnEngine = new TurnEngine();
            });
        }
        
        [Test]
        public void CardSystem_EffectsLoadSuccessfully()
        {
            var effect = CardEffectFactory.Get(ActionEffectId.FireballBase);
            Assert.IsNotNull(effect, "Card effects should be loaded");
        }
        
        [Test]
        public void ExplorationSystem_InitializesSuccessfully()
        {
            Assert.DoesNotThrow(() => {
                var map = new MapState();
                var exploration = new ExplorationService(map);
            });
        }
        
        [Test]
        public void CombatSystem_InitializesSuccessfully()
        {
            Assert.DoesNotThrow(() => {
                var attacker = new MK.Logic.Runtime.UnitState(new UnitCard("test", "Test", "Test", 1, 3, 5, RecruitLocation.Village, new AttackProfile[0], new Ability[0]));
                var defender = new MK.Logic.Runtime.UnitState(new UnitCard("test2", "Test2", "Test2", 1, 2, 4, RecruitLocation.Keep, new AttackProfile[0], new Ability[0]));
            });
        }
        
        [Test]
        public void ResourceSystem_InitializesSuccessfully()
        {
            Assert.DoesNotThrow(() => {
                var manaPool = new ManaPool();
                manaPool.ResetTokens();
            });
        }
        
        [Test]
        public void RecruitmentSystem_InitializesSuccessfully()
        {
            Assert.DoesNotThrow(() => {
                var service = new RecruitmentService();
            });
        }
        
        [Test]
        public void AllPart1Systems_IntegrationTest()
        {
            Assert.DoesNotThrow(() => {
                // Create all systems together
                var gameEngine = new GameEngine(new System.Collections.Generic.List<PlayerState>(), 6);
                var player = new PlayerState(1, "TestPlayer");
                var map = new MapState();
                var resources = new ManaPool();
                var recruitment = new RecruitmentService();
                
                // Test card effects
                var effect = CardEffectFactory.Get(ActionEffectId.FireballBase);
                
                // Test unit creation
                var unit = new MK.Logic.Runtime.UnitState(new UnitCard("test", "Test", "Test", 1, 3, 5, RecruitLocation.Village, new AttackProfile[0], new Ability[0]));
            });
        }
    }
}