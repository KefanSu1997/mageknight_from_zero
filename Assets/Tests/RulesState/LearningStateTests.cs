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
    public sealed class LearningStateTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard card) Fixture(int influence=0)
        {
            var p=new PlayerState();var c=new ActionContext{AdvActionSupply=new AdvActionSupply(),InfluencePool=influence,SkipDraw=true};
            c.AdvActionSupply.Offer.AddRange(new[]{new AdvActionCard("advanced_card_001"),new AdvActionCard("advanced_card_000"),new AdvActionCard("advanced_card_003")});
            c.AdvActionSupply.SetDeck(new[]{new AdvActionCard("advanced_card_004")});
            var card=new DeedCard("advanced_card_026",CardType.Action);p.Deck.Hand.Add(card);p.Mana.AddToken(ManaColor.White);
            return(p,c,card);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard card,bool strong)
            =>CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==card.Id),strong);
        private static void End(PlayerState p,ActionContext c)=>new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
        [TestCase(false)] [TestCase(true)]
        public void PlayGrantsInfluenceAndOptionalPermissionWithoutAutoPurchase(bool strong)
        {
            var(p,c,card)=Fixture(20);var before=c.AdvActionSupply.Offer.ToArray();Play(p,c,card,strong);
            Assert.That(c.InfluencePool,Is.EqualTo(strong?24:22));Assert.That(c.LearnUsed,Is.False);
            Assert.That(c.LearningModes,Is.EqualTo(strong?2:1));Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(before));
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White),Is.EqualTo(strong?0:1));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplicitPurchasePaysSixOrNineSelectsActualOfferAndRefills(bool strong)
        {
            var(p,c,card)=Fixture(strong?5:4);Play(p,c,card,strong);var unchosen=c.AdvActionSupply.Offer[0];
            var gained=CardLearningActions.Purchase(p,c,1,strong);
            Assert.That(c.InfluencePool,Is.Zero);Assert.That(c.LearnUsed,Is.True);
            Assert.That((strong?p.Deck.Hand:p.Deck.DiscardPile).Single(),Is.SameAs(gained));Assert.That(gained.Id,Is.EqualTo("advanced_card_000"));
            Assert.That(c.AdvActionSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"advanced_card_001","advanced_card_003","advanced_card_004"}));
            Assert.That(c.AdvActionSupply.Offer[0],Is.SameAs(unchosen));
            End(p,c);Assert.That(p.Deck.DiscardPile.First(),Is.SameAs(card));
            Assert.That(strong?p.Deck.Hand.Contains(gained):p.Deck.DiscardPile.Contains(gained),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void InsufficientPaymentPreservesPermissionThenRealInfluenceCardEnablesPurchase(bool strong)
        {
            var(p,c,card)=Fixture(strong?1:0);Play(p,c,card,strong);int before=c.InfluencePool;
            Assert.Throws<InvalidOperationException>(()=>CardLearningActions.Purchase(p,c,1,strong));
            Assert.That(c.InfluencePool,Is.EqualTo(before));Assert.That(c.LearnUsed,Is.False);Assert.That(c.AdvActionSupply.Offer.Count,Is.EqualTo(3));
            var diplomacy=new DeedCard("basic_card_008",CardType.Action);p.Deck.Hand.Add(diplomacy);p.Mana.AddToken(ManaColor.White);
            CardPlayActions.Play(p,c,diplomacy,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==diplomacy.Id),true);
            CardLearningActions.Purchase(p,c,1,strong);Assert.That(c.InfluencePool,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void RepeatPurchaseCannotSpendAgain(bool strong)
        {
            var(p,c,card)=Fixture(20);Play(p,c,card,strong);CardLearningActions.Purchase(p,c,1,strong);
            int before=c.InfluencePool;var supply=c.AdvActionSupply.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>CardLearningActions.Purchase(p,c,1,strong));
            Assert.That(c.InfluencePool,Is.EqualTo(before));Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
        }
        [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
        public void EndTurnExpiresUnusedPermissionAndNewCardEnablesNewTurn(bool strong,bool purchased)
        {
            var(p,c,card)=Fixture(20);Play(p,c,card,strong);
            if(purchased)CardLearningActions.Purchase(p,c,1,strong);
            End(p,c);
            Assert.That(c.LearningModes,Is.Zero);Assert.That(c.LearnUsed,Is.False);
            c.InfluencePool=20;Assert.Throws<InvalidOperationException>(()=>CardLearningActions.Purchase(p,c,1,strong));
            var next=new DeedCard(card.Id,card.Type);p.Deck.Hand.Add(next);p.Mana.AddToken(ManaColor.White);Play(p,c,next,strong);
            CardLearningActions.Purchase(p,c,1,strong);Assert.That(c.LearnUsed,Is.True);
        }
        [TestCase(-1)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void InvalidRequestPreservesPermissionSupplyAndInfluence(int failure)
        {
            var(p,c,card)=Fixture(20);Play(p,c,card,false);int index=failure<4?failure:1;
            if(failure==4)c.AdvActionSupply.Offer[1]=new AdvActionCard("basic_card_021");
            if(failure==5)c.AdvActionSupply=null;
            var supply=c.AdvActionSupply?.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>CardLearningActions.Purchase(p,c,index,failure==6));
            Assert.That(c.InfluencePool,Is.EqualTo(22));Assert.That(c.LearnUsed,Is.False);Assert.That(p.Deck.DiscardPile,Is.Empty);
            if(supply!=null)Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
        }
        [Test]
        public void MissingWhiteManaDoesNotGrantInfluenceOrPermission()
        {
            var(p,c,card)=Fixture();p.Mana.Tokens.Clear();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,true));
            Assert.That(c.LearningModes,Is.Zero);Assert.That(c.InfluencePool,Is.Zero);Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
        }
    }
}
