using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class DemoPhaseResetStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PveJourneySession Journey()
        {
            var player = new PlayerState(0, "托瓦克");
            player.Deck.SetDeck(new[] { "basic_card_000", "basic_card_016", "basic_card_021", "basic_card_008", "basic_card_014" }
                .Select(id => new DeedCard(id, CardType.Action)));
            player.Mana.AddCrystal(ManaColor.Red, 2); player.Mana.AddCrystal(ManaColor.Blue, 2);
            var journey = new PveJourneySession(player, CardJsonLoader.LoadBasicActions(), manaRandom: new Random(1), tacticRandom: new Random(2));
            journey.SelectTactic("day_1"); journey.BeginTurn();
            return journey;
        }

        [Test] public void MeleeResetRestoresSharedPlayerPhysicalCardsResourcesAndRewards()
        {
            var journey = Journey(); var player = journey.Player;
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var battle = journey.BeginBattle(new[] { enemy });
            battle.Advance(); battle.Advance(); battle.Advance();
            var hand = player.Deck.Hand.ToArray(); var wounds = hand.Where(c => c.Type == CardType.Wound).ToArray();
            var rage = hand.Single(c => c.Id == "basic_card_021");
            var checkpoint = DemoStateCheckpoint.Capture(journey, battle);
            battle.Play(rage, "strong"); battle.Attack(0); battle.Advance();
            Assert.That(battle.Won, Is.True); Assert.That(player.Fame, Is.EqualTo(2));
            checkpoint.Restore();
            Assert.That(journey.Battle, Is.SameAs(battle)); Assert.That(journey.Context, Is.SameAs(battle.Context));
            Assert.That(journey.Player, Is.SameAs(player)); Assert.That(battle.Enemies[0], Is.SameAs(enemy));
            Assert.That(battle.Phase, Is.EqualTo(Phase.Melee)); Assert.That(battle.Context.InBattle, Is.True);
            Assert.That(player.Fame, Is.Zero); Assert.That(player.Mana.Crystals[ManaColor.Red], Is.EqualTo(2));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(hand.Length));
            for (int i = 0; i < hand.Length; i++) Assert.That(player.Deck.Hand[i], Is.SameAs(hand[i]));
            foreach (var wound in wounds) Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, wound)), Is.True);
            Assert.That(battle.Context.DefeatedEnemyIndices, Is.Empty); Assert.That(battle.Context.CombatPower.Attacks, Is.Empty);
            battle.Play(rage, "strong"); battle.Attack(0); battle.Advance();
            Assert.That(player.Fame, Is.EqualTo(2), "same battle can replay without double rewards");
            checkpoint.Restore(); Assert.That(player.Fame, Is.Zero, "checkpoint itself remains reusable");
        }

        [Test] public void DamageResetRestoresActualUnitWoundsReadyStateAndSkillMarkers()
        {
            var journey = Journey(); var player = journey.Player;
            var unit = new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"), "army_2_006"));
            player.Units.Add(unit);
            player.HeroId = "Arythea"; player.Name = "阿瑞希亚"; player.Fame = 3;
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[4];
            HeroSkillAcquisition.Acquire(player, skill);
            var battle = journey.BeginBattle(new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") });
            battle.Advance(); battle.Advance();
            var checkpoint = DemoStateCheckpoint.Capture(journey, battle);
            battle.UseSkill(skill, 0);
            battle.PlanDamage(0, 0, unit); battle.Advance(); unit.Exhaust();
            Assert.That(SkillActions.IsUsed(player, skill), Is.True);
            Assert.That(unit.Wounds, Is.GreaterThan(0)); Assert.That(unit.IsReady, Is.False);
            checkpoint.Restore();
            Assert.That(battle.Phase, Is.EqualTo(Phase.AssignDamage)); Assert.That(player.Units.Single(), Is.SameAs(unit));
            Assert.That(unit.Wounds, Is.Zero); Assert.That(unit.IsReady, Is.True);
            Assert.That(player.Skills.Single(), Is.SameAs(skill));
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
            Assert.That(player.Mana.Crystals[ManaColor.Red], Is.EqualTo(2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.FlippedSkills, Is.Empty); Assert.That(battle.DamagePlan, Is.Empty);
            Assert.That(battle.Context.DamageAssignedUnits, Is.Empty);
        }

        [Test] public void DamagePhaseResetPreservesEarlierBlockPaymentAndActualReservation()
        {
            var journey = Journey(); var player = journey.Player;
            var battle = journey.BeginBattle(new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") });
            battle.Advance();
            var determination = player.Deck.Hand.Single(c => c.Id == "basic_card_016");
            battle.Play(determination, "strong"); battle.Block(0); battle.Advance();
            var checkpoint = DemoStateCheckpoint.Capture(journey, battle);
            battle.Advance(); battle.PlaySideways(player.Deck.Hand.First());
            checkpoint.Restore();
            Assert.That(battle.Phase, Is.EqualTo(Phase.AssignDamage));
            Assert.That(player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1), "earlier phase payment stays spent");
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(determination));
            Assert.That(battle.IsBlocked(0), Is.True); Assert.That(battle.ReservedBlocks.Count, Is.EqualTo(1));
            battle.Advance(); Assert.That(player.Wounds, Is.Zero, "restored TurnMachine keeps real prior defense");
        }

        [Test] public void CollectionsIncludingStructReferencesAndRandomResumeExactCheckpoint()
        {
            var original = new DeedCard("basic_card_000", CardType.Action);
            var list = new List<DeedCard> { original };
            var stack = new Stack<DeedCard>(); stack.Push(original);
            var dictionary = new Dictionary<string, (DeedCard Card, int Count)> { ["same"] = (original, 3) };
            var set = new HashSet<string> { "before" };
            var seeded = new Random(9); var unseeded = new Random();
            var checkpoint = DemoStateCheckpoint.Capture(list, stack, dictionary, set, seeded, unseeded);
            int seedNext = seeded.Next(), unseedNext = unseeded.Next();
            list.AddRange(Enumerable.Range(0, 32).Select(i => new DeedCard("basic_card_000", CardType.Action)));
            stack.Clear(); dictionary.Clear(); set.Add("after");
            checkpoint.Restore();
            Assert.That(list.Single(), Is.SameAs(original)); Assert.That(stack.Peek(), Is.SameAs(original));
            Assert.That(dictionary["same"].Card, Is.SameAs(original)); Assert.That(dictionary["same"].Count, Is.EqualTo(3));
            Assert.That(set, Is.EquivalentTo(new[] { "before" }));
            Assert.That(seeded.Next(), Is.EqualTo(seedNext)); Assert.That(unseeded.Next(), Is.EqualTo(unseedNext));
        }

        [Test] public void UnsupportedExternalStateRejectsCaptureInsteadOfPartialUndo()
            => Assert.Throws<NotSupportedException>(() => DemoStateCheckpoint.Capture(new MemoryStream()));
    }
}
