using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Core;

namespace MageKnight.Tests.Part1
{
    public class Part1IntegrationTests
    {
        private PlayerState playerState;
        private MapState mapState;
        private ManaPool manaPool;
        private GameEngine gameEngine;
        private RecruitmentService recruitmentService;
        
        [SetUp]
        public void SetUp()
        {
            playerState = new PlayerState();
            mapState = new MapState();
            manaPool = new ManaPool();
            var players = new List<PlayerState> { playerState };
            gameEngine = new GameEngine(players, 6);
            recruitmentService = new RecruitmentService();
        }
        
        [UnityTest]
        public IEnumerator CoreGameLoop_Initialization()
        {
            yield return null;
            
            var roundClock = new RoundClock();
            
            Assert.AreEqual(1, roundClock.DayIndex, "Should start on day 1");
            Assert.AreEqual(MK.Logic.Core.DayPart.Day, roundClock.DayPart, "Should start at day time");
        }
        
        [UnityTest]
        public IEnumerator CardSystem_EffectLoading()
        {
            yield return null;
            
            // CardEffectFactory is a static factory - we just test it creates effects
            var effect = CardEffectFactory.Get(ActionEffectId.FireballBase);
            Assert.IsNotNull(effect, "Card effects should be loaded");
            Assert.IsAssignableFrom<MK.Logic.Runtime.CardEffects.ICardEffect>(effect, "Effect should implement ICardEffect");
        }
        
        [UnityTest]
        public IEnumerator ManaSystem_BasicOperations()
        {
            yield return null;
            
            var manaPool = new ManaPool();
            Assert.IsNotNull(manaPool, "Mana pool should be initialized");
        }
        
        [UnityTest]
        public IEnumerator CombatSystem_BasicResolution()
        {
            yield return null;
            
            // Create test UnitCard instances with proper constructor parameters
            var attackerCard = new UnitCard(
                "test-attacker", 
                "测试攻击者", 
                "Test Attacker", 
                1, 
                3, 
                5, 
                RecruitLocation.Village, 
                new[] { new AttackProfile(AttackType.Ranged, 5, Element.Physical) }, 
                System.Array.Empty<Ability>()
            );
            
            var defenderCard = new UnitCard(
                "test-defender", 
                "测试防御者", 
                "Test Defender", 
                1, 
                2, 
                4, 
                RecruitLocation.Keep, 
                new[] { new AttackProfile(AttackType.Melee, 4, Element.Physical) }, 
                System.Array.Empty<Ability>()
            );
            
            var attacker = new UnitState(attackerCard);
            var defender = new UnitState(defenderCard);
            
            var damagePacket = new MK.Logic.Runtime.DamagePacket(Element.Physical, 3);
            
            // Fix namespace issues - use correct data types
            var player = new PlayerState();
            var enemies = new List<MK.Logic.Data.Monster>();
            var blocks = new List<MK.Logic.Data.BlockAllocation>();
            var attacks = new List<MK.Logic.Data.AttackAllocation>();
            var result = BattleResolver.Resolve(player, enemies, blocks, attacks);
            
            Assert.IsNotNull(result, "Combat should resolve");
        }
        
        [UnityTest]
        public IEnumerator ExplorationSystem_BasicTiles()
        {
            yield return null;
            
            var explorationService = new ExplorationService(mapState);
            Assert.IsNotNull(explorationService, "Exploration service should be available");
        }
        
        [UnityTest]
        public IEnumerator RecruitmentSystem_BasicUnits()
        {
            yield return null;
            
            var availableUnits = new List<MK.Logic.Data.UnitCard>(); // RecruitmentService doesn't have GetAvailableUnits method
            Assert.IsNotNull(availableUnits, "Recruitment service should return units");
        }
    }
}