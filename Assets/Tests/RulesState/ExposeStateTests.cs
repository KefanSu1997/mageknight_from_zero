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
    public sealed class ExposeStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, params Ability[] traits) => new("target", armor, 0, Element.Physical, 3, traits);
        private static void Play(PlayerState p, ActionContext c, string id = "magic_019", bool strong = false, int option = 0)
        {
            ActionCardData data;
            if (id.StartsWith("magic"))
            {
                var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
                data = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath, spell.EnImagePath,
                    spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            }
            else data = CardJsonLoader.LoadBasicActions().Single(a => a.Id == id);
            var card = new DeedCard(id, id.StartsWith("magic") ? CardType.Spell : CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, data, strong, option);
        }
        private static (PlayerState p, ActionContext c, TurnMachine t, List<Monster> enemies) Start(params Monster[] enemies)
        {
            var p = new PlayerState(); foreach (ManaColor color in Enum.GetValues(typeof(ManaColor))) p.Mana.AddToken(color, 3);
            var list = enemies.ToList(); var c = new ActionContext { DayPart = DayPart.Night, Enemies = list };
            var g = new GameState(); g.Players.Add(p); var t = new TurnMachine(g); t.BeginBattle(c);
            return (p, c, t, list);
        }
        private static void Defend(TurnMachine t, ActionContext c, List<Monster> enemies)
        {
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
        }
        private static AttackProfile[] Ranged(int power = 2) => new[] { new AttackProfile(AttackType.Ranged, power, Element.Physical) };
        private static int Effective(List<Monster> enemies, int target, ActionContext c, Element element = Element.Physical)
            => CombatMath.EffectiveAttack(enemies, new[] { target }, new[] { new AttackProfile(AttackType.Melee, 4, element) }, Phase.Melee, c);

        [TestCase(0)] [TestCase(1)]
        public void BaseOnlyRemovesChosenEnemyProtections(int target)
        {
            var (p, c, t, enemies) = Start(Enemy(2, Ability.Fortified, Ability.PhysicalResist), Enemy(2, Ability.Fortified, Ability.PhysicalResist));
            c.FortifiedSite = true; Play(p, c, option: target);
            Assert.That(c.IgnoreFortified, Is.False); Assert.That(c.IgnoreResist, Is.False);
            Assert.That(CombatMath.CanAttack(enemies, new[] { target }, Ranged(), Phase.Ranged, c), Is.True);
            Assert.That(CombatMath.CanAttack(enemies, new[] { 1-target }, Ranged(), Phase.Ranged, c), Is.False);
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0,1 }, Ranged(), Phase.Ranged, c), Is.False);
            Assert.That(Effective(enemies, target, c), Is.EqualTo(4)); Assert.That(Effective(enemies, 1-target, c), Is.EqualTo(2));
            Assert.That(CardCombatActions.Attack(p, c, enemies, new[] { target }, Phase.Ranged).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(3)); Assert.That(c.RangedPool, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)]
        public void MassChoiceRemovesOnlyFortificationsOrOnlyResistances(int option)
        {
            var (p, c, t, enemies) = Start(Enemy(2, Ability.Fortified, Ability.PhysicalResist), Enemy(2, Ability.Fortified, Ability.PhysicalResist, Ability.MagicResist));
            c.FortifiedSite = true; Play(p, c, strong: true, option: option);
            Assert.That(c.IgnoreFortified, Is.False); Assert.That(c.IgnoreResist, Is.False);
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0 }, Ranged(), Phase.Ranged, c), Is.EqualTo(option == 0));
            Assert.That(Effective(enemies, 0, c), Is.EqualTo(option == 1 ? 4 : 2));
            Assert.That(CombatMath.CanAttack(enemies, new[] { 1 }, Ranged(), Phase.Ranged, c), Is.False);
            Assert.That(Effective(enemies, 1, c), Is.EqualTo(2));
            if (option == 0) Assert.That(CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged).Kills, Is.Zero);
            else Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged));
            Defend(t, c, enemies); Play(p, c, "basic_card_021");
            Assert.That(t.Attack(new[] { 0 }, c).Kills, Is.EqualTo(option == 1 ? 1 : 0));
            Assert.That(p.Fame, Is.EqualTo(option == 1 ? 3 : 0));
        }

        [TestCase(false)] [TestCase(true)]
        public void ArcaneImmunityKeepsProtectionsButPrintedAttackStillWorks(bool fortified)
        {
            var (p, c, t, enemies) = Start(Enemy(1, fortified ? new[] { Ability.MagicResist, Ability.PhysicalResist, Ability.Fortified } : new[] { Ability.MagicResist, Ability.PhysicalResist }));
            Play(p, c); Assert.That(c.RangedPool, Is.EqualTo(2));
            Assert.That(Effective(enemies, 0, c), Is.EqualTo(2));
            if (fortified) Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged));
            else
            {
                var attack = CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged);
                Assert.That(attack.EffectivePower, Is.EqualTo(1)); Assert.That(attack.Kills, Is.EqualTo(1));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ResistanceRemovalEnablesOriginalBlueAndRedSpells(bool strong)
        {
            var (p, c, t, enemies) = Start(Enemy(9, Ability.FireResist, Ability.IceResist));
            Play(p, c, strong: strong, option: strong ? 1 : 0); Defend(t, c, enemies);
            Play(p, c, "magic_013"); Play(p, c, "magic_007", true);
            Assert.That(c.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 })); Assert.That(p.Fame, Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(strong ? 1 : 2));
            t.FinishBattle(c); c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(3)); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(0)] [TestCase(2)]
        public void SurvivingExposedEnemyKeepsModifiersAfterRangedCasualty(int killed)
        {
            var target = Enemy(2, Ability.Fortified, Ability.PhysicalResist);
            var (p, c, t, enemies) = Start(Enemy(1), target, Enemy(1)); Play(p, c, option: 1);
            var parts = c.CombatPower.AvailableAttacks(Phase.Ranged);
            t.ApplyRanged(enemies, new[] { new AttackAllocation(new[] { killed }, 2, Element.Physical, Components: parts) }, c);
            int index = enemies.FindIndex(e => ReferenceEquals(e, target));
            Assert.That(CombatMath.CanAttack(enemies, new[] { index }, Ranged(), Phase.Ranged, c), Is.True);
            t.SetBlock(0, Element.Physical); t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
            Play(p, c, "basic_card_021"); Assert.That(t.Attack(new[] { index }, c).Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(6));
        }

        [TestCase(-1)] [TestCase(0)] [TestCase(1)]
        public void BothProtectionsReturnWhenThisCombatEnds(int mode)
        {
            var (p, c, t, enemies) = Start(Enemy(9, Ability.Fortified, Ability.PhysicalResist, Ability.FireResist, Ability.IceResist));
            c.FortifiedSite = true; Play(p, c, strong: mode >= 0, option: Math.Max(0, mode));
            Defend(t, c, enemies); t.FinishBattle(c);
            Assert.That(CombatMath.CanAttack(enemies, new[] { 0 }, Ranged(), Phase.Ranged, c), Is.False);
            foreach (Element element in new[] { Element.Physical, Element.Fire, Element.Ice, Element.ColdFire })
                Assert.That(Effective(enemies, 0, c, element), Is.EqualTo(2));
            Assert.That(c.RangedPool, Is.Zero); t.BeginBattle(c);
            Assert.That(Assert.Throws<InvalidOperationException>(() => Play(p, c, "magic_013")).Message, Is.EqualTo("冰霜抗性敌人不能成为冰冻目标"));
            Play(p, c, "basic_card_007", true);
            Assert.That(Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, enemies, new[] { 0 }, Phase.Ranged)).Message,
                Is.EqualTo("城防限制：当前攻击不能指定这些目标")); Assert.That(c.RangedPool, Is.EqualTo(3));
        }

        [TestCase(false,-1)] [TestCase(false,1)] [TestCase(true,-1)] [TestCase(true,2)]
        public void InvalidSelectionDoesNotPayOrGrantAttack(bool strong,int option)
        {
            var (p,c,t,enemies)=Start(Enemy(3));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong:strong,option:option));
            Assert.That(p.Mana.Tokens.Values.All(v=>v==3),Is.True); Assert.That(c.RangedPool,Is.Zero);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [Test]
        public void DefeatedTargetRejectsWithoutChangingAnotherExposedEnemy()
        {
            var (p,c,t,enemies)=Start(Enemy(2),Enemy(4,Ability.PhysicalResist));
            Play(p,c,option:1); Defend(t,c,enemies); Play(p,c,"basic_card_021");
            Assert.That(t.Attack(new[]{0},c).Kills,Is.EqualTo(1));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Play(p,c)).Message,Is.EqualTo("目标已经被击败"));
            Assert.That(p.Mana.Tokens[ManaColor.White],Is.EqualTo(2)); Assert.That(c.RangedPool,Is.Zero);
            Assert.That(c.ResistancesRemoved,Is.EquivalentTo(new[]{1})); Assert.That(p.Fame,Is.EqualTo(3));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2)); Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo("magic_019"));
        }

        [TestCase(false)] [TestCase(true)]
        public void OutsideCombatDoesNotPayOrGrantAttack(bool strong)
        {
            var (p,c,t,enemies)=Start(Enemy(3)); c.InBattle=false;
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong:strong)).Message,Is.EqualTo("暴露必须在战斗中施放"));
            Assert.That(p.Mana.Tokens.Values.All(v=>v==3),Is.True); Assert.That(c.RangedPool,Is.Zero); Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
    }
}
