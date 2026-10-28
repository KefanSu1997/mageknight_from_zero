using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class DruidStaffBasicStateTests
    {
        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard staff, DeedCard material) Fixture(string materialId = null)
        {
            var player = new PlayerState();
            var context = new ActionContext { MovementMap = new MapState(), SkipDraw = true };
            for (int q = 0; q <= 2; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            var staff = new DeedCard("items_021", CardType.Artifact);
            player.Deck.Hand.Add(staff);
            DeedCard material = null;
            if (materialId != null)
            {
                material = new DeedCard(materialId, CardType.Action);
                player.Deck.Hand.Add(material);
                context.CardToDiscard = material;
            }
            return (player, context, staff, material);
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard staff, int option, bool burn = false)
            => CardPlayActions.PlayArtifact(player, context, staff,
                CardJsonLoader.LoadItems().Single(c => c.Id == staff.Id), burn, option);

        private static UnitState Unit(PlayerState player, string id, int wounds = 0)
        {
            var unit = new UnitState(CardJsonLoader.LoadUnit(id));
            unit.Exhaust();
            unit.AddWounds(wounds);
            player.Units.Add(unit);
            return unit;
        }

        private static void BeginBattle(PlayerState player, ActionContext context)
        {
            var state = new GameState();
            state.Players.Add(player);
            new TurnMachine(state).BeginBattle(context);
        }

        private static void VerifyEndTurn(PlayerState player, ActionContext context, DeedCard staff, bool burn)
        {
            Assert.That(burn ? player.Deck.RemovedCards.Single() : player.Deck.PlayedCards.First(), Is.SameAs(staff));
            if (context.InBattle) UnitDamageResolver.EndBattle(player, context);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Any(c => ReferenceEquals(c, staff)), Is.EqualTo(!burn));
            Assert.That(player.Deck.RemovedCards.Any(c => ReferenceEquals(c, staff)), Is.EqualTo(burn));
        }

        [Test]
        public void WhiteActualDiscardMovesTwoRevealedSpacesAndRequiresSafeDestination()
        {
            var (p, c, staff, material) = Fixture("basic_card_007");
            c.TravelPath.AddRange(new[] { new AxialCoord(1, 0), new AxialCoord(2, 0) });
            var target = new AxialCoord(2, 0);
            // 空要塞在正式状态中已是已征服地点；敌对落点必须有实际原守军。
            c.MovementMap.Places[target] = new PlaceState(PlaceType.Keep)
                { Enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_025") };
            Assert.That(c.MovementMap.Places[target].Conquered, Is.False);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, staff, 0));
            Assert.That(p.Deck.Hand, Does.Contain(material));
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(0, 0)));
            c.MovementMap.Places.Remove(target);
            Play(p, c, staff, 0);
            Assert.That(p.Position, Is.EqualTo(target));
            Assert.That(c.MovementPool, Is.Zero);
            Assert.That(c.TravelPath, Is.Empty);
            Assert.That(c.TemporalPortalTurn || c.TemporalPortalPending || c.TurnActionCommitted, Is.False);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(material));
            VerifyEndTurn(p, c, staff, false);
        }

        [Test]
        public void BlueActualDiscardGivesChosenCrystalsThatPayOriginalCard()
        {
            var (p, c, staff, material) = Fixture("basic_card_016");
            Play(p, c, staff, 1);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(2));
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.EqualTo(2));
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(material));
            BeginBattle(p, c);
            var fury = new DeedCard("basic_card_021", CardType.Action);
            p.Deck.Hand.Add(fury);
            CardPlayActions.Play(p, c, fury, CardJsonLoader.LoadBasicActions().Single(x => x.Id == fury.Id), true);
            Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            VerifyEndTurn(p, c, staff, false);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
        }

        [Test]
        public void RedActualDiscardReadiesOriginalLevelThreeUnitForActivation()
        {
            var (p, c, staff, material) = Fixture("basic_card_021");
            var unit = Unit(p, "army_003");
            Assert.That(unit.Card.Level, Is.EqualTo(3));
            c.TargetUnit = unit;
            Play(p, c, staff, 2);
            Assert.That(unit.IsReady, Is.True);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(material));
            BeginBattle(p, c);
            CardUnitActions.Use(p, c, unit, 0);
            Assert.That(c.MeleePool, Is.EqualTo(5));
            Assert.That(unit.IsReady, Is.False);
            VerifyEndTurn(p, c, staff, false);
        }

        [Test]
        public void GreenActualDiscardProvidesThreeHealingForHandAndOriginalUnit()
        {
            var (p, c, staff, material) = Fixture("basic_card_000");
            p.Wounds = 1;
            var unit = Unit(p, "army_2_001", 1);
            Play(p, c, staff, 3);
            Assert.That(c.Healing.Total, Is.EqualTo(3));
            Assert.That(p.Wounds, Is.EqualTo(1));
            Assert.That(unit.Wounds, Is.EqualTo(1));
            CardHealing.Spend(p, c, 1);
            Assert.That(c.Healing.Total, Is.EqualTo(2));
            CardHealing.Spend(p, c, unit: unit);
            Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(p.Wounds + unit.Wounds, Is.Zero);
            Assert.That(unit.IsReady, Is.False);
            Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x, material)), Is.True);
            VerifyEndTurn(p, c, staff, false);
        }

        [Test]
        public void BurnWhiteBlueMovesAndGivesTwoBlueCrystalsWithoutDiscard()
        {
            var (p, c, staff, _) = Fixture();
            var untouched = new DeedCard("basic_card_021", CardType.Action);
            p.Deck.Hand.Add(untouched);
            c.TravelPath.AddRange(new[] { new AxialCoord(1, 0), new AxialCoord(2, 0) });
            Play(p, c, staff, (1 << 16) | (1 << 24), true);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(2, 0)));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(2));
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(untouched));
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            VerifyEndTurn(p, c, staff, true);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(untouched));
        }

        [Test]
        public void BurnRedGreenReadiesAndHealsOriginalUnitThenItActuallyAttacks()
        {
            var (p, c, staff, _) = Fixture();
            var unit = Unit(p, "army_003", 1);
            c.TargetUnit = unit;
            Play(p, c, staff, 2 | (3 << 16), true);
            Assert.That(unit.IsReady, Is.True);
            Assert.That(unit.Wounds, Is.EqualTo(1));
            Assert.That(unit.CanActivate, Is.False);
            Assert.That(c.Healing.Total, Is.EqualTo(3));
            CardHealing.Spend(p, c, unit: unit);
            Assert.That(unit.Wounds, Is.Zero);
            Assert.That(c.Healing.Total, Is.Zero);
            BeginBattle(p, c);
            CardUnitActions.Use(p, c, unit, 0);
            Assert.That(c.MeleePool, Is.EqualTo(5));
            Assert.That(unit.IsReady, Is.False);
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            VerifyEndTurn(p, c, staff, true);
        }

        [Test]
        public void WrongOriginalDiscardColorRejectsBeforeAnyCardOrCrystalChanges()
        {
            var (p, c, staff, material) = Fixture("basic_card_021");
            Assert.Throws<InvalidOperationException>(() => Play(p, c, staff, 1));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, material)), Is.True);
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, staff)), Is.True);
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Deck.RemovedCards, Is.Empty);
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
        }

        [Test]
        public void InvalidSecondBurnTargetDoesNotGrantFirstCrystalsOrBurnStaff()
        {
            var (p, c, staff, _) = Fixture();
            // 正式原三级部队属于另一位玩家；不伪造一张尚未接入的四级部队。
            var foreign = new UnitState(CardJsonLoader.LoadUnit("army_003"));
            foreign.Exhaust();
            c.TargetUnit = foreign;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, staff, 1 | (2 << 16), true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(staff));
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.RemovedCards, Is.Empty);
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(foreign.IsReady, Is.False);
        }
    }
}
