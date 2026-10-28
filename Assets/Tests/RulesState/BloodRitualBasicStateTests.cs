using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class BloodRitualBasicStateTests
    {
        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, id.StartsWith("magic_") ? CardType.Spell : CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            ActionCardData original;
            if (card.Type == CardType.Spell)
            {
                var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == card.Id);
                original = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                    spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            }
            else original = (card.Id.StartsWith("advanced_")
                ? CardJsonLoader.LoadAdvancedActions() : CardJsonLoader.LoadBasicActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        private static MapState Plains(int length)
        {
            var map = new MapState();
            for (int q = 0; q <= length; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return map;
        }

        private static TurnMachine BeginBattle(PlayerState player, ActionContext context, out List<Monster> enemies)
        {
            var game = new GameState(); game.Players.Add(player);
            enemies = new List<Monster> { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_013") };
            Assert.That(enemies[0].Armor, Is.EqualTo(4));
            Assert.That(enemies[0].Attack, Is.EqualTo(4));
            Assert.That(enemies[0].Fame, Is.EqualTo(2));
            context.Enemies = enemies;
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Ranged));
            return turn;
        }

        private static void SiegeEightAndFinish(PlayerState player, ActionContext context,
            TurnMachine turn, List<Monster> enemies)
        {
            var targets = new[] { 0 };
            var parts = context.CombatPower.AvailableAttacks(Phase.Ranged, targets, enemies);
            Assert.That(context.SiegePool, Is.EqualTo(8));
            Assert.That(parts.Sum(p => p.Value), Is.EqualTo(8));
            Assert.That(CombatMath.EffectiveAttack(enemies, targets, parts, Phase.Ranged, context), Is.EqualTo(8));
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, context), Is.EqualTo(4));
            turn.ApplyRanged(enemies,
                new[] { new AttackAllocation(targets, 8, Element.Fire, Components: parts) }, context);
            Assert.That(enemies, Is.Empty);
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.SiegePool, Is.Zero);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), context);
            turn.FinishBattle(context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.End));
        }

        private static void EndTurn(PlayerState player, ActionContext context) =>
            new PlayerTurnEngine().EndTurn(player, context.DayPart,
                new ManaSource(1, () => context.DayPart, new Random(18)), context);

        [Test]
        public void BaseBlackTokenAndRedCrystalPayOriginalFirestormAndTwoRealWoundsRemain()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Night };
            var ritual = Add(player, "advanced_card_012"); var firestorm = Add(player, "magic_009");
            var turn = BeginBattle(player, context, out var enemies);
            Play(player, context, ritual, option: (int)ManaColor.Black);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(ritual));
            Play(player, context, firestorm, true);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(2));
            SiegeEightAndFinish(player, context, turn, enemies);
            Assert.That(player.Deck.PlayedCards, Is.EqualTo(new[] { ritual, firestorm }));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            EndTurn(player, context);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { ritual, firestorm }));
            Assert.That(player.Wounds, Is.EqualTo(2));
        }

        [Test]
        public void ThreeDifferentTokensMaySkipCrystalThenPayTwoMarchesAndActualMelee()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day, MovementMap = Plains(4) };
            player.Mana.AddToken(ManaColor.Red);
            context.BloodRitualColors.AddRange(new[] { ManaColor.Green, ManaColor.Gold, ManaColor.Red });
            var ritual = Add(player, "advanced_card_012"); var march1 = Add(player, "basic_card_000");
            var march2 = Add(player, "basic_card_000"); var fury = Add(player, "basic_card_021");
            Play(player, context, ritual, true);
            Assert.That(player.Mana.Tokens, Is.EquivalentTo(new Dictionary<ManaColor, int>
                { { ManaColor.Green, 1 }, { ManaColor.Gold, 1 }, { ManaColor.Red, 1 } }));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Play(player, context, march1, true);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            ManaTokenActions.ConvertGold(player, context, ManaColor.Green);
            Play(player, context, march2, true);
            Assert.That(context.MovementPool, Is.EqualTo(8));
            for (int q = 1; q <= 4; q++)
            {
                var target = new AxialCoord(q, 0);
                Assert.That(new MovementService().TryMoveUsingPool(player, target, context.MovementMap, context), Is.True);
                Assert.That(context.MovementPool, Is.EqualTo(8 - 2 * q));
            }
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(4, 0)));
            var turn = BeginBattle(player, context, out var enemies);
            turn.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), context);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
            Assert.That(player.Wounds, Is.EqualTo(3));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(2));
            Play(player, context, fury, true);
            var attack = turn.Attack(new[] { 0 }, context);
            Assert.That(attack.PrintedPower, Is.EqualTo(4));
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(4));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            turn.FinishBattle(context); EndTurn(player, context);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { ritual, march1, march2, fury }));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(3));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
        }

        [Test]
        public void CrystalChoiceCanUseTheNewThirdRedTokenAndRemainingColorsPayRealConsumers()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Night, MovementMap = Plains(2) };
            player.Mana.AddToken(ManaColor.Red);
            context.BloodRitualColors.AddRange(new[] { ManaColor.Green, ManaColor.Black, ManaColor.Red });
            context.BloodRitualCrystalColor = ManaColor.Red;
            var ritual = Add(player, "advanced_card_012"); var march = Add(player, "basic_card_000");
            var firestorm = Add(player, "magic_009");
            Play(player, context, ritual, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Play(player, context, march, true);
            for (int q = 1; q <= 2; q++)
            {
                Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0), context.MovementMap, context), Is.True);
                Assert.That(context.MovementPool, Is.EqualTo(4 - 2 * q));
            }
            var turn = BeginBattle(player, context, out var enemies);
            Play(player, context, firestorm, true);
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            SiegeEightAndFinish(player, context, turn, enemies);
            EndTurn(player, context);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { ritual, march, firestorm }));
            Assert.That(player.Wounds, Is.EqualTo(2));
        }

        [Test]
        public void InvalidThreeColorSelectionRejectsBeforeThePrintedRedFee()
        {
            foreach (var colors in new[]
            {
                new[] { ManaColor.Green, ManaColor.Red },
                new[] { ManaColor.Green, ManaColor.Red, (ManaColor)6 }
            })
            {
                var player = new PlayerState(); var context = new ActionContext();
                player.Mana.AddToken(ManaColor.Red); context.BloodRitualColors.AddRange(colors);
                var ritual = Add(player, "advanced_card_012");
                Assert.Throws<InvalidOperationException>(() => Play(player, context, ritual, true));
                Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
                Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
                Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
                Assert.That(player.Wounds, Is.Zero);
                Assert.That(player.Deck.Hand.Single(), Is.SameAs(ritual));
                Assert.That(player.Deck.PlayedCards, Is.Empty);
            }
        }

        [Test]
        public void UnaffordablePreselectedCrystalRejectsTheWholeCardBeforeThePrintedFee()
        {
            var player = new PlayerState(); var context = new ActionContext();
            player.Mana.AddToken(ManaColor.Red);
            context.BloodRitualColors.AddRange(new[] { ManaColor.Green, ManaColor.Black, ManaColor.Red });
            context.BloodRitualCrystalColor = ManaColor.Blue;
            var ritual = Add(player, "advanced_card_012");
            Assert.Throws<InvalidOperationException>(() => Play(player, context, ritual, true));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(ritual));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
        }

        [Test]
        public void PreexistingBlueManaPaysOptionalCrystalAlongsideThePrintedRedAndFundsAnOriginalSpell()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            player.Mana.AddToken(ManaColor.Red); player.Mana.AddToken(ManaColor.Blue);
            context.BloodRitualColors.AddRange(new[] { ManaColor.Green, ManaColor.Black, ManaColor.Red });
            context.BloodRitualCrystalColor = ManaColor.Blue;
            var ritual = Add(player, "advanced_card_012"); var snow = Add(player, "magic_015");
            Play(player, context, ritual, true);
            Assert.That(player.Mana.Tokens, Is.EquivalentTo(new Dictionary<ManaColor, int>
                { { ManaColor.Green, 1 }, { ManaColor.Black, 1 }, { ManaColor.Red, 1 } }));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(ritual));
            var turn = BeginBattle(player, context, out var enemies);
            Play(player, context, snow);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(context.RangedPool, Is.EqualTo(5));
            var parts = context.CombatPower.AvailableAttacks(Phase.Ranged, new[] { 0 }, enemies);
            Assert.That(CombatMath.EffectiveAttack(enemies, new[] { 0 }, parts, Phase.Ranged, context), Is.EqualTo(5));
            turn.ApplyRanged(enemies,
                new[] { new AttackAllocation(new[] { 0 }, 5, Element.Ice, Components: parts) }, context);
            Assert.That(enemies, Is.Empty);
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(2));
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), context);
            turn.FinishBattle(context); EndTurn(player, context);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { ritual, snow }));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
        }
    }
}
