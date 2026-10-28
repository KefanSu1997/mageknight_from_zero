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
    public sealed class HeadbandPaymentSessionTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static DeedCard Hand(PlayerState p,string id,CardType type=CardType.Action){var a=new DeedCard(id,type);p.Deck.Hand.Add(a);return a;}
        private static SkillCard Offer(ActionContext c,bool polarization=false){var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[polarization?7:6];c.SkillSupply.Offer.Add(s);return s;}
        private static PveJourneySession Journey(PlayerState p)
        {
            var map=new MapState();map.Placed[p.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(1,0)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Forest});
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),map:map);j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        [Test] public void TravelAndOutsideSecondPaymentPreserveTargetsAndConsumeRealTerrainCost()
        {
            var p=new PlayerState();var h=Hand(p,"items_022",CardType.Artifact);var druid=Hand(p,"basic_card_018");var move=Hand(p,"basic_card_000");
            var j=Journey(p);var c=j.Context;var s=Offer(c,true);p.Mana.AddToken(ManaColor.White);p.Mana.AddToken(ManaColor.Red);
            // Original Druidic Paths costs blue, so use actual white-to-blue polarization, then red-to-green for March.
            Assert.Throws<InvalidOperationException>(()=>j.PlayTravelWithBorrowedPayment(h,new(s,ManaColor.Blue,SourceColor:ManaColor.White),druid,"strong"));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.TargetTerrain,Is.Null);
            j.PlayTravelWithBorrowedPayment(h,new(s,ManaColor.Blue,SourceColor:ManaColor.White),druid,"strong",terrain:TerrainType.Forest);
            Assert.That(c.TargetTerrain,Is.Null);Assert.That(c.TargetHex,Is.Null);Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));int first=c.MovementPool;
            j.PlayOutsideWithBorrowedPayment(h,new(s,ManaColor.Green,SourceColor:ManaColor.Red),move,"strong");Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.MovementPool,Is.EqualTo(first+4));Assert.That(j.MovementCost(new AxialCoord(1,0)),Is.EqualTo(2));
            j.Move(new AxialCoord(1,0));Assert.That(p.Position,Is.EqualTo(new AxialCoord(1,0)));Assert.That(c.MovementPool,Is.EqualTo(first+2));
            Assert.That(p.Deck.PlayedCards.Count(x=>ReferenceEquals(x,h)),Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void PendingPaymentBlocksOrdinaryPlayAndRejectsDifferentPhysicalHeadband()
        {
            var p=new PlayerState();var h=Hand(p,"items_022",CardType.Artifact);var move=Hand(p,"basic_card_000");var influence=Hand(p,"basic_card_008");var a=Hand(p,"basic_card_014");var b=Hand(p,"basic_card_021");
            var j=Journey(p);var c=j.Context;var s=Offer(c);j.PlayOutsideWithBorrowedPayment(h,new(s,ManaColor.Green,a),move,"strong");
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideBattle(influence,"base"));
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithBorrowedPayment(new DeedCard("items_022",CardType.Artifact),new(s,ManaColor.White,b),influence,"strong"));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.InfluencePool,Is.Zero);
            j.PlayOutsideWithBorrowedPayment(h,new(s,ManaColor.White,b),influence,"strong");Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }
        [Test] public void CombatTypedEntryRejectsWrongPhaseAndInvalidAttackThenKeepsRealTargetOnPayment()
        {
            var p=new PlayerState();var h=Hand(p,"items_022",CardType.Artifact);var card=Hand(p,"advanced_card_043");var influence=Hand(p,"basic_card_008");var a=Hand(p,"basic_card_014");var b=Hand(p,"basic_card_021");
            var enemies=new[]{CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032"),CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032")};
            var battle=new PveBattleSession(p,enemies,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()));var c=battle.Context;c.SkillSupply=new SkillSupply();var s=Offer(c);
            Assert.Throws<InvalidOperationException>(()=>battle.PlayWithBorrowedPayment(h,new(s,ManaColor.White,a),card,"strong_enemy_white",target:1,attackIndex:4));
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.SelectedEnemy,Is.Null);
            battle.PlayWithBorrowedPayment(h,new(s,ManaColor.White,a),card,"strong_enemy_white",target:1);
            Assert.That(c.ColdStareSkips(enemies[1]),Is.True);Assert.That(c.ColdStareSkips(enemies[0]),Is.False);Assert.That(c.SelectedEnemy,Is.Null);Assert.That(c.SelectedActionManaColor,Is.Null);
            battle.PlayWithBorrowedPayment(h,new(s,ManaColor.White,b),influence,"strong");Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.InfluencePool,Is.EqualTo(4));
        }
        [Test] public void CombatCannotBorrowPaymentForOutsideOnlyMovement()
        {
            var p=new PlayerState();var h=Hand(p,"items_022",CardType.Artifact);var card=Hand(p,"basic_card_000");var material=Hand(p,"basic_card_021");
            var battle=new PveBattleSession(p,new[]{CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032")},CardJsonLoader.LoadBasicActions());var c=battle.Context;c.SkillSupply=new SkillSupply();var s=Offer(c);
            Assert.Throws<InvalidOperationException>(()=>battle.PlayWithBorrowedPayment(h,new(s,ManaColor.Green,material),card,"strong"));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.MovementPool,Is.Zero);Assert.That(p.Deck.DiscardPile,Is.Empty);
        }
        [Test] public void OutsideTypedEntryRejectsActiveBattleBeforeBorrowing()
        {
            var p=new PlayerState();var h=Hand(p,"items_022",CardType.Artifact);var card=Hand(p,"basic_card_000");var material=Hand(p,"basic_card_021");var j=Journey(p);var c=j.Context;var s=Offer(c);
            j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032")});
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithBorrowedPayment(h,new(s,ManaColor.Green,material),card,"strong"));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.MovementPool,Is.Zero);
        }
    }
}
