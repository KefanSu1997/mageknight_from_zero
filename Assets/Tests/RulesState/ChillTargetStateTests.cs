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
    public sealed class ChillTargetStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static Monster Enemy(int armor, int attack = 0, params Ability[] abilities)
            => new("enemy", armor, attack, Element.Physical, 3, abilities);

        private static void Play(PlayerState p, ActionContext c, string id, bool strong = false, int target = 0)
        {
            ActionCardData data;
            if (id.StartsWith("magic"))
            {
                var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
                data = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath, spell.EnImagePath,
                    spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            }
            else data = CardJsonLoader.LoadBasicActions().Single(a => a.Id == id);
            var card = new DeedCard(id, id.StartsWith("magic") ? CardType.Spell : CardType.Action);
            p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, data, strong, target);
        }

        private static (PlayerState p, ActionContext c, TurnMachine t) Start(List<Monster> enemies)
        {
            var p = new PlayerState();
            foreach (ManaColor color in Enum.GetValues(typeof(ManaColor))) p.Mana.AddToken(color, 3);
            var g = new GameState(); g.Players.Add(p);
            var c = new ActionContext { Enemies = enemies, DayPart = DayPart.Night };
            var t = new TurnMachine(g); t.BeginBattle(c);
            return (p, c, t);
        }

        private static void Ranged(TurnMachine t, ActionContext c, List<Monster> enemies, int target)
        {
            var parts = c.CombatPower.AvailableAttacks(Phase.Ranged);
            t.ApplyRanged(enemies, new[] { new AttackAllocation(new[] { target }, parts.Sum(a => a.Value), Element.Fire, Components: parts) }, c);
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(false, 2)] [TestCase(true, 2)]
        public void SurvivingChilledEnemyKeepsItsOwnModifiersAfterAnotherEnemyDies(bool strong, int removed)
        {
            var target = Enemy(strong ? 6 : 5, 4, Ability.FireResist);
            var enemies = new List<Monster> { Enemy(1), target, Enemy(1) };
            var (p, c, t) = Start(enemies);
            Play(p, c, "magic_013", strong, 1);
            Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(2));
            Play(p, c, "magic_009"); Ranged(t, c, enemies, removed);
            int targetIndex = enemies.FindIndex(e => ReferenceEquals(e, target));
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[] { targetIndex }));
            t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.Zero);
            Play(p, c, strong ? "basic_card_021" : "magic_009");
            var result = t.Attack(new[] { targetIndex }, c);
            Assert.That(result.EffectivePower, Is.EqualTo(strong ? 2 : 5));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(6));
            t.FinishBattle(c);
        }

        [TestCase(false)] [TestCase(true)]
        public void DefeatedChillTargetCannotPassItsModifiersToTheNextEnemy(bool strong)
        {
            var survivor = Enemy(5, 4, Ability.FireResist);
            var enemies = new List<Monster> { Enemy(1), survivor };
            var (p, c, t) = Start(enemies);
            Play(p, c, "magic_013", strong); Play(p, c, "magic_009"); Ranged(t, c, enemies, 0);
            Assert.That(c.SkipAttackIndices, Is.Empty);
            Assert.That(c.FireResistRemoved, Is.Empty); Assert.That(c.ArmorReduction, Is.Empty);
            t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.EqualTo(2));
            Play(p, c, "magic_009");
            Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(3));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void CombatSkipEndsBeforeTurnModifiersAndNextTurnRestoresOriginalEnemy(bool strong, bool skipDraw)
        {
            var enemies = new List<Monster> { Enemy(6, 4, Ability.FireResist) };
            var (p, c, t) = Start(enemies);
            Play(p, c, "magic_013", strong);
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.Zero);
            t.FinishBattle(c);
            Assert.That(c.SkipAttackIndices, Is.Empty, "停攻只限本场战斗");
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(strong ? 2 : 6));
            Assert.That(c.FireResistRemoved.Contains(0), Is.EqualTo(!strong));
            c.SkipDraw = skipDraw;
            var turns = new PlayerTurnEngine();
            turns.EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night, new Random(7)), c);
            Assert.That(c.ArmorReduction, Is.Empty); Assert.That(c.FireResistRemoved, Is.Empty);
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.DiscardPile.Single().Id, Is.EqualTo("magic_013"));
            Assert.That(p.Mana.Tokens.Values.All(v => v == 0), Is.True);
            turns.StartTurn(p, DayPart.Night);
            t.BeginBattle(c); t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.EqualTo(2));
            if (!strong) Play(p, c, "basic_card_002", target: 2);
            Play(p, c, strong ? "basic_card_021" : "magic_009");
            var attack = t.Attack(new[] { 0 }, c);
            Assert.That(attack.RequiredPower, Is.EqualTo(6)); Assert.That(attack.EffectivePower, Is.EqualTo(2));
            Assert.That(attack.Kills, Is.Zero); Assert.That(p.Fame, Is.Zero);
        }

        [Test]
        public void MultipleDeathsPreserveSurvivorModifiersAndPlayerSentinel()
        {
            var enemies = new List<Monster> { Enemy(1), Enemy(6), Enemy(1), Enemy(6) };
            var (p, c, t) = Start(enemies);
            Play(p, c, "magic_013", true, 3);
            c.EnemyAttackAdjust[1] = -1; c.EnemyAttackAdjust[2] = 2;
            c.IgnoredAbilities[3] = Ability.Swift;
            c.AttackElementChange[-1] = Element.Fire; c.AttackElementChange[1] = Element.Ice;
            c.SpellTargetIndices.AddRange(new[] { 3, 2, 1 }); c.NightWingsSkippedIndices.Add(3);
            c.KillEnemyIndex = 2;
            Play(p, c, "magic_009");
            var parts = c.CombatPower.AvailableAttacks(Phase.Ranged);
            t.ApplyRanged(enemies, new[] { new AttackAllocation(new[] { 0, 2 }, 5, Element.Fire, Components: parts) }, c);
            Assert.That(enemies.Count, Is.EqualTo(2));
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[] { 1 }));
            Assert.That(c.NightWingsSkippedIndices, Is.EquivalentTo(new[] { 1 }));
            Assert.That(c.ArmorReduction.Single().Key, Is.EqualTo(1));
            Assert.That(c.EnemyAttackAdjust.Single().Key, Is.Zero);
            Assert.That(c.EnemyAttackAdjust[0], Is.EqualTo(-1));
            Assert.That(c.IgnoredAbilities[1], Is.EqualTo(Ability.Swift));
            Assert.That(c.AttackElementChange[-1], Is.EqualTo(Element.Fire));
            Assert.That(c.AttackElementChange[0], Is.EqualTo(Element.Ice));
            Assert.That(c.SpellTargetIndices, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(c.KillEnemyIndex, Is.Null);
        }

        [TestCase(false, -1)] [TestCase(true, -1)] [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 2)] [TestCase(true, 2)] [TestCase(false, 3)] [TestCase(true, 3)]
        public void IllegalTargetDoesNotPayOrMoveTheOriginalCard(bool strong, int failure)
        {
            var enemies = new List<Monster> { Enemy(5, 4, failure == 2 ? Ability.MagicResist : Ability.FireResist) };
            var (p, c, t) = Start(enemies);
            if (failure == 3) c.InBattle = false;
            int target = failure < 2 ? failure : 0;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_013", strong, target));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Mana.Tokens.Values.All(v => v == 3), Is.True);
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.ArmorReduction, Is.Empty);
            Assert.That(c.FireResistRemoved, Is.Empty);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void IceResistanceRejectsBothHalvesBeforePayment(bool strong, bool fireToo)
        {
            var enemies = new List<Monster> { Enemy(6, 4, fireToo ? new[] { Ability.IceResist, Ability.FireResist } : new[] { Ability.IceResist }) };
            var (p, c, t) = Start(enemies);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_013", strong)).Message,
                Is.EqualTo("冰霜抗性敌人不能成为冰冻目标"));
            Assert.That(p.Mana.Tokens.Values.All(v => v == 3), Is.True);
            Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("magic_013")); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.FireResistRemoved, Is.Empty); Assert.That(c.ArmorReduction, Is.Empty);
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.EqualTo(2));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void ActuallyDefeatedEnemyCannotBeChilled(bool strong, bool destroyed)
        {
            var enemies = new List<Monster> { Enemy(2), Enemy(6, 0, Ability.FireResist) };
            var (p, c, t) = Start(enemies);
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
            Play(p, c, "magic_013", strong, 1);
            Play(p, c, destroyed ? "magic_022" : "basic_card_021", destroyed);
            if (!destroyed) Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.EqualTo(1));
            int black = p.Mana.Tokens[ManaColor.Black]; int played = p.Deck.PlayedCards.Count;
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_013", strong)).Message, Is.EqualTo("目标已经被击败"));
            Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(black));
            Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(played)); Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("magic_013"));
            Assert.That(c.SkipAttackIndices, Is.EquivalentTo(new[] { 1 }));
            Assert.That(c.FireResistRemoved.Contains(1), Is.EqualTo(!strong)); Assert.That(c.FireResistRemoved.Contains(0), Is.False);
            Assert.That(c.ArmorReduction.GetValueOrDefault(1), Is.EqualTo(strong ? 4 : 0)); Assert.That(c.ArmorReduction.ContainsKey(0), Is.False);
            Assert.That(p.Fame, Is.EqualTo(3)); Assert.That(c.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 }));
        }

        [TestCase(false)] [TestCase(true)]
        public void CastingAfterDamageDoesNotUndoWoundsAndStillEnablesOriginalAttack(bool strong)
        {
            var enemies = new List<Monster> { Enemy(strong ? 6 : 5, 4, Ability.FireResist) };
            var (p, c, t) = Start(enemies);
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.EqualTo(2));
            Play(p, c, "magic_013", strong); Play(p, c, strong ? "basic_card_021" : "magic_009");
            var result = t.Attack(new[] { 0 }, c);
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(result.EffectivePower, Is.EqualTo(strong ? 2 : 5));
            Assert.That(p.Wounds, Is.EqualTo(2)); Assert.That(p.Fame, Is.EqualTo(3));
            Assert.That(c.FireResistRemoved.Contains(0), Is.EqualTo(!strong));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(2)); Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.Hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void TwoLethalChillsStackToMinimumOneWithoutRemovingFireResistance()
        {
            var enemies = new List<Monster> { Enemy(6, 4, Ability.FireResist) };
            var (p, c, t) = Start(enemies);
            Play(p, c, "magic_013", true); Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(2));
            Play(p, c, "magic_013", true); Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(1));
            Assert.That(c.FireResistRemoved, Is.Empty); Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(1));
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            Assert.That(t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c).TotalWounds, Is.Zero);
            Play(p, c, "basic_card_021"); Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.EqualTo(1));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(3)); Assert.That(c.ArmorReduction, Is.Empty); Assert.That(p.Wounds, Is.Zero);
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)]
        [TestCase(false, 3)] [TestCase(true, 3)]
        public void OriginalBlueBlackCostsAndCrystalsAreAtomic(bool strong, int mode)
        {
            var enemies = new List<Monster> { Enemy(6) };
            var (p, c, t) = Start(enemies);
            if (mode != 2) p.Mana.Tokens[ManaColor.Blue] = 0;
            if (mode == 1 || mode == 3) p.Mana.Crystals[ManaColor.Blue] = 1;
            if (mode == 1) p.Mana.Tokens[ManaColor.Black] = 0;
            if (mode == 2) c.DayPart = DayPart.Day;
            if (mode == 3) Play(p, c, "magic_013", strong);
            else Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_013", strong)).Message,
                Is.EqualTo(mode == 2 ? "白昼不能施放强效法术" : "法力不足，不能支付所选效果"));
            Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(mode == 2 ? 3 : 0));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(mode == 1 ? 1 : 0));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(mode == 1 ? 0 : mode == 3 && strong ? 2 : 3));
            Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(mode == 3 ? 1 : 0));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(mode == 3 ? 0 : 1));
            Assert.That(c.SkipAttackIndices.Count, Is.EqualTo(mode == 3 ? 1 : 0));
            Assert.That(CombatMath.EffectiveArmor(enemies[0], 0, c), Is.EqualTo(mode == 3 && strong ? 2 : 6));
        }
    }
}
