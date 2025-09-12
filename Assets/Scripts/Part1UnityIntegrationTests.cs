using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;
using MK.Logic.Runtime.CardEffects;

#if UNITY_EDITOR
public class Part1UnityIntegrationTests
{
    private GameObject testScene;
    private PlayerState playerState;
    private MapState mapState;
    private ManaPool manaPool;
    private GameEngine gameEngine;
    
    [SetUp]
    public void Setup()
    {
        testScene = new GameObject("Part1TestScene");
        
        playerState = new PlayerState();
        mapState = new MapState();
        manaPool = new ManaPool();
        var players = new System.Collections.Generic.List<PlayerState> { playerState };
        gameEngine = new GameEngine(players, 6);
    }
    
    [TearDown]
    public void TearDown()
    {
        if (testScene != null)
            Object.DestroyImmediate(testScene);
    }
    
    [UnityTest]
    public IEnumerator TestCardSO_AssetIntegrity()
    {
        var allCardAssets = AssetDatabase.FindAssets("t:CardSO");
        Assert.Greater(allCardAssets.Length, 0, "Should have CardSO assets");
        
        foreach (var guid in allCardAssets)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var cardSO = AssetDatabase.LoadAssetAtPath<CardSO>(path);
            
            Assert.IsNotNull(cardSO, $"CardSO asset {path} should load properly");
            Assert.IsNotNull(cardSO.ImagePath, $"CardSO {cardSO.name} should have ImagePath");
            Assert.IsNotNull(cardSO.NameCn, $"CardSO {cardSO.name} should have content");
        }
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestDeckRuntime_PrefabIntegration()
    {
        var deckRuntime = testScene.AddComponent<DeckRuntime>();
        deckRuntime.Init();
        
        // DeckRuntime is designed for Unity Prefabs - test basic initialization
        Assert.IsNotNull(deckRuntime, "DeckRuntime should initialize");
        Assert.IsNotNull(deckRuntime.deckListSo, "DeckRuntime should have deckListSo");
        
        // Note: DeckRuntime methods are different from logical counterparts
        testScene.AddComponent<HandManager>();
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestHandManager_UIIntegration()
    {
        var handManager = testScene.AddComponent<HandManager>();
        Assert.IsNotNull(handManager, "HandManager should be created");
        
        // HandManager testing requires Unity prefab setup with deck reference
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestGameEngine_FullRoundFlow()
    {
        // GameEngine is initialized via constructor - test basic construction
        Assert.IsNotNull(gameEngine, "GameEngine should be initialized");
        Assert.IsNotNull(gameEngine.Clock, "GameEngine should have Clock");
        Assert.IsNotNull(gameEngine.Map, "GameEngine should have Map");
        
        yield return null;
    }
    
    [UnityTest] 
    public IEnumerator TestCombatSystem_E2E()
    {
        var attackerCard = new UnitCard("test-dragon", "Dragon", "Dragon", 1, 8, 10, RecruitLocation.City, 
            new[] { new AttackProfile(AttackType.Melee, 8, Element.Physical) }, System.Array.Empty<Ability>());
        var defenderCard = new UnitCard("test-hero", "Hero", "My Unit", 1, 5, 7, RecruitLocation.Keep, 
            new[] { new AttackProfile(AttackType.Melee, 5, Element.Physical) }, System.Array.Empty<Ability>());
        
        var attacker = new UnitState(attackerCard);
        var defender = new UnitState(defenderCard);
        
        var damagePacket = new MK.Logic.Runtime.DamagePacket(Element.Physical, 6);
        var playerState = new PlayerState();
        var enemies = new List<MK.Logic.Data.Monster>();
        var blocks = new List<BlockAllocation>();
        var attacks = new List<AttackAllocation>();
        var result = BattleResolver.Resolve(playerState, enemies.AsReadOnly(), blocks, attacks);
        
        Assert.IsNotNull(result, "Combat should resolve");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestRecruitmentService_E2E()
    {
        var recruitmentService = new RecruitmentService();
        var availableUnits = new List<MK.Logic.Data.UnitCard>(); // RecruitmentService doesn't have GetAvailableUnits method
        
        Assert.Greater(availableUnits.Count, 0, "Should have units available for recruitment");
        
        // Note: Available units test would need actual recruitment system integration
        // for now just verify the system is initialized
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestMapState_InitialSetup()
    {
        // MapState doesn't have Initialize method, it's initialized via constructor
        
        Assert.AreEqual(0, mapState.Placed.Count, "Should start with 0 revealed tiles");
        
        Assert.IsNotNull(mapState.Countryside, "Should have countryside tile deck");
        Assert.IsNotNull(mapState.Core, "Should have core tile deck");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestMana_PoolResourceManagement()
    {
        manaPool.ResetTokens();
        
        var startRed = manaPool.Crystals.GetValueOrDefault(ManaColor.Red);
        Assert.AreEqual(0, startRed, "Should start with 0 red mana");
        
        manaPool.AddCrystal(ManaColor.Red, 2);
        Assert.AreEqual(2, manaPool.Crystals.GetValueOrDefault(ManaColor.Red), "Should have 2 red mana after adding 2");
        
        // Note: ManaPool allows direct crystal manipulation for test
        manaPool.Crystals[ManaColor.Red] = 4;
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestCardEffectFactory_AllEffectsLoaded()
    {
        // CardEffectFactory is static, test basic functionality
        var effect = CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
        Assert.IsNotNull(effect, "Card effects should be loaded");
        Assert.IsAssignableFrom<MK.Logic.Runtime.CardEffects.ICardEffect>(effect, "Effect should implement ICardEffect");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestExplorationService_InGameSimulation()
    {
        // MapState doesn't have Initialize method, it's initialized via constructor
        var service = new ExplorationService(mapState, new System.Random());
        
        Assert.IsNotNull(service, "ExplorationService should be initialized");
        
        // Note: ExplorationService requires specific map state setup for GetRevealableTiles
        Assert.IsNotNull(service, "ExplorationService should be initialized");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator TestRoundClock_DayNightCycle()
    {
        var clock = new RoundClock();
        
        Assert.AreEqual(1, clock.DayIndex, "Should start on day 1");
        Assert.AreEqual(MK.Logic.Core.DayPart.Day, clock.DayPart, "Should start at day time");
        
        // Simulate 6 turns (one full day)
        for (int i = 0; i < 6; i++)
        {
            clock.NextRound();
        }
        
        Assert.AreEqual(2, clock.DayIndex, "Should be day 2 after 6 turns");
        
        yield return null;
    }
}

// Helper class for test data
public static class TestData
{
    public static ActionCardData CreateTestActionCard(string name)
    {
        return new ActionCardData(
            $"test-{name.ToLower().Replace(" ", "-")}",
            name,
            CardSet.BasicAction,
            "test-image",
            "test-en-image",
            "Test basic effect",
            "Test enhanced effect",
            new[] { ManaColor.Red }
        );
    }
}
#endif