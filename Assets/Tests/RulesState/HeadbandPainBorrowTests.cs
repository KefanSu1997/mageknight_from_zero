using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandPainBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(int wounds)
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply()};var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[5];c.SkillSupply.Offer.Add(s);
            for(int i=0;i<wounds;i++)p.Deck.GainWound();return(p,c,h,s);
        }
        [Test] public void TwoExactWoundsProduceTwoEachAndOnlyDiscardAtFormalEnd()
        {
            var(p,c,h,s)=Setup(2);var wounds=p.Deck.Hand.Where(x=>x.Type==CardType.Wound).ToArray();
            HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[1],SidewaysType.Move);
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.SidewaysWoundValue,Is.Zero);
            Assert.That(p.Deck.PlayedCards.Any(x=>ReferenceEquals(x,wounds[1])),Is.True);Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,wounds[0])),Is.True);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[1],SidewaysType.Move));
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.FinishIfImpossible(p,c));
            HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[0],SidewaysType.Influence);
            Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(3));Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Skills,Is.Empty);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,h)),Is.True);
            foreach(var wound in wounds)Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,wound)),Is.True);
        }
        [Test] public void ExistingOriginalPainPermissionSurvivesBorrowAndStillConsumesOneWound()
        {
            var(p,c,h,s)=Setup(3);var wounds=p.Deck.Hand.Where(x=>x.Type==CardType.Wound).ToArray();new PainPowerEffect().Execute(p,c);
            HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[0],SidewaysType.Move);Assert.That(c.SidewaysWoundValue,Is.EqualTo(2));
            HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[1],SidewaysType.Move);Assert.That(c.SidewaysWoundValue,Is.EqualTo(2));
            Assert.That(new ActionSystem(false).PlaySideways(wounds[2],p,c,SidewaysType.Influence),Is.True);
            Assert.That(c.SidewaysWoundValue,Is.Zero);Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(c.InfluencePool,Is.EqualTo(2));
        }
        [Test] public void NoSecondWoundCanFinishWithoutMintingAnotherPermission()
        {
            var(p,c,h,s)=Setup(1);var wound=p.Deck.Hand.Single(x=>x.Type==CardType.Wound);p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));
            HeadbandBorrowResolution.UsePain(p,c,h,s,wound,SidewaysType.Move);Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.False);
            Assert.That(c.SidewaysWoundValue,Is.Zero);HeadbandBorrowResolution.FinishIfImpossible(p,c);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.MovementPool,Is.EqualTo(2));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void FakeOrdinaryAndNullMaterialsRejectWithoutChangingPermission(int invalid)
        {
            var(p,c,h,s)=Setup(1);new PainPowerEffect().Execute(p,c);var ordinary=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(ordinary);
            var chosen=invalid==0?new DeedCard("w",CardType.Wound):invalid==1?ordinary:null;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePain(p,c,h,s,chosen,SidewaysType.Move));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.SidewaysWoundValue,Is.EqualTo(2));Assert.That(c.MovementPool,Is.Zero);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [TestCase(Phase.Ranged,SidewaysType.Attack)] [TestCase(Phase.Block,SidewaysType.Attack)] [TestCase(Phase.Melee,SidewaysType.Move)]
        public void InvalidCombatPhaseRejectsWithoutPlaying(Phase phase,SidewaysType type)
        {
            var(p,c,h,s)=Setup(1);c.InBattle=true;c.CombatPhase=phase;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePain(p,c,h,s,p.Deck.Hand.Single(x=>x.Type==CardType.Wound),type));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [Test] public void BlockContributionsKeepActualWoundSourcesAndConsumeAgainstOriginalEnemy()
        {
            var(p,c,h,s)=Setup(2);var wounds=p.Deck.Hand.Where(x=>x.Type==CardType.Wound).ToArray();var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var battle=new MK.Logic.Runtime.Battles.PveBattleSession(p,new[]{enemy},CardJsonLoader.LoadBasicActions());c=battle.Context;c.SkillSupply=new SkillSupply();c.SkillSupply.Offer.Add(s);battle.Advance();
            HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[0],SidewaysType.Block);HeadbandBorrowResolution.UsePain(p,c,h,s,wounds[1],SidewaysType.Block);
            Assert.That(c.BlockPool,Is.EqualTo(4));Assert.That(c.CombatPower.Blocks.Count,Is.EqualTo(2));
            Assert.That(c.CombatPower.Blocks[0].SourceCard,Is.SameAs(wounds[0]));Assert.That(c.CombatPower.Blocks[1].SourceCard,Is.SameAs(wounds[1]));
            battle.Block(0);Assert.That(c.BlockPool,Is.Zero);Assert.That(battle.IsBlocked(0),Is.True);
            int before=p.Wounds;battle.Advance();battle.Advance();Assert.That(p.Wounds,Is.EqualTo(before));
        }
    }
}
