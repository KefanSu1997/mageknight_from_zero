using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class OfferRoundRotationStateTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static readonly string[] Advanced={"advanced_card_001","advanced_card_000","advanced_card_003","advanced_card_004","advanced_card_002"};
        private static readonly string[] Spells={"magic_015","magic_009","magic_005","magic_006","magic_021"};
        private static GameEngine Game(PlayerState p)
        {
            var game=new GameEngine(new[]{p},5);
            game.Globals.AdvActions.SetDeck(Advanced.Select(id=>new AdvActionCard(id)));
            game.Globals.Spells.SetDeck(Spells.Select(id=>new SpellCard(id)));
            game.Globals.AdvActions.Refill(false);game.Globals.Spells.Refill();return game;
        }
        [TestCase(false)] [TestCase(true)]
        public void RoundRotatesOnlyBottomAndReturnsSameOriginalInstanceAfterReserve(bool spells)
        {
            var game=Game(new PlayerState());var a=game.Globals.AdvActions;var s=game.Globals.Spells;
            var originalA=a.Offer.ToArray();var originalS=s.Offer.ToArray();
            for(int round=1;round<=3;round++)
            {
                if(spells)s.Refill();else a.Refill(false);
                var ids=spells?s.Offer.Select(c=>c.Id):a.Offer.Select(c=>c.Id);
                var deck=spells?Spells:Advanced;
                Assert.That(ids,Is.EqualTo(Enumerable.Range(round,3).Select(i=>deck[i%5])));
                if(round==1)
                {
                    Assert.That(spells?(object)s.Offer[0]:a.Offer[0],Is.SameAs(spells?(object)originalS[1]:originalA[1]));
                    Assert.That(spells?(object)s.Offer[1]:a.Offer[1],Is.SameAs(spells?(object)originalS[2]:originalA[2]));
                }
            }
            Assert.That(spells?(object)s.Offer[2]:a.Offer[2],Is.SameAs(spells?(object)originalS[0]:originalA[0]));
        }
        [TestCase(false)] [TestCase(true)]
        public void EmptyReserveCyclesBottomToTopWithoutShrinkingOrDuplicating(bool spells)
        {
            var game=Game(new PlayerState());var a=game.Globals.AdvActions;var s=game.Globals.Spells;
            a.SetDeck(Array.Empty<AdvActionCard>());s.SetDeck(Array.Empty<SpellCard>());
            var aa=a.Offer.ToArray();var ss=s.Offer.ToArray();
            if(spells)s.Refill();else a.Refill(false);
            Assert.That(spells?s.Offer.Count:a.Offer.Count,Is.EqualTo(3));
            Assert.That(spells?(object)s.Offer[2]:a.Offer[2],Is.SameAs(spells?(object)ss[0]:aa[0]));
            Assert.That(spells?s.Offer.Distinct().Count():a.Offer.Distinct().Count(),Is.EqualTo(3));
        }
        [TestCase(false)] [TestCase(true)]
        public void UnderfilledSupplyOnlyCyclesActualCards(bool spells)
        {
            var game=Game(new PlayerState());var a=game.Globals.AdvActions;var s=game.Globals.Spells;
            a.SetDeck(Array.Empty<AdvActionCard>());s.SetDeck(Array.Empty<SpellCard>());
            a.Take(1);s.Claim(1);var aa=a.Offer.ToArray();var ss=s.Offer.ToArray();
            if(spells)s.Refill();else a.Refill(false);
            Assert.That(spells?s.Offer.Count:a.Offer.Count,Is.EqualTo(2));
            Assert.That(spells?(object)s.Offer[0]:a.Offer[0],Is.SameAs(spells?(object)ss[1]:aa[1]));
            Assert.That(spells?(object)s.Offer[1]:a.Offer[1],Is.SameAs(spells?(object)ss[0]:aa[0]));
        }
        [Test]
        public void RealGameRoundTransitionRotatesBothOffersOnceAndAdvancesDayNight()
        {
            var game=Game(new PlayerState());var s0=game.Globals.Spells.Offer[0];var a0=game.Globals.AdvActions.Offer[0];
            for(int round=1;round<=3;round++)
            {
                game.EndRound();
                Assert.That(game.Globals.AdvActions.Offer.Select(c=>c.Id),Is.EqualTo(Enumerable.Range(round,3).Select(i=>Advanced[i%5])));
                Assert.That(game.Globals.Spells.Offer.Select(c=>c.Id),Is.EqualTo(Enumerable.Range(round,3).Select(i=>Spells[i%5])));
                Assert.That(game.Clock.RoundIndex,Is.EqualTo(round+1));
                Assert.That(game.Clock.DayPart,Is.EqualTo(round%2==1?DayPart.Night:DayPart.Day));
            }
            Assert.That(game.Globals.AdvActions.Offer[2],Is.SameAs(a0));Assert.That(game.Globals.Spells.Offer[2],Is.SameAs(s0));
        }
        [TestCase(false)] [TestCase(true)]
        public void WisdomAcquiredCardNeverReentersOfferWhileOtherCardsCycle(bool burn)
        {
            var p=new PlayerState();var game=Game(p);
            var c=new ActionContext{AdvActionSupply=game.Globals.AdvActions,SpellSupply=game.Globals.Spells,SkipDraw=true};
            var book=new DeedCard("items_013",CardType.Artifact);var material=new DeedCard("basic_card_021",CardType.Action);
            p.Deck.Hand.AddRange(new[]{book,material});c.CardToRemove=material;
            CardPlayActions.PlayArtifact(p,c,book,CardJsonLoader.LoadItems().Single(x=>x.Id==book.Id),burn,1);
            var acquired=p.Deck.Hand.Single();new PlayerTurnEngine().EndTurn(p,DayPart.Day,game.Globals.Mana,c);
            for(int round=0;round<3;round++)
            {
                game.EndRound();Assert.That(p.Deck.Hand,Is.Empty);
                Assert.That(p.Deck.DrawPile.Count(c=>ReferenceEquals(c,acquired)),Is.EqualTo(1));
                Assert.That(burn?game.Globals.Spells.Offer.Any(x=>x.Id==acquired.Id):game.Globals.AdvActions.Offer.Any(x=>x.Id==acquired.Id),Is.False);
            }
            Assert.That(p.Deck.RemovedCards.Contains(material),Is.True);
            Assert.That(burn?p.Deck.RemovedCards.Contains(book):p.Deck.DrawPile.Contains(book),Is.True);
            Assert.That(p.Deck.DiscardPile,Is.Empty);
        }
    }
}
