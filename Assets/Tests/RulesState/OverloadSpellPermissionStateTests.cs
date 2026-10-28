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
    public sealed class OverloadSpellPermissionStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));

        private static (PlayerState p,ActionContext c,DeedCard source,DeedCard[] materials) Fixture(string material="basic_card_021",bool stepwise=false)
        {
            var p=new PlayerState();var source=new DeedCard("advanced_card_024",CardType.Action);
            var copied=new DeedCard("advanced_card_025",CardType.Action);
            var cards=Enumerable.Range(0,3).Select(_=>new DeedCard(material,CardType.Action)).ToArray();
            p.Deck.Hand.AddRange(new[]{source,copied}.Concat(cards));
            var c=new ActionContext { InBattle=true,DayPart=DayPart.Night,CardToRemove=copied,OverloadStepwise=stepwise,SkipDraw=true,SpellSupply=new SpellSupply() };
            c.SpellSupply.Offer.AddRange(new[]{new SpellCard("magic_009"),new SpellCard("magic_015"),new SpellCard("magic_021")});
            if(stepwise)c.CardToDiscard=cards[0];
            else c.OverloadSelections=cards.Select(card=>new RepeatedCardSelection { CardToDiscard=card }).ToList();
            return(p,c,source,cards);
        }

        private static void Play(PlayerState p,ActionContext c,DeedCard source)
            => CardPlayActions.Play(p,c,source,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==source.Id));

        [TestCase("basic_card_021",ManaColor.Red,0,Element.Fire)]
        [TestCase("basic_card_016",ManaColor.Blue,1,Element.Ice)]
        public void ThreeRealDiscardsPermitThreePaidOriginalCastsNotFour(string material,ManaColor color,int index,Element element)
        {
            var(p,c,source,cards)=Fixture(material);var offer=c.SpellSupply.Offer.ToArray();
            Play(p,c,source);p.Mana.AddToken(color,4);
            for(int i=0;i<3;i++)
            {
                CardSpellActions.PlayFromOffer(p,c,index);
                Assert.That(c.RangedPool,Is.EqualTo(5*(i+1)));Assert.That(c.SpellColors.Count,Is.EqualTo(2-i));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(color),Is.EqualTo(3-i));
            }
            Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,index));
            Assert.That(c.RangedPool,Is.EqualTo(15));Assert.That(p.Mana.Tokens.GetValueOrDefault(color),Is.EqualTo(1));
            Assert.That(c.CombatPower.Attacks.All(a=>a.Element==element),Is.True);
            Assert.That(c.SpellSupply.Offer,Is.EqualTo(offer));
            for(int i=0;i<offer.Length;i++)Assert.That(c.SpellSupply.Offer[i],Is.SameAs(offer[i]));
            Assert.That(p.Deck.DiscardPile.Select(x=>x.Id),Is.EqualTo(cards.Reverse().Select(x=>x.Id)));
            for(int i=0;i<3;i++)Assert.That(p.Deck.DiscardPile[i],Is.SameAs(cards[2-i]));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));Assert.That(p.Deck.RemovedCards.Single().Id,Is.EqualTo("advanced_card_025"));
        }

        [Test]
        public void WrongColorIndexAndMissingManaPreserveAllThreePermissions()
        {
            var(p,c,source,_)=Fixture();Play(p,c,source);
            foreach(int index in new[]{1,-1,3,0})Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,index));
            Assert.That(c.SpellColors,Is.EqualTo(new[]{ManaColor.Red,ManaColor.Red,ManaColor.Red}));
            Assert.That(c.RangedPool,Is.Zero);Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(c.SpellSupply.Offer.Count,Is.EqualTo(3));
        }

        [TestCase(DayPart.Day)] [TestCase(DayPart.Night)]
        public void StrongCastPaysEachOriginalColorAndBlackAndFailureKeepsRemainingPermission(DayPart part)
        {
            var(p,c,source,_)=Fixture();c.DayPart=part;Play(p,c,source);p.Mana.AddToken(ManaColor.Red,3);p.Mana.AddToken(ManaColor.Black,2);
            if(part==DayPart.Day)
            {
                Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,0,true));
                Assert.That(c.SpellColors.Count,Is.EqualTo(3));Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(3));
                Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));Assert.That(p.Wounds,Is.Zero);
            }
            else
            {
                CardSpellActions.PlayFromOffer(p,c,0,true);CardSpellActions.PlayFromOffer(p,c,0,true);
                Assert.That(c.SiegePool,Is.EqualTo(16));Assert.That(p.Wounds,Is.EqualTo(2));
                Assert.That(c.SpellColors.Count,Is.EqualTo(1));Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
                Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,0,true));
                Assert.That(c.SpellColors.Count,Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Wounds,Is.EqualTo(2));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void EndTurnClearsUnusedPermissionsAndKeepsAllActualCards(bool skip)
        {
            var(p,c,source,cards)=Fixture();Play(p,c,source);c.SkipDraw=skip;
            p.Mana.AddCrystal(ManaColor.Red);
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(c.SpellColors,Is.Empty);Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Count+p.Deck.DiscardPile.Count+p.Deck.DrawPile.Count+p.Deck.PlayedCards.Count,Is.EqualTo(4));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,0));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(c.SpellSupply.Offer.Count,Is.EqualTo(3));
        }

        [Test]
        public void StepwiseDiscardUsesCurrentPhysicalHandAndCannotCastMidComposite()
        {
            var(p,c,source,cards)=Fixture(stepwise:true);p.Mana.AddToken(ManaColor.Red,3);Play(p,c,source);
            Assert.Throws<InvalidOperationException>(()=>CardSpellActions.PlayFromOffer(p,c,0));
            Assert.That(c.SpellColors.Count,Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection { CardToDiscard=new DeedCard(cards[1].Id,cards[1].Type) }));
            Assert.That(c.PendingRepeatedEffect.Remaining,Is.EqualTo(2));Assert.That(p.Deck.Hand[0],Is.SameAs(cards[1]));
            foreach(var card in cards.Skip(1))CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection { CardToDiscard=card });
            Assert.That(c.PendingRepeatedEffect,Is.Null);
            for(int i=0;i<3;i++)CardSpellActions.PlayFromOffer(p,c,0);
            Assert.That(c.RangedPool,Is.EqualTo(15));Assert.That(c.SpellColors,Is.Empty);Assert.That(p.Mana.Tokens,Is.Empty);
        }

        [Test]
        public void OfferedSelfWoundTriggersRealKnockoutAndDiscardsOtherHandCards()
        {
            var(p,c,source,_)=Fixture();Play(p,c,source);
            var other=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(other);
            var enemy=MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json")).Single(x=>x.Id=="monster_067");
            var damage=CardCombatActions.Block(p,c,enemy);
            Assert.That(damage.Wounds,Is.EqualTo(4));Assert.That(c.IsKnockedOut,Is.False);
            Assert.That(p.Deck.Hand.Contains(other),Is.True);
            CardCombatActions.EnterPhase(c,Phase.Melee);
            p.Mana.AddToken(ManaColor.Red);p.Mana.AddToken(ManaColor.Black);
            CardSpellActions.PlayFromOffer(p,c,0,true);
            Assert.That(c.BattleWoundsReceived,Is.EqualTo(5));Assert.That(c.IsKnockedOut,Is.True);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.Hand.All(x=>x.Type==CardType.Wound),Is.True);
            Assert.That(p.Deck.DiscardPile[0],Is.SameAs(other));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Assert.That(c.SiegePool,Is.EqualTo(8));Assert.That(c.SpellColors.Count,Is.EqualTo(2));
            Assert.That(c.SpellSupply.Offer[0].Id,Is.EqualTo("magic_009"));
        }
    }
}
