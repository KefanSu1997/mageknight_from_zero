using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class FearBannerStateTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PlayerState p, ActionContext c, DeedCard b, TurnMachine t) Fixture()
        {
            var p = new PlayerState { ExtraCommandSlots = 1 };
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var c = new ActionContext { TargetUnit = p.Units[0], SkipDraw = true };
            var b = new DeedCard("items_001", CardType.Artifact); p.Deck.Hand.Add(b);
            var game = new GameState(); game.Players.Add(p);
            var turn = new TurnMachine(game); turn.BeginBattle(c);
            c.Enemies = new List<Monster> { Enemy("monster_025"), Enemy("monster_025"), Enemy("monster_025"), Enemy("monster_025") };
            turn.ApplyRanged(c.Enemies, Array.Empty<AttackAllocation>(), c);
            return (p,c,b,turn);
        }
        private static Monster Enemy(string id) => CardJsonLoader.LoadMonsters().Single(m => m.Id == id);
        private static void Play(PlayerState p, ActionContext c, DeedCard b, bool burn = false)
            => CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i => i.Id == b.Id),burn);
        private static void AssertUnspent(PlayerState p, ActionContext c, UnitState unit)
        {
            Assert.That(unit.IsReady, Is.True); Assert.That(p.Fame, Is.Zero);
            Assert.That(c.SkipAttackIndices.Count, Is.Zero);
        }

        [Test] public void RealAttachmentAndUnitExhaustionCancelOnlySelectedAttackAndGiveOneFame()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); var unit = p.Units[0];
            Assert.That(unit.Banner, Is.SameAs(b)); Assert.That(p.Deck.Hand.Count, Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero); AssertUnspent(p,c,unit);
            BannerActions.UseFear(p,c,unit,2);
            Assert.That(unit.IsReady, Is.False); Assert.That(p.Units[1].IsReady, Is.True);
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[]{2})); Assert.That(p.Fame, Is.EqualTo(1));
            Assert.That(c.WasBlockedThisBattle(c.Enemies![2]), Is.False);
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,unit,1));
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p,c,unit,0));
            t.SetBlock(0,Element.Physical); var result = t.ResolveDefense(c.Enemies!,Array.Empty<BlockAllocation>(),c);
            Assert.That(result.TotalWounds, Is.EqualTo(6)); Assert.That(p.Wounds, Is.EqualTo(6));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(6)); Assert.That(p.Fame, Is.EqualTo(1));
        }

        [Test] public void ArcaneImmuneAttackCanBeCancelledButEnemyCannotBePreventedByBurn()
        {
            var (p,c,b,t) = Fixture(); c.Enemies = new List<Monster>{CardJsonLoader.LoadMonsters().First(m => m.Abilities.Contains(Ability.MagicResist))};
            Play(p,c,b); BannerActions.UseFear(p,c,p.Units[0],0); Assert.That(p.Fame, Is.EqualTo(1));
            t.SetBlock(0,Element.Physical); Assert.That(t.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c).TotalWounds, Is.Zero);
            var second = new DeedCard("items_001",CardType.Artifact); p.Deck.Hand.Add(second); c.SpellTargetIndices.Add(0);
            Assert.Throws<InvalidOperationException>(() => Play(p,c,second,true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(second)); Assert.That(p.Deck.RemovedCards.Count, Is.Zero);
        }

        [TestCase(Phase.Ranged)] [TestCase(Phase.AssignDamage)] [TestCase(Phase.Melee)] [TestCase(Phase.End)]
        public void OutsideBlockPhaseCannotSpendOrReward(Phase phase)
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); CardCombatActions.EnterPhase(c,phase);
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],0)); AssertUnspent(p,c,p.Units[0]);
        }

        [Test] public void ActualTurnMachineClosingBlockAlsoClosesBannerActivationWindow()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); t.SetBlock(0,Element.Physical);
            Assert.That(c.CombatPhase, Is.EqualTo(Phase.AssignDamage));
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],0)); AssertUnspent(p,c,p.Units[0]);
        }

        [TestCase("wounded")] [TestCase("exhausted")] [TestCase("foreign")] [TestCase("other")] [TestCase("destroyed")]
        public void InvalidUnitCannotCancelOrReward(string invalid)
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); var unit = p.Units[0];
            if(invalid == "wounded") unit.AddWounds(1);
            if(invalid == "exhausted") unit.Exhaust();
            if(invalid == "foreign") p.Units.Remove(unit);
            if(invalid == "other") unit = p.Units[1];
            if(invalid == "destroyed") unit.Destroy();
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,unit,0));
            Assert.That(p.Fame, Is.Zero); Assert.That(c.SkipAttackIndices.Count, Is.Zero);
        }

        [TestCase(-1)] [TestCase(4)] public void InvalidEnemyPreservesReadinessAndFame(int target)
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b);
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],target)); AssertUnspent(p,c,p.Units[0]);
        }

        [Test] public void DefeatedOrAlreadyCancelledEnemyCannotFarmFame()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); CardCombatActions.DestroyEnemy(p,c,0); int fame = p.Fame;
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],0));
            Assert.That(p.Units[0].IsReady, Is.True); Assert.That(p.Fame, Is.EqualTo(fame));
            BannerActions.UseFear(p,c,p.Units[0],1);
            var second = new DeedCard(b.Id,b.Type); p.Deck.Hand.Add(second); c.TargetUnit = p.Units[1]; Play(p,c,second);
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[1],1));
            Assert.That(p.Units[1].IsReady, Is.True); Assert.That(p.Fame, Is.EqualTo(fame + 1));
        }

        [Test] public void AlreadySuccessfullyBlockedAttackCannotBeCancelledForFame()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); c.CombatPower.AddBlock(3,Element.Physical);
            CardCombatActions.Block(p,c,c.Enemies!,0);
            Assert.That(c.WasBlockedThisBattle(c.Enemies![0]), Is.True);
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],0)); AssertUnspent(p,c,p.Units[0]);
        }

        [Test] public void RealEnergyFlowRejectsCombatThenReadiesUnitForNextBattle()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); BannerActions.UseFear(p,c,p.Units[0],0);
            var spell = CardJsonLoader.LoadSpells().Single(a => a.Id == "magic_000");
            var actual = new DeedCard(spell.Id,CardType.Spell); p.Deck.Hand.Add(actual);
            p.Mana.AddToken(ManaColor.Green); c.TargetUnit = p.Units[0];
            var original = new ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.Play(p,c,actual,original));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(actual)); Assert.That(p.Units[0].IsReady, Is.False);
            UnitDamageResolver.EndBattle(p,c); CardPlayActions.Play(p,c,actual,original);
            c.InBattle = true; CardCombatActions.EnterPhase(c,Phase.Block); BannerActions.UseFear(p,c,p.Units[0],1);
            Assert.That(p.Fame, Is.EqualTo(2)); Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[]{1}));
            Assert.That(p.Units[0].IsReady, Is.False); Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(actual));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
        }

        [Test] public void EndBattleClearsCancellationWithoutReadyingAndNextRoundRestoresUnit()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b); BannerActions.UseFear(p,c,p.Units[0],0);
            UnitDamageResolver.EndBattle(p,c); Assert.That(c.SkipAttackIndices.Count, Is.Zero);
            Assert.That(p.Units[0].IsReady, Is.False); Assert.That(p.Units[0].Banner, Is.SameAs(b));
            new GameEngine(new[]{p},3).StartNewRound(Array.Empty<TacticCard>());
            c.InBattle = true; CardCombatActions.EnterPhase(c,Phase.Block);
            BannerActions.UseFear(p,c,p.Units[0],0); Assert.That(p.Fame, Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void BurnSelectsZeroThroughThreeActualEnemiesAndPermanentlyRemovesOnlyArtifact(int count)
        {
            var (p,c,b,t) = Fixture(); c.SpellTargetIndices.AddRange(new[]{3,1,2}.Take(count)); Play(p,c,b,true);
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[]{3,1,2}.Take(count)));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(b)); Assert.That(p.Deck.Hand.Count, Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero); Assert.That(p.Units.All(u => u.IsReady), Is.True); Assert.That(p.Fame, Is.Zero);
            t.SetBlock(0,Element.Physical); Assert.That(t.ResolveDefense(c.Enemies!,Array.Empty<BlockAllocation>(),c).TotalWounds, Is.EqualTo((4-count)*2));
            t.FinishBattle(c); Assert.That(c.SkipAttackIndices.Count, Is.Zero);
        }

        [TestCase("duplicate")] [TestCase("four")] [TestCase("negative")] [TestCase("range")] [TestCase("defeated")] [TestCase("outside")]
        public void InvalidBurnSelectionIsAtomic(string invalid)
        {
            var (p,c,b,t) = Fixture();
            c.SpellTargetIndices.AddRange(invalid == "duplicate" ? new[]{0,0} : invalid == "four" ? new[]{0,1,2,3} : invalid == "negative" ? new[]{0,-1} : invalid == "range" ? new[]{0,4} : new[]{0});
            if(invalid == "defeated") CardCombatActions.DestroyEnemy(p,c,0);
            if(invalid == "outside") UnitDamageResolver.EndBattle(p,c);
            int fame = p.Fame;
            Assert.Throws<InvalidOperationException>(() => Play(p,c,b,true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(b)); Assert.That(p.Deck.RemovedCards.Count, Is.Zero);
            Assert.That(c.SkipAttackIndices.Count, Is.Zero); Assert.That(p.Fame, Is.EqualTo(fame));
        }

        [Test] public void BurnTargetFollowsSurvivorAfterRangedKillAndDoesNotPreventMeleeKill()
        {
            var (p,c,b,t) = Fixture(); c.CombatPhase = Phase.Ranged;
            var game = new GameState(); game.Players.Add(p); t = new TurnMachine(game); t.BeginBattle(c);
            c.SpellTargetIndices.Add(3); Play(p,c,b,true);
            t.ApplyRanged(c.Enemies!,new[]{new AttackAllocation(new[]{0},c.Enemies![0].Armor,Element.Physical,IsSiege:true)},c);
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[]{2})); Assert.That(c.Enemies.Count, Is.EqualTo(3));
            t.SetBlock(0,Element.Physical); Assert.That(t.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c).TotalWounds, Is.EqualTo(4));
            c.CombatPower.AddAttack(c.Enemies[2].Armor,Element.Physical); Assert.That(t.Attack(new[]{2},c).Kills, Is.EqualTo(1));
        }

        [Test] public void AttachmentCannotBeBurnedAndReplacementReturnsExactCardToDiscard()
        {
            var (p,c,b,t) = Fixture(); Play(p,c,b);
            Assert.Throws<InvalidOperationException>(() => Play(p,c,b,true));
            var replacement = new DeedCard("items_003",CardType.Artifact); p.Deck.Hand.Add(replacement); Play(p,c,replacement);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(b)); Assert.That(p.Units[0].Banner, Is.SameAs(replacement));
            Assert.Throws<InvalidOperationException>(() => BannerActions.UseFear(p,c,p.Units[0],0));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidAttachmentPreservesActualHand(int invalid)
        {
            var (p,c,b,t) = Fixture(); if(invalid == 0) c.TargetUnit = null;
            if(invalid == 1) c.TargetUnit = new UnitState(p.Units[0].Card);
            if(invalid == 2) p.Units[0].Destroy();
            Assert.Throws<InvalidOperationException>(() => Play(p,c,b)); Assert.That(p.Deck.Hand.Single(), Is.SameAs(b));
        }
    }
}
