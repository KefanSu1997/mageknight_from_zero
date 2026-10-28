using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class OverloadContinuationStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard source) Fixture(string material)
        {
            var p = new PlayerState();
            var source = new DeedCard("advanced_card_024", CardType.Action);
            var target = new DeedCard(material, CardType.Action);
            p.Deck.Hand.AddRange(new[] { source, target });
            p.Mana.AddToken(ManaColor.Red);
            return (p, new ActionContext { CardToRemove=target, OverloadStepwise=true, SkipDraw=true }, source);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard source, bool strong, int option=0)
            => CardPlayActions.Play(p,c,source,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==source.Id),strong,option);
        private static void Next(PlayerState p, ActionContext c, int option)
            => CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection { Option=option });

        [TestCase(false, 2)]
        [TestCase(true, 1)]
        public void ImprovisationRequiresAllSeparateMaterialsBeforeStarting(bool strong, int count)
        {
            var(p,c,source)=Fixture("basic_card_023");
            var materials=Enumerable.Range(0,count).Select(_=>new DeedCard("basic_card_021",CardType.Action)).ToArray();
            p.Deck.Hand.AddRange(materials);
            p.Deck.Hand.Add(new DeedCard("w",CardType.Wound));
            c.CardToDiscard=materials[0];
            var hand=p.Deck.Hand.ToArray();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,strong));
            Assert.That(p.Deck.Hand,Is.EqualTo(hand));
            Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            Assert.That(c.PendingRepeatedEffect,Is.Null);
            Assert.That(c.MovementPool,Is.Zero);
        }

        [TestCase(false, 3)]
        [TestCase(true, 2)]
        public void ImprovisationConsumesDistinctMaterialsAcrossEveryUse(bool strong, int count)
        {
            var(p,c,source)=Fixture("basic_card_023");
            var donor=c.CardToRemove;
            var materials=Enumerable.Range(0,count).Select(_=>new DeedCard("basic_card_021",CardType.Action)).ToArray();
            p.Deck.Hand.AddRange(materials);c.CardToDiscard=materials[0];
            Play(p,c,source,strong);
            for(int i=1;i<count;i++)
                CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection {Option=0,CardToDiscard=materials[i]});
            Assert.That(c.PendingRepeatedEffect,Is.Null);
            Assert.That(c.MovementPool,Is.EqualTo(count*(strong?5:3)));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(donor));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Assert.That(p.Deck.DiscardPile,Is.EquivalentTo(materials));
            Assert.That(p.Deck.Hand,Is.Empty);
        }

        [Test]
        public void ClaimRefillsBeforeNextChoiceAndPaysNewColorOnlyOnce()
        {
            var(p,c,source)=Fixture("advanced_card_025");
            p.Mana.AddToken(ManaColor.Red);p.Mana.AddToken(ManaColor.Green);
            var first=new SpellCard("magic_006");var refill=new SpellCard("magic_000");
            c.SpellSupply=new SpellSupply();c.SpellSupply.Offer.Add(first);c.SpellSupply.SetDeck(new[]{refill});
            Play(p,c,source,true);
            Assert.That(c.PendingRepeatedEffect.Remaining,Is.EqualTo(1));
            Assert.That(c.PendingRepeatedEffect.Completed,Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Assert.That(p.Deck.DiscardPile.Single().Id,Is.EqualTo("magic_006"));
            Assert.That(c.SpellSupply.Offer.Single(),Is.SameAs(refill));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.EqualTo(1));
            var pending=c.PendingRepeatedEffect;
            Assert.Throws<InvalidOperationException>(()=>Next(p,c,0));
            Assert.That(c.PendingRepeatedEffect,Is.SameAs(pending));
            Assert.That(pending.Remaining,Is.EqualTo(1));
            Assert.That(c.SpellSupply.Offer.Single(),Is.SameAs(refill));
            Next(p,c,2);
            Assert.That(c.PendingRepeatedEffect,Is.Null);
            Assert.That(c.SpellSupply.Offer,Is.Empty);
            Assert.That(p.Deck.DiscardPile.Select(x=>x.Id),Is.EqualTo(new[]{"magic_000","magic_006"}));
            Assert.That(p.Mana.Tokens,Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>Next(p,c,2));
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
        }

        [Test]
        public void LaterInsufficientCostDoesNotUndoFirstClaimOrConsumeSecond()
        {
            var(p,c,source)=Fixture("advanced_card_025");p.Mana.AddToken(ManaColor.Red,2);
            c.SpellSupply=new SpellSupply();c.SpellSupply.Offer.Add(new SpellCard("magic_006"));
            var green=new SpellCard("magic_000");var red=new SpellCard("magic_007");
            c.SpellSupply.SetDeck(new[]{green,red});Play(p,c,source,true);
            Assert.Throws<InvalidOperationException>(()=>Next(p,c,2));
            Assert.That(c.SpellSupply.Offer,Is.EqualTo(new[]{green,red}));
            Assert.That(p.Deck.DiscardPile.Single().Id,Is.EqualTo("magic_006"));
            Assert.That(c.PendingRepeatedEffect.Remaining,Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));
            Next(p,c,256);
            Assert.That(c.PendingRepeatedEffect,Is.Null);
            Assert.That(c.SpellSupply.Offer.Single(),Is.SameAs(green));
            Assert.That(p.Deck.DiscardPile.Select(x=>x.Id),Is.EqualTo(new[]{"magic_007","magic_006"}));
            Assert.That(p.Mana.Tokens,Is.Empty);
        }

        [Test]
        public void DrawThenChooseAgainFromObservedHandWithoutReplayingFirstDraw()
        {
            var(p,c,source)=Fixture("basic_card_001");
            var cards=new[]{new DeedCard("basic_card_021",CardType.Action),new DeedCard("basic_card_016",CardType.Action)};
            foreach(var card in cards.Reverse()) p.Deck.PutOnTop(card);
            Play(p,c,source,false,1);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(cards[0]));
            Assert.That(c.PendingRepeatedEffect.Remaining,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>Next(p,c,5));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(1));Assert.That(p.Deck.DrawPile.Count,Is.EqualTo(1));
            Next(p,c,1);Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));
            Next(p,c,0);Assert.That(c.PendingRepeatedEffect,Is.Null);
            Assert.That(c.Healing.Total,Is.EqualTo(1));
            Assert.That(p.Deck.Hand,Is.EqualTo(cards));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
        }

        [Test]
        public void CompositeBlockCannotBeSplitAcrossEnemiesOrTurns()
        {
            var(p,c,source)=Fixture("basic_card_016");c.InBattle=true;
            var enemy=MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json")).Single(x=>x.Id=="monster_002");
            Play(p,c,source,true);Assert.That(c.BlockPool,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Block(p,c,enemy));
            Assert.Throws<InvalidOperationException>(()=>new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c));
            Assert.That(c.BlockPool,Is.EqualTo(5));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Next(p,c,0);Assert.That(c.BlockPool,Is.EqualTo(10));
            var result=CardCombatActions.Block(p,c,enemy);
            Assert.That(result.PrintedPower,Is.EqualTo(10));Assert.That(result.Wounds,Is.Zero);Assert.That(c.BlockPool,Is.Zero);
        }

        [Test]
        public void CompositePostprocessingRunsOnceAfterAllUses()
        {
            var(p,c,source)=Fixture("basic_card_021");c.InBattle=true;c.AmbushAttackBonus=2;c.NextMeleeAsRanged=true;
            Play(p,c,source,true);
            Assert.That(c.MeleePool,Is.EqualTo(4));Assert.That(c.RangedPool,Is.Zero);Assert.That(c.AmbushTriggered,Is.False);
            Next(p,c,0);
            Assert.That(c.MeleePool,Is.Zero);Assert.That(c.RangedPool,Is.EqualTo(10));
            Assert.That(c.AmbushTriggered,Is.True);Assert.That(c.NextMeleeAsRanged,Is.False);
            Assert.Throws<InvalidOperationException>(()=>Next(p,c,0));Assert.That(c.RangedPool,Is.EqualTo(10));
        }

        [Test]
        public void OtherPlayerOrAnotherCardCannotHijackContinuation()
        {
            var(p,c,source)=Fixture("basic_card_015");Play(p,c,source,true,1);
            var other=new PlayerState();other.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>Next(other,c,2));
            var next=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(next);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,next,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==next.Id)));
            Assert.That(c.MovementPool,Is.Zero);Assert.That(c.PendingRepeatedEffect.Remaining,Is.EqualTo(1));
            Next(p,c,2);Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(1));
            Assert.That(other.Mana.Crystals,Is.Empty);Assert.That(other.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.EqualTo(1));
        }

        [Test]
        public void IllegalFirstEffectRejectsBeforeHostFeeOrPermanentRemoval()
        {
            var(p,c,source)=Fixture("basic_card_001");c.InBattle=true;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,true,1));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(c.PendingRepeatedEffect,Is.Null);
        }

        [Test]
        public void MovementWaitsForWholeCardThenConsumesRealTerrainCost()
        {
            var(p,c,source)=Fixture("basic_card_000");
            c.MovementMap=new MapState();var target=new AxialCoord(1,0);
            c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            Play(p,c,source,true);
            Assert.That(new MovementService().TryMoveUsingPool(p,target,c.MovementMap,c),Is.False);
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(c.MovementPool,Is.EqualTo(4));
            Next(p,c,0);
            Assert.That(new MovementService().TryMoveUsingPool(p,target,c.MovementMap,c),Is.True);
            Assert.That(p.Position,Is.EqualTo(target));Assert.That(c.MovementPool,Is.EqualTo(6));
        }
    }
}
