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
    public sealed class WhirlwindStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, int attack = 0, params Ability[] traits) => new("target", armor, attack, Element.Physical, 3, traits);
        private static DeedCard Play(PlayerState p, ActionContext c, bool strong, int target = 0)
        {
            var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_022");
            var instance = new DeedCard(spell.Id, CardType.Spell); p.Deck.Hand.Add(instance);
            CardPlayActions.Play(p, c, instance, new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost), strong, target);
            return instance;
        }
        private static (PlayerState p, ActionContext c, TurnMachine t, List<Monster> enemies) Start(params Monster[] enemies)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White, 3); p.Mana.AddToken(ManaColor.Black, 3);
            var list = enemies.ToList(); var c = new ActionContext { DayPart = DayPart.Night, Enemies = list };
            var g = new GameState(); g.Players.Add(p); var t = new TurnMachine(g); t.BeginBattle(c);
            return (p, c, t, list);
        }
        private static BattleResult Defend(TurnMachine t, ActionContext c, List<Monster> enemies)
        {
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            return t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
        }

        [TestCase(DayPart.Day)] [TestCase(DayPart.Night)]
        public void WhirlwindStopsOnlySelectedEnemyAndExpiresBeforeNextBattle(DayPart day)
        {
            var (p, c, t, enemies) = Start(Enemy(5, 4, Ability.FireResist, Ability.IceResist), Enemy(5, 2)); c.DayPart = day;
            var card = Play(p, c, false);
            Assert.That(Defend(t, c, enemies).TotalWounds, Is.EqualTo(1)); Assert.That(p.Fame, Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            t.FinishBattle(c); Assert.That(c.SkipAttackIndices, Is.Empty);
            c.SkipDraw = true; var turns = new PlayerTurnEngine();
            turns.EndTurn(p, day, new ManaSource(1, () => day), c); turns.StartTurn(p, day);
            t.BeginBattle(c); Assert.That(Defend(t, c, enemies).TotalWounds, Is.EqualTo(3)); t.FinishBattle(c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card)); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(4)); Assert.That(p.Wounds, Is.EqualTo(4));
        }

        [TestCase(false)] [TestCase(true)]
        public void TornadoActuallyDefeatsOnceWithoutSpendingAttackOrUndoingDamage(bool sword)
        {
            var (p, c, t, enemies) = Start(Enemy(99, 4, Ability.Fortified, Ability.FireResist, Ability.IceResist), Enemy(2));
            Assert.That(Defend(t, c, enemies).TotalWounds, Is.EqualTo(2));
            if (sword)
            {
                var artifact = new DeedCard("items_008", CardType.Artifact); p.Deck.Hand.Add(artifact);
                CardPlayActions.PlayArtifact(p, c, artifact, CardJsonLoader.LoadItems().Single(i => i.Id == artifact.Id), true);
            }
            var fury = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(fury);
            CardPlayActions.Play(p, c, fury, CardJsonLoader.LoadBasicActions().Single(a => a.Id == fury.Id));
            var attack = c.CombatPower.Attacks.Single();
            var card = Play(p, c, true);
            Assert.That(p.Fame, Is.EqualTo(sword ? 4 : 3), "消灭实际敌人必须立即获得其名望，仅一次");
            Assert.That(p.Wounds, Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(2));
            Assert.That(p.Deck.PlayedCards.Last(), Is.SameAs(card)); Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(2));
            Assert.That(c.CombatPower.Attacks.Single(), Is.EqualTo(attack));
            var error = Assert.Throws<InvalidOperationException>(() => t.Attack(new[] { 0 }, c));
            Assert.That(error.Message, Is.EqualTo("目标已经被击败")); Assert.That(c.MeleePool, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(p, enemies, Array.Empty<BlockAllocation>(),
                new[] { new AttackAllocation(new[] { 0 }, 100, Element.Physical) }, ctx: c));
            Assert.That(t.Attack(new[] { 1 }, c).Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(sword ? 8 : 6));
            var closed = BattleResolver.Resolve(p, enemies, Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: c);
            Assert.That(closed.AllKilled, Is.True); Assert.That(closed.KilledIndices, Is.Empty);
            Assert.That(closed.TotalWounds, Is.Zero); Assert.That(p.Fame, Is.EqualTo(sword ? 8 : 6));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(2)); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(3)); // two wounds and the refused second Tornado
        }

        [TestCase(false, -1)] [TestCase(true, -1)] [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 2)] [TestCase(true, 2)] [TestCase(false, 3)] [TestCase(true, 3)]
        public void IllegalTargetPreservesAllResources(bool strong, int failure)
        {
            var (p, c, t, enemies) = Start(Enemy(5, 0, failure == 2 ? Ability.MagicResist : Ability.FireResist));
            Defend(t, c, enemies); if (failure == 3) c.InBattle = false;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, strong, failure < 2 ? failure : 0));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("magic_022")); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.KillEnemyIndex, Is.Null); Assert.That(p.Fame, Is.Zero);
        }

        [TestCase(Phase.None)] [TestCase(Phase.Ranged)] [TestCase(Phase.Block)] [TestCase(Phase.AssignDamage)] [TestCase(Phase.End)]
        public void TornadoRejectsOutsideAttackPhaseBeforePayment(Phase phase)
        {
            var (p, c, t, enemies) = Start(Enemy(5)); c.CombatPhase = phase;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Fame, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void EnemyAlreadyDefeatedByAnOriginalCardCannotBeDestroyedAgain(bool shield)
        {
            var (p, c, t, enemies) = Start(Enemy(2, 2));
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c);
            if (shield)
            {
                p.Mana.AddToken(ManaColor.Red);
                var data = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_008");
                var instance = new DeedCard(data.Id, CardType.Spell); p.Deck.Hand.Add(instance);
                CardPlayActions.Play(p, c, instance, new ActionCardData(data.Id, data.Name, data.Set, data.ImagePath,
                    data.EnImagePath, data.Top.Effect, data.Bottom.Effect, data.Top.ManaCost), true);
            }
            t.SetBlock(c.BlockPool, Element.Fire);
            t.ResolveDefense(enemies, c.CombatPower.AvailableBlocks(0), c);
            if (!shield)
            {
                var fury = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(fury);
                CardPlayActions.Play(p, c, fury, CardJsonLoader.LoadBasicActions().Single(a => a.Id == fury.Id));
                Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.EqualTo(1));
            }
            Assert.That(p.Fame, Is.EqualTo(3)); int black = p.Mana.Tokens[ManaColor.Black];
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, true)).Message, Is.EqualTo("目标已经被击败"));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(black));
            Assert.That(p.Fame, Is.EqualTo(3)); Assert.That(p.Deck.Hand.Any(card => card.Id == "magic_022"), Is.True);
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)]
        public void OriginalManaAndNightRequirementsStillApply(bool strong, int failure)
        {
            var (p, c, t, enemies) = Start(Enemy(5)); Defend(t, c, enemies);
            if (failure == 0) p.Mana.Tokens[ManaColor.White] = 0;
            if (failure == 1) p.Mana.Tokens[ManaColor.Black] = 0;
            if (failure == 2) c.DayPart = DayPart.Day;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, strong)).Message,
                Is.EqualTo(failure == 2 ? "白昼不能施放强效法术" : "法力不足，不能支付所选效果"));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(failure == 0 ? 0 : 3));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(failure == 1 ? 0 : 3));
            Assert.That(p.Fame, Is.Zero); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void NightWhirlwindChainsIntoOriginalShieldOrSwordAndTornado(bool sword)
        {
            var (p, c, t, enemies) = Start(Enemy(99, 2), Enemy(4));
            Play(p, c, false, sword ? 0 : 1);
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c);
            if (sword)
            {
                var instance = new DeedCard("items_008", CardType.Artifact); p.Deck.Hand.Add(instance);
                CardPlayActions.PlayArtifact(p, c, instance, CardJsonLoader.LoadItems().Single(i => i.Id == instance.Id), true);
            }
            else
            {
                p.Mana.AddToken(ManaColor.Red);
                var data = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_008");
                var instance = new DeedCard(data.Id, CardType.Spell); p.Deck.Hand.Add(instance);
                CardPlayActions.Play(p, c, instance, new ActionCardData(data.Id, data.Name, data.Set, data.ImagePath,
                    data.EnImagePath, data.Top.Effect, data.Bottom.Effect, data.Top.ManaCost), true);
            }
            t.SetBlock(c.BlockPool, Element.Fire); var defense = t.ResolveDefense(enemies, c.CombatPower.AvailableBlocks(0), c);
            Assert.That(defense.TotalWounds, Is.Zero);
            if (sword)
            {
                Play(p, c, true); Assert.That(p.Fame, Is.EqualTo(4));
                var fury = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(fury);
                CardPlayActions.Play(p, c, fury, CardJsonLoader.LoadBasicActions().Single(a => a.Id == fury.Id));
                Assert.That(t.Attack(new[] { 1 }, c).Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(8));
            }
            else
            {
                Assert.That(defense.KilledIndices, Is.EqualTo(new[] { 0 }));
                Assert.Throws<InvalidOperationException>(() => Play(p, c, true)); Assert.That(p.Fame, Is.EqualTo(3));
            }
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(sword ? 1 : 2));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(2));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(sword ? 3 : 2));
        }
    }
}
