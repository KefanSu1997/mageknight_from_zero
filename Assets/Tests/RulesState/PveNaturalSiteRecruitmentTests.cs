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
    public sealed class PveNaturalSiteRecruitmentTests
    {
        private static readonly AxialCoord SiteHex = new AxialCoord(0,1);
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private PveJourneySession Start(PlaceType type, CityColor color = CityColor.None, params string[] units)
        {
            var p = new PlayerState { HeroId="Krang", Fame=35 };
            p.Deck.SetDeck(new[]{"basic_card_000","basic_card_008","basic_card_008","advanced_card_008","basic_card_021","basic_card_016"}
                .Select(id=>new DeedCard(id,CardType.Action)));
            var map=new MapState();
            foreach(var hex in new[]{new AxialCoord(0,0),SiteHex})
                map.Placed.Add(hex,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            map.Places.Add(SiteHex,new PlaceState(type){OwnerId=p.Id,CityColor=color});
            var j = new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
                map:map,unitDeck:Array.Empty<UnitCard>());
            j.SelectTactic("day_1");j.BeginTurn();
            // A finite original-card supply fixture, no synthetic unit or rewritten price/icon.
            j.Game.Globals.Units.SetDeck((units.Length==0?new[]{"army_2_009"}:units).Select(CardJsonLoader.LoadUnit));
            j.Game.Globals.Units.StartRound(1,units.Any(id=>id.StartsWith("army_")&&!id.StartsWith("army_2_")));
            Play(j,"basic_card_000"); j.Move(SiteHex);
            return j;
        }
        private void Play(PveJourneySession j,string id,string choice="base")
            =>j.PlayOutsideBattle(j.Player.Deck.Hand.First(c=>c.Id==id),choice);
        private void Promise(PveJourneySession j)
        { j.Player.Mana.AddToken(ManaColor.White); Play(j,"basic_card_008","strong"); }
        private void ThirteenOriginalInfluence(PveJourneySession j)
        {
            Promise(j);Promise(j);Play(j,"advanced_card_008");
            j.SidewaysInfluence(j.Player.Deck.Hand.Single(c=>c.Id=="basic_card_021"));
        }
        [TestCase(PlaceType.Village,RecruitLocation.Village)]
        [TestCase(PlaceType.Keep,RecruitLocation.Keep)]
        [TestCase(PlaceType.MageTower,RecruitLocation.MageTower)]
        [TestCase(PlaceType.City,RecruitLocation.City)]
        public void CurrentOriginalSiteRecruitsRealSupplyAndSameScoutFightsFollowingTurn(PlaceType type,RecruitLocation location)
        {
            var j=Start(type);var site=j.CurrentInteractionSite;
            if(type==PlaceType.MageTower||type==PlaceType.City) site.OwnerId=j.Player.Id+1;
            Assert.That(j.CurrentSiteRecruitLocation,Is.EqualTo(location));j.BeginSiteInteraction();
            Assert.That(j.Context.InteractionSite,Is.SameAs(site));Promise(j);
            var actual=j.Game.Globals.Units.Offer.Single();j.RecruitSiteUnit(actual);
            var unit=j.Player.Units.Single();Assert.That(unit.Card,Is.SameAs(actual));
            Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Game.Globals.Units.Offer,Is.Empty);
            j.EndSiteInteraction();Assert.Throws<InvalidOperationException>(()=>j.BeginSiteInteraction());
            j.EndTurn();j.BeginTurn();Assert.That(j.Context.InteractionSite,Is.Null);
            var b=j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"));
            b.Advance();b.Advance();b.Advance(); b.UseUnit(unit,0);
            b.Play(j.Player.Deck.Hand.Single(c=>c.Id=="basic_card_021"),"attack");
            b.Attack(new[]{0});
            Assert.That(b.Context.DefeatedEnemyIndices.Contains(0),Is.True);
            Assert.That(j.Player.Units.Single(),Is.SameAs(unit));Assert.That(unit.IsReady,Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void UnavailableSiteRejectsBeforeInfluenceOrActionIsCommitted(int invalid)
        {
            var type=invalid==3?PlaceType.MageTower:invalid==4?PlaceType.City:invalid==5?PlaceType.Monastery:PlaceType.Keep;
            var j=Start(type);var site=j.CurrentInteractionSite;
            if(invalid==0) site.OwnerId=j.Player.Id+1;
            if(invalid==1||invalid==3) site.Enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032");
            if(invalid==2) site.GuardNeedsReplacement=true;
            if(invalid==4) site.AdditionalGuards.Add(CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032"));
            Assert.That(j.CanInteractAtCurrentSite,Is.False);
            Assert.Throws<InvalidOperationException>(()=>j.BeginSiteInteraction());
            Assert.That(j.Context.InteractionSite,Is.Null);Assert.That(j.Context.InfluencePool,Is.Zero);
            Assert.That(j.Context.TurnActionCommitted,Is.False);Assert.That(j.Game.Globals.Units.Offer.Count,Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void WhiteCityAloneAllowsVillageOnlyCardWithoutRefugeeSurcharge(bool white)
        {
            var j=Start(PlaceType.City,white?CityColor.White:CityColor.Blue,"army_2_006");
            var card=j.Game.Globals.Units.Offer.Single();j.BeginSiteInteraction();Promise(j);
            Assert.That(j.CanRecruitSiteIcon(card),Is.EqualTo(white));
            if(!white){Assert.Throws<InvalidOperationException>(()=>j.RecruitSiteUnit(card));Assert.That(j.Context.InfluencePool,Is.EqualTo(4));return;}
            j.RecruitSiteUnit(card);Assert.That(j.Context.InfluencePool,Is.Zero);
            var farmer=j.Player.Units.Single();j.UseOutsideUnit(farmer,1);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(2));Assert.That(farmer.IsReady,Is.False);
            j.EndTurn();Assert.That(j.Context.InteractionSite,Is.Null);
            Assert.That(j.Context.InteractionPosition,Is.Null);Assert.That(j.Context.InteractionCityInfluence,Is.Zero);
            Assert.That(RecruitmentService.AllowsWhiteCityRecruitment(j.Player,j.Context),Is.False);
        }
        [TestCase(false,2,5)] [TestCase(false,-2,7)]
        [TestCase(true,2,4)] [TestCase(true,-2,0)]
        public void CityShieldsStayIndependentOfFrozenReputationAcrossTwoRealPurchases(bool heroes,int reputation,int remaining)
        {
            var j=Start(PlaceType.City,CityColor.White,heroes?"army_008":"army_2_011","army_2_009");
            var site=j.CurrentInteractionSite;site.CityShields.AddRange(new[]{j.Player.Id,j.Player.Id,j.Player.Id+1});
            j.Player.Reputation=reputation;j.BeginSiteInteraction();
            Assert.That(j.Context.InteractionCityInfluence,Is.EqualTo(2));
            ThirteenOriginalInfluence(j);
            var special=j.Game.Globals.Units.Offer.Single(c=>c.Id==(heroes?"army_008":"army_2_011"));
            var scout=j.Game.Globals.Units.Offer.Single(c=>c.Id=="army_2_009");
            j.RecruitSiteUnit(special);j.RecruitSiteUnit(scout);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(remaining));
            Assert.That(j.Context.InteractionCityInfluence,Is.EqualTo(2));
            Assert.That(j.Player.Units[0].Card,Is.SameAs(special));Assert.That(j.Player.Units[1].Card,Is.SameAs(scout));
            Assert.That(j.Game.Globals.Units.Offer,Is.Empty);
        }
        [Test]
        public void ArcaneDisguiseRemovesOnlyTrackPenaltyAndActualPurchaseUsesCityShields()
        {
            var j=Start(PlaceType.City,CityColor.White,"army_2_006");j.Player.Reputation=-2;
            var site=j.CurrentInteractionSite;site.CityShields.AddRange(new[]{j.Player.Id,j.Player.Id});
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[5];j.Player.Skills.Add(skill);
            j.BeginSiteInteraction();Assert.That(j.Context.InfluencePool,Is.EqualTo(1));
            j.UseOutsideSkill(skill,1);Assert.That(j.Context.InfluencePool,Is.EqualTo(2));
            Play(j,"basic_card_008");j.RecruitSiteUnit(j.Game.Globals.Units.Offer.Single());
            Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Player.Reputation,Is.EqualTo(-2));
        }
        [TestCase(false)] [TestCase(true)]
        public void PositionOrSiteReferenceDriftRejectsRecruitBeforePayment(bool replacement)
        {
            var j=Start(PlaceType.City,CityColor.White,"army_2_006");j.BeginSiteInteraction();Promise(j);
            var card=j.Game.Globals.Units.Offer.Single();
            if(replacement) j.Game.Map.Places[SiteHex]=new PlaceState(PlaceType.City){CityColor=CityColor.White};
            else j.Player.Position=new AxialCoord(0,0);
            Assert.Throws<InvalidOperationException>(()=>j.RecruitSiteUnit(card));
            Assert.That(CardUnitActions.TryRecruit(j.Player,j.Context,j.Game.Globals.Units.Offer,0,RecruitLocation.City),Is.False);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(4));Assert.That(j.Player.Units,Is.Empty);
            Assert.That(j.Game.Globals.Units.Offer.Single(),Is.SameAs(card));
        }
        [Test]
        public void WhiteCityFamiliarNeedsActualColorPaymentBeforeTakingSupply()
        {
            var j=Start(PlaceType.City,CityColor.White,"army_2_004");j.BeginSiteInteraction();Promise(j);Play(j,"basic_card_008");
            var card=j.Game.Globals.Units.Offer.Single();
            Assert.Throws<InvalidOperationException>(()=>j.RecruitSiteUnit(card));
            Assert.That(j.Context.InfluencePool,Is.EqualTo(6));
            j.Player.Mana.AddToken(ManaColor.Green);j.RecruitSiteUnit(card,ManaColor.Green);
            Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Player.Mana.GetTotal(),Is.Zero);
            var unit=j.Player.Units.Single();Assert.That(unit.Card,Is.SameAs(card));Assert.That(unit.AttunementColor,Is.EqualTo(ManaColor.Green));
        }
        [Test]
        public void VillageCompatibilityWrapperCannotOpenOrHealAtAnotherSite()
        {
            var j=Start(PlaceType.Keep);Assert.Throws<InvalidOperationException>(()=>j.BeginVillageInteraction());
            j.BeginSiteInteraction();Promise(j);
            Assert.Throws<InvalidOperationException>(()=>j.RecruitVillageUnit(j.Game.Globals.Units.Offer.Single()));
            Assert.Throws<InvalidOperationException>(()=>j.BuyVillageHealing());
            Assert.That(j.Context.InfluencePool,Is.EqualTo(4));
        }
    }
}
