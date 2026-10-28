using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class NatureForceBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static MK.Logic.Data.Monster Enemy()
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_007");
            Assert.That(enemy.Attack, Is.EqualTo(3));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Armor, Is.EqualTo(4));
            Assert.That(enemy.Fame, Is.EqualTo(3));
            Assert.That(enemy.Abilities, Is.EquivalentTo(new[] { Ability.Swift }));
            return enemy;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong = false)
        {
            var original = (card.Id.StartsWith("advanced_card_") ? CardJsonLoader.LoadAdvancedActions()
                : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id);
            Assert.That(original.RequiredCrystals, Is.EqualTo(new[]
                { card.Id == "advanced_card_038" ? ManaColor.Green : ManaColor.Red }));
            if (card.Id == "advanced_card_038") Assert.That(original.Name, Is.EqualTo("自然之力"));
            CardPlayActions.Play(player, context, card, original, strong);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static void Begin(TurnMachine turn, ActionContext context, MK.Logic.Data.Monster enemy)
        {
            context.Enemies = new List<MK.Logic.Data.Monster> { enemy };
            turn.BeginBattle(context);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Block));
        }

        private static void AssignPrintedAttack(TurnMachine turn, PlayerState player, ActionContext context,
            UnitState farmer, int expectedUnitWounds)
        {
            Assert.That(context.BlockPool, Is.Zero);
            turn.SetBlock(0, Element.Physical);
            var result = turn.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context,
                new[] { new UnitDamageAllocation(0, new[] { farmer }) });
            Assert.That(result.TotalWounds, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(farmer.Wounds, Is.EqualTo(expectedUnitWounds));
            Assert.That(context.DamageAssignedUnits.Count, Is.EqualTo(1));
            Assert.That(context.DamageAssignedUnits.Contains(farmer), Is.True);
            Assert.That(player.Units.Single(), Is.SameAs(farmer));
            Assert.That(farmer.IsReady, Is.True, "Assigning damage does not exhaust a unit");
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
        }

        [Test]
        public void ActualNatureForceProtectsOriginalFarmerThenExpiresForItsNextCombatDamage()
        {
            var player = new PlayerState();
            var farmer = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            Assert.That(farmer.Card.Armor, Is.EqualTo(3));
            Assert.That(farmer.Card.Level, Is.EqualTo(1));
            Assert.That(farmer.Card.Abilities.Contains(Ability.PhysicalResist), Is.False);
            player.Units.Add(farmer);
            var context = new ActionContext { DayPart = DayPart.Day, TargetUnit = farmer };
            var nature = new DeedCard("advanced_card_038", CardType.Action);
            var fury = new DeedCard("basic_card_021", CardType.Action);
            player.Deck.Hand.AddRange(new[] { nature, fury });
            player.Mana.AddToken(ManaColor.Red);
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game);
            var firstEnemy = Enemy();
            Begin(turn, context, firstEnemy);

            Play(player, context, nature);
            Assert.That(farmer.PhysicalResistTemp, Is.True);
            Assert.That(farmer.IsReady, Is.True);
            Assert.That(farmer.Wounds, Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.SiegePool, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
            // Swift changes required block, not incoming physical damage: printed damage3
            // is fully absorbed by the chosen original farmer's resistant armor3.
            AssignPrintedAttack(turn, player, context, farmer, 0);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            var hit = turn.Attack(new[] { 0 }, context);
            Assert.That(hit.PrintedPower, Is.EqualTo(4));
            Assert.That(hit.EffectivePower, Is.EqualTo(4));
            Assert.That(hit.RequiredPower, Is.EqualTo(4));
            Assert.That(hit.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(3));
            turn.FinishBattle(context);
            Assert.That(farmer.PhysicalResistTemp, Is.False);
            Assert.That(context.DamageAssignedUnits, Is.Empty);
            var turns = new PlayerTurnEngine();
            turns.EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(2));
            Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, nature)), Is.EqualTo(1));
            Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, fury)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.PlayedCards, Is.Empty);

            turns.StartTurn(player, DayPart.Day);
            var secondEnemy = Enemy();
            Assert.That(secondEnemy, Is.Not.SameAs(firstEnemy));
            Begin(turn, context, secondEnemy);
            // The same farmer remains eligible, but the old card's resistance is gone.
            AssignPrintedAttack(turn, player, context, farmer, 1);
            Assert.That(farmer.PhysicalResistTemp, Is.False);
            turn.FinishBattle(context);
            Assert.That(farmer.Wounds, Is.EqualTo(1));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(3), "The second enemy was not attacked or defeated");
            Assert.That(context.DefeatedEnemyIndices, Is.Empty);
            Assert.That(player.Units.Single(), Is.SameAs(farmer));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(2));
            Assert.That(player.Deck.RemovedCards, Is.Empty);
        }
    }
}
