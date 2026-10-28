using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class ScoutsStateTests
    {
        private sealed class QuietExploration : Random { public override int Next(int maxValue) => maxValue - 1; }
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Token(string id = "monster_027") => CardJsonLoader.LoadMonsters().Single(m => m.Id == id);
        private static UnitState AddScout(PlayerState p)
        {
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_009")); p.Units.Add(unit); return unit;
        }
        private static MapTile Plain(int id) => new(TileSet.Countryside, id, Enumerable.Repeat(TerrainType.Plains, 6).ToArray());
        private static ActionContext Context()
        {
            var c = new ActionContext { DayPart = DayPart.Night, MovementMap = new MapState() };
            for (int q = 0; q <= 4; q++) c.MovementMap.Placed[new AxialCoord(q, 0)] = Plain(q);
            return c;
        }
        private static DeedCard Play(PveBattleSession battle, string id, string mode)
        {
            var card = new DeedCard(id, CardType.Action); battle.Player.Deck.Hand.Add(card); battle.Play(card, mode); return card;
        }

        [Test] public void PrintedSiegeAddsOneToActualOriginalRangedAttack()
        {
            var p = new PlayerState { Fame = 8 }; var scout = AddScout(p); var enemy = Token("monster_032");
            Assert.That(scout.Card.Level, Is.EqualTo(1)); Assert.That(scout.Card.Armor, Is.EqualTo(2));
            Assert.That(scout.Card.InfluenceCost, Is.EqualTo(4)); Assert.That(scout.Card.Actions.Length, Is.EqualTo(3));
            Assert.That(scout.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village | RecruitLocation.Keep | RecruitLocation.MageTower | RecruitLocation.Monastery | RecruitLocation.City));
            Assert.That(scout.Card.Actions.SelectMany(a => a.Cost), Is.Empty);
            var crossbow = new UnitState(CardJsonLoader.LoadUnit("army_2_012")); p.Units.Add(crossbow);
            Assert.That(p.FreeSlots, Is.Zero, "等级3的两支原部队使用两个实际指挥槽");
            var s = new PveBattleSession(p, new[] { enemy }, CardJsonLoader.LoadBasicActions());
            s.UseUnit(scout, 0); s.UseUnit(crossbow, 1);
            Assert.That(s.Context.SiegePool, Is.EqualTo(1)); Assert.That(s.Context.RangedPool, Is.EqualTo(2));
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(8 + enemy.Fame));
            Assert.That(scout.IsReady, Is.False); Assert.That(p.Wounds, Is.Zero);
        }

        [Test] public void MapPeekIsPrivateAndSelectsOneActualGuardWithinThreeSpaces()
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            var first = Token("monster_025"); var second = Token("monster_027");
            var site = new PlaceState(PlaceType.City) { Enemy = first }; site.AdditionalGuards.Add(second);
            c.MovementMap.Places[new AxialCoord(3, 0)] = site;
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(new AxialCoord(3, 0), 1));
            Assert.That(c.LastScoutObservation.Enemy, Is.SameAs(second)); Assert.That(c.LastScoutObservation.Site, Is.SameAs(site));
            Assert.That(c.MovementMap.RevealedSites, Is.Empty); Assert.That(site.Defenders[0], Is.SameAs(first));
            Assert.That(site.Defenders[1], Is.SameAs(second)); Assert.That(c.TurnActionCommitted, Is.False);
            Assert.That(scout.IsReady, Is.False); Assert.That(p.Fame, Is.Zero);
        }

        [Test] public void FaceDownRuinCanBeReadWithoutRevealingOrInventingAnEnemyReward()
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            var site = new PlaceState(PlaceType.AncientRuin) { RuinToken = RuinTokenType.Altar };
            c.MovementMap.Places[new AxialCoord(2, 0)] = site;
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(new AxialCoord(2, 0)));
            Assert.That(c.LastScoutObservation.Site, Is.SameAs(site)); Assert.That(c.LastScoutObservation.Ruin, Is.EqualTo(RuinTokenType.Altar));
            Assert.That(c.LastScoutObservation.Enemy, Is.Null); Assert.That(c.MovementMap.RevealedSites, Is.Empty);
            Assert.That(site.RuinConquered, Is.False); Assert.That(p.Fame, Is.Zero);
        }

        [TestCase("distance")] [TestCase("revealed")] [TestCase("faceup")] [TestCase("missing")]
        [TestCase("index")] [TestCase("both")]
        public void InvalidPeekDoesNotExhaustOrChangeMap(string reason)
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            var at = new AxialCoord(reason == "distance" ? 4 : 3, 0);
            var site = new PlaceState(reason == "faceup" ? PlaceType.OrcRampaging : PlaceType.Keep) { Enemy = Token("monster_025") };
            if (reason != "missing") c.MovementMap.Places[at] = site;
            if (reason == "revealed") c.MovementMap.RevealedSites.Add(site);
            var target = new ScoutTarget(at, reason == "index" ? 1 : 0, reason == "both" ? EnemyTokenColor.Gray : null);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p, c, scout, 1, scoutTarget: target));
            Assert.That(scout.CanActivate, Is.True); Assert.That(c.LastScoutObservation, Is.Null);
            Assert.That(p.Fame, Is.Zero); Assert.That(c.TurnActionCommitted, Is.False);
            Assert.That(c.MovementMap.RevealedSites.Count, Is.EqualTo(reason == "revealed" ? 1 : 0));
        }

        [Test] public void SupplyPeekKeepsExactTopInstanceAndLaterDrawTakesThatInstance()
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            var first = Token(); var equalCopy = first with { }; var purple = Token("monster_028");
            c.EnemySupply = new EnemyTokenSupply(new[] { first, purple, equalCopy });
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(SupplyColor: first.TokenColor));
            Assert.That(c.LastScoutObservation.Enemy, Is.SameAs(first)); Assert.That(c.EnemySupply.DrawPile.Count, Is.EqualTo(3));
            Assert.That(c.EnemySupply.InUse, Is.Empty); Assert.That(c.EnemySupply.DiscardPile, Is.Empty);
            Assert.That(c.EnemySupply.Draw(first.TokenColor), Is.SameAs(first));
            Assert.That(c.EnemySupply.Peek(first.TokenColor), Is.SameAs(equalCopy));
            Assert.That(c.EnemySupply.Peek(purple.TokenColor), Is.SameAs(purple));
        }

        [TestCase(false)] [TestCase(true)]
        public void ScoutedPhysicalEnemyGivesOneFameOnlyWhenActuallyDefeated(bool melee)
        {
            var p = new PlayerState { Fame = 8 }; var scout = AddScout(p); var c = Context();
            var target = Token(); c.EnemySupply = new EnemyTokenSupply(new[] { target });
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(SupplyColor: target.TokenColor));
            var actual = c.EnemySupply.Draw(target.TokenColor);
            var s = new PveBattleSession(p, new[] { actual }, CardJsonLoader.LoadBasicActions(), c);
            if (melee)
            {
                s.Advance(); s.Advance(); s.Advance(); p.Mana.AddToken(ManaColor.Red);
                Play(s, "basic_card_021", "strong");
                var sideways = new DeedCard("basic_card_000", CardType.Action); p.Deck.Hand.Add(sideways); s.PlaySideways(sideways);
            }
            else
            {
                var crossbow = new UnitState(CardJsonLoader.LoadUnit("army_2_012")); p.Units.Add(crossbow);
                p.Mana.AddToken(ManaColor.White); s.UseUnit(crossbow, 1); Play(s, "basic_card_007", "strong");
            }
            var result = s.Attack(0);
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(result.Fame, Is.EqualTo(target.Fame + 1));
            Assert.That(p.FreeSlots, Is.GreaterThanOrEqualTo(0));
            Assert.That(p.Fame, Is.EqualTo(8 + target.Fame + 1)); Assert.That(s.Enemies[0], Is.SameAs(target));
            Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(p.Fame, Is.EqualTo(8 + target.Fame + 1));
        }

        [Test] public void SamePrintedEnemyDoesNotTakeScoutedInstancesRewardAndDirectDestructionConsumesItOnce()
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            var target = Token(); var copy = target with { };
            c.EnemySupply = new EnemyTokenSupply(new[] { target, copy });
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(SupplyColor: target.TokenColor));
            c.EnemySupply.Draw(target.TokenColor); c.EnemySupply.Draw(target.TokenColor);
            var s = new PveBattleSession(p, new[] { copy, target }, CardJsonLoader.LoadBasicActions(), c);
            // Exercise the shared direct-defeat consumer used by original destroy effects.
            CardCombatActions.DestroyEnemy(p, c, 0); Assert.That(p.Fame, Is.EqualTo(copy.Fame));
            CardCombatActions.DestroyEnemy(p, c, 1); Assert.That(p.Fame, Is.EqualTo(copy.Fame + target.Fame + 1));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.DestroyEnemy(p, c, 1));
            Assert.That(p.Fame, Is.EqualTo(copy.Fame + target.Fame + 1));
        }

        [Test] public void UnspentPeekRewardExpiresAtActualTurnEndWithoutRemovingEnemyFromSupply()
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context(); c.SkipDraw = true;
            var target = Token(); c.EnemySupply = new EnemyTokenSupply(new[] { target });
            CardUnitActions.Use(p, c, scout, 1, scoutTarget: new ScoutTarget(SupplyColor: target.TokenColor));
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, null, c);
            Assert.That(c.LastScoutObservation, Is.Null); Assert.That(c.EnemySupply.Peek(target.TokenColor), Is.SameAs(target));
            Assert.That(scout.IsReady, Is.False);
            new PveBattleSession(p, new[] { c.EnemySupply.Draw(target.TokenColor) }, CardJsonLoader.LoadBasicActions(), c);
            CardCombatActions.DestroyEnemy(p, c, 0); Assert.That(p.Fame, Is.EqualTo(target.Fame));
        }

        [Test] public void SupplyPeekRecyclesOnlyItsEmptyColorWithoutTakingTheToken()
        {
            var gray = Token("monster_025"); var purple = Token("monster_028");
            var supply = new EnemyTokenSupply(new[] { gray, purple });
            Assert.That(supply.Draw(gray.TokenColor), Is.SameAs(gray)); supply.Discard(gray);
            Assert.That(supply.Peek(gray.TokenColor), Is.SameAs(gray));
            Assert.That(supply.DiscardPile, Is.Empty); Assert.That(supply.InUse, Is.Empty);
            Assert.That(supply.Peek(purple.TokenColor), Is.SameAs(purple));
            Assert.That(supply.Draw(gray.TokenColor), Is.SameAs(gray));
        }

        [Test] public void MovementPaysForDistanceTwoTileExplorationButDoesNotGrantTwoHexMovement()
        {
            var p = new PlayerState(); var scout = AddScout(p);
            var map = new MapState(); map.Placed[new AxialCoord(0, 0)] = Plain(0); map.Placed[new AxialCoord(1, 0)] = Plain(1);
            var next = Plain(2); map.Countryside.Push(next);
            var c = new ActionContext { MovementMap = map, DayPart = DayPart.Day, SkipDraw = true };
            CardUnitActions.Use(p, c, scout, 2);
            Assert.That(c.MovementPool, Is.EqualTo(2)); Assert.That(c.ScoutExplorationRange, Is.EqualTo(2));
            Assert.That(c.SpaceBendingActive, Is.False);
            var exploration = new ExplorationService(map, new QuietExploration());
            Assert.That(exploration.ExploreUsingPool(p, new AxialCoord(3, 0), 0, c).Success, Is.False);
            Assert.That(c.MovementPool, Is.EqualTo(2)); Assert.That(map.Countryside.Peek(), Is.SameAs(next));
            var result = exploration.ExploreUsingPool(p, new AxialCoord(2, 0), 0, c);
            Assert.That(result.Success, Is.True); Assert.That(map.Placed[new AxialCoord(2, 0)], Is.SameAs(next));
            Assert.That(map.Countryside.Count, Is.Zero); Assert.That(c.MovementPool, Is.Zero);
            // A normal original March produces movement, not permission to skip an intervening hex.
            var march = new DeedCard("basic_card_000", CardType.Action); p.Deck.Hand.Add(march);
            CardPlayActions.Play(p, c, march, CardJsonLoader.LoadBasicActions().Single(x => x.Id == march.Id), false);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(2, 0), map, c), Is.False);
            Assert.That(c.MovementPool, Is.EqualTo(2)); Assert.That(p.Position, Is.EqualTo(new AxialCoord(0, 0)));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.ScoutExplorationRange, Is.EqualTo(1)); Assert.That(c.MovementPool, Is.Zero);
            Assert.That(scout.IsReady, Is.False);
        }

        [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(true, 1)] [TestCase(true, 2)]
        public void BootAbilitiesRejectAfterActionOrDuringBattle(bool battle, int ability)
        {
            var p = new PlayerState(); var scout = AddScout(p); var c = Context();
            c.InBattle = battle; c.TurnActionCommitted = !battle;
            var enemy = Token(); c.EnemySupply = new EnemyTokenSupply(new[] { enemy });
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p, c, scout, ability,
                scoutTarget: ability == 1 ? new ScoutTarget(SupplyColor: enemy.TokenColor) : null));
            Assert.That(scout.CanActivate, Is.True); Assert.That(c.MovementPool, Is.Zero); Assert.That(c.LastScoutObservation, Is.Null);
            Assert.That(c.EnemySupply.DrawPile.Single(), Is.SameAs(enemy));
        }
    }
}
