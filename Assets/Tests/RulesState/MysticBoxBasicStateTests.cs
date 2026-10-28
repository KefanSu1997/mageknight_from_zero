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
    public sealed class MysticBoxBasicStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard box, ArtifactCard top, ArtifactCard second)
            Fixture(string topId = "items_010")
        {
            var p = new PlayerState { Fame = 0, Reputation = 0, ExtraCommandSlots = 1 };
            var c = new ActionContext { DayPart = DayPart.Day, SkipDraw = true,
                ArtifactSupply = new ArtifactSupply(), MovementMap = new MapState() };
            for (int q = 0; q <= 1; q++)
                c.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            var box = new DeedCard("items_024", CardType.Artifact);
            p.Deck.Hand.Add(box);
            var top = new ArtifactCard(topId);
            var second = new ArtifactCard("items_004");
            c.ArtifactSupply.SetDeck(new[] { top, second });
            return (p, c, box, top, second);
        }

        private static void Reveal(PlayerState p, ActionContext c, DeedCard box)
            => CardPlayActions.PlayArtifact(p, c, box,
                CardJsonLoader.LoadItems().Single(x => x.Id == "items_024"));

        private static void Use(PlayerState p, ActionContext c, DeedCard box, bool burn = false, int option = 0)
            => CardPlayActions.UseMysticBox(p, c, box, burn, option);

        private static void EndTurn(PlayerState p, ActionContext c)
            => new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);

        private static int PhysicalBoxCount(PlayerState p, DeedCard box)
            => p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.DiscardPile).Concat(p.Deck.RemovedCards)
                .Count(x => ReferenceEquals(x, box)) + p.Units.Count(u => ReferenceEquals(u.Banner, box));

        private static void CheckSupplyAtBottom(ActionContext c, ArtifactCard top, ArtifactCard second)
        {
            Assert.That(c.MysticBox, Is.Null);
            Assert.That(c.ArtifactSupply.Count, Is.EqualTo(2));
            Assert.That(c.ArtifactSupply.Draw(), Is.SameAs(second));
            Assert.That(c.ArtifactSupply.Draw(), Is.SameAs(top));
            Assert.That(c.ArtifactSupply.Count, Is.Zero);
        }

        [Test]
        public void RevealWithoutUseReturnsSameBoxToHandAndSameArtifactToSupplyBottom()
        {
            var (p, c, box, top, second) = Fixture();
            Assert.Throws<InvalidOperationException>(() => Use(p, c, box));
            c.ArtifactSupply.SetDeck(Array.Empty<ArtifactCard>());
            Assert.Throws<InvalidOperationException>(() => Reveal(p, c, box));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(box));
            Assert.That(c.MysticBox, Is.Null);
            c.ArtifactSupply.SetDeck(new[] { top, second });
            Reveal(p, c, box);
            Assert.That(c.ArtifactSupply.Count, Is.EqualTo(1));
            Assert.That(c.MysticBox.RevealedArtifact, Is.SameAs(top));
            Assert.That(c.MysticBox.Used, Is.False);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(box));
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(p.Fame, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => Reveal(p, c, box));
            Assert.That(c.ArtifactSupply.Count, Is.EqualTo(1));
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            EndTurn(p, c);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(box));
            Assert.That(p.Deck.PlayedCards.Concat(p.Deck.DiscardPile).Concat(p.Deck.RemovedCards), Is.Empty);
            Assert.That(p.Fame, Is.Zero);
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Use(p, c, box));
            CheckSupplyAtBottom(c, top, second);
        }

        [TestCase(false, 4, 3, 0)]
        [TestCase(true, 9, 4, 5)]
        public void BorrowedGoldPouchUsesOriginalEffectAndRecruitmentButMovesOnlyBox(
            bool burn, int influence, int fame, int remainder)
        {
            var (p, c, box, top, second) = Fixture();
            var farmer = CardJsonLoader.LoadUnit("army_2_006");
            Assert.That(farmer.Id, Is.EqualTo("army_2_006"));
            var offer = new List<UnitCard> { farmer };
            Reveal(p, c, box);
            Assert.That(p.Fame, Is.Zero);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => Use(p, c,
                new DeedCard("items_024", CardType.Artifact), burn));
            Assert.That(c.MysticBox.Used, Is.False);
            Use(p, c, box, burn);
            Assert.That(c.MysticBox.RevealedArtifact, Is.SameAs(top));
            Assert.That(c.MysticBox.Used, Is.True);
            Assert.That(c.ArtifactSupply.Count, Is.EqualTo(1));
            Assert.That(c.InfluencePool, Is.EqualTo(influence));
            Assert.That(p.Fame, Is.EqualTo(fame));
            Assert.That(burn ? p.Deck.RemovedCards.Single() : p.Deck.PlayedCards.Single(), Is.SameAs(box));
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Use(p, c, box, burn));
            Assert.That(c.InfluencePool, Is.EqualTo(influence));
            Assert.That(p.Fame, Is.EqualTo(fame));
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(p.Units.Single().Card, Is.SameAs(farmer));
            Assert.That(offer, Is.Empty);
            Assert.That(c.InfluencePool, Is.EqualTo(remainder));
            Assert.That(p.Fame, Is.EqualTo(fame));
            EndTurn(p, c);
            Assert.That(p.Deck.Hand.Concat(p.Deck.PlayedCards), Is.Empty);
            Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x, box)), Is.EqualTo(!burn));
            Assert.That(p.Deck.RemovedCards.Any(x => ReferenceEquals(x, box)), Is.EqualTo(burn));
            Assert.That(p.Deck.DiscardPile.Concat(p.Deck.RemovedCards).All(x => x.Id == "items_024"), Is.True);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(fame));
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            CheckSupplyAtBottom(c, top, second);
        }

        [Test]
        public void BorrowedCourageBannerReadiesOriginalUnitThenUnbindsBoxAtEndTurn()
        {
            var (p, c, box, top, second) = Fixture("items_003");
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            unit.Exhaust();
            p.Units.Add(unit);
            Reveal(p, c, box);
            c.TargetUnit = new UnitState(unit.Card); // 同原卡定义，但不是自有实体
            Assert.Throws<InvalidOperationException>(() => Use(p, c, box));
            Assert.That(c.MysticBox.Used, Is.False);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(box));
            Assert.That(p.Fame, Is.Zero);
            Assert.That(unit.Banner, Is.Null);
            c.TargetUnit = unit;
            Use(p, c, box);
            Assert.That(unit.Banner, Is.SameAs(box));
            Assert.That(unit.Banner.Id, Is.EqualTo("items_024")); // 物理实体不改成原旗
            Assert.That(p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.RemovedCards), Is.Empty);
            Assert.That(c.ArtifactSupply.Count, Is.EqualTo(1));
            Assert.That(c.MysticBox.RevealedArtifact, Is.SameAs(top));
            Assert.That(p.Fame, Is.EqualTo(1));
            Assert.That(unit.IsReady, Is.False); // 附着不等于自动重整
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            BannerActions.UseCourage(p, c, unit);
            Assert.That(unit.IsReady, Is.True);
            Assert.That(unit.CourageBannerSpent, Is.True);
            CardUnitActions.Use(p, c, unit, 2);
            Assert.That(unit.IsReady, Is.False);
            Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(c.MovementPool, Is.Zero);
            EndTurn(p, c);
            Assert.That(unit.Banner, Is.Null);
            Assert.That(unit.CourageBannerSpent, Is.False);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(box));
            Assert.That(p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.RemovedCards), Is.Empty);
            Assert.That(PhysicalBoxCount(p, box), Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseCourage(p, c, unit));
            CheckSupplyAtBottom(c, top, second);
        }
    }
}
