using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CityDefenderStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static void Play(PlayerState p, ActionContext c, string id, bool enhanced = false)
        {
            var card = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
            CardPlayActions.Play(p, c, p.Deck.Hand.First(s => s.Id == id),
                new ActionCardData(card.Id, card.Name, card.Set, card.ImagePath, card.EnImagePath,
                    card.Top.Effect, card.Bottom.Effect, card.Top.ManaCost), enhanced);
        }

        private static (PlayerState p, ActionContext c, PlaceState city, TurnMachine turn) Start(bool sameId = false)
        {
            var originals = CardJsonLoader.LoadMonsters();
            var tokens = new[] { originals.Single(m => m.Id == "monster_027"), originals.Single(m => m.Id == (sameId ? "monster_027" : "monster_002")) with {} };
            var supply = new EnemyTokenSupply(tokens);
            var city = new PlaceState(PlaceType.City) { Enemy = supply.Draw(EnemyTokenColor.Gray) };
            city.AdditionalGuards.Add(supply.Draw(sameId ? EnemyTokenColor.Gray : EnemyTokenColor.Purple));
            var p = new PlayerState();
            var c = new ActionContext { DayPart = DayPart.Night, EnemySupply = supply, MovementMap = new MapState() };
            for (int i = 0; i <= 1; i++) c.MovementMap.Placed[new AxialCoord(i, 0)] = new MapTile(TileSet.Countryside, i, new[] { TerrainType.Forest });
            c.MovementMap.Places[new AxialCoord(1, 0)] = city;
            c.TravelPath.Add(new AxialCoord(1, 0));
            foreach (var id in new[] { "magic_004", "magic_009", "magic_009" }) p.Deck.Hand.Add(new DeedCard(id, CardType.Spell));
            p.Mana.AddToken(ManaColor.Green); p.Mana.AddToken(ManaColor.Black); p.Mana.AddToken(ManaColor.Red, 2);
            Play(p, c, "magic_004", true);
            var game = new GameState(); game.Players.Add(p); var turn = new TurnMachine(game); turn.BeginBattle(c);
            return (p, c, city, turn);
        }

        private static void DefenseAndFinish(ActionContext c, TurnMachine turn)
        {
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(c.Enemies, Array.Empty<BlockAllocation>(), c);
            turn.FinishBattle(c);
        }

        [TestCase(false)] [TestCase(true)]
        public void PartialVictoryKeepsExactSurvivorAndOnlyOneShieldAfterCompaction(bool killSecond)
        {
            var (p, c, city, turn) = Start(); var guards = city.Defenders.ToArray();
            Assert.That(c.Enemies.Count, Is.EqualTo(2));
            Play(p, c, "magic_009");
            turn.ApplyRanged(c.Enemies, new[] { new AttackAllocation(new[] { killSecond ? 1 : 0 }, 5, Element.Fire) }, c);
            Assert.That(c.Enemies.Single(), Is.SameAs(guards[killSecond ? 0 : 1]));
            DefenseAndFinish(c, turn);
            Assert.Throws<InvalidOperationException>(() => CardTravelActions.FinishTunnelAssault(p, c, false));
            Assert.That(city.CityShields, Is.Empty); Assert.That(c.EnemySupply.DiscardPile, Is.Empty);
            CardTravelActions.FinishTunnelAssault(p, c, true);
            Assert.That(city.Defenders.Single(), Is.SameAs(guards[killSecond ? 0 : 1]));
            Assert.That(c.EnemySupply.InUse.Single(), Is.SameAs(city.Enemy));
            Assert.That(c.EnemySupply.DiscardPile.Single(), Is.SameAs(guards[killSecond ? 1 : 0]));
            Assert.That(city.Conquered, Is.False); Assert.That(city.OwnerId, Is.Null);
            Assert.That(city.CityShields, Is.EqualTo(new[] { p.Id })); Assert.That(p.Position.Q, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(4)); Assert.That(p.Wounds, Is.EqualTo(3));
            Assert.That(p.Deck.DiscardPile.Count(card => card.Type == CardType.Wound), Is.EqualTo(killSecond ? 0 : 3), "毒素额外伤牌进入弃牌堆");
            Assert.That(FortifiedSiteActions.HandBonus(p, c.MovementMap), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardTravelActions.FinishTunnelAssault(p, c, true));
        }

        [TestCase(false)] [TestCase(true)]
        public void AllDefendersMustDieAndLeaderUsesCountThenEarliestShield(bool earlierLeader)
        {
            var (p, c, city, turn) = Start(true);
            city.CityShields.AddRange(earlierLeader ? new[] { 9, p.Id, 9, 9 } : new[] { 9 });
            Play(p, c, "magic_009"); CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Ranged);
            Assert.That(c.IsTunnelDefenderDefeated(city.AdditionalGuards.Single()), Is.False, "同ID的第二实体不能被误记为击败");
            Play(p, c, "magic_009"); CardCombatActions.Attack(p, c, c.Enemies, new[] { 1 }, Phase.Ranged);
            turn.ApplyRanged(c.Enemies, Array.Empty<AttackAllocation>(), c); DefenseAndFinish(c, turn);
            Assert.Throws<InvalidOperationException>(() => CardTravelActions.FinishTunnelAssault(p, c, true));
            CardTravelActions.FinishTunnelAssault(p, c, false);
            Assert.That(city.Conquered, Is.True); Assert.That(city.Defenders, Is.Empty); Assert.That(p.Position.Q, Is.EqualTo(1));
            Assert.That(city.OwnerId, Is.EqualTo(earlierLeader ? 9 : p.Id));
            Assert.That(city.CityShields.Count(id => id == p.Id), Is.EqualTo(earlierLeader ? 3 : 2));
            Assert.That(FortifiedSiteActions.HandBonus(p, c.MovementMap), Is.EqualTo(earlierLeader ? 1 : 2));
            Assert.That(p.Fame, Is.EqualTo(8)); Assert.That(p.Wounds, Is.Zero); Assert.That(p.Reputation, Is.Zero);
            Assert.That(c.EnemySupply.DiscardPile.Count, Is.EqualTo(2)); Assert.That(c.EnemySupply.InUse, Is.Empty);
        }

        [Test] public void ForeignSameIdEnemyDoesNotCountAsCityGuard()
        {
            var (p, c, city, turn) = Start(); var foreign = city.Enemy with {};
            c.Enemies.Add(foreign); Play(p, c, "magic_009");
            turn.ApplyRanged(c.Enemies, new[] { new AttackAllocation(new[] { 2 }, 5, Element.Fire) }, c);
            DefenseAndFinish(c, turn); CardTravelActions.FinishTunnelAssault(p, c, true);
            Assert.That(city.Defenders.Count, Is.EqualTo(2)); Assert.That(city.CityShields, Is.Empty);
            Assert.That(c.EnemySupply.DiscardPile, Is.Empty); Assert.That(p.Fame, Is.EqualTo(4));
        }

        [Test] public void ReplacingAnAdditionalGuardRejectsBeforeAnySettlementMutation()
        {
            var (p, c, city, turn) = Start(); Play(p, c, "magic_009");
            turn.ApplyRanged(c.Enemies, new[] { new AttackAllocation(new[] { 0 }, 5, Element.Fire) }, c);
            DefenseAndFinish(c, turn); city.AdditionalGuards[0] = city.AdditionalGuards[0] with {};
            Assert.Throws<InvalidOperationException>(() => CardTravelActions.FinishTunnelAssault(p, c, true));
            Assert.That(city.CityShields, Is.Empty); Assert.That(c.EnemySupply.DiscardPile, Is.Empty);
            Assert.That(city.Defenders.Count, Is.EqualTo(2)); Assert.That(c.TunnelAssaultActive, Is.True);
        }
    }
}
