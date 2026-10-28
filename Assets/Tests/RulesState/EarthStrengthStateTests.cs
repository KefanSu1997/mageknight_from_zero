using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class EarthStrengthStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Play(PlayerState p,ActionContext c,bool strong,int option)
        {
            var card=new DeedCard("basic_card_004",CardType.Action);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),strong,option);
            return card;
        }
        [TestCase(false)] [TestCase(true)]
        public void HealBranchWaitsForTargetAndDoesNotGrantMovementOrBlock(bool strong)
        {
            var p=new PlayerState{Wounds=3};var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            var card=Play(p,c,strong,0);
            Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(c.Healing.Total,Is.EqualTo(strong?2:1));
            Assert.That(c.MovementPool,Is.Zero);Assert.That(c.BlockPool,Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.EqualTo(strong?0:1));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }

        [TestCase(false,"army_2_006",1)] [TestCase(true,"army_2_001",2)]
        public void SelectedOriginalUnitUsesPrintedHealingCost(bool strong,string id,int level)
        {
            var p=new PlayerState{Wounds=3};var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            var u=new UnitState(CardJsonLoader.LoadUnit(id));u.AddWounds(1);u.Exhaust();p.Units.Add(u);
            Play(p,c,strong,0);Assert.That(u.Card.Level,Is.EqualTo(level));CardHealing.Spend(p,c,unit:u);
            Assert.That(u.Wounds,Is.Zero);Assert.That(u.IsReady,Is.False);Assert.That(p.Wounds,Is.EqualTo(3));
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Fame,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void MovementChoiceMovesActualHexWithoutGrantingHealing(bool strong)
        {
            var p=new PlayerState{Wounds=3};var c=new ActionContext{MovementMap=new MapState()};p.Mana.AddToken(ManaColor.Green);
            foreach(int q in new[]{0,1,2})c.MovementMap.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,new[]{TerrainType.Plains});
            Play(p,c,strong,2);
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(1,0),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.EqualTo(strong?2:0));Assert.That(p.Position.Q,Is.EqualTo(1));
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(c.BlockPool,Is.Zero);Assert.That(p.Wounds,Is.EqualTo(3));
        }

        [TestCase(false,DayPart.Day,2,Element.Physical)]
        [TestCase(true,DayPart.Day,3,Element.Ice)]
        [TestCase(true,DayPart.Night,5,Element.Fire)]
        public void BlockChoiceActuallyBlocksAndIgnoresTerrainDiscount(bool strong,DayPart part,int expected,Element attack)
        {
            var p=new PlayerState();var c=new ActionContext{InBattle=true,CurrentTerrain=TerrainType.Forest,DayPart=part};
            c.TerrainCostOverride[TerrainType.Forest]=1;p.Mana.AddToken(ManaColor.Green);Play(p,c,strong,1);
            var enemy=new MK.Logic.Data.Monster("block_fixture",4,expected,attack,2,Array.Empty<Ability>());
            var result=CardCombatActions.Block(p,c,enemy);
            Assert.That(result.PrintedPower,Is.EqualTo(expected));Assert.That(result.EffectivePower,Is.EqualTo(expected));
            Assert.That(result.Wounds,Is.Zero);Assert.That(c.BlockPool,Is.Zero);Assert.That(c.Healing.Total,Is.Zero);Assert.That(c.MovementPool,Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void MissingGreenRejectsAllEnhancedChoicesWithoutSideEffects(int option)
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true,option));
            Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(c.Healing.Total+c.MovementPool+c.BlockPool,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void BattleHealRejectionPreservesGreenAndCard(bool strong)
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext{InBattle=true};p.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong,0));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualCardCannotBeReusedAndUnspentHealingExpires(bool strong)
        {
            var p=new PlayerState();var c=new ActionContext{SkipDraw=true};p.Mana.AddToken(ManaColor.Green);
            var card=Play(p,c,strong,0);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),strong,2));
            Assert.That(c.Healing.Total,Is.EqualTo(strong?2:1));Assert.That(c.MovementPool,Is.Zero);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }
    }
}
