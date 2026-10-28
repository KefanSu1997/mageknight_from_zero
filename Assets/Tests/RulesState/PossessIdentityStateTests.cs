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
    public sealed class PossessIdentityStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, int attack = 0) => new("target", armor, attack, Element.Fire, 3, Array.Empty<Ability>());
        [TestCase(-1)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void IllegalPossessionPreservesManaAndOriginalHand(int failure)
        {
            var p = new PlayerState();
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            var c = new ActionContext { DayPart = DayPart.Night, InBattle = failure != 3,
                Enemies = new List<Monster> { Enemy(5, 4) with { Abilities = failure == 2 ? new[] { Ability.MagicResist } : Array.Empty<Ability>() } } };
            if (failure == 4) c.Enemies = null;
            if (failure == 5) c.Enemies.Clear();
            var error = Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_021", true, failure < 2 ? failure : 0));
            Assert.That(error.Message, Is.EqualTo(failure == 2 ? "奥术免疫敌人不能成为附身目标" : "附身必须选择当前战斗中的敌人"));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("magic_021"));
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.PossessedEnemyIndices, Is.Empty);
            Assert.That(c.CombatPower.Attacks, Is.Empty);
        }

        [TestCase(Ability.FireResist)] [TestCase(Ability.IceResist)] [TestCase(Ability.PhysicalResist)]
        public void WhitePossessionCanUseResistantSourceAndAttackAnotherArcaneImmuneEnemy(Ability resistance)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            var source = Enemy(6, 4) with { Abilities = new[] { resistance } };
            var enemies = new List<Monster> { source, Enemy(4) with { Abilities = new[] { Ability.MagicResist } } };
            var c = new ActionContext { DayPart = DayPart.Night, Enemies = enemies };
            var g = new GameState(); g.Players.Add(p); var t = new TurnMachine(g); t.BeginBattle(c);
            Play(p, c, "magic_021", true);
            Assert.That(p.Deck.Hand, Is.Empty); Assert.That(p.Deck.PlayedCards.Single().Id, Is.EqualTo("magic_021"));
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.Zero);
            Assert.That(t.Attack(new[] { 1 }, c).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(3)); Assert.That(c.MeleePool, Is.Zero);
            t.FinishBattle(c);
        }

        private static void Play(PlayerState p, ActionContext c, string id, bool strong = false, int target = 0)
        {
            ActionCardData data;
            if (id.StartsWith("magic"))
            {
                var s = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
                data = new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost);
            }
            else data = CardJsonLoader.LoadBasicActions().Single(a => a.Id == id);
            var card = new DeedCard(id, id.StartsWith("magic") ? CardType.Spell : CardType.Action);
            p.Deck.Hand.Add(card); CardPlayActions.Play(p, c, card, data, strong, target);
        }
        private static (PlayerState p, ActionContext c, TurnMachine t) Start(List<Monster> enemies, int source, int rangedTarget)
        {
            var p = new PlayerState();
            foreach (ManaColor color in Enum.GetValues(typeof(ManaColor))) p.Mana.AddToken(color, 3);
            var g = new GameState(); g.Players.Add(p);
            var c = new ActionContext { Enemies = enemies, DayPart = DayPart.Night, NextMeleeAsRanged = true };
            var t = new TurnMachine(g); t.BeginBattle(c);
            Play(p, c, "magic_021", true, source);
            Assert.That(c.RangedPool, Is.Zero); Assert.That(c.NextMeleeAsRanged, Is.True);
            c.NextMeleeAsRanged = false;
            Play(p, c, "magic_009");
            var parts = c.CombatPower.AvailableAttacks(Phase.Ranged);
            t.ApplyRanged(enemies, new[] { new AttackAllocation(new[] { rangedTarget }, 5, Element.Fire, Components: parts) }, c);
            t.SetBlock(0, Element.Physical); t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
            return (p, c, t);
        }

        [TestCase(0)] [TestCase(2)]
        public void MovingSourceRemainsForbiddenAndOrdinaryCardCanStillAttackIt(int killed)
        {
            var source = Enemy(6, 4);
            var enemies = new List<Monster> { Enemy(1), source, Enemy(1) };
            var (p, c, t) = Start(enemies, 1, killed);
            int current = enemies.FindIndex(e => ReferenceEquals(e, source));
            Assert.Throws<InvalidOperationException>(() => t.Attack(new[] { current }, c));
            Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(p, enemies, Array.Empty<BlockAllocation>(),
                new[] { new AttackAllocation(new[] { current }, 4, Element.Fire, Components: c.CombatPower.Attacks) }, ctx: c));
            Assert.That(p.Wounds, Is.Zero);
            Play(p, c, "basic_card_021");
            Assert.That(t.Attack(new[] { current }, c).PrintedPower, Is.EqualTo(2));
            Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.That(t.Attack(new[] { 1 - current }, c).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(6)); t.FinishBattle(c);
        }

        [TestCase(1)] [TestCase(2)]
        public void EqualLookingMonstersAreDifferentTargetsAfterCompaction(int sourceIndex)
        {
            var source = Enemy(4, 4); var other = source with { };
            var enemies = new List<Monster> { Enemy(1), sourceIndex == 1 ? source : other, sourceIndex == 1 ? other : source };
            Assert.That(source, Is.EqualTo(other)); Assert.That(ReferenceEquals(source, other), Is.False);
            var (p, c, t) = Start(enemies, sourceIndex, 0);
            int current = sourceIndex - 1;
            Assert.Throws<InvalidOperationException>(() => t.Attack(new[] { current }, c));
            Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.That(t.Attack(new[] { 1 - current }, c).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(6)); Assert.That(p.Wounds, Is.EqualTo(2));
        }

        [Test]
        public void DefeatedSourceDoesNotMakeAnUnrelatedSurvivorForbidden()
        {
            var enemies = new List<Monster> { Enemy(1, 4), Enemy(4) };
            var (p, c, t) = Start(enemies, 0, 0);
            c.CombatPower.ConvertNewMeleeToRanged(0);
            Assert.That(c.RangedPool, Is.Zero); Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(6)); t.FinishBattle(c);
            Assert.That(c.CombatPower.Attacks, Is.Empty);
        }
    }
}
