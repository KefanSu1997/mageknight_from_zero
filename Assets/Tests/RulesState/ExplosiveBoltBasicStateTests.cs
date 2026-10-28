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
    public sealed class ExplosiveBoltBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action); player.Deck.Hand.Add(card); return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("advanced_card_") ? CardJsonLoader.LoadAdvancedActions()
                : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_041")
            {
                Assert.That(original.Name, Is.EqualTo("爆裂箭矢"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.White, ManaColor.Red }));
            }
            else Assert.That(original.RequiredCrystals, Is.EqualTo(new[] {
                card.Id == "basic_card_007" ? ManaColor.White : card.Id == "basic_card_016" ? ManaColor.Blue : ManaColor.Red }));
            CardPlayActions.Play(player, context, card, original, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static MK.Logic.Data.Monster Enemy(string id)
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == id);
            Assert.That(enemy.Attack, Is.EqualTo(id == "monster_027" ? 6 : 4));
            Assert.That(enemy.Armor, Is.EqualTo(id == "monster_027" ? 5 : id == "monster_032" ? 3 : 4));
            Assert.That(enemy.Fame, Is.EqualTo(id == "monster_027" ? 4 : 2));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Abilities, Is.Empty);
            return enemy;
        }

        private static TurnMachine Begin(PlayerState player, ActionContext context, params MK.Logic.Data.Monster[] enemies)
        {
            context.Enemies = new List<MK.Logic.Data.Monster>(enemies);
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game); turn.BeginBattle(context); return turn;
        }

        private static void Ranged(TurnMachine turn, ActionContext context, int printed, params int[] targets)
        {
            var parts = context.CombatPower.AvailableAttacks(Phase.Ranged);
            Assert.That(parts.Sum(p => p.Value), Is.EqualTo(printed));
            Assert.That(parts.All(p => p.Type == AttackType.Ranged && p.Element == Element.Physical), Is.True);
            turn.ApplyRanged(context.Enemies,
                new[] { new AttackAllocation(targets, printed, Element.Physical, Components: parts) }, context);
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Block));
        }

        private static UnitState Farmer(PlayerState player)
        {
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            Assert.That(unit.Card.Armor, Is.EqualTo(3)); Assert.That(unit.Card.Level, Is.EqualTo(1));
            Assert.That(unit.Card.Actions[0].Kind, Is.EqualTo(UnitAbilityKind.AttackOrBlock));
            Assert.That(unit.Card.Actions[0].Value, Is.EqualTo(2));
            Assert.That(unit.Card.Actions[0].Cost, Is.Empty);
            player.Units.Add(unit); return unit;
        }

        private static void Defense(TurnMachine turn, PlayerState player, ActionContext context, int printed, int required, int oldWounds)
        {
            var blocks = context.CombatPower.AvailableBlocks(0).Select(b => b with { TargetIndex = 0 }).ToArray();
            Assert.That(blocks.Sum(b => b.Value), Is.EqualTo(printed));
            Assert.That(AbilityRules.RequiredBlock(context.Enemies[0]), Is.EqualTo(required));
            turn.SetBlock(printed, Element.Physical);
            var result = turn.ResolveDefense(context.Enemies, blocks, context);
            Assert.That(result.TotalWounds, Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(oldWounds));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
        }

        private static void AttackAndEnd(TurnMachine turn, PlayerState player, ActionContext context,
            int armor, int fame, int wounds, params DeedCard[] originals)
        {
            var result = turn.Attack(new[] { 0 }, context);
            Assert.That(result.PrintedPower, Is.EqualTo(4)); Assert.That(result.EffectivePower, Is.EqualTo(4));
            Assert.That(result.RequiredPower, Is.EqualTo(armor)); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(fame));
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero, "Later Fury has no Explosive Bolt contribution");
            turn.FinishBattle(context);
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.PlayedCards, Is.Empty); Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(originals.Length));
            foreach (var card in originals)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(wounds));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Mana.Tokens.Values.Sum() + player.Mana.Crystals.Values.Sum(), Is.Zero);
        }

        [Test]
        public void WhitePaymentStillRespectsRedImmunityAndActualTargetIdentityThenExpires()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_041"); player.Mana.AddToken(ManaColor.White);
            var killed = Enemy("monster_032"); var survivor = Enemy("monster_027");
            var fire = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_001");
            Assert.That(fire.Abilities, Does.Contain(Ability.FireResist));
            var turn = Begin(player, context, killed, fire, survivor);
            context.SelectedActionManaColor = ManaColor.White; Play(player, context, source, true);
            Ranged(turn, context, 3, 0);
            Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(1));
            foreach (var rejected in new[] { killed, fire, Enemy("monster_027") })
            {
                Assert.Throws<InvalidOperationException>(() => CardCombatActions.ResolveExplosiveBoltArmor(context, rejected));
                Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(1));
                Assert.That(CombatMath.EffectiveArmor(survivor, 1, context), Is.EqualTo(5));
            }
            CardCombatActions.ResolveExplosiveBoltArmor(context, survivor);
            Assert.That(CombatMath.EffectiveArmor(survivor, 1, context), Is.EqualTo(4));
            UnitDamageResolver.EndBattle(player, context);
            Assert.That(CombatMath.EffectiveArmor(survivor, 1, context), Is.EqualTo(5));
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.ResolveExplosiveBoltArmor(context, survivor));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));
        }

        [Test]
        public void BaseRealWoundAndBothCrystalsPayOriginalSwiftnessAndFury()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_041"); var swift = Add(player, "basic_card_007");
            var determination = Add(player, "basic_card_016"); var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Blue);
            var target = Enemy("monster_032"); var survivor = Enemy("monster_013");
            var turn = Begin(player, context, target, survivor);
            Play(player, context, source);
            var sourceWound = player.Deck.Hand.Single(c => c.Type == CardType.Wound);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Play(player, context, swift, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.Zero);
            Ranged(turn, context, 3, 0);
            Assert.That(context.Enemies.Single(), Is.SameAs(survivor));
            Assert.That(player.Fame, Is.EqualTo(2)); Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Play(player, context, determination, true, 1);
            Defense(turn, player, context, 5, 4, 1);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            AttackAndEnd(turn, player, context, 4, 4, 1, source, swift, determination, fury);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(sourceWound));
        }

        [Test]
        public void StrongKillSelectsActualSurvivorArmorAndOriginalFuryConsumesReduction()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_041"); var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021"); var farmer = Farmer(player);
            foreach (var color in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Red }) player.Mana.AddToken(color);
            var target = Enemy("monster_032"); var survivor = Enemy("monster_027");
            var turn = Begin(player, context, target, survivor);
            context.SelectedActionManaColor = ManaColor.White;
            Play(player, context, source, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Ranged(turn, context, 3, 0);
            Assert.That(context.Enemies.Single(), Is.SameAs(survivor));
            Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(survivor, 0, context), Is.EqualTo(5));
            CardCombatActions.ResolveExplosiveBoltArmor(context, survivor);
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Assert.That(survivor.Armor, Is.EqualTo(5), "Never modify the original definition");
            Assert.That(CombatMath.EffectiveArmor(survivor, 0, context), Is.EqualTo(4));
            Play(player, context, determination, true, 1);
            CardUnitActions.Use(player, context, farmer, 0, 1);
            Assert.That(farmer.IsReady, Is.False); Defense(turn, player, context, 7, 6, 0);
            Play(player, context, fury, true);
            AttackAndEnd(turn, player, context, 4, 6, 0, source, determination, fury);
            Assert.That(farmer.Wounds, Is.Zero);
        }

        [Test]
        public void TwoKillsGrantTwoActualSelectionsAndLaterUnrelatedKillGrantsNone()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_041"); var swift = Add(player, "basic_card_007");
            var determination = Add(player, "basic_card_016"); var fury = Add(player, "basic_card_021");
            var farmer = Farmer(player);
            player.Mana.AddToken(ManaColor.White, 2); player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Red);
            var first = Enemy("monster_032"); var second = Enemy("monster_032"); var survivor = Enemy("monster_027");
            Assert.That(second, Is.Not.SameAs(first));
            var turn = Begin(player, context, first, second, survivor);
            context.SelectedActionManaColor = ManaColor.White;
            Play(player, context, source, true); Play(player, context, swift, true);
            Ranged(turn, context, 6, 0, 1);
            Assert.That(context.Enemies.Single(), Is.SameAs(survivor));
            Assert.That(player.Fame, Is.EqualTo(4)); Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(2));
            CardCombatActions.ResolveExplosiveBoltArmor(context, survivor);
            Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(survivor, 0, context), Is.EqualTo(4));
            CardCombatActions.ResolveExplosiveBoltArmor(context, survivor);
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(survivor, 0, context), Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.ResolveExplosiveBoltArmor(context, survivor));
            Assert.That(CombatMath.EffectiveArmor(survivor, 0, context), Is.EqualTo(3));
            Play(player, context, determination, true, 1); CardUnitActions.Use(player, context, farmer, 0, 1);
            Defense(turn, player, context, 7, 6, 0); Play(player, context, fury, true);
            AttackAndEnd(turn, player, context, 3, 8, 0, source, swift, determination, fury);
        }

        [Test]
        public void OriginalRangedContributionUsedInMeleeAlsoCreatesItsActualKillReward()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_041"); var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021"); var farmer = Farmer(player);
            foreach (var color in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Red }) player.Mana.AddToken(color);
            var first = Enemy("monster_032"); var survivor = Enemy("monster_027");
            var turn = Begin(player, context, first, survivor);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            Play(player, context, determination, true, 1); CardUnitActions.Use(player, context, farmer, 0, 1);
            var blocks = context.CombatPower.AvailableBlocks(1).Select(b => b with { TargetIndex = 1 }).ToArray();
            Assert.That(blocks.Sum(b => b.Value), Is.EqualTo(7));
            turn.SetBlock(7, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, blocks, context);
            Assert.That(damage.TotalWounds, Is.EqualTo(2), "Only original032 attack4 is unblocked");
            Assert.That(player.Wounds, Is.EqualTo(2));
            context.SelectedActionManaColor = ManaColor.White;
            Play(player, context, source, true);
            Assert.That(context.RangedPool, Is.EqualTo(3)); Assert.That(context.MeleePool, Is.Zero);
            var firstHit = turn.Attack(new[] { 0 }, context);
            Assert.That(firstHit.PrintedPower, Is.EqualTo(3)); Assert.That(firstHit.RequiredPower, Is.EqualTo(3));
            Assert.That(firstHit.Kills, Is.EqualTo(1)); Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.ExplosiveBoltArmorUses, Is.EqualTo(1));
            Assert.That(context.Enemies[1], Is.SameAs(survivor));
            CardCombatActions.ResolveExplosiveBoltArmor(context, survivor);
            Assert.That(CombatMath.EffectiveArmor(survivor, 1, context), Is.EqualTo(4));
            Play(player, context, fury, true);
            var secondHit = turn.Attack(new[] { 1 }, context);
            Assert.That(secondHit.PrintedPower, Is.EqualTo(4)); Assert.That(secondHit.RequiredPower, Is.EqualTo(4));
            Assert.That(secondHit.Kills, Is.EqualTo(1)); Assert.That(player.Fame, Is.EqualTo(6));
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            turn.FinishBattle(context);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            foreach (var card in new[] { source, determination, fury })
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(context.ExplosiveBoltArmorUses, Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
        }
    }
}
