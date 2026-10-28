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
    public sealed class PveJourneySessionTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static Monster Enemy() => CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
        private static PveJourneySession Create(params string[] ids)
        {
            var p = new PlayerState();
            p.Deck.SetDeck(ids.Select(id => new DeedCard(id,CardType.Action)));
            p.Mana.AddCrystal(ManaColor.Red,2); p.Mana.AddCrystal(ManaColor.Blue,2);
            return new PveJourneySession(p,CardJsonLoader.LoadBasicActions());
        }
        private static DeedCard Card(PlayerState p,string id) => p.Deck.Hand.First(c => c.Id == id);
        private static void Win(PveBattleSession b)
        {
            b.Advance(); b.Play(Card(b.Player,"basic_card_016"),"strong"); b.Block(0);
            b.Advance(); b.Advance(); b.Play(Card(b.Player,"basic_card_021"),"strong"); b.Attack(0); b.Advance();
            Assert.That(b.Won,Is.True);
        }

        [Test] public void TwoBattlesUseSamePlayerAndRealCostsThenRoundReturnsSameCards()
        {
            var s=Create("basic_card_016","basic_card_021","basic_card_016","basic_card_021","basic_card_000","basic_card_000","basic_card_014");
            var p=s.Player; var cards=p.Deck.DrawPile.ToArray();
            s.PrepareRound(); s.SelectTactic("day_1"); s.BeginTurn();
            var firstContext=s.Context; var e=Enemy(); var first=s.BeginBattle(new[]{e});
            Assert.That(first.Context,Is.SameAs(firstContext)); Win(first); s.EndTurn();
            Assert.That(s.CompletedBattles,Is.EqualTo(1)); Assert.That(p.Fame,Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5)); Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
            Assert.That(p.Deck.DrawPileCount,Is.Zero); Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>s.EndTurn()); Assert.That(s.CompletedTurns,Is.EqualTo(1));
            s.BeginTurn(); Assert.That(s.Context,Is.Not.SameAs(firstContext));
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{e}));
            Assert.That(s.Context.TurnActionCommitted,Is.False);
            // A second physical token with the same printed values is allowed; identity is not record equality.
            var second=s.BeginBattle(new[]{e with { }}); Assert.That(second.Player,Is.SameAs(p)); Win(second); s.EndTurn();
            Assert.That(p.Fame,Is.EqualTo(4)); Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero); Assert.That(s.CompletedBattles,Is.EqualTo(2));
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(4)); Assert.That(p.Deck.Hand.Count,Is.EqualTo(3));
            s.AnnounceRoundEnd(); s.EndRound(); Assert.That(s.Game.Clock.DayPart,Is.EqualTo(DayPart.Night));
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(7));
            foreach(var card in cards) Assert.That(p.Deck.DrawPile.Count(c=>ReferenceEquals(c,card)),Is.EqualTo(1));
            s.PrepareRound(); s.SelectTactic("night_1"); s.BeginTurn();
            Assert.That(s.Game.CurrentPlayer,Is.SameAs(p)); Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(2));
        }

        [Test] public void ActiveBattleAndSpentTurnRejectAnotherEncounterWithoutReplacingContext()
        {
            var s=Create("basic_card_016","basic_card_021"); var e=Enemy();
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{e}));
            s.SelectTactic("day_1"); s.BeginTurn(); var c=s.Context;
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{e,e}));
            Assert.That(c.TurnActionCommitted,Is.False); Assert.That(c.InBattle,Is.False);
            var b=s.BeginBattle(new[]{e});
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{Enemy()}));
            Assert.Throws<InvalidOperationException>(()=>s.EndTurn()); Assert.That(s.Game.HasActiveTurn,Is.True);
            Assert.That(s.Context,Is.SameAs(c)); Win(b);
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{Enemy()}));
            Assert.That(s.Battle,Is.SameAs(b)); s.EndTurn();
        }

        [Test] public void WoundedExhaustedUnitPersistsAndNewRoundReadiesWithoutHealing()
        {
            var s=Create("basic_card_000","basic_card_000"); var p=s.Player;
            var u=new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"),"army_2_006"));
            p.Units.Add(u); s.SelectTactic("day_1"); s.BeginTurn();
            var b=s.BeginBattle(new[]{Enemy()}); b.Advance(); b.Advance();
            b.PlanDamage(0,0,u); b.Advance(); Assert.That(u.Wounds,Is.EqualTo(1));
            u.Exhaust(); b.Advance(); Assert.That(b.Won,Is.False); s.EndTurn(new[]{p.Deck.Hand.First(c=>c.Type!=CardType.Wound)}); s.BeginTurn();
            Assert.That(p.Units.Single(),Is.SameAs(u)); Assert.That(u.IsReady,Is.False); Assert.That(u.Wounds,Is.EqualTo(1));
            var again=s.BeginBattle(b.Enemies); // The actual undefeated token remains available.
            again.Advance(); again.Advance();
            Assert.Throws<InvalidOperationException>(()=>again.PlanDamage(0,0,u));
            again.Advance(); again.Advance(); s.EndTurn(new[]{p.Deck.Hand.First(c=>c.Type!=CardType.Wound)}); s.AnnounceRoundEnd(); s.EndRound(); s.PrepareRound();
            Assert.That(p.Units.Single(),Is.SameAs(u)); Assert.That(u.IsReady,Is.True); Assert.That(u.Wounds,Is.EqualTo(1));
            Assert.That(u.CanActivate,Is.False);
        }

        [Test] public void PriorBattleCommandsStayClosedAfterEndTurnAndWhenNextTurnBegins()
        {
            var s=Create("basic_card_016","basic_card_021","basic_card_000"); s.SelectTactic("day_1"); s.BeginTurn();
            var b=s.BeginBattle(new[]{Enemy()}); Win(b); s.EndTurn();
            Assert.Throws<InvalidOperationException>(()=>b.HealWound());
            s.BeginTurn();
            Assert.Throws<InvalidOperationException>(()=>b.UseCourage(null));
            Assert.Throws<InvalidOperationException>(()=>b.PlayArtifact(null,false));
            Assert.Throws<InvalidOperationException>(()=>b.Advance());
            Assert.That(b.Won,Is.True); Assert.That(s.Player.Fame,Is.EqualTo(2));
        }

        [Test] public void UnresolvedTacticDoesNotCreateContextOrLosePlayerState()
        {
            var s=Create("basic_card_000","basic_card_014"); s.SelectTactic("day_2");
            Assert.Throws<InvalidOperationException>(()=>s.BeginTurn()); Assert.That(s.Context,Is.Null);
            s.Game.ResolveTacticSelection(s.Player,Array.Empty<DeedCard>()); s.BeginTurn();
            Assert.That(s.Game.CurrentPlayer,Is.SameAs(s.Player));
            Assert.That(s.Context.ManaSource,Is.SameAs(s.Game.Globals.Mana));
            Assert.That(s.Context.MovementMap,Is.SameAs(s.Game.Map));
        }
    }
}
