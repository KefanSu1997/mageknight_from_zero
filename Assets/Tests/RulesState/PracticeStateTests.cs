using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class PracticeStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PlayerState p, ActionContext c, DeedCard card, DeedCard material) Fixture(string material = "basic_card_021", string offered = "advanced_card_000")
        {
            var p = new PlayerState();
            var c = new ActionContext { AdvActionSupply = new AdvActionSupply(), SkipDraw = true };
            var card = new DeedCard("advanced_card_027", CardType.Action);
            var m = new DeedCard(material, CardType.Action);
            p.Deck.Hand.AddRange(new[] { card, m }); c.CardToRemove = m;
            c.AdvActionSupply.Offer.Add(new AdvActionCard(offered));
            p.Mana.AddToken(ManaColor.Green);
            return (p, c, card, m);
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong, int index = 0)
            => CardPlayActions.Play(p, c, card, CardJsonLoader.LoadAdvancedActions().Single(x => x.Id == card.Id), strong, index);

        [TestCase(false,"basic_card_021","advanced_card_000")]
        [TestCase(true,"basic_card_021","advanced_card_000")]
        [TestCase(false,"basic_card_014","advanced_card_001")]
        [TestCase(true,"basic_card_014","advanced_card_001")]
        [TestCase(false,"basic_card_008","advanced_card_002")]
        [TestCase(true,"basic_card_008","advanced_card_002")]
        [TestCase(false,"basic_card_000","advanced_card_003")]
        [TestCase(true,"basic_card_000","advanced_card_003")]
        public void OriginalColorsPermanentMaterialAndDestination(bool strong, string material, string offered)
        {
            var (p,c,card,m) = Fixture(material,offered); Play(p,c,card,strong);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(m));
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.That((strong ? p.Deck.Hand : p.Deck.DiscardPile).Single().Id, Is.EqualTo(offered));
            Assert.That(strong ? p.Deck.DiscardPile.Count : p.Deck.Hand.Count, Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(strong ? 0 : 1));
            Assert.That(c.CardToRemove, Is.Null); Assert.That(c.AdvActionSupply.Offer, Is.Empty);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,card)), Is.True);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(m));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplicitSecondOfferAndDuplicateMaterialRetainExactOtherInstances(bool strong)
        {
            var (p,c,card,m) = Fixture("advanced_card_004");
            var twin = new DeedCard(m.Id,m.Type); p.Deck.Hand.Insert(1,twin);
            var unchosen=c.AdvActionSupply.Offer[0]; c.AdvActionSupply.Offer.Add(new AdvActionCard("advanced_card_004"));
            c.AdvActionSupply.SetDeck(new[]{new AdvActionCard("advanced_card_003")}); Play(p,c,card,strong,1);
            Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,twin)), Is.True);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(m));
            Assert.That(c.AdvActionSupply.Offer[0], Is.SameAs(unchosen));
            Assert.That(c.AdvActionSupply.Offer.Select(x=>x.Id), Is.EqualTo(new[]{"advanced_card_000","advanced_card_003"}));
            Assert.That((strong ? p.Deck.Hand.Last() : p.Deck.DiscardPile.Single()).Id, Is.EqualTo("advanced_card_004"));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void InvalidSelectionOrMissingManaPreservesCardsAndPayment(int failure)
        {
            var (p,c,card,m) = Fixture(); int index=0;
            if(failure==0)c.CardToRemove=null;
            if(failure==1)c.CardToRemove=new DeedCard(m.Id,m.Type);
            if(failure==2 || failure==3 || failure==4)
            {
                var bad=new DeedCard(failure==2 ? "wound_0" : "magic_009",failure==2 ? CardType.Wound : failure==3 ? CardType.Spell : CardType.Action);
                p.Deck.Hand.Add(bad);c.CardToRemove=bad;
            }
            if(failure==5)c.CardToRemove=card;
            if(failure==6)c.AdvActionSupply=null;
            if(failure==7)index=-1;
            if(failure==8)index=1;
            if(failure==9)c.AdvActionSupply.Offer[0]=new AdvActionCard("advanced_card_001");
            if(failure==10)c.AdvActionSupply.Offer[0]=new AdvActionCard("basic_card_021");
            if(failure==11)p.Mana.Tokens.Clear();
            var hand=p.Deck.Hand.ToArray();var material=c.CardToRemove;var offer=c.AdvActionSupply?.Offer.ToArray();
            var mana=p.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,true,index));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(hand.Length));
            for(int i=0;i<hand.Length;i++)Assert.That(p.Deck.Hand[i],Is.SameAs(hand[i]));
            Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(c.CardToRemove,Is.SameAs(material));Assert.That(p.Mana.GetTotal(),Is.EqualTo(mana));
            if(offer!=null) for(int i=0;i<offer.Length;i++)Assert.That(c.AdvActionSupply.Offer[i],Is.SameAs(offer[i]));
        }
        [Test]
        public void GainedStrongCardCanBePlayedButBasicGainedCardCannot()
        {
            foreach(bool strong in new[]{false,true})
            {
                var (p,c,card,m)=Fixture(); Play(p,c,card,strong);
                var gained=(strong?p.Deck.Hand:p.Deck.DiscardPile).Single();
                var original=CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==gained.Id);
                if(strong){CardPlayActions.Play(p,c,gained,original);Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));}
                else Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,gained,original));
            }
        }

        [TestCase(false,"basic_card_021","advanced_card_000",false)]
        [TestCase(false,"basic_card_014","advanced_card_001",false)]
        [TestCase(false,"basic_card_008","advanced_card_002",false)]
        [TestCase(false,"basic_card_000","advanced_card_003",false)]
        [TestCase(false,"basic_card_021","advanced_card_000",true)]
        [TestCase(true,"basic_card_021","advanced_card_000",true)]
        public void DiscardObservationIsTopFirstAfterActualTurnEnd(bool strong,string material,string offered,bool playGained)
        {
            var(p,c,card,m)=Fixture(material,offered);Play(p,c,card,strong);
            if(playGained)
            {
                var gained=(strong?p.Deck.Hand:p.Deck.DiscardPile).Single();
                var original=CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==offered);
                if(strong)CardPlayActions.Play(p,c,gained,original);
                else Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,gained,original));
            }
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            // DiscardPile exposes top first: the later discard precedes the earlier one.
            Assert.That(p.Deck.DiscardPile.Select(x=>x.Id),Is.EqualTo(strong
                ? new[]{offered,"advanced_card_027"} : new[]{"advanced_card_027",offered}));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
        }
    }
}
