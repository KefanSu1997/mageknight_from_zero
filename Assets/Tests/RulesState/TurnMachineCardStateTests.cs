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
    public sealed class TurnMachineCardStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, int attack = 0) => new("target", armor, attack, Element.Physical, 3, Array.Empty<Ability>());
        private static (PlayerState p, ActionContext c, GameState g, TurnMachine t) Battle()
        {
            var p = new PlayerState(); var g = new GameState(); g.Players.Add(p);
            var c = new ActionContext { DayPart = DayPart.Night }; var t = new TurnMachine(g); t.BeginBattle(c);
            return (p,c,g,t);
        }
        private static void Fireball(PlayerState p, ActionContext c, bool strong)
        {
            p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Black);
            var s = CardJsonLoader.LoadSpells().Single(x => x.Id == "magic_009");
            var instance = new DeedCard(s.Id, CardType.Spell); p.Deck.Hand.Add(instance);
            CardPlayActions.Play(p,c,instance,new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost),strong);
        }
        private static AttackAllocation[] Attacks(ActionContext c, Phase phase)
        {
            var parts = c.CombatPower.AvailableAttacks(phase);
            return parts.Length == 0 ? Array.Empty<AttackAllocation>()
                : new[] { new AttackAllocation(new[] { 0 },parts.Sum(p => p.Value),Element.Physical,Components:parts) };
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalFireballMissIsConsumedBeforeMeleeAndBattleActuallyEnds(bool strong)
        {
            var(p,c,g,t)=Battle(); Fireball(p,c,strong);
            var enemies=new List<Monster>{Enemy(9)};
            t.ApplyRanged(enemies,Attacks(c,Phase.Ranged),c);
            Assert.That(c.RangedPool+c.SiegePool,Is.Zero);
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.Block));
            t.SetBlock(0,Element.Physical);
            var r=t.ResolveBattle(enemies,Array.Empty<BlockAllocation>(),Attacks(c,Phase.Melee),ctx:c);
            Assert.That(r.AllKilled,Is.False); Assert.That(p.Fame,Is.Zero);
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.End)); Assert.That(c.CombatPhase,Is.EqualTo(Phase.End));
            Assert.That(c.InBattle,Is.False); Assert.That(p.Wounds,Is.EqualTo(strong?1:0));
            Assert.That(p.Deck.PlayedCards.Single().Id,Is.EqualTo("magic_009"));
            Assert.Throws<InvalidOperationException>(()=>t.ResolveBattle(enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c));
            t.BeginBattle(c); Assert.That(g.CurrentPhase,Is.EqualTo(Phase.Ranged));
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,enemies,new[]{0},Phase.Ranged));
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalShieldBlocksOnceThenNextBattleTakesDamageWithoutNewCard(bool burn)
        {
            var(p,c,g,t)=Battle(); var enemies=new List<Monster>{Enemy(5,2)};
            t.ApplyRanged(enemies,Array.Empty<AttackAllocation>(),c);
            var card=new DeedCard("items_020",CardType.Artifact);p.Deck.Hand.Add(card);
            CardPlayActions.PlayArtifact(p,c,card,CardJsonLoader.LoadItems().Single(x=>x.Id==card.Id),burn);
            var blocks=c.CombatPower.AvailableBlocks(0);t.SetBlock(blocks.Sum(b=>b.Value),blocks[0].Element);
            Assert.That(t.ResolveBattle(enemies,blocks,Array.Empty<AttackAllocation>(),ctx:c).TotalWounds,Is.Zero);
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.End)); Assert.That(c.InBattle,Is.False);Assert.That(c.BlockPool,Is.Zero);
            Assert.That(burn?p.Deck.RemovedCards.Single():p.Deck.PlayedCards.Single(),Is.SameAs(card));
            t.BeginBattle(c);t.ApplyRanged(enemies,Array.Empty<AttackAllocation>(),c);t.SetBlock(0,Element.Physical);
            Assert.That(t.ResolveBattle(enemies,c.CombatPower.AvailableBlocks(0),Array.Empty<AttackAllocation>(),ctx:c).TotalWounds,Is.EqualTo(1));
            Assert.That(p.Wounds,Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulRangedKillIsNotRewardedAgainWhenClosingEmptyBattle()
        {
            var(p,c,g,t)=Battle();Fireball(p,c,false);var enemies=new List<Monster>{Enemy(5)};
            t.ApplyRanged(enemies,Attacks(c,Phase.Ranged),c);Assert.That(p.Fame,Is.EqualTo(3));Assert.That(enemies,Is.Empty);
            t.SetBlock(0,Element.Physical);var r=t.ResolveBattle(enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(r.AllKilled,Is.True);Assert.That(p.Fame,Is.EqualTo(3));Assert.That(c.RangedPool,Is.Zero);
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.End));Assert.That(c.InBattle,Is.False);
        }

        [Test]
        public void ChoosingNoRangedAttackDiscardsUnusedOriginalFireballPowerAtPhaseEnd()
        {
            var(p,c,g,t)=Battle();Fireball(p,c,false);var enemies=new List<Monster>{Enemy(5)};
            t.ApplyRanged(enemies,Array.Empty<AttackAllocation>(),c);
            Assert.That(c.RangedPool,Is.Zero);Assert.That(p.Fame,Is.Zero);Assert.That(enemies.Count,Is.EqualTo(1));
            t.SetBlock(0,Element.Physical);t.ResolveBattle(enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.End));Assert.That(c.InBattle,Is.False);
        }

        [Test]
        public void WrongPhaseAndInvalidAllocationDoNotFinishBattleOrSpendPower()
        {
            var(p,c,g,t)=Battle();var enemies=new List<Monster>{Enemy(3,2)};
            Assert.Throws<InvalidOperationException>(()=>t.BeginBattle(c));
            Assert.Throws<InvalidOperationException>(()=>t.ResolveBattle(enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c));
            t.ApplyRanged(enemies,Array.Empty<AttackAllocation>(),c);c.CombatPower.AddBlock(4);t.SetBlock(4,Element.Physical);
            Assert.Throws<InvalidOperationException>(()=>t.ResolveBattle(enemies,new[]{new BlockAllocation(0,4,Element.Physical,ForUnitIndex:99)},Array.Empty<AttackAllocation>(),ctx:c));
            Assert.That(g.CurrentPhase,Is.EqualTo(Phase.AssignDamage));Assert.That(c.InBattle,Is.True);
            Assert.That(c.BlockPool,Is.EqualTo(4));Assert.That(p.Wounds,Is.Zero);Assert.That(p.Fame,Is.Zero);
        }
    }
}
