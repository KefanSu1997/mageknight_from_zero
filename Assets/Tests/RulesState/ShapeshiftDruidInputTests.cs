using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class ShapeshiftDruidInputTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Hand(PlayerState p, int id) { var c = new DeedCard("basic_card_" + id.ToString("000"), CardType.Action); p.Deck.Hand.Add(c); return c; }
        private static (PveJourneySession j, SkillCard s, DeedCard d) Start(bool own = true)
        {
            var p = new PlayerState(); var d = Hand(p,18); var map = new MapState();
            var enemies = new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Where(m => m.TokenColor == EnemyTokenColor.Green));
            OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map,enemies);
            var j = new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map,enemySupply:enemies); j.SelectTactic("day_1"); j.BeginTurn();
            var s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[6]; if(own)p.Skills.Add(s);else j.Context.SkillSupply.Offer.Add(s);
            j.BeginBattle(new[]{MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Single(m=>m.Id=="monster_032")});
            j.Battle.Advance(); return(j,s,d);
        }
        [TestCase(false)] [TestCase(true)] public void ActualHexOrTerrainSurvivesConversionAndFeedsBlockThenRealMovement(bool enhanced)
        {
            var(j,s,d)=Start();var p=j.Player;var c=j.Context;var target=new AxialCoord(1,-1);
            if(enhanced)p.Mana.AddToken(ManaColor.Blue);var oldHex=new AxialCoord(0,0);c.TargetHex=oldHex;c.TargetTerrain=TerrainType.Hills;
            j.Battle.PlayShapeshift(s,d,enhanced,0,FixedOutputKind.Block,hex:enhanced?null:target,terrain:enhanced?TerrainType.Forest:null);
            Assert.That(c.BlockPool,Is.EqualTo(enhanced?4:2));Assert.That(c.MovementPool,Is.Zero);Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(c.TargetHex,Is.EqualTo(oldHex));Assert.That(c.TargetTerrain,Is.EqualTo(TerrainType.Hills));
            if(!enhanced)j.Battle.Play(Hand(p,16),"block");
            j.Battle.Block(0);j.Battle.Advance();j.Battle.Advance();Assert.That(p.Wounds,Is.Zero);
            p.Mana.AddToken(ManaColor.Red);j.Battle.Play(Hand(p,21),"strong");Assert.That(j.Battle.Attack(0).Kills,Is.EqualTo(1));j.Battle.Advance();
            j.PlayTravelCard(Hand(p,0),"base");Assert.That(j.MovementCost(target),Is.EqualTo(2));Assert.Throws<InvalidOperationException>(()=>j.Move(target));
            // The discount survives, but the real battle already committed this turn's action: no postbattle move.
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(p.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(p.Deck.PlayedCards.Any(x=>ReferenceEquals(x,d)),Is.True);
        }
        [Test] public void StrongDruidConvertsFourToActualMeleeAndPreservesTerrainReduction()
        {
            var(j,s,d)=Start();var p=j.Player;while(j.Battle.Phase!=Phase.Melee)j.Battle.Advance();p.Mana.AddToken(ManaColor.Blue);
            j.Battle.PlayShapeshift(s,d,true,0,FixedOutputKind.Attack,terrain:TerrainType.Forest);
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.That(j.Context.TerrainMoveReduction[TerrainType.Forest],Is.EqualTo(1));
            Assert.That(j.Battle.Attack(0).Kills,Is.EqualTo(1));Assert.That(p.Mana.GetTotal(),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)] public void MissingRealInputRejectsBeforeFeeCardAndSkillUseAndRestoresOldInput(bool enhanced)
        {
            var(j,s,d)=Start();var p=j.Player;var c=j.Context;p.Mana.AddToken(ManaColor.Blue);c.TargetHex=new AxialCoord(0,0);c.TargetTerrain=TerrainType.Forest;
            Assert.Throws<InvalidOperationException>(()=>j.Battle.PlayShapeshift(s,d,enhanced,0,FixedOutputKind.Block,hex:enhanced?null:new AxialCoord(99,99)));
            Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,d)),Is.True);Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);
            Assert.That(c.BlockPool,Is.Zero);Assert.That(c.TargetHex,Is.EqualTo(new AxialCoord(0,0)));Assert.That(c.TargetTerrain,Is.EqualTo(TerrainType.Forest));
        }
        [Test] public void HeadbandSecondDruidProbeEnumeratesRealMapWithoutMutatingInputsOrResources()
        {
            var(j,s,d)=Start(false);var p=j.Player;var c=j.Context;var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);var second=Hand(p,18);
            j.UseHeadbandShapeshift(h,s,d,false,0,FixedOutputKind.Block,hex:new AxialCoord(1,-1));
            c.TargetHex=null;c.TargetTerrain=null;var pending=c.PendingHeadbandBorrow;
            Assert.That(pending.HasLegalInput(),Is.True);Assert.That(c.TargetHex,Is.Null);Assert.That(c.TargetTerrain,Is.Null);Assert.That(c.BlockPool,Is.EqualTo(2));Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>j.UseHeadbandShapeshift(h,s,second,false,0,FixedOutputKind.Block,hex:new AxialCoord(99,99)));
            Assert.That(pending.Remaining,Is.EqualTo(1));Assert.That(c.BlockPool,Is.EqualTo(2));
            j.UseHeadbandShapeshift(h,s,second,false,0,FixedOutputKind.Block,hex:new AxialCoord(1,0));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.BlockPool,Is.EqualTo(4));j.Battle.Block(0);j.Battle.Advance();j.Battle.Advance();Assert.That(p.Wounds,Is.Zero);
            Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Skills,Is.Empty);
        }
    }
}
