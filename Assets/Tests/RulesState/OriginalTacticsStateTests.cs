using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class OriginalTacticsStateTests
    {
        private sealed class Rolls : Random
        {
            public Queue<int> Values = new(); public List<int> Sides = new();
            public override int Next(int maxValue) { Sides.Add(maxValue); return Values.Count == 0 ? 0 : Values.Dequeue(); }
        }
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static TacticCard Tactic(string id) => OriginalTactics.Get(id).Card;
        private static DeedCard[] Cards(int count) => Enumerable.Range(0,count).Select(i => new DeedCard("basic_card_000",CardType.Action)).ToArray();
        private static (GameEngine Game, PlayerState Player, DeedCard[] Cards) Start(string id,int count=12,Rolls rolls=null)
        {
            var p = new PlayerState(); var g = new GameEngine(new[]{p},3,manaRandom:rolls ?? new Rolls(),tacticRandom:new Random(52));
            // Set up a night fixture through the existing empty-round boundary; no player is rebuilt later.
            if(id.StartsWith("night_")) g.EndRound();
            var cards=Cards(count); p.Deck.SetDeck(cards); g.PrepareNewRound(); g.StartNewRound(new[]{Tactic(id)});
            return(g,p,cards);
        }
        private static void Unique(PlayerState p,IEnumerable<DeedCard> originals)
        {
            var actual=p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile).Concat(p.Deck.PlayedCards)
                .Concat(p.Deck.TacticAsideCards).Concat(p.Deck.RoundAsideCards).Concat(p.Deck.RemovedCards).ToArray();
            Assert.That(actual.Length,Is.EqualTo(originals.Count()));
            foreach(var card in originals) Assert.That(actual.Count(c=>ReferenceEquals(c,card)),Is.EqualTo(1));
        }

        [TestCase("day_1",5)] [TestCase("night_1",5)] [TestCase("day_5",7)]
        public void OriginalSelectionUsesExistingArtAndDrawsRealCards(string id,int expected)
        {
            var(g,p,cards)=Start(id);
            Assert.That(File.Exists(OriginalTactics.Get(id).ArtworkPath),Is.True);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(expected)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(12-expected));
            Assert.That(p.TacticHandBonus,Is.Zero); Assert.That(p.Deck.Hand.Last(),Is.SameAs(cards[expected-1]));
            Assert.That(g.BeginNextTurn(new ActionContext()),Is.SameAs(p)); Unique(p,cards);
        }
        [Test] public void ForgedOriginalTacticRejectsBeforeRefillOrRoundMutation()
        {
            var p=new PlayerState();p.Deck.SetDeck(Cards(8));var g=new GameEngine(new[]{p},3);
            Assert.Throws<InvalidOperationException>(()=>g.StartNewRound(new[]{Tactic("day_5") with {HandBonus=20}}));
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DrawPileCount,Is.EqualTo(8));Assert.That(g.IsRoundPrepared,Is.False);
        }
        [Test] public void RethinkDiscardsChosenInstancesDrawsBeforeShuffleAndPreservesWound()
        {
            var(g,p,cards)=Start("day_2"); p.Deck.GainWound();var wound=p.Deck.Hand.Last();
            var selected=new[]{p.Deck.Hand[0],wound};var replacement=p.Deck.DrawPile.Take(2).ToArray();
            Assert.Throws<InvalidOperationException>(()=>g.BeginNextTurn(new ActionContext()));
            Assert.Throws<InvalidOperationException>(()=>g.ResolveTacticSelection(p,new[]{selected[0],selected[0]}));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(6));Assert.That(p.Deck.DiscardPile,Is.Empty);
            g.ResolveTacticSelection(p,selected);
            foreach(var c in replacement)Assert.That(p.Deck.Hand.Any(h=>ReferenceEquals(c,h)),Is.True);
            foreach(var c in selected)Assert.That(p.Deck.DrawPile.Any(h=>ReferenceEquals(c,h)),Is.True);
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,wound)),Is.False);Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>g.ResolveTacticSelection(p,Array.Empty<DeedCard>()));
            Unique(p,cards.Append(wound));g.BeginNextTurn(new ActionContext());
        }
        [TestCase("Tovak")] [TestCase("Arythea")]
        [TestCase("Goldyx")] [TestCase("Norowas")] [TestCase("Wolfhawk")]
        [TestCase("Krang")] [TestCase("Braevalar")]
        public void OriginalHeroJourneyRethinkReplacesTwoCardsBeforeReturningThemToDeck(string heroId)
        {
            var originals=CardJsonLoader.LoadBasicActions();
            var player=OriginalHeroes.Get(heroId).CreatePlayer(originals,new Random(17));
            var cards=player.Deck.DrawPile.ToArray();
            var journey=new PveJourneySession(player,originals,tacticRandom:new Random(23));
            journey.PrepareRound(); journey.SelectTactic("day_2");
            Assert.That(player.Deck.Hand.Count,Is.EqualTo(5));
            Assert.That(player.Deck.DrawPileCount,Is.EqualTo(11));
            var chosen=player.Deck.Hand.Take(2).ToArray();
            var replacement=player.Deck.DrawPile.Take(2).ToArray();
            journey.Game.ResolveTacticSelection(player,chosen);
            Assert.That(player.Deck.Hand.Count,Is.EqualTo(5));
            Assert.That(player.Deck.DrawPileCount,Is.EqualTo(11));
            foreach(var card in replacement) Assert.That(player.Deck.Hand.Any(h=>ReferenceEquals(h,card)),Is.True);
            foreach(var card in chosen) Assert.That(player.Deck.Hand.Any(h=>ReferenceEquals(h,card)),Is.False);
            Assert.That(player.Deck.DiscardPile,Is.Empty); Unique(player,cards);
            journey.BeginTurn(); Assert.That(journey.Player,Is.SameAs(player));
            Assert.That(player.Deck.Hand.Count,Is.EqualTo(5));
        }
        [Test] public void RethinkEmptyDrawPileDoesNotDrawBackTheTwoDiscardedCards()
        {
            var(g,p,cards)=Start("day_2",5);
            var chosen=p.Deck.Hand.Take(2).ToArray();
            Assert.That(p.Deck.DrawPileCount,Is.Zero);
            g.ResolveTacticSelection(p,chosen);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(3));
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(2));
            foreach(var card in chosen) Assert.That(p.Deck.DrawPile.Any(h=>ReferenceEquals(h,card)),Is.True);
            Assert.That(p.Deck.DiscardPile,Is.Empty); Unique(p,cards);
        }
        [Test] public void GreatStartCannotManufactureCardsWhenOnlyOneRemains()
        {
            var(g,p,cards)=Start("day_5",6);Assert.That(p.Deck.Hand.Count,Is.EqualTo(6));Assert.That(p.Deck.DrawPileCount,Is.Zero);Unique(p,cards);
        }
        [TestCase(1,5)] [TestCase(2,6)]
        public void PlanningChecksActualKeptHandOnlyAtEndTurnRefill(int kept,int expected)
        {
            var(g,p,cards)=Start("day_4");var c=new ActionContext();g.BeginNextTurn(c);
            foreach(var card in p.Deck.Hand.Skip(kept).ToArray())p.Deck.TryDiscardExact(card);
            g.EndCurrentTurn(c);Assert.That(p.Deck.Hand.Count,Is.EqualTo(expected));Assert.That(p.TacticHandBonus,Is.Zero);Unique(p,cards);
        }
        [Test] public void ManaStealKeepsActualDieAcrossTurnsThenPaysOriginalCardAndReturnsOnlyWhenUsed()
        {
            var rolls=new Rolls();var(g,p,cards)=Start("day_3",12,rolls);var source=g.Globals.Mana;
            var die=source.Dice[0];die.TurnTo(ManaColor.Green);
            var other=source.Dice[1];other.TurnTo(ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(()=>g.ResolveTacticSelection(p,die:new ManaDie()));
            g.ResolveTacticSelection(p,die:die);Assert.That(source.Dice.Contains(die),Is.False);Assert.That(p.HeldManaDie,Is.Null);
            var first=new ActionContext();g.BeginNextTurn(first);g.EndCurrentTurn(first,new[]{p.Deck.Hand[0]});
            Assert.That(p.Tactic.ReservedDie,Is.SameAs(die));Assert.That(source.TacticClaims.ContainsKey(die),Is.True);
            var c=new ActionContext();g.BeginNextTurn(c);g.UseReservedTacticMana();
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.HeldManaDie,Is.Null);
            CardManaActions.UseSourceDie(p,c,other,ManaColor.Blue);Assert.That(p.HeldManaDie,Is.SameAs(other));
            var march=p.Deck.Hand[0];CardPlayActions.Play(p,c,march,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==march.Id),true);
            Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>g.UseReservedTacticMana());
            int calls=rolls.Sides.Count;g.EndCurrentTurn(c);
            Assert.That(rolls.Sides.Count-calls,Is.EqualTo(2));Assert.That(rolls.Sides.Skip(calls),Is.All.EqualTo(6));
            Assert.That(source.Dice.Contains(die),Is.True);Assert.That(source.Dice.Contains(other),Is.True);
            Assert.That(source.TacticClaims,Is.Empty);Assert.That(p.Tactic.Spent,Is.True);Unique(p,cards);
        }
        [Test] public void UnusedTacticDieReturnsOnRealRoundEndAndNextRoundHasNoOldReservation()
        {
            var(g,p,cards)=Start("day_3",5);var die=g.Globals.Mana.Dice[0];g.ResolveTacticSelection(p,die:die);
            g.AnnounceRoundEnd();g.EndRound();
            Assert.That(g.Globals.Mana.Dice.Contains(die),Is.True);Assert.That(g.Globals.Mana.TacticClaims,Is.Empty);Assert.That(p.Tactic,Is.Null);
            g.PrepareNewRound();g.StartNewRound(new[]{Tactic("night_1")});Unique(p,cards);
        }
        [Test] public void RightMomentUsesActualExtraTurnAndDoesNotRefreshUnitOrReplacePlayer()
        {
            var p=new PlayerState();var q=new PlayerState();p.Deck.SetDeck(Cards(8));q.Deck.SetDeck(Cards(8));
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(unit);
            var g=new GameEngine(new[]{p,q},3,manaRandom:new Rolls());g.StartNewRound(new[]{Tactic("day_6"),Tactic("day_1")});
            var qc=new ActionContext();Assert.That(g.BeginNextTurn(qc),Is.SameAs(q));g.EndCurrentTurn(qc,new[]{q.Deck.Hand[0]});
            var c=new ActionContext();Assert.That(g.BeginNextTurn(c),Is.SameAs(p));unit.Exhaust();unit.AddWounds(1);
            var card=p.Deck.Hand[0];CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),false);
            g.UseRightMoment();Assert.Throws<InvalidOperationException>(()=>g.UseRightMoment());g.EndCurrentTurn(c);
            var extra=new ActionContext();Assert.That(g.BeginNextTurn(extra),Is.SameAs(p));
            Assert.That(p.Units.Single(),Is.SameAs(unit));Assert.That(unit.IsReady,Is.False);Assert.That(unit.Wounds,Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,card)),Is.True);g.EndCurrentTurn(extra,new[]{p.Deck.Hand[0]});
            Assert.That(g.BeginNextTurn(new ActionContext()),Is.SameAs(q));
        }
        [Test] public void LongNightRestoresThreeActualDiscardedCardsOnlyOnce()
        {
            var(g,p,cards)=Start("night_2",5);
            foreach(var card in p.Deck.Hand.ToArray())p.Deck.TryDiscardExact(card);
            Assert.That(p.Deck.DrawPileCount,Is.Zero);g.UseLongNight(p);
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(3));Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
            Assert.That(p.Tactic.Spent,Is.True);Unique(p,cards);
            Assert.Throws<InvalidOperationException>(()=>g.UseLongNight(p));
            var c=new ActionContext();g.BeginNextTurn(c);g.EndCurrentTurn(c);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(3));Unique(p,cards);
        }
        [Test] public void LongNightWithNonemptyDeckRejectsWithoutSpendingItsRoundUse()
        {
            var(g,p,cards)=Start("night_2");Assert.Throws<InvalidOperationException>(()=>g.UseLongNight(p));
            Assert.That(p.Tactic.Spent,Is.False);Assert.That(p.Deck.DrawPileCount,Is.EqualTo(7));Unique(p,cards);
        }
        [Test] public void ManaSearchPrioritizesGoldUsesSixFacesAndDoesNotTakeDiceOrGrantMana()
        {
            var rolls=new Rolls();var(g,p,cards)=Start("night_3",12,rolls);var c=new ActionContext();g.BeginNextTurn(c);
            var dice=g.Globals.Mana.Dice.ToArray();dice[0].TurnTo(ManaColor.Gold);dice[1].TurnTo(ManaColor.Gold);dice[2].TurnTo(ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(()=>g.UseManaSearch(new[]{dice[0],dice[2]}));
            Assert.That(p.Tactic.ManaSearchUsedThisTurn,Is.False);Assert.That(dice[0].Face,Is.EqualTo(ManaColor.Gold));
            rolls.Values.Enqueue(4);rolls.Values.Enqueue(5);g.UseManaSearch(new[]{dice[0],dice[1]});
            Assert.That(dice[0].Face,Is.EqualTo(ManaColor.Gold));Assert.That(dice[1].Face,Is.EqualTo(ManaColor.Black));
            Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(p.HeldManaDie,Is.Null);Assert.That(g.Globals.Mana.Dice.Count,Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(()=>g.UseManaSearch(new[]{dice[0]}));
            CardManaActions.UseSourceDie(p,c,dice[1],ManaColor.Black);
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            g.EndCurrentTurn(c,new[]{p.Deck.Hand[0]});var next=new ActionContext();g.BeginNextTurn(next);
            Assert.That(p.Tactic.ManaSearchUsedThisTurn,Is.False);
            var basic=g.Globals.Mana.Dice.First(d=>d.Face<=ManaColor.White);CardManaActions.UseSourceDie(p,next,basic,basic.Face);
            Assert.Throws<InvalidOperationException>(()=>g.UseManaSearch(new[]{g.Globals.Mana.Dice[0]}));
        }
        [Test] public void MidnightMeditationShufflesChosenWoundAndCardsBeforeDrawingWithoutTouchingDiscard()
        {
            var(g,p,cards)=Start("night_4");p.Deck.GainWound();var wound=p.Deck.Hand.Last();var discarded=p.Deck.Hand[0];p.Deck.TryDiscardExact(discarded);
            var chosen=new[]{wound,p.Deck.Hand[0]};g.UseMidnightMeditation(chosen);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(discarded));
            Assert.That(p.Tactic.Spent,Is.True);Unique(p,cards.Append(wound));
            Assert.Throws<InvalidOperationException>(()=>g.UseMidnightMeditation(Array.Empty<DeedCard>()));
            g.BeginNextTurn(new ActionContext());
        }
        [Test] public void PreparationSearchesExactDeckCopyAndShufflesOnlyRemainingDrawPile()
        {
            var(g,p,cards)=Start("night_5");var selected=p.Deck.DrawPile.Last();var sameIdHand=p.Deck.Hand[0];
            Assert.Throws<InvalidOperationException>(()=>g.ResolveTacticSelection(p,new[]{sameIdHand}));
            Assert.That(g.HasPendingTacticSelection,Is.True);Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));
            g.ResolveTacticSelection(p,new[]{selected});Assert.That(p.Deck.Hand.Last(),Is.SameAs(selected));
            Assert.That(p.Deck.DrawPile.Any(c=>ReferenceEquals(c,selected)),Is.False);Assert.That(p.Deck.Hand[0],Is.SameAs(sameIdHand));Unique(p,cards);
        }
        [TestCase(false)] [TestCase(true)]
        public void SparingPowerStoresRealTopCardOncePerTurnAndCollectsOrReturnsAtRoundEnd(bool collect)
        {
            var(g,p,cards)=Start("night_6",8);var stored=p.Deck.DrawPile[0];
            Assert.Throws<InvalidOperationException>(()=>g.BeginNextTurn(new ActionContext()));g.ResolveSparingPower(false);
            Assert.That(p.Deck.TacticAsideCards.Single(),Is.SameAs(stored));Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>g.ResolveSparingPower(false));
            var c=new ActionContext();g.BeginNextTurn(c);g.EndCurrentTurn(c,new[]{p.Deck.Hand[0]});
            if(collect)
            {
                g.ResolveSparingPower(true);Assert.That(p.Deck.Hand.Last(),Is.SameAs(stored));Assert.That(p.Deck.TacticAsideCards,Is.Empty);
                Assert.That(p.Tactic.Spent,Is.True);var next=new ActionContext();g.BeginNextTurn(next);g.EndCurrentTurn(next,new[]{p.Deck.Hand[0]});
            }
            else
            {
                g.ResolveSparingPower(false);var next=new ActionContext();g.BeginNextTurn(next);g.EndCurrentTurn(next,new[]{p.Deck.Hand[0]});
                Assert.That(p.Deck.TacticAsideCards.Count,Is.EqualTo(2));
            }
            // Empty the remaining draw pile through real draws; retained hand is allowed when declaring.
            p.Deck.DrawExact(p.Deck.DrawPileCount);g.AnnounceRoundEnd();g.EndRound();
            Assert.That(p.Deck.TacticAsideCards,Is.Empty);Assert.That(p.Deck.DrawPile.Count(x=>ReferenceEquals(x,stored)),Is.EqualTo(1));Unique(p,cards);
        }
        [TestCase(DayPart.Day,5,ManaColor.Black)] [TestCase(DayPart.Night,4,ManaColor.Gold)]
        public void OrdinarySourceReturnCanRollADepletedFaceAndUsageStillRejectsIt(DayPart part,int roll,ManaColor expected)
        {
            var random=new Rolls();var source=new ManaSource(1,()=>part,random);var p=new PlayerState();var c=new ActionContext{DayPart=part,ManaSource=source};
            var die=source.Dice[0];CardManaActions.UseSourceDie(p,c,die,ManaColor.Red);random.Values.Enqueue(roll);source.Return(die);
            Assert.That(die.Face,Is.EqualTo(expected));Assert.That(random.Sides.Last(),Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(()=>CardManaActions.UseSourceDie(p,c,die,expected==ManaColor.Gold?ManaColor.Red:ManaColor.Black));
            Assert.That(source.Dice.Contains(die),Is.True);
        }
    }
}
