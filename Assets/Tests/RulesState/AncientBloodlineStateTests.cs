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
    public sealed class AncientBloodlineStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PlayerState p, ActionContext c, DeedCard source) Fixture(string offered="advanced_card_000")
        {
            var p=new PlayerState();
            var c=new ActionContext { AdvActionSupply=new AdvActionSupply(), SkipDraw=true, DayPart=DayPart.Day };
            c.AdvActionSupply.Offer.AddRange(new[]{new AdvActionCard("advanced_card_019"),new AdvActionCard(offered),
                new AdvActionCard(offered=="advanced_card_001"?"advanced_card_003":"advanced_card_001")});
            c.AdvActionSupply.SetDeck(new[]{new AdvActionCard("advanced_card_004")});
            var source=new DeedCard("advanced_card_030",CardType.Action);p.Deck.Hand.Add(source);
            return (p,c,source);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard source,bool strong,int option=1)
            =>CardPlayActions.Play(p,c,source,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==source.Id),strong,option);
        private static void End(PlayerState p,ActionContext c)=>new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);

        [TestCase("advanced_card_000",ManaColor.Red,false)] [TestCase("advanced_card_000",ManaColor.Red,true)]
        [TestCase("advanced_card_001",ManaColor.Blue,false)] [TestCase("advanced_card_001",ManaColor.Blue,true)]
        [TestCase("advanced_card_003",ManaColor.Green,false)] [TestCase("advanced_card_003",ManaColor.Green,true)]
        [TestCase("advanced_card_002",ManaColor.White,false)] [TestCase("advanced_card_002",ManaColor.White,true)]
        public void BasePaysMatchingColorGainsRealCardAndWoundAndRefills(string id,ManaColor color,bool crystal)
        {
            var(p,c,source)=Fixture(id);var unselected=c.AdvActionSupply.Offer[0];
            if(crystal)p.Mana.AddCrystal(color);else p.Mana.AddToken(color);
            Play(p,c,source,false,1|((int)color<<8));
            Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(x=>x.Type==CardType.Action).Id,Is.EqualTo(id));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));Assert.That(p.DiscardWounds,Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(color),Is.Zero);Assert.That(p.Mana.Crystals.GetValueOrDefault(color),Is.Zero);
            Assert.That(c.AdvActionSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"advanced_card_019",
                id=="advanced_card_001"?"advanced_card_003":"advanced_card_001","advanced_card_004"}));
            Assert.That(c.AdvActionSupply.Offer[0],Is.SameAs(unselected));
            var gained=p.Deck.Hand.Single(x=>x.Type==CardType.Action);var wound=p.Deck.Hand.Single(x=>x.Type==CardType.Wound);
            End(p,c);Assert.That(p.Deck.Hand.Contains(gained),Is.True);Assert.That(p.Deck.Hand.Contains(wound),Is.True);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(source));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void BaseInvalidChoiceOrMissingCostLeavesEveryZoneUntouched(int failure)
        {
            var(p,c,source)=Fixture();p.Mana.AddToken(ManaColor.Red);int option=1;
            if(failure==0)option=-1;
            if(failure==1)option=3;
            if(failure==2)option=1|(1<<8);
            if(failure==3)option=1|(4<<8);
            if(failure==4)p.Mana.Tokens.Clear();
            if(failure==5)c.AdvActionSupply.Offer[1]=new AdvActionCard("magic_000");
            if(failure==6)c.AdvActionSupply=null;
            if(failure==7)c.BloodlineWoundToDiscard=true;
            int mana=p.Mana.Tokens.GetValueOrDefault(ManaColor.Red);var supply=c.AdvActionSupply?.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,false,option));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(source));Assert.That(p.Wounds,Is.Zero);Assert.That(p.DiscardWounds,Is.Zero);
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(mana));
            if(supply!=null)Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
        }

        [TestCase("advanced_card_000",false)] [TestCase("advanced_card_000",true)]
        [TestCase("advanced_card_001",false)] [TestCase("advanced_card_001",true)]
        [TestCase("advanced_card_003",false)] [TestCase("advanced_card_003",true)]
        [TestCase("advanced_card_026",false)] [TestCase("advanced_card_026",true)]
        public void StrongBorrowsOriginalOfferWithoutDonorManaOrRemovingIt(string id,bool discard)
        {
            var(p,c,source)=Fixture(id);p.Mana.AddToken(ManaColor.Red);c.BloodlineWoundToDiscard=discard;
            var supply=c.AdvActionSupply.Offer.ToArray();Play(p,c,source,true);
            Assert.That(p.Wounds,Is.EqualTo(discard?0:1));Assert.That(p.DiscardWounds,Is.EqualTo(discard?1:0));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
            Assert.That(p.Deck.Hand.Any(x=>x.Id==id),Is.False);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            if(id=="advanced_card_026")
            {Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(c.LearningModes,Is.EqualTo(2));}
            else if(id=="advanced_card_003")Assert.That(c.SiegePool,Is.EqualTo(3));
            else
            {Assert.That(c.RangedPool,Is.EqualTo(3));Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(id=="advanced_card_000"?Element.Fire:Element.Ice));}
            End(p,c);Assert.That(c.RangedPool+c.SiegePool+c.InfluencePool,Is.Zero);
            Assert.That(p.Deck.DiscardPile.Contains(source),Is.True);Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
            Assert.That(p.Wounds+p.DiscardWounds,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void CopiedMagicTalentExtraRedMustBePaidAtomicallyWithBloodlineRed(bool enough)
        {
            var(p,c,source)=Fixture("advanced_card_025");p.Mana.AddToken(ManaColor.Red,enough?2:1);
            c.SpellSupply=new SpellSupply();c.SpellSupply.Offer.Add(new SpellCard("magic_009"));c.BloodlineEffectOption=0;
            if(!enough)
            {
                Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,true));
                Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(),Is.SameAs(source));
                Assert.That(p.Wounds,Is.Zero);Assert.That(c.SpellSupply.Offer.Count,Is.EqualTo(1));
                return;
            }
            Play(p,c,source,true);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Single().Id,Is.EqualTo("magic_009"));Assert.That(c.SpellSupply.Offer,Is.Empty);
            Assert.That(c.AdvActionSupply.Offer[1].Id,Is.EqualTo("advanced_card_025"));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue),Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void StrongInvalidDonorChoiceOrMissingHostManaIsAtomic(int failure)
        {
            var(p,c,source)=Fixture(failure==0?"advanced_card_038":failure==1?"advanced_card_030":"advanced_card_000");
            p.Mana.AddToken(ManaColor.Red);c.BloodlineEffectOption=failure==0?2:1;
            if(failure==2)p.Mana.Tokens.Clear();int option=failure==3?20:1;
            int mana=p.Mana.Tokens.GetValueOrDefault(ManaColor.Red);var supply=c.AdvActionSupply.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,true,option));
            Assert.That(p.Wounds,Is.Zero);Assert.That(p.DiscardWounds,Is.Zero);Assert.That(p.Deck.Hand.Single(),Is.SameAs(source));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(mana));Assert.That(c.AdvActionSupply.Offer,Is.EqualTo(supply));
            if(failure==1)
            {
                c.AdvActionSupply.Offer[1]=new AdvActionCard("advanced_card_000");c.BloodlineEffectOption=0;
                Play(p,c,source,true);Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(c.RangedPool,Is.EqualTo(3));
            }
        }

        [Test]
        public void BorrowedEffectDoesNotMakeNextNormalCardFreeAndAppliesAttackBonusOnce()
        {
            var(p,c,source)=Fixture("advanced_card_001");p.Mana.AddToken(ManaColor.Red);c.AmbushAttackBonus=2;
            var next=new DeedCard("advanced_card_001",CardType.Action);p.Deck.Hand.Add(next);Play(p,c,source,true);
            Assert.That(c.RangedPool,Is.EqualTo(5));Assert.That(c.AmbushTriggered,Is.True);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,next,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==next.Id),true));
            Assert.That(p.Deck.Hand.Contains(next),Is.True);Assert.That(c.RangedPool,Is.EqualTo(5));Assert.That(p.Wounds,Is.EqualTo(1));
        }
    }
}
