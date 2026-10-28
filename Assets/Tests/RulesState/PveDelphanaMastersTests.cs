using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveDelphanaMastersTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(params string[] ids)
        {
            var p = new PlayerState { Fame = 8 };
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_005")));
            var originals = CardJsonLoader.LoadMonsters();
            return new PveBattleSession(p, ids.Select(id => originals.Single(x => x.Id == id) with { }), CardJsonLoader.LoadBasicActions());
        }
        private static void Play(PveBattleSession s, string id, string choice = "base")
        {
            var card = new DeedCard(id, CardType.Action); s.Player.Deck.Hand.Add(card); s.Play(card, choice);
        }
        private static void Sideways(PveBattleSession s, string id)
        {
            var card = new DeedCard(id, CardType.Action); s.Player.Deck.Hand.Add(card); s.PlaySideways(card);
        }

        [TestCase("monster_017")] [TestCase("monster_004")]
        public void PrintedWhiteAndGreenBothFeedActualOnePointRangedAttack(string enemyId)
        {
            var s = Create(enemyId); var u = s.Player.Units[0]; var p = s.Player;
            Assert.That(u.Card.Level, Is.EqualTo(4)); Assert.That(u.Card.Armor, Is.EqualTo(3));
            Assert.That(u.Card.InfluenceCost, Is.EqualTo(13)); Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.City));
            Assert.That(u.Card.Abilities, Is.EquivalentTo(new[] { Ability.PhysicalResist, Ability.FireResist, Ability.IceResist }));
            Assert.That(u.Card.InteractionRecruitOnly, Is.True); Assert.That(u.Card.Actions.Length, Is.EqualTo(4));
            Assert.That(u.Card.Actions.SelectMany(a => a.Cost), Is.EqualTo(new[] { ManaColor.Blue, ManaColor.Red, ManaColor.Green, ManaColor.White }));
            p.Mana.AddCrystal(ManaColor.White); p.Mana.AddToken(ManaColor.Green, 2);
            s.UseUnit(u, 3, target: 0); Assert.That(u.IsReady, Is.False);
            s.UseUnit(u, 2, target: 0); Assert.That(u.IsReady, Is.False);
            Assert.That(CombatMath.EffectiveArmor(s.Enemies[0], 0, s.Context), Is.EqualTo(1));
            Assert.That(CombatMath.IsFortified(s.Enemies[0], 0, s.Context), Is.False);
            Assert.That(CombatMath.HasResistance(s.Enemies[0], 0, Element.Physical, s.Context), Is.False);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, target: 0));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.Zero);
            Play(s, "basic_card_011"); Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(p.Wounds, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(8 + s.Enemies[0].Fame));
            Assert.That(s.Context.UnitArmorReduction(s.Enemies[0]), Is.Zero);
            Assert.That(s.Context.ResistancesRemoved, Is.Empty);
        }

        [Test]
        public void SameMasterContinuesAcrossPhasesAndTargetsWithoutResettingPriorEffects()
        {
            var s = Create("monster_004", "monster_054"); var p = s.Player; var u = p.Units[0];
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Blue);
            s.UseUnit(u, 3, target: 0); s.Advance();
            // The other original enemy still resists blue: rejecting it must preserve the first effect.
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 0, target: 1));
            Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(s.Context.ResistancesRemoved, Is.EquivalentTo(new[] { 0 }));
            Assert.That(DelphanaMastersActions.Used(s.Context, u, 0), Is.False);
            s.UseUnit(u, 0, target: 0);
            Assert.That(s.Context.IsAttackCancelled(0, 0), Is.True); Assert.That(u.IsReady, Is.False);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(DelphanaMastersActions.Used(s.Context, u, 0), Is.True);
            Assert.That(DelphanaMastersActions.Used(s.Context, u, 3), Is.True);
        }

        [Test]
        public void AllFourDifferentAbilitiesContinueAcrossPhasesWithActualPaymentsAndConsumers()
        {
            var s = Create("monster_017", "monster_032"); var p = s.Player; var masters = p.Units[0];
            var hero = new UnitState(CardJsonLoader.LoadUnit("army_008")); p.Units.Add(hero);
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Green);
            p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Blue, 2);
            s.UseUnit(masters, 3, target: 0); s.Advance();
            s.UseUnit(masters, 0, target: 1); s.UseUnit(masters, 1, target: 0);
            s.UseUnit(hero, 1); s.Block(0);
            s.Advance(); s.Advance();
            Assert.That(p.Wounds, Is.Zero); Assert.That(s.Context.DefeatedEnemyIndices, Is.EquivalentTo(new[] { 0 }));
            Assert.That(p.Fame, Is.EqualTo(11)); Assert.That(s.Context.IsAttackCancelled(1, 0), Is.True);
            s.UseUnit(masters, 2, target: 1); Play(s, "basic_card_021", "attack");
            Assert.That(s.Attack(1).Kills, Is.EqualTo(1)); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(p.Fame, Is.EqualTo(13));
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(p.Units.All(u => !u.IsReady), Is.True);
            Assert.That(Enumerable.Range(0, 4).All(i => DelphanaMastersActions.Used(s.Context, masters, i)), Is.True);
            s.Context.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, s.Context);
            Assert.That(Enumerable.Range(0, 4).Any(i => DelphanaMastersActions.Used(s.Context, masters, i)), Is.False);
            Assert.That(masters.IsReady, Is.False, "普通回合结束不能重整部队");
        }

        [TestCase(true)] [TestCase(false)]
        public void RedDestroysOriginalMultiAttackEnemyOnlyWhenEveryAttackIsReallyBlocked(bool both)
        {
            var s = Create("monster_058"); var p = s.Player; var u = p.Units[0];
            Assert.That(s.Enemies[0].AttackCount, Is.EqualTo(2));
            p.Mana.AddToken(ManaColor.Red); s.Advance(); s.UseUnit(u, 1, target: 0);
            Assert.That(s.Context.DefeatedEnemyIndices, Is.Empty); Assert.That(p.Fame, Is.EqualTo(8));
            Sideways(s, "basic_card_000"); s.Block(0, 0);
            if (both) { Sideways(s, "basic_card_011"); s.Block(0, 1); }
            s.Advance(); s.Advance();
            Assert.That(s.Context.DefeatedEnemyIndices.Contains(0), Is.EqualTo(both));
            Assert.That(p.Fame, Is.EqualTo(8 + (both ? s.Enemies[0].Fame : 0)));
            Assert.That(p.Wounds, Is.EqualTo(both ? 0 : 1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            s.Advance(); Assert.That(s.Won, Is.EqualTo(both));
            Assert.That(DelphanaMastersActions.CanContinue(s.Context, u), Is.False);
        }

        [Test]
        public void RedThenBlueOnSameEnemyPreventsDamageButDoesNotCountAsSuccessfulBlock()
        {
            var s = Create("monster_032"); var p = s.Player; var u = p.Units[0];
            p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Blue);
            s.Advance(); s.UseUnit(u, 1, target: 0); s.UseUnit(u, 0, target: 0);
            s.Advance(); s.Advance();
            Assert.That(p.Wounds, Is.Zero); Assert.That(p.Fame, Is.EqualTo(8));
            Assert.That(s.Context.DefeatedEnemyIndices, Is.Empty); Assert.That(s.Context.SuccessfullyBlockedAttacks, Is.Empty);
        }

        [TestCase("monster_004", 0, ManaColor.Blue)]
        [TestCase("monster_029", 1, ManaColor.Red)]
        [TestCase("monster_014", 2, ManaColor.Green)]
        [TestCase("monster_014", 3, ManaColor.White)]
        public void OriginalImmunityRejectsBeforePaymentOrActivation(string enemyId, int index, ManaColor color)
        {
            var s = Create(enemyId); var p = s.Player; var u = p.Units[0]; p.Mana.AddToken(color);
            if (index < 2) s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, index, target: 0));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1)); Assert.That(u.IsReady, Is.True);
            Assert.That(DelphanaMastersActions.Used(s.Context, u, index), Is.False);
        }

        [Test]
        public void WoundOrMissingFeeCannotUnlockAnotherAbilityAfterFirstActivation()
        {
            var s = Create("monster_017"); var p = s.Player; var u = p.Units[0];
            p.Mana.AddToken(ManaColor.White); s.UseUnit(u, 3, target: 0);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, target: 0));
            Assert.That(s.Context.FortificationsRemoved.Contains(0), Is.True);
            Assert.That(DelphanaMastersActions.Used(s.Context, u, 2), Is.False);
            p.Mana.AddToken(ManaColor.Green); u.AddWounds(1);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, target: 0));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(1)); Assert.That(u.Wounds, Is.EqualTo(1));
        }

        [Test] public void RecruitByInteractionButRejectFreeAcquisitionAndOfferedAbility()
        {
            var p = new PlayerState(); var card = CardJsonLoader.LoadUnit("army_005");
            var c = new ActionContext { InfluencePool = 13, UnitOffer = new List<UnitCard> { card } };
            new MK.Logic.Runtime.CardEffects.BattleSummonsEffect(false).Execute(p, c);
            p.Mana.AddToken(ManaColor.White);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.RecruitFree(p, c, 0));
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.UseFromOffer(p, c, 0, 3, target: 0));
            Assert.That(c.OfferedUnitUses, Is.EqualTo(1)); Assert.That(c.UnitOffer.Single(), Is.SameAs(card));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(1)); Assert.That(p.Units, Is.Empty);
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.City);
            Assert.That(CardUnitActions.TryRecruit(p, c, c.UnitOffer, 0, RecruitLocation.City), Is.True);
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(c.UnitOffer, Is.Empty);
            Assert.That(p.Units.Single().Card, Is.SameAs(card)); Assert.That(p.Units.Single().CanActivate, Is.True);
        }
    }
}
