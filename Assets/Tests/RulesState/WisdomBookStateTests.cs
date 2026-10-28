using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class WisdomBookStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard book,DeedCard material) Fixture(string materialId="basic_card_021",string advanced="advanced_card_000",string spell="magic_009")
        {
            var p=new PlayerState();var c=new ActionContext{AdvActionSupply=new AdvActionSupply(),SpellSupply=new SpellSupply(),SkipDraw=true};
            var b=new DeedCard("items_013",CardType.Artifact);var m=new DeedCard(materialId,CardType.Action);
            p.Deck.Hand.AddRange(new[]{b,m});c.CardToRemove=m;
            c.AdvActionSupply.Offer.Add(new AdvActionCard(advanced));c.SpellSupply.Offer.Add(new SpellCard(spell));
            return(p,c,b,m);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard b,bool burn=false,int index=0)
            => CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(x=>x.Id=="items_013"),burn,index);
        private static void End(PlayerState p,ActionContext c) => new PlayerTurnEngine().EndTurn(p,c.DayPart,new ManaSource(1,()=>c.DayPart),c);
        [TestCase("basic_card_021","advanced_card_000",ManaColor.Red)]
        [TestCase("basic_card_014","advanced_card_001",ManaColor.Blue)]
        [TestCase("basic_card_008","advanced_card_002",ManaColor.White)]
        [TestCase("basic_card_000","advanced_card_003",ManaColor.Green)]
        public void BaseRemovesExactOriginalActionAndGetsSameColorAdvancedToHand(string material,string advanced,ManaColor color)
        {
            var(p,c,b,m)=Fixture(material,advanced);Play(p,c,b);
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(b));
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo(advanced));Assert.That(p.Deck.Hand.Single().Type,Is.EqualTo(CardType.Action));
            Assert.That(CardColorHelper.Get(advanced),Is.EqualTo(color));Assert.That(c.AdvActionSupply.Offer,Is.Empty);
            Assert.That(c.SpellSupply.Offer.Count,Is.EqualTo(1));Assert.That(c.CardToRemove,Is.Null);Assert.That(p.Mana.GetTotal(),Is.Zero);
            End(p,c);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo(advanced));
        }
        [TestCase("basic_card_021","magic_009",ManaColor.Red,DayPart.Day)]
        [TestCase("basic_card_014","magic_015",ManaColor.Blue,DayPart.Night)]
        [TestCase("basic_card_008","magic_021",ManaColor.White,DayPart.Day)]
        [TestCase("basic_card_000","magic_001",ManaColor.Green,DayPart.Night)]
        public void BurnAcquiresSpellAndCrystalWithoutCastingOrPayingSpellCosts(string material,string spell,ManaColor color,DayPart part)
        {
            var(p,c,b,m)=Fixture(material,spell:spell);c.DayPart=part;Play(p,c,b,true);
            Assert.That(p.Deck.RemovedCards,Is.EqualTo(new[]{m,b}));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo(spell));Assert.That(p.Deck.Hand.Single().Type,Is.EqualTo(CardType.Spell));
            Assert.That(c.SpellSupply.Offer,Is.Empty);Assert.That(c.AdvActionSupply.Offer.Count,Is.EqualTo(1));
            Assert.That(p.Mana.Crystals[color],Is.EqualTo(1));Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(c.RangedPool+c.SiegePool+c.InfluencePool,Is.Zero);
            Assert.That(c.AllowBlackAtDay,Is.False);End(p,c);Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Mana.Crystals[color],Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplicitOfferIndexSelectsSecondSameColorAndLeavesOtherActualOffer(bool burn)
        {
            var(p,c,b,m)=Fixture();var otherA=c.AdvActionSupply.Offer[0];var otherS=c.SpellSupply.Offer[0];
            c.AdvActionSupply.Offer.Add(new AdvActionCard("advanced_card_004"));c.SpellSupply.Offer.Add(new SpellCard("magic_006"));
            Play(p,c,b,burn,1);
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo(burn?"magic_006":"advanced_card_004"));
            Assert.That(burn?(object)c.SpellSupply.Offer.Single():c.AdvActionSupply.Offer.Single(),Is.SameAs(burn?(object)otherS:otherA));
            Assert.That(p.Deck.RemovedCards.Any(x=>ReferenceEquals(x,m)),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void AdvancedActionMaterialAndEqualIdCopiesPreserveExactIdentity(bool burn)
        {
            var(p,c,b,m)=Fixture("advanced_card_004");var twin=new DeedCard(m.Id,m.Type);p.Deck.Hand.Insert(1,twin);
            Play(p,c,b,burn);Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,twin)),Is.True);
            Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,m)),Is.False);Assert.That(p.Deck.RemovedCards.First(),Is.SameAs(m));
        }
        [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(true,1)]
        [TestCase(false,2)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        [TestCase(false,4)] [TestCase(true,4)] [TestCase(false,5)] [TestCase(true,5)]
        [TestCase(false,6)] [TestCase(true,6)] [TestCase(false,7)] [TestCase(true,7)]
        [TestCase(false,8)] [TestCase(true,8)] [TestCase(false,9)] [TestCase(true,9)]
        public void InvalidMaterialOfferOrColorRejectsBeforeAnyResourceMutation(bool burn,int failure)
        {
            var(p,c,b,m)=Fixture();int index=0;
            if(failure==0)c.CardToRemove=null;
            if(failure==1)c.CardToRemove=new DeedCard(m.Id,m.Type);
            if(failure==2){var wound=new DeedCard("w",CardType.Wound);p.Deck.Hand.Add(wound);c.CardToRemove=wound;}
            if(failure==3){var spell=new DeedCard("magic_009",CardType.Spell);p.Deck.Hand.Add(spell);c.CardToRemove=spell;}
            if(failure==4)c.CardToRemove=b;
            if(failure==5){if(burn)c.SpellSupply=null;else c.AdvActionSupply=null;}
            if(failure==6)index=-1;
            if(failure==7)index=1;
            if(failure==8){c.AdvActionSupply.Offer[0]=new AdvActionCard("advanced_card_001");c.SpellSupply.Offer[0]=new SpellCard("magic_015");}
            if(failure==9){var fake=new DeedCard("magic_009",CardType.Action);p.Deck.Hand.Add(fake);c.CardToRemove=fake;}
            var hand=p.Deck.Hand.ToArray();var material=c.CardToRemove;
            var advanced=c.AdvActionSupply?.Offer.ToArray();var spells=c.SpellSupply?.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,burn,index));
            Assert.That(p.Deck.Hand,Is.EqualTo(hand));Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(c.CardToRemove,Is.SameAs(material));Assert.That(p.Mana.GetTotal(),Is.Zero);
            if(advanced!=null)Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(advanced));if(spells!=null)Assert.That(c.SpellSupply.Offer,Is.EqualTo(spells));
        }
        [Test]
        public void FullCrystalInventoryTurnsPrintedExcessIntoTemporaryMana()
        {
            var(p,c,b,m)=Fixture();p.Mana.AddCrystal(ManaColor.Red,3);Play(p,c,b,true);
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(3));Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            End(p,c);Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(3));
        }
        [Test]
        public void AcquiredAdvancedCanImmediatelyPlayItsOriginalEffect()
        {
            var(p,c,b,m)=Fixture();Play(p,c,b);var card=p.Deck.Hand.Single();
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==card.Id));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.EqualTo(new[]{b,card}));
            End(p,c);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
        }
        [Test]
        public void AcquiredSpellPaysPrintedCostFromActualNewCrystalAndDayStrongStillRejects()
        {
            var(p,c,b,m)=Fixture();Play(p,c,b,true);var card=p.Deck.Hand.Single();var s=CardJsonLoader.LoadSpells().Single(x=>x.Id==card.Id);
            var data=new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,card,data,true));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
            CardPlayActions.Play(p,c,card,data);Assert.That(c.RangedPool,Is.EqualTo(5));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));Assert.That(p.Deck.RemovedCards,Is.EqualTo(new[]{m,b}));
        }
    }
}
