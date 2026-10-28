using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveJourneyPlayerViewTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private (PveJourneySession j,PlayerState a,PlayerState b) Create()
        {
            var a=new PlayerState(101,"第一位"){HeroId="Arythea"};var b=new PlayerState(202,"同伴"){HeroId="Tovak"};
            foreach(var p in new[]{a,b})p.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var j=new PveJourneySession(a,CardJsonLoader.LoadBasicActions(),companions:new[]{b},
                manaRandom:new Random(17),tacticRandom:new Random(23),supplyRandom:new Random(31));
            return(j,a,b);
        }

        [Test] public void ViewFollowsRealTacticOrderAndNextTurnWithoutGrantingControlOrReplacingContext()
        {
            var(j,a,b)=Create();j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(a));Assert.That(j.Context,Is.Null);
            j.SelectTactics("day_4","day_1");j.PreparePlayerView();
            Assert.That(j.Player,Is.SameAs(b));Assert.That(j.Game.HasActiveTurn,Is.False);Assert.That(j.Context,Is.Null);
            var bCard=b.Deck.Hand[0];Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideBattle(bCard,"base"));
            Assert.That(b.Deck.Hand.Contains(bCard),Is.True);
            j.BeginTurn();var bContext=j.Context;j.PreparePlayerView();j.PreparePlayerView();
            Assert.That(j.Player,Is.SameAs(b));Assert.That(j.Game.CurrentPlayer,Is.SameAs(b));Assert.That(j.Context,Is.SameAs(bContext));
            j.EndTurn(new[]{bCard});j.PreparePlayerView();
            Assert.That(j.Player,Is.SameAs(a));Assert.That(j.Game.HasActiveTurn,Is.False);Assert.That(j.Context,Is.SameAs(bContext));
            Assert.That(j.CompletedTurns,Is.EqualTo(1));
            var aCard=a.Deck.Hand[0];Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideBattle(aCard,"base"));
            j.BeginTurn();Assert.That(j.Player,Is.SameAs(a));Assert.That(j.Context,Is.Not.SameAs(bContext));
            Assert.That(j.Context.ManaSource,Is.SameAs(bContext.ManaSource));j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(a));
            j.EndTurn(new[]{aCard});j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(b));
            Assert.That(j.Game.HasActiveTurn,Is.False);Assert.That(j.CompletedTurns,Is.EqualTo(2));
        }

        [Test] public void PendingTacticViewAdvancesOnlyAfterEachPlayersActualSelection()
        {
            var(j,a,b)=Create();j.SelectTactics("day_2","day_3");j.PreparePlayerView();
            Assert.That(j.Game.HasPendingTacticSelection,Is.True);Assert.That(j.Player,Is.SameAs(a));
            Assert.Throws<InvalidOperationException>(()=>j.BeginTurn());Assert.That(j.Context,Is.Null);
            var selected=a.Deck.Hand[0];j.Game.ResolveTacticSelection(a,new[]{selected});j.PreparePlayerView();
            Assert.That(j.Player,Is.SameAs(b));Assert.That(j.Game.HasPendingTacticSelection,Is.True);
            Assert.That(a.Tactic.SelectionResolved,Is.True);Assert.That(b.Tactic.SelectionResolved,Is.False);
            var die=j.Game.Globals.Mana.Dice.First(d=>FamiliarActions.IsBasicColor(d.Face));
            j.Game.ResolveTacticSelection(b,die:die);j.PreparePlayerView();
            Assert.That(j.Game.HasPendingTacticSelection,Is.False);Assert.That(j.Player,Is.SameAs(a));
            Assert.That(j.Game.HasActiveTurn,Is.False);Assert.That(j.Context,Is.Null);
            j.BeginTurn();Assert.That(j.Game.CurrentPlayer,Is.SameAs(a));Assert.That(j.Player,Is.SameAs(a));
            j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(a));
        }

        [Test] public void PreparedRoundViewShowsCompanionsActualUnpaidFamiliarBeforeTactics()
        {
            var(j,a,b)=Create();var familiar=new UnitState(CardJsonLoader.LoadUnit("army_2_004"));b.Units.Add(familiar);
            b.Mana.AddCrystal(ManaColor.Blue);j.PrepareRound();Assert.That(familiar.NeedsAttunementUpkeep,Is.True);
            j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(b));Assert.That(j.Game.IsRoundPrepared,Is.True);
            Assert.That(j.Game.HasActiveTurn,Is.False);Assert.That(j.Context,Is.Null);
            Assert.Throws<InvalidOperationException>(()=>j.SelectTactics("day_1","day_4"));
            j.Game.ResolveFamiliarUpkeep(b,familiar,ManaColor.Blue);j.PreparePlayerView();
            Assert.That(j.Player,Is.SameAs(a));Assert.That(b.Units.Single(),Is.SameAs(familiar));
            Assert.That(familiar.NeedsAttunementUpkeep,Is.False);Assert.That(b.Mana.Crystals[ManaColor.Blue],Is.Zero);
            j.SelectTactics("day_4","day_1");j.PreparePlayerView();Assert.That(j.Player,Is.SameAs(b));
            j.BeginTurn();Assert.That(j.Player,Is.SameAs(b));Assert.That(b.Units.Single(),Is.SameAs(familiar));
        }
    }
}
