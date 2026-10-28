using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeadbandPuppetBorrowTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private (PlayerState p, ActionContext c, DeedCard h, SkillCard s) Setup(bool headband = true)
        {
            var p = new PlayerState(); new PlayerTurnEngine().StartTurn(p, DayPart.Day);
            var original = CardJsonLoader.LoadMonsters().Single(x => x.Id == "monster_032");
            var supply = new EnemyTokenSupply(new[] { original, original with { } });
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Melee,
                EnemySupply = supply, SkillSupply = new SkillSupply(),
                Enemies = new[] { supply.Draw(original.TokenColor), supply.Draw(original.TokenColor) } };
            var s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[6]; c.SkillSupply.Offer.Add(s);
            var h = new DeedCard("items_022", CardType.Artifact); if (headband) p.Deck.Hand.Add(h);
            return (p, c, h, s);
        }
        private void Fury(PlayerState p, ActionContext c, int target)
        {
            var card = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(card);
            p.Mana.AddToken(ManaColor.Red);
            CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), true);
            Assert.That(CardCombatActions.Attack(p, c, c.Enemies, new[] { target }, Phase.Melee).Kills, Is.EqualTo(1));
        }
        private void NextTurn(PlayerState p, ActionContext c)
        {
            c.InBattle = false;
            var turns = new PlayerTurnEngine(); turns.EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            turns.StartTurn(p, DayPart.Day);
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualKillKeepThenImmediateAttackConsumesSameTokenWithoutAcquiringSkill(bool headbandDrawnAfterKill)
        {
            var (p,c,h,s) = Setup(!headbandDrawnAfterKill); Fury(p,c,0);
            if (headbandDrawnAfterKill) p.Deck.Hand.Add(h);
            var enemy = c.Enemies[0];
            Assert.That(p.PuppetTokens.DefeatedThisTurn.Single(), Is.SameAs(enemy));
            c.EnemySupply.Discard(enemy);
            Assert.That(c.EnemySupply.DiscardPile, Is.Empty);
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,0);
            Assert.That(c.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(), Is.True);
            Assert.Throws<InvalidOperationException>(() => HeadbandBorrowResolution.FinishIfImpossible(p,c));
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,1);
            Assert.That(c.PendingHeadbandBorrow, Is.Null);
            Assert.That(c.CombatPower.Attacks.Single().Value, Is.EqualTo(2));
            var material = new DeedCard("basic_card_016", CardType.Action); p.Deck.Hand.Add(material);
            Assert.That(new ActionSystem(false).PlaySideways(material,p,c,SidewaysType.Attack), Is.True);
            Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{1},Phase.Melee).Kills, Is.EqualTo(1));
            Assert.That(c.EnemySupply.DiscardPile.Single(), Is.SameAs(enemy));
            Assert.That(p.PuppetTokens.KeptTokens, Is.Empty);
            Assert.That(p.Deck.PlayedCards.Count(x => ReferenceEquals(x,h)), Is.EqualTo(1));
            Assert.That(p.Skills, Is.Empty); Assert.That(SkillActions.IsUsed(p,s), Is.False);
            Assert.That(c.SkillSupply.Offer.Single(), Is.SameAs(s));
            Assert.Throws<InvalidOperationException>(() => HeadbandBorrowResolution.UsePuppet(p,c,h,s,c.Enemies[1],0));
            NextTurn(p,c);
            Assert.That(c.EnemySupply.PuppetReservations, Is.Empty);
            Assert.That(c.EnemySupply.DiscardPile.Count, Is.EqualTo(2));
        }
        [Test]
        public void TwoStoredTokensSurviveFormalTurnAndRoundThenActualBlockConsumesOnlySelectedToken()
        {
            var(p,c,h,s)=Setup(); Fury(p,c,0); Fury(p,c,1);
            foreach(var enemy in c.Enemies) HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,0);
            Assert.That(c.PendingHeadbandBorrow, Is.Null); NextTurn(p,c);
            Assert.That(p.PuppetTokens.KeptTokens.Count, Is.EqualTo(2));
            Assert.That(c.EnemySupply.PuppetReservations.Count, Is.EqualTo(2));
            var game = new GameEngine(new[]{p},3); game.EndRound(); game.PrepareNewRound();
            // The original physical headband returned by the formal round shuffle/refill is reused.
            Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,h)), Is.True);
            var old = c.Enemies[0]; var other = c.Enemies[1];
            var next = CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032");
            c.Enemies=new[]{next}; c.DefeatedEnemyIndices.Clear(); c.InBattle=true; c.CombatPhase=Phase.Block;
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,old,2);
            Assert.That(c.CombatPower.Blocks.Single().Value, Is.EqualTo(2));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(), Is.True);
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,other,2);
            CardCombatActions.Block(p,c,c.Enemies,0);
            Assert.That(c.SuccessfullyBlockedAttacks.Contains((0,0)), Is.True);
            Assert.That(p.PuppetTokens.KeptTokens, Is.Empty);
            Assert.That(c.EnemySupply.DiscardPile.Count, Is.EqualTo(2));
            Assert.That(SkillActions.IsUsed(p,s), Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void FirstInvalidInputLeavesHeadbandAndPublicSkillUntouched(int invalid)
        {
            var(p,c,h,s)=Setup(); Fury(p,c,0); var enemy=c.Enemies[0]; int mode=0;
            if(invalid==0) enemy=enemy with{};
            if(invalid==1) s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[6];
            if(invalid==2) h=new DeedCard("items_022",CardType.Artifact);
            if(invalid==3) enemy=c.Enemies[1];
            if(invalid==4) mode=3;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,mode));
            Assert.That(c.PendingHeadbandBorrow, Is.Null); Assert.That(p.PuppetTokens.KeptTokens, Is.Empty);
            Assert.That(p.Deck.Hand.Count(x=>x.Id=="items_022"), Is.EqualTo(1));
            Assert.That(p.PuppetTokens.DefeatedThisTurn.Count, Is.EqualTo(1));
            Assert.That(c.SkillSupply.Offer.Count, Is.EqualTo(1)); Assert.That(p.Skills, Is.Empty);
        }
        [Test]
        public void FailedSecondUsePreservesFirstKeepAndImpossiblePhaseCanFinish()
        {
            var(p,c,h,s)=Setup(); Fury(p,c,0); var enemy=c.Enemies[0];
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,0);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePuppet(p,c,new DeedCard("items_022",CardType.Artifact),s,enemy,1));
            c.CombatPhase=Phase.Ranged;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,1));
            Assert.That(c.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
            Assert.That(p.PuppetTokens.KeptTokens.Single(), Is.SameAs(enemy));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(), Is.False);
            HeadbandBorrowResolution.FinishIfImpossible(p,c);
            Assert.That(c.PendingHeadbandBorrow, Is.Null);
            Assert.That(c.EnemySupply.PuppetReservations.Single(), Is.SameAs(enemy));
            var other=new PlayerState(); var otherHeadband=new DeedCard("items_022",CardType.Artifact); other.Deck.Hand.Add(otherHeadband);
            c.CombatPhase=Phase.Melee;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UsePuppet(other,c,otherHeadband,s,enemy,1));
            Assert.That(p.PuppetTokens.KeptTokens.Single(), Is.SameAs(enemy));
        }
        [Test]
        public void OwnedOncePerTurnStillBlocksSecondUseWhileBorrowLeavesOwnUsageUnchanged()
        {
            var(p,c,h,s)=Setup(); var own=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[6]; p.Skills.Add(own);
            Fury(p,c,0); var enemy=c.Enemies[0]; PuppetMasterActions.Keep(p,c,own,enemy);
            Assert.Throws<InvalidOperationException>(()=>PuppetMasterActions.Consume(p,c,own,enemy,false));
            HeadbandBorrowResolution.UsePuppet(p,c,h,s,enemy,1);
            Assert.That(SkillActions.IsUsed(p,own), Is.True);
            Assert.That(p.Skills.Single(), Is.SameAs(own));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(), Is.False);
            HeadbandBorrowResolution.FinishIfImpossible(p,c);
            Assert.That(c.EnemySupply.DiscardPile.Single(), Is.SameAs(enemy));
        }
        [Test]
        public void PublicSkillCandidatesReleaseAtEndTurnAndAbsentSkillDoesNotReserve()
        {
            var(p,c,h,s)=Setup(false); Fury(p,c,0);
            Assert.That(c.EnemySupply.PuppetReservations.Count, Is.EqualTo(1));
            NextTurn(p,c);
            Assert.That(c.EnemySupply.PuppetReservations, Is.Empty);
            Assert.That(c.EnemySupply.DiscardPile.Single(), Is.SameAs(c.Enemies[0]));
            c.SkillSupply.Offer.Clear(); c.InBattle=true; c.CombatPhase=Phase.Melee;
            Fury(p,c,1);
            Assert.That(p.PuppetTokens.DefeatedThisTurn, Is.Empty);
            Assert.That(c.EnemySupply.PuppetReservations, Is.Empty);
        }
    }
}
