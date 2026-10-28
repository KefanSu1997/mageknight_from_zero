using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SpellTomeStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard book) Fixture(string payment="basic_card_021",string spell="magic_009")
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Day,SpellSupply=new SpellSupply()};
            var book=new DeedCard("items_023",CardType.Artifact);p.Deck.Hand.Add(book);
            var card=new DeedCard(payment,payment.StartsWith("magic")?CardType.Spell:CardType.Action);p.Deck.Hand.Add(card);c.CardToDiscard=card;
            c.SpellSupply.SetDeck(new[]{new SpellCard(spell)});c.SpellSupply.Refill();return(p,c,book);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard book,bool burn=false,int index=0)
            => CardPlayActions.PlayArtifact(p,c,book,CardJsonLoader.LoadItems().Single(s=>s.Id=="items_023"),burn,index);

        [TestCase("basic_card_021","magic_009")] [TestCase("basic_card_014","magic_015")]
        [TestCase("basic_card_000","magic_001")] [TestCase("basic_card_008","magic_021")]
        [TestCase("magic_009","magic_009")]
        public void MatchingOriginalColorWorksWithoutManaAndLeavesSpellInOffer(string payment,string spell)
        {
            var(p,c,b)=Fixture(payment,spell);var selected=c.CardToDiscard;var offered=c.SpellSupply.Offer.Single();p.Wounds=2;
            Play(p,c,b);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(selected));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(b));
            Assert.That(c.SpellSupply.Offer.Single(),Is.SameAs(offered));Assert.That(p.Mana.Tokens,Is.Empty);
            Assert.That(c.CardToDiscard,Is.Null);
            if(spell=="magic_009"||spell=="magic_015")Assert.That(c.RangedPool,Is.EqualTo(5));
            if(spell=="magic_001")
            {
                MK.Logic.Runtime.CardEffects.CardHealing.Spend(p,c,2);
                Assert.That(p.Wounds,Is.Zero);
            }
            if(spell=="magic_021")Assert.That(c.InfluencePool,Is.EqualTo(4));
        }

        [TestCase("magic_009",DayPart.Day)] [TestCase("magic_009",DayPart.Night)]
        [TestCase("magic_015",DayPart.Day)] [TestCase("magic_015",DayPart.Night)]
        public void BurningAllowsStrongSpellByDayWithoutColorOrBlackCost(string spell,DayPart part)
        {
            var(p,c,b)=Fixture(spell=="magic_009"?"basic_card_021":"basic_card_014",spell);c.DayPart=part;
            Play(p,c,b,true);Assert.That(c.SiegePool,Is.EqualTo(8));Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(c.AllowBlackAtDay,Is.False);Assert.That(c.SpellSupply.Offer.Count,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidPaymentColorOrOfferNeverDiscardsOrBurns(int failure)
        {
            var(p,c,b)=Fixture();int index=0;
            if(failure==0)c.CardToDiscard=new DeedCard(c.CardToDiscard.Id,c.CardToDiscard.Type);
            if(failure==1){var wound=new DeedCard("w",CardType.Wound);p.Deck.Hand.Add(wound);c.CardToDiscard=wound;}
            if(failure==2)c.CardToDiscard=b;
            if(failure==3){c.SpellSupply.SetDeck(new[]{new SpellCard("magic_015")});c.SpellSupply.Refill();}
            if(failure==4)index=-1;if(failure==5)index=1;
            var before=p.Deck.Hand.ToArray();Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true,index));
            Assert.That(p.Deck.Hand,Is.EqualTo(before));Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(c.CombatPower.Attacks,Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void NestedOptionAndExtraManaValidateBeforeTomeDiscard(bool invalidOption)
        {
            var(p,c,b)=Fixture("basic_card_014","magic_017");c.TomeSpellOption=invalidOption?9:0;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
        }

        [Test]
        public void ExtraEffectManaStillCostsButPrintedBlueAndBlackAreWaived()
        {
            var(p,c,b)=Fixture("basic_card_014","magic_017");p.Mana.AddToken(ManaColor.Red);Play(p,c,b,true);
            Assert.That(c.MeleePool,Is.EqualTo(10));Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.ColdFire));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);
        }

        [Test]
        public void FreeDayPermissionDoesNotLeakToNextNormalSpell()
        {
            var(p,c,b)=Fixture();Play(p,c,b,true);var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_009");
            var data=new ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost);
            Assert.Throws<InvalidOperationException>(()=>new ActionSystem().Play(data,p,c,true));
            c.DayPart=DayPart.Night;Assert.Throws<InvalidOperationException>(()=>new ActionSystem().Play(data,p,c,true));
            Assert.That(c.SiegePool,Is.EqualTo(8));
        }
    }
}
