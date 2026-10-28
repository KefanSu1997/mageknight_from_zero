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
    public sealed class OfferReplenishmentStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static ActionContext Supply()
        {
            var c=new ActionContext{AdvActionSupply=new AdvActionSupply(),SpellSupply=new SpellSupply(),SkipDraw=true};
            c.AdvActionSupply.SetDeck(new[]{"advanced_card_001","advanced_card_000","advanced_card_003","advanced_card_004","advanced_card_002"}.Select(id=>new AdvActionCard(id)));
            c.SpellSupply.SetDeck(new[]{"magic_015","magic_009","magic_005","magic_006","magic_021"}.Select(id=>new SpellCard(id)));
            c.AdvActionSupply.Refill(false);c.SpellSupply.Refill();return c;
        }
        [TestCase(false)] [TestCase(true)]
        public void GainImmediatelyFillsTopPreservesUnchosenInstancesAndAllowsTakingNewCard(bool spell)
        {
            var c=Supply();var firstA=c.AdvActionSupply.Offer[0];var lastA=c.AdvActionSupply.Offer[2];
            var firstS=c.SpellSupply.Offer[0];var lastS=c.SpellSupply.Offer[2];
            if(spell)
            {
                var selected=c.SpellSupply.Offer[1];Assert.That(c.SpellSupply.Claim(1),Is.SameAs(selected));
                Assert.That(c.SpellSupply.Offer,Is.EqualTo(new[]{firstS,lastS,c.SpellSupply.Offer[2]}));
                Assert.That(c.SpellSupply.Claim(2).Id,Is.EqualTo("magic_006"));
                Assert.That(c.SpellSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"magic_015","magic_005","magic_021"}));
            }
            else
            {
                var selected=c.AdvActionSupply.Offer[1];Assert.That(c.AdvActionSupply.Take(1),Is.SameAs(selected));
                Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(new[]{firstA,lastA,c.AdvActionSupply.Offer[2]}));
                Assert.That(c.AdvActionSupply.Take(2).Id,Is.EqualTo("advanced_card_004"));
                Assert.That(c.AdvActionSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"advanced_card_001","advanced_card_003","advanced_card_002"}));
            }
            Assert.That(c.AdvActionSupply.DiscardPile,Is.Empty);Assert.That(c.SpellSupply.DiscardPile,Is.Empty);
        }
        [TestCase(false,-1)] [TestCase(false,3)] [TestCase(true,-1)] [TestCase(true,3)]
        public void InvalidSelectionDoesNotRemoveShiftOrDraw(bool spell,int index)
        {
            var c=Supply();var a=c.AdvActionSupply.Offer.ToArray();var s=c.SpellSupply.Offer.ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(()=>{if(spell)c.SpellSupply.Claim(index);else c.AdvActionSupply.Take(index);});
            Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(a));Assert.That(c.SpellSupply.Offer,Is.EqualTo(s));
            if(spell){c.SpellSupply.Claim(1);Assert.That(c.SpellSupply.Offer[2].Id,Is.EqualTo("magic_006"));}
            else{c.AdvActionSupply.Take(1);Assert.That(c.AdvActionSupply.Offer[2].Id,Is.EqualTo("advanced_card_004"));}
        }
        [TestCase(false)] [TestCase(true)]
        public void EmptyReserveLeavesVacancyWithoutInventingCardsOrRecyclingUnchosen(bool spell)
        {
            var c=Supply();c.AdvActionSupply.SetDeck(Array.Empty<AdvActionCard>());c.SpellSupply.SetDeck(Array.Empty<SpellCard>());
            if(spell)c.SpellSupply.Claim(1);else c.AdvActionSupply.Take(1);
            Assert.That(spell?c.SpellSupply.Offer.Count:c.AdvActionSupply.Offer.Count,Is.EqualTo(2));
            Assert.That(c.AdvActionSupply.DiscardPile,Is.Empty);Assert.That(c.SpellSupply.DiscardPile,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void TwoActualWisdomBooksCanAcquireNewlyReplenishedOriginalInSameTurn(bool burn)
        {
            var c=Supply();var p=new PlayerState();
            var books=new[]{new DeedCard("items_013",CardType.Artifact),new DeedCard("items_013",CardType.Artifact)};
            var materials=new[]{new DeedCard("basic_card_021",CardType.Action),new DeedCard("basic_card_021",CardType.Action)};
            p.Deck.Hand.AddRange(books);p.Deck.Hand.AddRange(materials);
            var original=CardJsonLoader.LoadItems().Single(x=>x.Id=="items_013");
            c.CardToRemove=materials[0];CardPlayActions.PlayArtifact(p,c,books[0],original,burn,1);
            Assert.That(burn?c.SpellSupply.Offer[2].Id:c.AdvActionSupply.Offer[2].Id,Is.EqualTo(burn?"magic_006":"advanced_card_004"));
            c.CardToRemove=materials[1];CardPlayActions.PlayArtifact(p,c,books[1],original,burn,2);
            Assert.That(p.Deck.Hand.Select(x=>x.Id),Is.EqualTo(burn?new[]{"magic_009","magic_006"}:new[]{"advanced_card_000","advanced_card_004"}));
            Assert.That(p.Deck.RemovedCards.Contains(materials[0])&&p.Deck.RemovedCards.Contains(materials[1]),Is.True);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red),Is.EqualTo(burn?2:0));
            var remaining=burn?c.SpellSupply.Offer.Select(x=>x.Id).ToArray():c.AdvActionSupply.Offer.Select(x=>x.Id).ToArray();
            new PlayerTurnEngine().EndTurn(p,c.DayPart,new ManaSource(1,()=>c.DayPart),c);
            Assert.That(burn?c.SpellSupply.Offer.Select(x=>x.Id):c.AdvActionSupply.Offer.Select(x=>x.Id),Is.EqualTo(remaining));
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(burn?0:2));
            Assert.That(c.AdvActionSupply.DiscardPile,Is.Empty);Assert.That(c.SpellSupply.DiscardPile,Is.Empty);
        }
        [Test]
        public void MagicTalentPaysRealBlueAndRedThenAcquiresAndImmediatelyReplenishes()
        {
            var c=Supply();var p=new PlayerState();var card=new DeedCard("advanced_card_025",CardType.Action);
            p.Deck.Hand.Add(card);p.Mana.AddToken(ManaColor.Blue);p.Mana.AddToken(ManaColor.Red);
            var untouched=c.SpellSupply.Offer[0];
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==card.Id),true,1<<8);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));Assert.That(p.Deck.Hand,Is.Empty);
            Assert.That(p.Deck.DiscardPile.Single().Id,Is.EqualTo("magic_009"));Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(c.SpellSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"magic_015","magic_005","magic_006"}));
            Assert.That(c.SpellSupply.Offer[0],Is.SameAs(untouched));Assert.That(c.SpellSupply.DiscardPile,Is.Empty);
        }
    }
}
