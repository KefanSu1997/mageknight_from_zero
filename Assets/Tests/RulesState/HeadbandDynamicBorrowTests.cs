using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandDynamicBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(string pool,int index)
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply()};
            var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills(pool)[index];c.SkillSupply.Offer.Add(s);return(p,c,h,s);
        }
        [Test] public void SidewaysUsesTwoActualCardsAndPreservesOwnMarkersOnFailure()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard("basic_card_001",CardType.Action);
            p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);
            var own=CardJsonLoader.LoadHeroSkills("hero_skill_pool_005")[7];p.Skills.Add(own);
            var ownMaterial=new DeedCard("basic_card_002",CardType.Action);p.Deck.Hand.Add(ownMaterial);
            var ownColor=CardColorHelper.Get(ownMaterial.Id).Value;p.Mana.AddToken(ownColor);
            SkillActions.PlaySideways(p,c,own,ownMaterial,SidewaysType.Influence,ownColor);
            Assert.That(SkillActions.IsUsed(p,own),Is.True);
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move);
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(p.Deck.PlayedCards.Contains(h),Is.True);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>RepeatedEffectResolution.RequireCompleted(c));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.MovementPool,Is.EqualTo(2));
            HeadbandBorrowResolution.UseSideways(p,c,h,s,b,SidewaysType.Influence);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.InfluencePool,Is.EqualTo(6));Assert.That(p.Skills.Single(),Is.SameAs(own));
            Assert.That(SkillActions.IsUsed(p,own),Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
            Assert.That(p.Deck.PlayedCards.Count(x=>ReferenceEquals(x,h)),Is.EqualTo(1));
        }
        [Test] public void UniversalSecondPaymentUsesIndependentActualColorAndCannotRepeatFirst()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_005",7);var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard("basic_card_001",CardType.Action);
            p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);var ca=CardColorHelper.Get(a.Id).Value;var cb=CardColorHelper.Get(b.Id).Value;p.Mana.AddToken(ca);
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move,ca);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,b,SidewaysType.Move,cb));
            Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(p.Deck.Hand.Contains(b),Is.True);
            p.Mana.AddToken(cb);HeadbandBorrowResolution.UseSideways(p,c,h,s,b,SidewaysType.Influence,cb);
            Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void InvalidIdentityAndSelfMaterialPreserveFirstTransaction()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(a);
            var fake=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[6];
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,fake,a,SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,h,SidewaysType.Move));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.MovementPool,Is.Zero);
        }
        [Test] public void CannotAbandonLegalSecondInputButCanFinishWhenNoneRemain()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard("basic_card_001",CardType.Action);p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.FinishIfImpossible(p,c));
            HeadbandBorrowResolution.UseSideways(p,c,h,s,b,SidewaysType.Move);
            var(p2,c2,h2,s2)=Setup("hero_skill_pool_007",6);var only=new DeedCard("basic_card_000",CardType.Action);p2.Deck.Hand.Add(only);
            HeadbandBorrowResolution.UseSideways(p2,c2,h2,s2,only,SidewaysType.Move);HeadbandBorrowResolution.FinishIfImpossible(p2,c2);
            Assert.That(c2.PendingHeadbandBorrow,Is.Null);Assert.That(c2.MovementPool,Is.EqualTo(2));
        }

        [Test] public void BullseyePreservesTwoOriginalContributionsAndConsumesAgainstActualEnemy()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_004",4);
            var a=new DeedCard("advanced_card_000",CardType.Action);var b=new DeedCard("advanced_card_001",CardType.Action);p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);
            p.Mana.AddCrystal(ManaColor.Red);p.Mana.AddCrystal(ManaColor.Blue);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var battle=new MK.Logic.Runtime.Battles.PveBattleSession(p,new[]{enemy},CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()));
            c=battle.Context;c.SkillSupply=new SkillSupply();c.SkillSupply.Offer.Add(s);
            battle.Play(a,"strong");battle.Play(b,"strong");var first=c.CombatPower.Attacks[0];var second=c.CombatPower.Attacks[1];
            HeadbandBorrowResolution.UseBullseye(p,c,h,s,first);
            Assert.Throws<InvalidOperationException>(()=>battle.Advance());
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseBullseye(p,c,h,s,first));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            var boosted=c.CombatPower.Attacks[0];Assert.That(boosted.Value,Is.EqualTo(first.Value+1));Assert.That(boosted.Element,Is.EqualTo(first.Element));Assert.That(boosted.SourceCard,Is.SameAs(a));
            HeadbandBorrowResolution.UseBullseye(p,c,h,s,second);Assert.That(c.CombatPower.Attacks[1].SourceCard,Is.SameAs(b));
            Assert.That(c.CombatPower.Attacks[1].Value,Is.EqualTo(second.Value+1));Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.PendingHeadbandBorrow,Is.Null);
            battle.Attack(0);Assert.That(c.DefeatedEnemyIndices.Count,Is.EqualTo(1));
        }
        [Test] public void PendingBorrowBlocksFormalEndAndFinishDiscardsSameHeadband()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(a);
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move);
            var source=new ManaSource(1,()=>DayPart.Day);var turns=new PlayerTurnEngine();
            Assert.Throws<InvalidOperationException>(()=>turns.EndTurn(p,DayPart.Day,source,c));
            Assert.That(p.Deck.PlayedCards.Contains(h),Is.True);
            HeadbandBorrowResolution.FinishIfImpossible(p,c);turns.EndTurn(p,DayPart.Day,source,c);
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,h)),Is.True);
        }
        [Test] public void RepelBorrowKeepsActualSourcePromiseWithoutPersonalSkillMarker()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",7);var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard("basic_card_001",CardType.Action);p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move,null,true);
            Assert.That(p.ForgoSourceDiceThisTurn,Is.True);Assert.That(c.MovementPool,Is.EqualTo(3));
            HeadbandBorrowResolution.UseSideways(p,c,h,s,b,SidewaysType.Influence,null,true);
            Assert.That(c.InfluencePool,Is.EqualTo(3));Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }

        [Test] public void ActualHeadbandAndPublicReferenceAreRequiredAndContinuationCannotChangeIdentity()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard("basic_card_001",CardType.Action);p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);
            var fake=new DeedCard("items_022",CardType.Artifact);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,fake,s,a,SidewaysType.Move));
            var interactive=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[9];c.SkillSupply.Offer.Add(interactive);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,interactive,a,SidewaysType.Move));
            HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(new PlayerState(),c,h,s,b,SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,fake,s,b,SidewaysType.Move));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.MovementPool,Is.EqualTo(2));
        }
        [Test] public void BullseyeRejectsContributionWithoutAnActualCardBeforeMovingHeadband()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_004",4);c.InBattle=true;c.CombatPhase=Phase.Melee;c.CombatPower.AddAttack(3);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseBullseye(p,c,h,s,c.CombatPower.Attacks.Single()));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(c.MeleePool,Is.EqualTo(3));
        }

        [TestCase(Phase.Ranged,SidewaysType.Attack)]
        [TestCase(Phase.Block,SidewaysType.Move)]
        [TestCase(Phase.Block,SidewaysType.Influence)]
        [TestCase(Phase.Melee,SidewaysType.Block)]
        public void BorrowCannotBypassActualCombatPhase(Phase phase,SidewaysType type)
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(a);c.InBattle=true;c.CombatPhase=phase;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,a,type));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [Test] public void RestOrCommittedActionPreventsBorrowedMovement()
        {
            var(p,c,h,s)=Setup("hero_skill_pool_007",6);var a=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(a);c.RestedThisTurn=true;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move));
            c.RestedThisTurn=false;c.TurnActionCommitted=true;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSideways(p,c,h,s,a,SidewaysType.Move));
            Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
    }
}
