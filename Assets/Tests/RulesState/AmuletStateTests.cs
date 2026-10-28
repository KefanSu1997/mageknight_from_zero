using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class AmuletStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Play(PlayerState p, ActionContext c, string id, bool burn = false, int option = 0)
        {
            var card = new DeedCard(id, CardType.Artifact); p.Deck.Hand.Add(card);
            CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == id), burn, option);
            return card;
        }
        private static void Spell(PlayerState p, ActionContext c, string id, bool strong = false)
        {
            var s = CardJsonLoader.LoadSpells().Single(x => x.Id == id);
            var card = new DeedCard(id, CardType.Spell); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost),strong);
        }
        private static void End(PlayerState p, ActionContext c) => new PlayerTurnEngine().EndTurn(p,c.DayPart,new ManaSource(1,()=>c.DayPart),c);
        private static MapState Map(TerrainType terrain)
        {
            var map = new MapState();
            for(int q=0;q<=4;q++) map.Placed[new AxialCoord(q,0)] = new MapTile(TileSet.Countryside,q,new[]{terrain});
            return map;
        }
        [TestCase(false)] [TestCase(true)]
        public void BothAmuletsDoNotChangeActualTimeAndExpireWithoutSkipDraw(bool skip)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Night,SkipDraw=skip};
            Play(p,c,"items_014"); Play(p,c,"items_015",false,(int)ManaColor.Black);
            Assert.That(c.DayPart,Is.EqualTo(DayPart.Night)); Assert.That(c.AllowGoldAtNight,Is.True);
            Assert.That(c.AllowBlackAtDay,Is.False); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            End(p,c);p.Mana.AddToken(ManaColor.Gold);
            Assert.Throws<InvalidOperationException>(()=>ManaTokenActions.ConvertGold(p,c,ManaColor.Red));
            Assert.That(p.Mana.Tokens[ManaColor.Gold],Is.EqualTo(1));
        }
        [Test]
        public void DarknessMayChooseTheSameColorThreeTimes()
        {
            var p=new PlayerState();var c=new ActionContext();
            Play(p,c,"items_015",true,((int)ManaColor.Green)*0x010101);
            Assert.That(p.Mana.Tokens.Single().Key,Is.EqualTo(ManaColor.Green));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(3));
        }
        [TestCase(false,DayPart.Day)] [TestCase(true,DayPart.Day)]
        [TestCase(false,DayPart.Night)] [TestCase(true,DayPart.Night)]
        public void SunGrantsPrintedGoldAndPreservesActualCardLifecycle(bool burn, DayPart part)
        {
            var p=new PlayerState(); var c=new ActionContext{DayPart=part,SkipDraw=true};
            var card=Play(p,c,"items_014",burn);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.EqualTo(burn?3:1));
            Assert.That(p.Deck.Hand,Is.Empty);
            Assert.That(burn?p.Deck.RemovedCards.Single():p.Deck.PlayedCards.Single(),Is.SameAs(card));
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,card,CardJsonLoader.LoadItems().Single(x=>x.Id==card.Id),burn));
            End(p,c);
            Assert.That(p.Mana.Tokens,Is.Empty);
            Assert.That(burn?p.Deck.RemovedCards.Single():p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }
        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)]
        [TestCase(ManaColor.White)] [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)]
        public void DarknessCanGenerateEachOfSixPrintedColors(ManaColor color)
        {
            var p=new PlayerState(); var c=new ActionContext{DayPart=DayPart.Day};
            Play(p,c,"items_015",false,(int)color);
            Assert.That(p.Mana.Tokens.Single().Key,Is.EqualTo(color));
            Assert.That(p.Mana.Tokens[color],Is.EqualTo(1));
            Assert.That(c.AllowBlackAtDay,Is.True);
            Assert.That(c.DayPart,Is.EqualTo(DayPart.Day));
        }
        [Test]
        public void DarknessBurnGrantsThreeIndependentColorsAndReallyBurns()
        {
            var p=new PlayerState(); var c=new ActionContext{DayPart=DayPart.Night,SkipDraw=true};
            var card=Play(p,c,"items_015",true,(int)ManaColor.Blue|((int)ManaColor.Gold<<8)|((int)ManaColor.Black<<16));
            Assert.That(p.Mana.Tokens,Is.EquivalentTo(new Dictionary<ManaColor,int>{{ManaColor.Blue,1},{ManaColor.Gold,1},{ManaColor.Black,1}}));
            Assert.That(c.AllowBlackAtDay,Is.False); Assert.That(c.TerrainCostOverride,Is.Empty);
            End(p,c); Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(card)); Assert.That(p.Deck.DiscardPile,Is.Empty);
        }
        [TestCase(false,6)] [TestCase(false,256)] [TestCase(true,1536)] [TestCase(true,-1)] [TestCase(true,16777216)]
        public void InvalidColorSelectionIsAtomic(bool burn,int option)
        {
            var p=new PlayerState(); var c=new ActionContext();var card=new DeedCard("items_015",CardType.Artifact);p.Deck.Hand.Add(card);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,card,CardJsonLoader.LoadItems().Single(x=>x.Id==card.Id),burn,option));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));Assert.That(p.Mana.Tokens,Is.Empty);
            Assert.That(c.AllowBlackAtDay,Is.False);Assert.That(c.TerrainCostOverride,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
        }
        [TestCase("items_014",DayPart.Night,TerrainType.Forest)]
        [TestCase("items_015",DayPart.Day,TerrainType.Desert)]
        public void PrintedTerrainCostActuallyConsumesThreeMovementAndExpires(string id,DayPart part,TerrainType terrain)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=part,MovementMap=Map(terrain),MovementPool=6,SkipDraw=true};Play(p,c,id);
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(1,0),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.EqualTo(3)); Assert.That(c.DayPart,Is.EqualTo(part));
            Assert.That(MovementService.GetCost(new AxialCoord(2,0),c.MovementMap,part,c),Is.EqualTo(3));
            End(p,c);
            Assert.That(MovementService.GetCost(new AxialCoord(2,0),c.MovementMap,part,c),Is.EqualTo(5));
            Assert.That(c.AllowBlackAtDay||c.AllowGoldAtNight||c.RevealSitesAsDay,Is.False);
        }
        [Test]
        public void SunGoldPaysActualSpellAtNightAndCannotSubstituteBlack()
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Night};Play(p,c,"items_014",true);
            Assert.Throws<InvalidOperationException>(()=>ManaTokenActions.ConvertGold(p,c,ManaColor.Black));
            Assert.That(p.Mana.Tokens[ManaColor.Gold],Is.EqualTo(3));
            ManaTokenActions.ConvertGold(p,c,ManaColor.Red); Spell(p,c,"magic_009");
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero); Assert.That(p.Mana.Tokens[ManaColor.Gold],Is.EqualTo(2));
            ManaTokenActions.ConvertGold(p,c,ManaColor.Red);
            Assert.Throws<InvalidOperationException>(()=>Spell(p,c,"magic_009",true));
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1),"Missing black is an atomic cost failure.");
        }
        [TestCase(DayPart.Day)] [TestCase(DayPart.Night)]
        public void GoldUseRequiresTimePermissionAndRealToken(DayPart part)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=part};p.Mana.AddToken(ManaColor.Gold);
            if(part==DayPart.Night)
            {
                Assert.Throws<InvalidOperationException>(()=>ManaTokenActions.ConvertGold(p,c,ManaColor.Green));
                Assert.That(p.Mana.Tokens[ ManaColor.Gold ],Is.EqualTo(1));
            }
            else
            {
                ManaTokenActions.ConvertGold(p,c,ManaColor.Green);
                Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.Zero);
                Assert.That(p.Mana.Tokens[ ManaColor.Green ],Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(()=>ManaTokenActions.ConvertGold(p,c,ManaColor.Green));
            }
        }
        [Test]
        public void DarknessPaysActualEnhancedSpellInDayThenPermissionExpires()
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Day,SkipDraw=true};
            Play(p,c,"items_015",true,(int)ManaColor.Red|((int)ManaColor.Black<<8)|((int)ManaColor.Black<<16));
            Spell(p,c,"magic_009",true);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            Assert.That(c.DayPart,Is.EqualTo(DayPart.Day)); End(p,c);
            p.Mana.AddToken(ManaColor.Red);p.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(()=>Spell(p,c,"magic_009",true));
            Assert.That(p.Mana.Tokens.Values.Sum(),Is.EqualTo(2));
        }
        [TestCase("items_014",DayPart.Night,TerrainType.Forest)]
        [TestCase("items_015",DayPart.Day,TerrainType.Desert)]
        public void ExistingCheaperTerrainIsPreservedForTwoRealMoves(string id,DayPart day,TerrainType terrain)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=day,MovementMap=Map(terrain),MovementPool=4};
            c.TerrainCostOverride[terrain]=2;Play(p,c,id);
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(1,0),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.EqualTo(2));
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(2,0),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.Zero);
        }

        [Test]
        public void GoldChosenAsBluePaysOriginalStaminaThenActualMove()
        {
            var p=new PlayerState();var c=new ActionContext{MovementMap=Map(TerrainType.Plains)};
            Play(p,c,"items_014");ManaTokenActions.ConvertGold(p,c,ManaColor.Blue);
            var original=CardJsonLoader.LoadBasicActions().Single(a=>a.Id=="basic_card_014");
            var actual=new DeedCard(original.Id,CardType.Action);p.Deck.Hand.Add(actual);
            CardPlayActions.Play(p,c,actual,original,true);
            Assert.That(p.Mana.Tokens.Values.Sum(),Is.Zero);
            Assert.That(p.Deck.PlayedCards.Last(),Is.SameAs(actual));
            Assert.That(c.MovementPool,Is.EqualTo(4));
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(1,0),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.EqualTo(2));
        }

        [Test]
        public void CityRevealsAtNightWithoutAmuletButKeepAndTowerStayHidden()
        {
            var p=new PlayerState();var map=Map(TerrainType.Plains);
            var city=new PlaceState(PlaceType.City);var keep=new PlaceState(PlaceType.Keep);var tower=new PlaceState(PlaceType.MageTower);
            map.Places[new AxialCoord(1,0)]=city;
            map.Places[new AxialCoord(0,0)]=keep;
            map.Places[new AxialCoord(2,0)]=tower;
            SiteVisibility.RevealVisible(p,map,DayPart.Night,new ActionContext{DayPart=DayPart.Night});
            Assert.That(map.RevealedSites,Is.EquivalentTo(new[]{city}));
        }

        [Test]
        public void SunRevealsActualNearbySitesAndAllExploredRuinsThenMovingRevealsNextSite()
        {
            var p=new PlayerState();var map=Map(TerrainType.Plains);var c=new ActionContext{DayPart=DayPart.Night,MovementMap=map,MovementPool=2,SkipDraw=true};
            var guard=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_025");
            var keep=new PlaceState(PlaceType.Keep){Enemy=guard};var tower=new PlaceState(PlaceType.MageTower);var ruin=new PlaceState(PlaceType.AncientRuin){RuinToken=RuinTokenType.Altar};var dungeon=new PlaceState(PlaceType.Dungeon);var hidden=new PlaceState(PlaceType.AncientRuin);
            map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,5,new[]{TerrainType.Plains});
            map.Places[new AxialCoord(0,1)]=keep;map.Places[new AxialCoord(2,0)]=tower;map.Places[new AxialCoord(4,0)]=ruin;map.Places[new AxialCoord(0,0)]=dungeon;map.Places[new AxialCoord(5,0)]=hidden;
            Play(p,c,"items_014");
            Assert.That(keep.Enemy,Is.SameAs(guard)); Assert.That(keep.Conquered,Is.False);
            Assert.That(map.RevealedSites,Is.EquivalentTo(new[]{keep,ruin})); Assert.That(ruin.RuinToken,Is.EqualTo(RuinTokenType.Altar));
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(1,0),map,c),Is.True);
            Assert.That(map.RevealedSites,Is.EquivalentTo(new[]{keep,tower,ruin}));
            End(p,c); Assert.That(map.RevealedSites,Is.EquivalentTo(new[]{keep,tower,ruin}));
            var later=new PlaceState(PlaceType.Keep);map.Places[new AxialCoord(2,0)]=later;c.MovementPool=2;
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(2,0),map,c),Is.True);
            Assert.That(map.RevealedSites.Contains(later),Is.False,"A replacement site entity is not the already revealed tower, and expired sunlight cannot expose it.");
        }
    }
}
