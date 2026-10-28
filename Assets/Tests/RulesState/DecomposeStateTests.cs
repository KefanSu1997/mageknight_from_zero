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
    public sealed class DecomposeStateTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static readonly ManaColor[] Colors={ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White};
        private static (PlayerState p,ActionContext c,DeedCard source,DeedCard material) Fixture(string id="basic_card_021")
        {
            var p=new PlayerState();var source=new DeedCard("advanced_card_020",CardType.Action);var m=new DeedCard(id,CardType.Action);
            p.Deck.Hand.AddRange(new[]{source,m});p.Mana.AddToken(ManaColor.Red);
            return(p,new ActionContext{CardToRemove=m,SkipDraw=true},source,m);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard source,bool strong,int option=0)
            =>CardPlayActions.Play(p,c,source,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==source.Id),strong,option);

        [TestCase("basic_card_021",ManaColor.Red,false)] [TestCase("basic_card_021",ManaColor.Red,true)]
        [TestCase("basic_card_016",ManaColor.Blue,false)] [TestCase("basic_card_016",ManaColor.Blue,true)]
        [TestCase("advanced_card_003",ManaColor.Green,false)] [TestCase("advanced_card_003",ManaColor.Green,true)]
        [TestCase("advanced_card_002",ManaColor.White,false)] [TestCase("advanced_card_002",ManaColor.White,true)]
        public void OriginalMaterialColorDeterminesCrystalsAndPermanentRemovalAcrossRound(string id,ManaColor color,bool strong)
        {
            var(p,c,source,m)=Fixture(id);Play(p,c,source,strong,99);
            foreach(var actual in Colors)Assert.That(p.Mana.Crystals.GetValueOrDefault(actual),Is.EqualTo(strong?(actual==color?0:1):(actual==color?2:0)));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(c.CardToRemove,Is.Null);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(strong?0:1));
            var game=new GameEngine(new[]{p},5);new PlayerTurnEngine().EndTurn(p,DayPart.Day,game.Globals.Mana,c);game.EndRound();
            Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(source));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
            Assert.That(p.Mana.Tokens,Is.Empty);
            foreach(var actual in Colors)Assert.That(p.Mana.Crystals.GetValueOrDefault(actual),Is.EqualTo(strong?(actual==color?0:1):(actual==color?2:0)));
        }

        [TestCase(false)] [TestCase(true)]
        public void SameIdBasicCardsRemoveOnlySelectedInstanceAndRejectRemovedMaterial(bool strong)
        {
            var(p,c,source,m)=Fixture();var unselected=new DeedCard(m.Id,m.Type);p.Deck.Hand.Insert(1,unselected);
            Play(p,c,source,strong);Assert.That(p.Deck.Hand.Single(),Is.SameAs(unselected));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
            var next=new DeedCard(source.Id,source.Type);p.Deck.Hand.Add(next);p.Mana.AddToken(ManaColor.Red);c.CardToRemove=m;
            int mana=p.Mana.GetTotal();Assert.Throws<InvalidOperationException>(()=>Play(p,c,next,strong));
            Assert.That(p.Mana.GetTotal(),Is.EqualTo(mana));Assert.That(p.Deck.Hand.Contains(unselected),Is.True);Assert.That(p.Deck.Hand.Contains(next),Is.True);
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void InvalidMaterialAndMissingRedCannotRemoveOrReward(int failure)
        {
            var(p,c,source,m)=Fixture();
            if(failure==0)c.CardToRemove=null;
            if(failure==1)c.CardToRemove=new DeedCard(m.Id,m.Type);
            if(failure>=2 && failure<=5)
            {
                var bad=new DeedCard(failure==2?"wound_0":failure==5?"items_000":"magic_009",
                    failure==2?CardType.Wound:failure==3?CardType.Spell:failure==5?CardType.Artifact:CardType.Action);
                p.Deck.Hand.Add(bad);c.CardToRemove=bad;
            }
            if(failure==6)c.CardToRemove=source;
            if(failure==7)p.Mana.Tokens.Clear();
            var hand=p.Deck.Hand.ToArray();int mana=p.Mana.GetTotal();var selection=c.CardToRemove;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,true));
            Assert.That(p.Deck.Hand,Is.EqualTo(hand));Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Mana.GetTotal(),Is.EqualTo(mana));Assert.That(c.CardToRemove,Is.SameAs(selection));
        }

        [TestCase(2)] [TestCase(3)]
        public void BasicCrystalOverflowBecomesTemporaryManaAndExpires(int stored)
        {
            var(p,c,source,m)=Fixture();p.Mana.Tokens.Clear();p.Mana.AddCrystal(ManaColor.Red,stored);Play(p,c,source,false);
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(3));Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(stored-1));
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(p.Mana.Tokens,Is.Empty);Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(3));
        }
        [Test]
        public void StrongPaysRedCrystalFirstAndOverflowsOnlyRewardedColors()
        {
            var(p,c,source,m)=Fixture("basic_card_016");p.Mana.Tokens.Clear();
            foreach(var color in Colors)p.Mana.AddCrystal(color,3);
            Play(p,c,source,true);
            foreach(var color in Colors)Assert.That(p.Mana.Crystals[color],Is.EqualTo(3));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue),Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.White],Is.EqualTo(1));
            Assert.That(c.SpentCrystals[ManaColor.Red],Is.EqualTo(1));
        }

        [TestCase("basic_card_021","basic_card_021",ManaColor.Red,false)]
        [TestCase("basic_card_016","basic_card_016",ManaColor.Blue,false)]
        [TestCase("advanced_card_003","basic_card_000",ManaColor.Green,false)]
        [TestCase("advanced_card_002","basic_card_008",ManaColor.White,false)]
        [TestCase("basic_card_021","basic_card_016",ManaColor.Blue,true)]
        public void AwardedCrystalPaysActualNextOriginalCard(string material,string nextId,ManaColor color,bool strong)
        {
            var(p,c,source,m)=Fixture(material);p.Mana.Tokens.Clear();if(strong)p.Mana.AddToken(ManaColor.Red);
            var next=new DeedCard(nextId,CardType.Action);p.Deck.Hand.Add(next);Play(p,c,source,strong);
            CardPlayActions.Play(p,c,next,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==next.Id),true);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(color),Is.EqualTo(strong?0:1));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2));Assert.That(p.Deck.Hand,Is.Empty);
            if(nextId=="basic_card_021")Assert.That(c.MeleePool,Is.EqualTo(4));
            if(nextId=="basic_card_016")Assert.That(c.BlockPool,Is.EqualTo(5));
            if(nextId=="basic_card_000")Assert.That(c.MovementPool,Is.EqualTo(4));
            if(nextId=="basic_card_008")Assert.That(c.InfluencePool,Is.EqualTo(4));
        }

        [TestCase(false)] [TestCase(true)]
        public void BloodlineBorrowUsesRealDecomposeMaterialAndValidatesBeforeHostPayment(bool valid)
        {
            var p=new PlayerState();var source=new DeedCard("advanced_card_030",CardType.Action);var m=new DeedCard("basic_card_021",CardType.Action);
            p.Deck.Hand.AddRange(new[]{source,m});p.Mana.AddToken(ManaColor.Red);
            var c=new ActionContext{AdvActionSupply=new AdvActionSupply(),CardToRemove=valid?m:new DeedCard(m.Id,m.Type)};
            var offer=new AdvActionCard("advanced_card_020");c.AdvActionSupply.Offer.Add(offer);
            if(!valid)
            {
                Assert.Throws<InvalidOperationException>(()=>Play(p,c,source,true));
                Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.RemovedCards,Is.Empty);
                return;
            }
            Play(p,c,source,true);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red),Is.Zero);
            foreach(var color in new[]{ManaColor.Blue,ManaColor.Green,ManaColor.White})Assert.That(p.Mana.Crystals[color],Is.EqualTo(1));
            Assert.That(c.AdvActionSupply.Offer.Single(),Is.SameAs(offer));
        }
    }
}
