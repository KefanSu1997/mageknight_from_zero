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
    public sealed class FreeRecruitStateTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PlayerState p, ActionContext c, DeedCard card) Fixture(bool banner, bool full = false)
        {
            var p = new PlayerState { Reputation = -7 };
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            var c = new ActionContext { DayPart = DayPart.Night, InfluencePool = 2, CharmRecruitDiscount = 3,
                UnitOffer = new List<UnitCard> { CardJsonLoader.LoadUnit("army_2_011"), CardJsonLoader.LoadUnit("army_003") } };
            var card = new DeedCard(banner ? "items_017" : "magic_020", banner ? CardType.Artifact : CardType.Spell);
            p.Deck.Hand.Add(card);
            if (full) { var old = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); old.Exhaust(); old.AddWounds(1); p.Units.Add(old); }
            return (p,c,card);
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, int option = 0)
        {
            if (card.Type == CardType.Artifact)
                CardPlayActions.PlayArtifact(p,c,card,CardJsonLoader.LoadItems().Single(i=>i.Id==card.Id),true,option);
            else
            {
                var original = CardJsonLoader.LoadSpells().Single(s=>s.Id==card.Id);
                CardPlayActions.Play(p,c,card,new ActionCardData(original.Id,original.Name,original.Set,original.ImagePath,
                    original.EnImagePath,original.Top.Effect,original.Bottom.Effect,original.Top.ManaCost),true,option);
            }
        }

        [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(true,1)]
        public void FreeGainConsumesSelectedOfferAndActualCardWithoutInteraction(bool banner,int index)
        {
            var (p,c,card)=Fixture(banner);var selected=c.UnitOffer[index];
            Play(p,c,card,index);
            Assert.That(p.Units.Single().Card,Is.SameAs(selected));Assert.That(p.Units.Single().CanActivate,Is.True);
            Assert.That(c.UnitOffer.Count,Is.EqualTo(1));Assert.That(c.UnitOffer.Contains(selected),Is.False);
            Assert.That(p.FreeSlots,Is.Zero);Assert.That(p.TempCommandSlots,Is.Zero);
            Assert.That(p.Reputation,Is.EqualTo(-7));Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(c.CharmRecruitDiscount,Is.EqualTo(3));
            Assert.That(c.InNegotiation,Is.False);Assert.That(c.InteractionReputationModifier,Is.Null);Assert.That(c.FreeRecruit,Is.False);
            Assert.That(p.Fame,Is.EqualTo(banner?2:0));Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White),Is.EqualTo(banner?1:0));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.EqualTo(banner?1:0));
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(banner?1:0));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(banner?0:1));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,index));
            Assert.That(c.UnitOffer.Count,Is.EqualTo(1));Assert.That(p.Units.Count,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(banner?2:0));
            c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(p.Units.Count,Is.EqualTo(1));Assert.That(p.Units[0].IsReady,Is.True);
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(banner?0:1));Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(banner?1:0));
        }

        [TestCase(false)] [TestCase(true)]
        public void FullCommandReplacesOnlySelectedOwnedUnitAndNewUnitCanImmediatelyAct(bool banner)
        {
            var(p,c,card)=Fixture(banner,true);var old=p.Units.Single();c.UnitToDisband=old;
            Play(p,c,card);Assert.That(old.IsDestroyed,Is.True);Assert.That(p.Units.Contains(old),Is.False);
            Assert.That(c.UnitToDisband,Is.Null);Assert.That(p.Units.Single().Card.Id,Is.EqualTo("army_2_011"));
            CardUnitActions.Use(p,c,p.Units.Single(),2);Assert.That(c.InfluencePool,Is.EqualTo(6));
            Assert.That(p.Reputation,Is.EqualTo(-7));Assert.That(p.Units.Single().IsReady,Is.False);
        }

        [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(true,1)]
        [TestCase(false,2)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        [TestCase(false,4)] [TestCase(true,4)]
        public void InvalidChoiceRejectsBeforeAnyCostDisbandFameOrBurn(bool banner,int invalid)
        {
            var(p,c,card)=Fixture(banner,invalid<=1);int index=0;
            if(invalid==1)c.UnitToDisband=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            if(invalid==2)index=99;
            if(invalid==3)c.UnitOffer.Clear();
            if(invalid==4)c.UnitToDisband=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            var owned=p.Units.ToArray();var offer=c.UnitOffer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,index));
            Assert.That(p.Units,Is.EqualTo(owned));Assert.That(p.Units.All(u=>!u.IsDestroyed),Is.True);
            Assert.That(c.UnitOffer,Is.EqualTo(offer));Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
            Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Fame,Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.White],Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void SpellDayOrMissingBlackCannotDisbandOrTakeOffer(bool day)
        {
            var(p,c,card)=Fixture(false,true);c.UnitToDisband=p.Units.Single();
            if(day)c.DayPart=DayPart.Day;else p.Mana.Tokens[ ManaColor.Black ]=0;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card));
            Assert.That(p.Units.Single().IsDestroyed,Is.False);Assert.That(c.UnitOffer.Count,Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ ManaColor.White ],Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
        }

        [TestCase(false)] [TestCase(true)]
        public void FreeHeroesDoNotDoubleOngoingInteraction(bool banner)
        {
            var (p,c,card) = Fixture(banner); p.Reputation = 2;
            c.UnitOffer[0] = CardJsonLoader.LoadUnit("army_008");
            RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            Play(p,c,card);
            Assert.That(p.Units.Single().Card.Id, Is.EqualTo("army_008"));
            Assert.That(c.InteractionDoubled, Is.False); Assert.That(c.InteractionAppliedModifier, Is.EqualTo(1));
            Assert.That(c.InfluencePool, Is.EqualTo(3)); Assert.That(p.Reputation, Is.EqualTo(2));
            Assert.That(c.UnitOffer.Count, Is.EqualTo(1));
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.RemovedCards.Count, Is.EqualTo(banner ? 1 : 0));
            Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(banner ? 0 : 1));
        }

        [TestCase(false)] [TestCase(true)]
        public void FreeThugsDoNotReverseOngoingInteractionAndRecruitRewardsStillApply(bool banner)
        {
            var(p,c,card)=Fixture(banner);p.Reputation=2;
            RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            c.FamePerRecruit=1;c.ReputationPerRecruit=1;
            Play(p,c,card);
            Assert.That(c.InteractionAppliedModifier,Is.EqualTo(1));Assert.That(c.InteractionReversed,Is.False);
            Assert.That(c.InfluencePool,Is.EqualTo(3));Assert.That(c.CharmRecruitDiscount,Is.EqualTo(3));
            Assert.That(p.Reputation,Is.EqualTo(3));Assert.That(p.Fame,Is.EqualTo(banner?3:1));
        }
    }
}
