using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class JourneyInteractionTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState{Fame=8};p.Deck.SetDeck(ids.Select(id=>new DeedCard(id,CardType.Action)));
            var map=new MapState();foreach(var pos in new[]{new AxialCoord(0,0),new AxialCoord(0,1)})
                map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            map.Places.Add(new AxialCoord(0,1),new PlaceState(PlaceType.Village));
            var deck=new[]{"army_2_006","army_2_002","army_2_001","army_2_000","army_2_011","army_2_009"}.Select(CardJsonLoader.LoadUnit).ToArray();
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),map:map,unitDeck:deck);
            j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static void Play(PveJourneySession j,string id,string choice="base")=>j.PlayOutsideBattle(j.Player.Deck.Hand.First(c=>c.Id==id),choice);
        private static void Enter(PveJourneySession j)
        {Play(j,"basic_card_000");j.Move(new AxialCoord(0,1));j.BeginVillageInteraction();}
        [Test] public void ActualMovementRecruitRewardsAndSameUnitInFollowingBattle()
        {
            var j=Start("basic_card_000","basic_card_012","advanced_card_014","basic_card_021","basic_card_008");var p=j.Player;
            Assert.Throws<InvalidOperationException>(()=>j.BeginVillageInteraction());Assert.That(j.Context.TurnActionCommitted,Is.False);
            Enter(j);var original=j.Game.Globals.Units.Offer[0];
            Play(j,"basic_card_012");Play(j,"advanced_card_014");Assert.That(j.Context.InfluencePool,Is.EqualTo(5));
            j.RecruitVillageUnit(original);var unit=p.Units.Single();Assert.That(unit.Card,Is.SameAs(original));
            Assert.That(j.Context.InfluencePool,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(9));Assert.That(p.Reputation,Is.EqualTo(1));
            Assert.That(j.Game.Globals.Units.Offer.Count,Is.EqualTo(2));Assert.Throws<InvalidOperationException>(()=>j.RecruitVillageUnit(original));
            Assert.Throws<InvalidOperationException>(()=>j.BeginBattle(CardJsonLoader.LoadMonsters().Take(1)));
            Assert.Throws<InvalidOperationException>(()=>j.Rest(p.Deck.Hand[0]));Assert.Throws<InvalidOperationException>(()=>j.Move(new AxialCoord(0,0)));
            j.EndTurn();j.BeginTurn();Assert.That(p.Units.Single(),Is.SameAs(unit));Assert.That(j.Context.UnitOffer,Is.SameAs(j.Game.Globals.Units.Offer));
            var battle=j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_007"));
            battle.Advance();battle.Advance();battle.Advance();battle.UseUnit(unit,0);
            Assert.That(unit.IsReady,Is.False);Assert.That(battle.Context.MeleePool,Is.EqualTo(2));
            battle.Play(p.Deck.Hand.Single(c=>c.Id=="basic_card_021"),"attack");battle.Advance();j.EndTurn();
            Assert.That(p.Units.Single(),Is.SameAs(unit));Assert.That(unit.IsReady,Is.False);
            j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();Assert.That(unit.IsReady,Is.True);
            Assert.That(j.Game.Globals.Units.Offer.Count,Is.EqualTo(3));Assert.That(j.Game.Globals.Units.Offer.Any(c=>ReferenceEquals(c,original)),Is.False);
        }
        [Test] public void WrongIconsInsufficientInfluenceAndFullSlotsLeaveSupplyAndResourcesIntact()
        {
            var j=Start("basic_card_000","basic_card_008","basic_card_008");Enter(j);
            var offer=j.Game.Globals.Units.Offer;var farmer=offer[0];var guard=offer[2];
            Assert.Throws<InvalidOperationException>(()=>j.RecruitVillageUnit(farmer));Play(j,"basic_card_008");Play(j,"basic_card_008");
            Assert.Throws<InvalidOperationException>(()=>j.RecruitVillageUnit(guard));Assert.That(j.Context.InfluencePool,Is.EqualTo(4));Assert.That(offer.Count,Is.EqualTo(3));
            j.Player.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_000")));j.Player.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_002")));
            Assert.Throws<InvalidOperationException>(()=>j.RecruitVillageUnit(farmer));Assert.That(j.Context.InfluencePool,Is.EqualTo(4));
            var old=j.Player.Units[0];j.DismissUnit(old);Assert.That(old.IsDestroyed,Is.True);j.RecruitVillageUnit(farmer);
            Assert.That(j.Player.Units.Count,Is.EqualTo(2));Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(offer.Count,Is.EqualTo(2));
        }
        [Test] public void VillageHealingBuysRealPointsAndConsumesActualWoundsWithoutReady()
        {
            var j=Start("basic_card_000","advanced_card_019","basic_card_008");var p=j.Player;p.Deck.GainWound();
            var wound=p.Deck.Hand.Last();Enter(j);Play(j,"advanced_card_019");Assert.That(j.Context.InfluencePool,Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(()=>j.BuyVillageHealing(2));Assert.That(j.Context.InfluencePool,Is.EqualTo(4));
            j.BuyVillageHealing();Assert.That(j.Context.InfluencePool,Is.EqualTo(1));Assert.That(j.Context.Healing.Total,Is.EqualTo(1));
            j.HealOutsideBattle();Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,wound)),Is.False);
            Play(j,"basic_card_008");j.BuyVillageHealing();var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));unit.AddWounds(1);unit.Exhaust();p.Units.Add(unit);
            j.HealOutsideBattle(unit);Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsReady,Is.False);
        }
        [Test] public void FrozenReputationAndClosedInteractionCannotBeRestarted()
        {
            var j=Start("basic_card_000","advanced_card_008","basic_card_008");j.Player.Reputation=2;Enter(j);
            int start=j.Context.InfluencePool;Assert.That(start,Is.GreaterThan(0));Play(j,"advanced_card_008");
            Assert.That(j.Context.InfluencePool,Is.EqualTo(start+4));Assert.That(j.Player.Reputation,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>j.BeginVillageInteraction());j.EndVillageInteraction();
            Assert.That(j.Context.InfluencePool,Is.Zero);Assert.Throws<InvalidOperationException>(()=>j.BeginVillageInteraction());
            Assert.Throws<InvalidOperationException>(()=>j.BuyVillageHealing());
        }
        [Test] public void ForbiddenReputationRejectsBeforeCommittingAction()
        {
            var j=Start("basic_card_000","basic_card_008");Play(j,"basic_card_000");j.Move(new AxialCoord(0,1));j.Player.Reputation=-7;
            Assert.Throws<InvalidOperationException>(()=>j.BeginVillageInteraction());Assert.That(j.Context.TurnActionCommitted,Is.False);Assert.That(j.Context.InteractionReputationModifier,Is.Null);
        }
        [Test] public void IntimidateRealDiscountAndSidewaysPayRemainingPrintedCost()
        {
            var j=Start("basic_card_000","basic_card_026","basic_card_008");Enter(j);Play(j,"basic_card_026");
            j.RecruitVillageUnit(j.Game.Globals.Units.Offer[0]);Assert.That(j.Player.Reputation,Is.EqualTo(-1));Assert.That(j.Context.InfluencePool,Is.Zero);
            Assert.That(j.Context.RecruitDiscount,Is.Zero);var card=j.Player.Deck.Hand.Single();j.SidewaysInfluence(card);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(1));Assert.That(j.Player.Deck.PlayedCards.Last(),Is.SameAs(card));
        }
        [Test] public void SupplyRotatesWholeOfferAndAlternatesActualEliteAfterCoreReveal()
        {
            var regular=Enumerable.Range(0,6).Select(_=>CardJsonLoader.LoadUnit("army_2_006")).ToArray();
            var elite=Enumerable.Range(0,3).Select(_=>CardJsonLoader.LoadUnit("army_003")).ToArray();var supply=new UnitSupply();supply.SetDeck(regular.Concat(elite));
            supply.StartRound(1,false);Assert.That(supply.Offer,Is.EqualTo(regular.Take(3)));var first=supply.Offer[0];supply.Offer.RemoveAt(0);
            supply.StartRound(1,true);Assert.That(supply.Offer[0],Is.SameAs(elite[0]));Assert.That(supply.Offer[1],Is.SameAs(regular[3]));Assert.That(supply.Offer[2],Is.SameAs(elite[1]));
            Assert.That(supply.RemainingCount,Is.EqualTo(5));Assert.That(supply.Offer.Any(c=>ReferenceEquals(c,first)),Is.False);
        }
        [Test] public void OptionalIntimidateDeclineKeepsReputationAndStrongReadyPaysRealLevel()
        {
            var j=Start("basic_card_000","basic_card_026","basic_card_008");Enter(j);Play(j,"basic_card_026");Play(j,"basic_card_008");
            j.Context.UseIntimidateRecruitDiscount=false;j.RecruitVillageUnit(j.Game.Globals.Units.Offer[0]);
            Assert.That(j.Player.Reputation,Is.Zero);Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Context.IntimidateUsed,Is.True);
            var next=Start("basic_card_026","basic_card_008");var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));next.Player.Units.Add(unit);unit.Exhaust();unit.AddWounds(1);
            next.Player.Mana.AddToken(ManaColor.Red);Play(next,"basic_card_026","strong");next.ReadyWithInfluence(unit);
            Assert.That(unit.IsReady,Is.True);Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(next.Context.InfluencePool,Is.EqualTo(4));Assert.That(next.Player.Reputation,Is.EqualTo(-1));
        }
    }
}
