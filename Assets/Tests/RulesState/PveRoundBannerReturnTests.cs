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
    public sealed class PveRoundBannerReturnTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static DeedCard Banner(PlayerState p,string id) { var c=new DeedCard(id,CardType.Artifact);p.Deck.Hand.Add(c);p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));return c; }
        private static UnitState Unit(PlayerState p) {var u=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(u);return u;}
        private static PveJourneySession Journey(PlayerState p,PlayerState other=null,MapState map=null,UnitCard[] offer=null)
            => new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),artifacts:CardJsonLoader.LoadItems(),map:map,unitDeck:offer,companions:other==null?null:new[]{other});

        [TestCase(false)] [TestCase(true)] public void OrdinaryFlagSelectionOnlyAtRealRoundBoundaryPreservesUnitAndExactCard(bool take)
        {
            var p=new PlayerState();var flag=Banner(p,"items_000");var unit=Unit(p);var j=Journey(p);
            Assert.That(j.RoundBannerUnits(p),Is.Empty);j.SelectTactic("day_1");j.BeginTurn();unit.Exhaust();unit.AddWounds(1);
            j.PlayOutsideArtifact(flag,false,unit);Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(p,unit,true));
            j.EndTurn();Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(p,unit,true));j.AnnounceRoundEnd();
            Assert.That(j.RoundBannerUnits(p).Single(),Is.SameAs(unit));j.ChooseBannerRoundReturn(p,unit,true);Assert.That(j.IsBannerReturning(p,unit),Is.True);
            j.ChooseBannerRoundReturn(p,unit,false);Assert.That(j.IsBannerReturning(p,unit),Is.False);j.ChooseBannerRoundReturn(p,unit,take);
            Assert.That(unit.Banner,Is.SameAs(flag));j.EndRound();
            Assert.That(p.Units.Single(),Is.SameAs(unit));Assert.That(unit.IsReady,Is.False);Assert.That(unit.Wounds,Is.EqualTo(1));
            Assert.That(unit.Banner,take?Is.Null:Is.SameAs(flag));Assert.That(p.Deck.DrawPile.Count(c=>ReferenceEquals(c,flag)),Is.EqualTo(take?1:0));Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(j.RoundBannerUnits(p),Is.Empty);Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(p,unit,true));
            j.PrepareRound();Assert.That(unit.IsReady,Is.True);Assert.That(unit.Wounds,Is.EqualTo(1));
            if(take)Assert.That(p.Deck.Hand.Count(c=>ReferenceEquals(c,flag)),Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(p,unit,true));
        }

        [Test] public void ActualCommandRecruitReturnRemovesOnlyBannerCommandedUnitAndShufflesFlag()
        {
            var p=new PlayerState();var flag=Banner(p,"items_017");var ordinary=Unit(p);
            var map=new MapState();map.Placed[p.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Places[p.Position]=new PlaceState(PlaceType.Village);
            var offered=CardJsonLoader.LoadUnit("army_2_006");var j=Journey(p,map:map,offer:new[]{offered});j.SelectTactic("day_1");j.BeginTurn();
            j.PlaySpecialArtifact(flag,false);j.SelectCommandBanner(flag);j.BeginVillageInteraction();j.RecruitVillageUnit(offered);var recruited=p.Units.Single(u=>ReferenceEquals(u.Card,offered));
            Assert.That(recruited.Banner,Is.SameAs(flag));j.EndTurn();j.AnnounceRoundEnd();j.ChooseBannerRoundReturn(p,recruited,true);j.EndRound();
            Assert.That(p.Units.Single(),Is.SameAs(ordinary));Assert.That(recruited.IsDestroyed,Is.True);Assert.That(recruited.Banner,Is.Null);
            Assert.That(p.Deck.DrawPile.Count(c=>ReferenceEquals(c,flag)),Is.EqualTo(1));Assert.That(p.Deck.RemovedCards,Is.Empty);
            j.PrepareRound();Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,flag)),Is.True);Assert.That(p.Units.Single(),Is.SameAs(ordinary));
        }

        [Test] public void TwoPlayersChooseActualOwnersAfterFinalTurnWithoutChangingCurrentPlayer()
        {
            var a=new PlayerState(1,"A");var b=new PlayerState(2,"B");var fa=Banner(a,"items_000");var fb=Banner(b,"items_002");var ua=Unit(a);var ub=Unit(b);
            var j=Journey(a,b);j.SelectTactics("day_1","day_4");j.BeginTurn();j.PlayOutsideArtifact(fa,false,ua);j.EndTurn();
            j.BeginTurn();j.PlayOutsideArtifact(fb,false,ub);j.EndTurn();j.AnnounceRoundEnd();
            Assert.That(j.Game.ClockTracker.RoundEnded,Is.False);Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(a,ua,true));
            j.BeginTurn();j.EndTurn(new[]{b.Deck.Hand.Single()});Assert.That(j.Game.ClockTracker.RoundEnded,Is.True);Assert.That(j.Player,Is.SameAs(b));
            Assert.That(j.RoundBannerUnits(a).Single(),Is.SameAs(ua));Assert.That(j.RoundBannerUnits(b).Single(),Is.SameAs(ub));
            Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(b,ua,true));Assert.Throws<InvalidOperationException>(()=>j.ChooseBannerRoundReturn(new PlayerState(1,"A"),ua,true));
            j.ChooseBannerRoundReturn(a,ua,true);j.ChooseBannerRoundReturn(b,ub,false);Assert.That(j.Player,Is.SameAs(b));j.EndRound();
            Assert.That(ua.Banner,Is.Null);Assert.That(ub.Banner,Is.SameAs(fb));Assert.That(a.Deck.DrawPile.Any(c=>ReferenceEquals(c,fa)),Is.True);
            Assert.That(b.Deck.DrawPile.Any(c=>ReferenceEquals(c,fa)),Is.False);Assert.That(b.Deck.DrawPile.Any(c=>ReferenceEquals(c,fb)),Is.False);
            j.PrepareRound();Assert.That(a.Units.Single(),Is.SameAs(ua));Assert.That(b.Units.Single(),Is.SameAs(ub));
        }
    }
}
