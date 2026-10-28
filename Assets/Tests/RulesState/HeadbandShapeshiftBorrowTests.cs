using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeadbandShapeshiftBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private DeedCard Card(PlayerState p,int id){var c=new DeedCard("basic_card_"+id.ToString("000"),CardType.Action);p.Deck.Hand.Add(c);return c;}
        private (PveBattleSession b,DeedCard h,SkillCard s) Setup()
        {
            var p=new PlayerState();var b=new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"),CardJsonLoader.LoadBasicActions());
            b.Advance();var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[6];b.Context.SkillSupply=new SkillSupply();b.Context.SkillSupply.Offer.Add(s);return(b,h,s);
        }
        [Test] public void StrongOriginalFeeThenSecondFailurePreservesFirstAndActualBlockConsumer()
        {
            var(b,h,s)=Setup();var p=b.Player;var c=b.Context;var first=Card(p,0);var second=Card(p,0);p.Mana.AddCrystal(ManaColor.Green);
            HeadbandBorrowResolution.UseShapeshift(p,c,h,s,first,true,0,FixedOutputKind.Block);
            var pending=c.PendingHeadbandBorrow;Assert.That(c.BlockPool,Is.EqualTo(4));Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseShapeshift(p,c,h,s,second,true,0,FixedOutputKind.Block));
            Assert.That(pending.Completed,Is.EqualTo(1));Assert.That(pending.Remaining,Is.EqualTo(1));Assert.That(p.Deck.Hand,Does.Contain(second));
            Assert.That(pending.HasLegalInput(),Is.True);Assert.That(c.BlockPool,Is.EqualTo(4));Assert.That(c.MovementPool,Is.Zero);
            HeadbandBorrowResolution.UseShapeshift(p,c,h,s,second,false,0,FixedOutputKind.Block);
            Assert.That(c.BlockPool,Is.EqualTo(6));Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Skills,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);
            Assert.That(p.Deck.PlayedCards.Count(),Is.EqualTo(3));b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
        }
        [Test] public void ActualImprovisationMaterialAndHeadbandProtectionAndReadOnlyAvailability()
        {
            var(b,h,s)=Setup();var p=b.Player;var c=b.Context;var improv=Card(p,23);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseShapeshift(p,c,h,s,improv,false,0,FixedOutputKind.Block,h));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand,Does.Contain(h));
            var material=Card(p,8);var second=Card(p,23);
            HeadbandBorrowResolution.UseShapeshift(p,c,h,s,improv,false,0,FixedOutputKind.Block,material);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));Assert.That(c.BlockPool,Is.EqualTo(3));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.False);Assert.That(p.Deck.Hand.Single(),Is.SameAs(second));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseShapeshift(p,c,h,s,second,false,0,FixedOutputKind.Block,material));
            Assert.That(c.BlockPool,Is.EqualTo(3));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));
            HeadbandBorrowResolution.FinishIfImpossible(p,c);Assert.That(c.PendingHeadbandBorrow,Is.Null);
            // No temporary conversion/material survives: ordinary March produces movement.
            var march=Card(p,0);CardPlayActions.Play(p,c,march,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==march.Id));
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(c.BlockPool,Is.EqualTo(3));
        }
        [Test] public void ActualConcentrationPermissionSurvivesProbeAndAppliesOnlyToFirstBorrow()
        {
            var(b,h,s)=Setup();var p=b.Player;var c=b.Context;
            var focus=Card(p,6);p.Mana.AddCrystal(ManaColor.Green);
            CardPlayActions.Play(p,c,focus,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==focus.Id),true);
            Assert.That(c.NextCardEnhanced,Is.True);
            var first=Card(p,0);var second=Card(p,0);
            HeadbandBorrowResolution.UseShapeshift(p,c,h,s,first,false,0,FixedOutputKind.Block);
            Assert.That(c.NextCardEnhanced,Is.False);Assert.That(p.Mana.GetTotal(),Is.Zero);
            int afterFirst=c.BlockPool;
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);Assert.That(c.BlockPool,Is.EqualTo(afterFirst));
            HeadbandBorrowResolution.UseShapeshift(p,c,h,s,second,false,0,FixedOutputKind.Block);
            Assert.That(c.BlockPool,Is.EqualTo(afterFirst+2));Assert.That(afterFirst,Is.EqualTo(7));
            b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
        }
    }
}
