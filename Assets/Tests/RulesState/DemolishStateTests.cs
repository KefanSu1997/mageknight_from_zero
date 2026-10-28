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
    public sealed class DemolishStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, int attack = 0, params Ability[] traits) => new("target", armor, attack, Element.Physical, 3, traits);
        private static DeedCard Play(PlayerState p, ActionContext c, bool strong, int target = 0)
        {
            var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_007");
            var instance = new DeedCard(spell.Id, CardType.Spell); p.Deck.Hand.Add(instance);
            CardPlayActions.Play(p, c, instance, new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost), strong, target);
            return instance;
        }
        private static (PlayerState p, ActionContext c, TurnMachine t, List<Monster> enemies) Start(params Monster[] enemies)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red, 3); p.Mana.AddToken(ManaColor.Black, 3);
            var list = enemies.ToList(); var c = new ActionContext { DayPart = DayPart.Night, Enemies = list, FortifiedSite = true };
            var g = new GameState(); g.Players.Add(p); var t = new TurnMachine(g); t.BeginBattle(c);
            return (p, c, t, list);
        }
        private static void Defend(TurnMachine t, ActionContext c, List<Monster> enemies)
        {
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
        }
        private static void Fury(PlayerState p, ActionContext c)
        {
            var card = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(a => a.Id == card.Id));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SiteFortificationIsIgnoredButEnemyFortificationStillNeedsSiege(int mode)
        {
            bool intrinsic = mode != 0;
            var (p, c, t, enemies) = Start(Enemy(3, 0, intrinsic ? Ability.Fortified : Ability.FireResist));
            Play(p, c, false);
            Assert.That(c.IgnoreFortified, Is.False, "拆毁没有忽略怪物自身城防的许可");
            var ordinary = new[] { new AttackProfile(AttackType.Ranged, 3, Element.Physical) };
            var siege = new[] { new AttackProfile(AttackType.Siege, 3, Element.Physical) };
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0 }, ordinary, Phase.Ranged, c), Is.EqualTo(!intrinsic));
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0 }, siege, Phase.Ranged, c), Is.True);
            p.Mana.AddToken(mode == 1 ? ManaColor.Green : ManaColor.White);
            var data = mode == 1 ? CardJsonLoader.LoadAdvancedActions().Single(a => a.Id == "advanced_card_003")
                : CardJsonLoader.LoadBasicActions().Single(a => a.Id == "basic_card_007");
            var attackCard = new DeedCard(data.Id, CardType.Action); p.Deck.Hand.Add(attackCard);
            CardPlayActions.Play(p, c, attackCard, data, true);
            if (mode == 2)
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged)).Message,
                    Is.EqualTo("城防限制：当前攻击不能指定这些目标"));
                Assert.That(c.RangedPool, Is.EqualTo(3)); Assert.That(p.Fame, Is.Zero);
            }
            else
            {
                Assert.That(CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged).Kills, Is.EqualTo(1));
                Assert.That(p.Fame, Is.EqualTo(3));
            }
        }

        [TestCase(DayPart.Day)] [TestCase(DayPart.Night)]
        public void ArmorReductionStacksHasMinimumAndExpiresAtTurnEnd(DayPart day)
        {
            var (p, c, t, enemies) = Start(Enemy(3), Enemy(1), Enemy(5, 0, Ability.FireResist), Enemy(5, 0, Ability.MagicResist), Enemy(5, 0, Ability.IceResist));
            c.DayPart = day; Play(p, c, false); Play(p, c, false);
            Assert.That(enemies.Select((e, i) => CombatMath.EffectiveArmor(e, i, c)), Is.EqualTo(new[] { 1, 1, 5, 5, 3 }));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            Defend(t, c, enemies); t.FinishBattle(c);
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(1), "减甲持续到本回合结束");
            c.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, day, new ManaSource(1, () => day), c);
            Assert.That(c.ArmorReduction, Is.Empty); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(2));
            t.BeginBattle(c);
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0 }, new[] { new AttackProfile(AttackType.Ranged, 3, Element.Physical) }, Phase.Ranged, c), Is.False);
        }

        [Test]
        public void RemovedFireResistanceAllowsRedSpellEffects()
        {
            var (p, c, t, enemies) = Start(Enemy(9, 0, Ability.FireResist), Enemy(3, 0, Ability.FireResist));
            c.FireResistRemoved.UnionWith(new[] { 0, 1 }); Defend(t, c, enemies); Play(p, c, true);
            Assert.That(c.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 }));
            Assert.That(CombatMath.EffectiveArmor(enemies[1], 1, c), Is.EqualTo(2)); Assert.That(p.Fame, Is.EqualTo(3));
        }

        [Test]
        public void OriginalChillRemovesFireResistanceBeforeDisintegrate()
        {
            var (p, c, t, enemies) = Start(Enemy(9, 0, Ability.FireResist), Enemy(3, 0, Ability.FireResist));
            Play(p, c, false); Defend(t, c, enemies); p.Mana.AddToken(ManaColor.Blue);
            var chill = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_013");
            var card = new DeedCard(chill.Id, CardType.Spell); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, new ActionCardData(chill.Id, chill.Name, chill.Set, chill.ImagePath,
                chill.EnImagePath, chill.Top.Effect, chill.Bottom.Effect, chill.Top.ManaCost), false);
            Assert.That(c.FireResistRemoved, Is.EquivalentTo(new[] { 0 }));
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(9));
            Play(p, c, true);
            Assert.That(c.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 })); Assert.That(p.Fame, Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveArmor(enemies[1], 1, c), Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(2));
        }

        [Test]
        public void DisintegrateActuallyDestroysThenOtherArmorEnablesOriginalFury()
        {
            var (p, c, t, enemies) = Start(Enemy(99, 4, Ability.Fortified, Ability.IceResist), Enemy(3), Enemy(3, 0, Ability.FireResist), Enemy(3, 0, Ability.MagicResist));
            Defend(t, c, enemies); Fury(p, c); var attack = c.CombatPower.Attacks.Single(); var spell = Play(p, c, true);
            Assert.That(c.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 })); Assert.That(p.Fame, Is.EqualTo(3));
            Assert.That(p.Wounds, Is.EqualTo(2)); Assert.That(c.CombatPower.Attacks.Single(), Is.EqualTo(attack));
            Assert.That(c.IgnoreFortified, Is.False);
            Assert.That(enemies.Select((e, i) => CombatMath.EffectiveArmor(e, i, c)), Is.EqualTo(new[] { 99, 2, 3, 3 }));
            Assert.That(CombatMath.CanAttack(enemies, new[] { 1 }, new[] { new AttackProfile(AttackType.Ranged, 3, Element.Physical) }, Phase.Ranged, c), Is.False);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, true)).Message, Is.EqualTo("目标已经被击败"));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(2));
            Assert.That(t.Attack(new[] { 1 }, c).Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(6));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.DiscardPile.Contains(spell), Is.True); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(3)); Assert.That(c.ArmorReduction, Is.Empty);
        }

        [TestCase(-1)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidDisintegrateTargetPreservesResources(int failure)
        {
            var (p, c, t, enemies) = Start(Enemy(5, 0, failure == 2 ? Ability.MagicResist : failure == 3 ? Ability.FireResist : Ability.IceResist));
            Defend(t, c, enemies); if (failure == 4) c.InBattle = false;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, failure < 2 ? failure : 0));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("magic_007")); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(c.ArmorReduction, Is.Empty); Assert.That(c.DefeatedEnemyIndices, Is.Empty); Assert.That(p.Fame, Is.Zero);
            Assert.That(c.IgnoreFortified, Is.False);
        }

        [TestCase(Phase.None)] [TestCase(Phase.Ranged)] [TestCase(Phase.Block)] [TestCase(Phase.AssignDamage)] [TestCase(Phase.End)]
        public void DisintegrateRejectsOtherPhasesBeforePayment(Phase phase)
        {
            var (p, c, t, enemies) = Start(Enemy(5)); c.CombatPhase = phase;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, true)).Message, Is.EqualTo("解离只能在攻击阶段打出"));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(3));
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(c.ArmorReduction, Is.Empty); Assert.That(c.DefeatedEnemyIndices, Is.Empty);
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)]
        public void PrintedManaAndNightRequirementsAreAtomic(bool strong, int failure)
        {
            var (p, c, t, enemies) = Start(Enemy(5)); Defend(t, c, enemies);
            if (failure == 0) p.Mana.Tokens[ManaColor.Red] = 0;
            if (failure == 1)
            {
                p.Mana.Tokens[ManaColor.Black] = 0; p.Mana.Tokens[ManaColor.Red] = 0;
                p.Mana.Crystals[ManaColor.Red] = 1;
            }
            if (failure == 2) c.DayPart = DayPart.Day;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, strong)).Message,
                Is.EqualTo(failure == 2 ? "白昼不能施放强效法术" : "法力不足，不能支付所选效果"));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(failure <= 1 ? 0 : 3));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(failure == 1 ? 0 : 3));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(failure == 1 ? 1 : 0));
            Assert.That(c.ArmorReduction, Is.Empty); Assert.That(c.DefeatedEnemyIndices, Is.Empty); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void RedCrystalPaysPrintedCostWhenTokenIsAbsent(bool strong)
        {
            var (p, c, t, enemies) = Start(Enemy(5)); Defend(t, c, enemies);
            p.Mana.Tokens[ManaColor.Red] = 0; p.Mana.Crystals[ManaColor.Red] = 1;
            Play(p, c, strong);
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.Zero); Assert.That(p.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(strong ? 2 : 3));
            Assert.That(p.Fame, Is.EqualTo(strong ? 3 : 0)); Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(1));
            Assert.That(c.DefeatedEnemyIndices.Count, Is.EqualTo(strong ? 1 : 0));
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(strong ? 5 : 4));
        }
    }
}
