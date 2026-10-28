using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class DeterminationBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, id == "w" ? CardType.Wound : CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static (PlayerState Player, ActionContext Context, DeedCard Source) Start(bool strong)
        {
            var player = new PlayerState();
            var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_037");
            if (strong) player.Mana.AddToken(ManaColor.Green);
            return (player, context, source);
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var data = (card.Id.StartsWith("basic_card_") ? CardJsonLoader.LoadBasicActions()
                : CardJsonLoader.LoadAdvancedActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_037")
            {
                Assert.That(data.Name, Is.EqualTo("不屈不挠"));
                Assert.That(data.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Green }));
            }
            CardPlayActions.Play(player, context, card, data, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static void Output(PlayerState player, ActionContext context, DeedCard source,
            int option, int value, params DeedCard[] discarded)
        {
            Assert.That(context.MovementPool, Is.EqualTo(option == 0 ? value : 0));
            Assert.That(context.InfluencePool, Is.EqualTo(option == 1 ? value : 0));
            Assert.That(context.MeleePool, Is.EqualTo(option == 2 ? value : 0));
            Assert.That(context.BlockPool, Is.EqualTo(option == 3 ? value : 0));
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(context.HealedHandWoundsThisTurn, Is.Zero, "Discarding a Wound is not healing");
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(discarded.Length));
            foreach (var material in discarded)
            {
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, material)), Is.EqualTo(1));
                Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, material)), Is.False);
            }
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));
            Assert.That(context.CardToDiscard, Is.Null);
            Assert.That(context.CardsToDiscard, Is.Empty);
        }

        private static MK.Logic.Data.Monster Enemy(bool harder)
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(e => e.Id == (harder ? "monster_027" : "monster_013"));
            Assert.That(enemy.Attack, Is.EqualTo(harder ? 6 : 4));
            Assert.That(enemy.Armor, Is.EqualTo(harder ? 5 : 4));
            Assert.That(enemy.Fame, Is.EqualTo(harder ? 4 : 2));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Abilities, Is.Empty);
            return enemy;
        }

        private static TurnMachine Begin(PlayerState player, ActionContext context, bool harder)
        {
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game);
            context.Enemies = new List<MK.Logic.Data.Monster> { Enemy(harder) };
            turn.BeginBattle(context);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            return turn;
        }

        private static void Defense(TurnMachine turn, PlayerState player, ActionContext context,
            int printedBlock, int newWounds)
        {
            int oldWounds = player.Wounds;
            var parts = context.CombatPower.AvailableBlocks(0).Select(b => b with { TargetIndex = 0 }).ToArray();
            Assert.That(parts.Sum(b => b.Value), Is.EqualTo(printedBlock));
            Assert.That(player.Armor, Is.EqualTo(2));
            turn.SetBlock(printedBlock, Element.Physical);
            var result = turn.ResolveDefense(context.Enemies, parts, context);
            Assert.That(result.TotalWounds, Is.EqualTo(newWounds));
            Assert.That(player.Wounds, Is.EqualTo(oldWounds + newWounds));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
        }

        private static void Attack(TurnMachine turn, PlayerState player, ActionContext context,
            int printed, int armor, int fame)
        {
            var result = turn.Attack(new[] { 0 }, context);
            Assert.That(result.PrintedPower, Is.EqualTo(printed));
            Assert.That(result.EffectivePower, Is.EqualTo(printed));
            Assert.That(result.RequiredPower, Is.EqualTo(armor));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(fame));
            Assert.That(context.MeleePool, Is.Zero);
            turn.FinishBattle(context);
        }

        private static void End(PlayerState player, ActionContext context, int newWounds, params DeedCard[] originals)
        {
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(originals.Length));
            foreach (var card in originals)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(newWounds));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Deck.Hand.All(c => !originals.Any(o => ReferenceEquals(o, c))), Is.True);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DrawPile, Is.Empty);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Mana.Tokens, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void NoDiscardMovementChoicePaysOriginalCostAndConsumesRealTerrain(bool strong)
        {
            var (player, context, source) = Start(strong);
            context.MovementMap = new MapState();
            context.CurrentTerrain = TerrainType.Plains;
            context.MovementMap.Placed[new AxialCoord(0, 0)] =
                new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            context.MovementMap.Placed[new AxialCoord(1, 0)] =
                new MapTile(TileSet.Countryside, 1, new[] { strong ? TerrainType.Forest : TerrainType.Plains });
            Play(player, context, source, strong, 0);
            Output(player, context, source, 0, strong ? 3 : 2);
            Assert.That(MovementService.GetCost(new AxialCoord(1, 0), context.MovementMap, DayPart.Day, context),
                Is.EqualTo(strong ? 3 : 2));
            Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(1, 0), context.MovementMap, context), Is.True);
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(context.MovementPool, Is.Zero);
            End(player, context, 0, source);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualWoundOrOrdinaryMaterialRaisesInfluenceAndRecruitsOriginalUnit(bool strong)
        {
            var (player, context, source) = Start(strong);
            var material = Add(player, strong ? "basic_card_008" : "w");
            var promise = strong ? null : Add(player, "basic_card_008");
            if (strong) context.CardsToDiscard.Add(material); else context.CardToDiscard = material;
            Play(player, context, source, strong, 1);
            Output(player, context, source, 1, strong ? 5 : 3, material);
            if (!strong) Play(player, context, promise);
            Assert.That(context.InfluencePool, Is.EqualTo(5));
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            Assert.That(candidate.InfluenceCost, Is.EqualTo(4));
            Assert.That(candidate.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village));
            var offer = new List<UnitCard> { candidate };
            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Village);
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Village, context), Is.EqualTo(4));
            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(1));
            Assert.That(offer, Is.Empty);
            Assert.That(player.Units.Single().Card, Is.SameAs(candidate));
            Assert.That(player.Units.Single().IsReady, Is.True);
            Assert.That(player.Reputation, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            End(player, context, 0, strong ? new[] { source, material } : new[] { source, material, promise });
        }

        [TestCase(false)] [TestCase(true)]
        public void ChosenOldWoundAndOptionalOrdinaryCardRaiseAttackWithoutHealingNewWounds(bool strong)
        {
            var (player, context, source) = Start(strong);
            var oldWound = Add(player, "w");
            var other = Add(player, strong ? "basic_card_008" : "basic_card_021");
            var turn = Begin(player, context, true);
            Defense(turn, player, context, 0, 3);
            var newWounds = player.Deck.Hand.Where(c => c.Type == CardType.Wound && !ReferenceEquals(c, oldWound)).ToArray();
            Assert.That(newWounds.Length, Is.EqualTo(3));
            if (strong) context.CardsToDiscard.AddRange(new[] { oldWound, other });
            else context.CardToDiscard = oldWound;
            Play(player, context, source, strong, 2);
            Output(player, context, source, 2, strong ? 7 : 3,
                strong ? new[] { oldWound, other } : new[] { oldWound });
            Assert.That(player.Wounds, Is.EqualTo(3));
            foreach (var wound in newWounds)
                Assert.That(player.Deck.Hand.Count(c => ReferenceEquals(c, wound)), Is.EqualTo(1));
            if (!strong) Play(player, context, other);
            Attack(turn, player, context, strong ? 7 : 5, 5, 4);
            End(player, context, 3, source, oldWound, other);
        }

        [TestCase(false)] [TestCase(true)]
        public void OptionalOrdinaryMaterialsRaiseBlockAndPrintedFollowupAttackKills(bool strong)
        {
            var (player, context, source) = Start(strong);
            var materials = strong ? new[] { Add(player, "basic_card_000"), Add(player, "basic_card_008") }
                : Array.Empty<DeedCard>();
            var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Red);
            var turn = Begin(player, context, strong);
            if (strong) context.CardsToDiscard.AddRange(materials);
            Play(player, context, source, strong, 3);
            Output(player, context, source, 3, strong ? 7 : 2, materials);
            if (!strong) Play(player, context, determination, option: 1);
            Defense(turn, player, context, strong ? 7 : 4, 0);
            Play(player, context, fury, true);
            if (strong) Play(player, context, determination);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Attack(turn, player, context, strong ? 6 : 4, strong ? 5 : 4, strong ? 4 : 2);
            End(player, context, 0, new[] { source }.Concat(materials).Concat(new[] { determination, fury }).ToArray());
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void InvalidMaterialOrChoiceRejectsBeforeGreenPaymentAndAllCardMutation(int failure)
        {
            var (player, context, source) = Start(true);
            var firstWound = Add(player, "w"); var secondWound = Add(player, "w");
            var actualPromise = Add(player, "basic_card_008");
            if (failure == 0) context.CardsToDiscard.AddRange(new[] { firstWound, secondWound });
            else if (failure == 1) context.CardsToDiscard.AddRange(new[]
                { firstWound, new DeedCard(actualPromise.Id, CardType.Action) });
            var before = player.Deck.Hand.ToArray();
            Assert.Throws<InvalidOperationException>(() => Play(player, context, source, true, failure == 2 ? 4 : 1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(before.Length));
            for (int i = 0; i < before.Length; i++) Assert.That(player.Deck.Hand[i], Is.SameAs(before[i]));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(context.MovementPool + context.InfluencePool + context.MeleePool + context.BlockPool, Is.Zero);
            Assert.That(context.HealedHandWoundsThisTurn, Is.Zero);
        }
    }
}
