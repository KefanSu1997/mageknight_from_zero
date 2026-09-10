using System;
using System.Linq;
using MageKnight.Adventure.Content;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Adventure;
using NUnit.Framework;
using UnityEngine;

namespace MK.Tests.Adventure
{
    public class AdventureSessionTests
    {
        private static AdventureDefinition Content(string name) => Resources.Load<AdventureDefinition>("Adventure/Scenarios/" + name);
        private static AdventureSession Session(string name) => new(Content(name).Snapshot());
        private static void Do(AdventureSession s, params string[] commands)
        { foreach (string command in commands) Assert.That(s.Execute(command), Is.True, command + " → " + s.Message); }
        private static void Play(AdventureSession s, int card, string target) => Do(s, "card:" + card, "target:" + target, "confirm");

        [Test]
        public void SelectionAndMissingTargetDoNotSpendCardsOrMana()
        {
            var s = Session("01_combat"); Do(s, "card:0");
            Assert.That(s.Hand.Count, Is.EqualTo(5)); Assert.That(s.Execute("confirm"), Is.False);
            Assert.That(s.Hand.Count, Is.EqualTo(5)); Assert.That(s.Played, Is.Empty);
            Do(s, "card:2"); Assert.That(s.Selected.Definition.Name, Is.EqualTo("狂怒"));
            Assert.That(s.Preview(s.Selected.Definition), Is.EqualTo(2), "狂怒基础也可以格挡2");
            Assert.That(s.Execute("enhance"), Is.False, "狂怒强化只能攻击，不能格挡4");
            Do(s, "cancel"); Assert.That(s.SelectedCard, Is.EqualTo(-1));
            Assert.That(s.ReadState()["redCrystal"], Is.EqualTo("1"));
        }

        [Test]
        public void PartialBlockGivesFullDamageAndRealWoundCardsButNeverDoubleDamage()
        {
            var s = Session("01_combat"); Play(s, 0, "enemy:0");
            Assert.That(s.Battle.EffectiveBlock(0), Is.EqualTo(2));
            Assert.That(s.Execute("card:0"), Is.False, "已打出的实例不能重用");
            Do(s, "confirm"); Assert.That(s.Player.Wounds, Is.EqualTo(2));
            Assert.That(s.Hand.Count, Is.EqualTo(6)); Assert.That(s.ReadState()["woundCards"], Is.EqualTo("2"));
            Assert.That(s.Execute("card:10"), Is.False, "伤牌无法作为行动打出");
            Play(s, 2, "enemy:0"); Play(s, 3, "enemy:0"); Do(s, "confirm");
            Assert.That(s.Player.Fame, Is.EqualTo(3)); Assert.That(s.Player.Wounds, Is.EqualTo(2));
            Assert.That(s.Completed, Is.True); Assert.That(s.Execute("confirm"), Is.False);
            Assert.That(s.ReadState()["actionCardTotal"], Is.EqualTo("10"));
        }

        [Test]
        public void EnhancedCardPaysOnceAndMissingManaPreservesCard()
        {
            var s = Session("05_wolf"); Do(s, "card:0", "target:enemy:0", "enhance", "confirm");
            Play(s, 1, "enemy:0"); Do(s, "confirm");
            Do(s, "card:2", "target:enemy:0", "enhance", "confirm");
            Assert.That(s.ReadState()["redCrystal"], Is.EqualTo("0")); Assert.That(s.Battle.EffectiveAttack(0), Is.EqualTo(4));
            Play(s, 3, "enemy:0");
            Do(s, "confirm"); Assert.That(s.Completed, Is.True);
            var noMana = new AdventureSession(Content("01_combat").Snapshot() with { RedCrystals = 0 });
            Do(noMana, "confirm", "card:2", "target:enemy:0", "enhance");
            int hand = noMana.Hand.Count; Assert.That(noMana.Execute("confirm"), Is.False);
            Assert.That(noMana.Hand.Count, Is.EqualTo(hand)); Assert.That(noMana.Battle.EffectiveAttack(0), Is.Zero);
        }

        [Test]
        public void ExplorationSeparatesRevealAndMovementAndRejectsInvalidDestinations()
        {
            var s = Session("02_exploration"); Do(s, "target:site:forest");
            Assert.That(s.Execute("confirm"), Is.False);
            Do(s, "card:0", "target:site:forest", "enhance", "confirm", "confirm");
            Assert.That(s.Movement, Is.EqualTo(1)); Assert.That(s.CurrentSite.Id, Is.EqualTo("forest"));
            Play(s, 1, "site:frontier"); Do(s, "confirm"); Assert.That(s.Movement, Is.EqualTo(1));
            Assert.That(s.CurrentSite.Id, Is.EqualTo("forest")); Assert.That(s.Execute("confirm"), Is.False);
            Do(s, "target:site:lake"); Assert.That(s.Execute("confirm"), Is.False);
            Assert.That(s.Movement, Is.EqualTo(1)); Assert.That(s.CardTotal, Is.EqualTo(10));
        }

        [Test]
        public void RecruitRequiresLocationFundsAndCapacityAndCannotDuplicate()
        {
            var s = Session("03_recruitment"); Do(s, "target:offer:guard"); Assert.That(s.Execute("confirm"), Is.False);
            Assert.That(s.Influence, Is.Zero);
            Do(s, "card:0", "target:offer:guard", "enhance", "confirm");
            Play(s, 1, "offer:guard"); Do(s, "confirm");
            Assert.That(s.Influence, Is.EqualTo(1)); Assert.That(s.Player.Units.Count, Is.EqualTo(1));
            Do(s, "target:offer:guard"); Assert.That(s.Execute("confirm"), Is.False);
            var wealthy = new AdventureSession(Content("03_recruitment").Snapshot() with { StartingInfluence = 20 });
            Do(wealthy, "target:offer:monk"); Assert.That(wealthy.Execute("confirm"), Is.False);
            Assert.That(wealthy.Influence, Is.EqualTo(20));
            Do(wealthy, "target:offer:guard", "confirm", "target:offer:ranger");
            Assert.That(wealthy.Execute("confirm"), Is.False); Assert.That(wealthy.Influence, Is.EqualTo(15));
        }

        [Test]
        public void JourneyKeepsActualCardsCrystalsAndUnitsAcrossTurns()
        {
            var s = Session("04_journey");
            Do(s, "card:0", "target:site:village", "enhance", "confirm", "confirm", "confirm");
            Assert.That(s.Phase, Is.EqualTo(AdventurePhase.Interaction)); Assert.That(s.Movement, Is.Zero);
            Do(s, "card:1", "target:offer:guard", "enhance", "confirm");
            Play(s, 2, "offer:guard"); Do(s, "confirm", "confirm");
            Assert.That(s.Phase, Is.EqualTo(AdventurePhase.Block)); Assert.That(s.Turn, Is.EqualTo(2));
            Assert.That(s.Discard.Count, Is.EqualTo(3)); Assert.That(s.DeckCount, Is.EqualTo(2));
            Assert.That(s.Hand.Select(c => c.Serial), Is.EqualTo(new[] { 3, 4, 5, 6, 7 }));
            Assert.That(s.Movement, Is.Zero); Assert.That(s.Influence, Is.Zero);
            Do(s, "unit:guard"); Assert.That(s.Player.Units[0].IsReady, Is.True);
            Assert.That(s.Execute("confirm"), Is.False, "未指定敌人不能用部队");
            Do(s, "target:enemy:0", "confirm"); Assert.That(s.Execute("unit:guard"), Is.False);
            Do(s, "card:5", "enhance", "confirm"); Assert.That(s.Battle.EffectiveBlock(0), Is.EqualTo(8));
            Do(s, "confirm", "card:3", "target:enemy:0", "enhance", "confirm");
            Play(s, 4, "enemy:0"); Do(s, "confirm");
            Assert.That(s.Completed, Is.True); Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.Fame, Is.EqualTo(4));
            Assert.That(s.Player.Units[0].IsReady, Is.False);
            foreach (string color in new[] { "redCrystal", "blueCrystal", "greenCrystal", "whiteCrystal" })
                Assert.That(s.ReadState()[color], Is.EqualTo("0"), color);
            Assert.That(s.CardTotal, Is.EqualTo(10));
        }

        [Test]
        public void TargetsAndElementPoolsRemainIndependentAndAggregateBeforeRounding()
        {
            var baseEnemy = Content("01_combat").encounter.enemies[0].Snapshot();
            var round = new EncounterRound(new EncounterSpec("pair", new[] {
                baseEnemy with { Armor = 5, Element = Element.Fire, Abilities = new[] { Ability.FireResist } }, baseEnemy }));
            round.Allocate(0, CardAction.Block, 3, Element.Physical); round.Allocate(0, CardAction.Block, 5, Element.Physical);
            Assert.That(round.EffectiveBlock(0), Is.EqualTo(4)); Assert.That(round.EffectiveBlock(1), Is.Zero);
            round.Allocate(1, CardAction.Block, 4, Element.Physical);
            var player = new PlayerState(); round.ResolveDefense(player); Assert.That(player.Wounds, Is.Zero);
            round.Allocate(0, CardAction.Attack, 3, Element.Fire); round.Allocate(0, CardAction.Attack, 3, Element.Fire);
            round.Allocate(0, CardAction.Attack, 2, Element.Physical);
            Assert.That(round.EffectiveAttack(0), Is.EqualTo(5)); Assert.That(round.EffectiveAttack(1), Is.Zero);
            round.ResolveAttack(player); Assert.That(round.Defeated, Is.EqualTo(new[] { true, false }));
            Assert.That(player.Fame, Is.EqualTo(3)); Assert.Throws<InvalidOperationException>(() => round.ResolveAttack(player));
        }

        [Test]
        public void PoisonAndParalysisAreReflectedInCardZones()
        {
            var definition = Content("01_combat").Snapshot();
            var enemy = definition.Encounter.Enemies[0] with { Abilities = new[] { Ability.Poison, Ability.Paralyze } };
            var s = new AdventureSession(definition with { Encounter = new EncounterSpec("poison", new[] { enemy }) });
            Do(s, "confirm"); Assert.That(s.Hand.Count, Is.EqualTo(2)); Assert.That(s.Discard.Count, Is.EqualTo(7));
            Assert.That(s.Hand.All(c => c.Definition.Source == null), Is.True);
            Assert.That(s.ReadState()["actionCardTotal"], Is.EqualTo("10"));
        }

        [Test]
        public void ContentSharesArtAndStatEditsAffectNewSessionWithoutUiChanges()
        {
            var forest = Content("05_wolf"); var village = Content("04_journey"); var combat = Content("01_combat");
            Assert.That(forest.hero, Is.SameAs(village.hero)); Assert.That(combat.hero, Is.SameAs(forest.hero));
            Assert.That(forest.encounter.enemies[0], Is.SameAs(village.encounter.enemies[0]));
            Assert.That(forest.encounter.location, Is.Not.SameAs(village.encounter.location));
            var edited = UnityEngine.Object.Instantiate(forest.encounter.enemies[0]);
            try { edited.armor = 9; Assert.That(edited.Snapshot().Monster().Armor, Is.EqualTo(9)); }
            finally { UnityEngine.Object.DestroyImmediate(edited); }
            Assert.That(forest.encounter.enemies[0].armor, Is.EqualTo(5));
            foreach (var content in Resources.LoadAll<AdventureDefinition>("Adventure/Scenarios"))
            {
                Assert.That(content.hero.artwork, Is.Not.Null); Assert.That(content.startingBackground.background, Is.Not.Null);
                Assert.That(content.deck.All(c => c != null && c.artwork != null), Is.True);
                Assert.That(new AdventureSession(content.Snapshot()).CardTotal, Is.EqualTo(10));
                if (content.encounter != null) Assert.That(content.encounter.enemies.Length, Is.LessThanOrEqualTo(content.encounter.location.enemyAnchors.Length));
                Assert.That(content.offers.Length, Is.LessThanOrEqualTo(content.startingBackground.offerAnchors.Length));
            }
        }
    }
}
