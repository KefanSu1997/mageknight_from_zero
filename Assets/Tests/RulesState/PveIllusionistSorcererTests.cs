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
    public sealed class PveIllusionistSorcererTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private UnitState Unit(string id) => new(UnitJsonLoader.Load(Folder, id));
        private PveBattleSession Create(string unitId, string enemy = "monster_032")
        {
            var p = new PlayerState { Fame = 8 };
            p.Units.Add(Unit(unitId));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(x => x.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private static DeedCard Play(PveBattleSession s, string id, string choice = "base")
        {
            var card = new DeedCard(id, CardType.Action);
            s.Player.Deck.Hand.Add(card); s.Play(card, choice);
            return card;
        }

        [Test]
        public void FreeInfluencePaysForAnActualOriginalOfferUnit()
        {
            var p = new PlayerState { Fame = 8 }; var c = new ActionContext();
            var illusionist = Unit("army_2_003"); p.Units.Add(illusionist);
            Assert.That(illusionist.Card.Level, Is.EqualTo(2));
            Assert.That(illusionist.Card.Armor, Is.EqualTo(2));
            Assert.That(illusionist.Card.InfluenceCost, Is.EqualTo(7));
            Assert.That(illusionist.Card.Abilities, Is.EquivalentTo(new[] { Ability.PhysicalResist }));
            Assert.That(illusionist.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.MageTower | RecruitLocation.Monastery));
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            CardUnitActions.Use(p, c, illusionist, 0);
            Assert.That(c.InfluencePool, Is.EqualTo(4));
            var peasants = UnitJsonLoader.Load(Folder, "army_2_006");
            var offer = new List<UnitCard> { peasants };
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(offer, Is.Empty);
            Assert.That(p.Units[1].Card, Is.SameAs(peasants));
            Assert.That(illusionist.IsReady, Is.False);
            Assert.That(p.Mana.Tokens, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void FreeWhiteCrystalOrOverflowPaysAnotherUnitsPrintedWhiteFee(int existingCrystals)
        {
            var s = Create("army_2_003", "monster_017"); var p = s.Player;
            var sorcerer = Unit("army_014"); p.Units.Add(sorcerer);
            p.Mana.AddCrystal(ManaColor.White, existingCrystals);
            s.UseUnit(p.Units[0], 2);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(existingCrystals == 0 ? 1 : 3));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(existingCrystals == 0 ? 0 : 1));
            s.UseUnit(sorcerer, 1, target: 0);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(existingCrystals));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Play(s, "basic_card_011");
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(11));
            Assert.That(p.Units.All(u => !u.IsReady), Is.True);
        }

        [Test]
        public void WhiteStopPreventsActualAttackWithoutCountingAsBlockAndExpiresAtBattleEnd()
        {
            var s = Create("army_2_003"); var p = s.Player; var unit = p.Units[0];
            p.Mana.AddCrystal(ManaColor.White); p.Mana.AddToken(ManaColor.Red);
            s.Advance(); s.UseUnit(unit, 1, target: 0);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(s.Context.IsAttackCancelled(0, 0), Is.True);
            Assert.That(s.IsBlocked(0), Is.False);
            Assert.That(s.Context.BlockPool, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1, target: 0));
            s.Advance(); s.Advance();
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(s.Context.SuccessfullyBlockedAttacks, Is.Empty);
            Play(s, "basic_card_021", "strong");
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); s.Advance();
            Assert.That(s.Won, Is.True);
            Assert.That(s.Context.SkipAttackIndices, Is.Empty);
            Assert.That(unit.IsReady, Is.False);
        }

        [TestCase("fortification", "monster_017")]
        [TestCase("site", "monster_032")]
        [TestCase("immune", "monster_014")]
        [TestCase("phase", "monster_032")]
        public void IllegalStopRejectsBeforeWhitePaymentAndExhaustion(string reason, string enemy)
        {
            var s = Create("army_2_003", enemy); var p = s.Player; var unit = p.Units[0];
            p.Mana.AddToken(ManaColor.White);
            if (reason != "phase") s.Advance();
            if (reason == "site") s.Context.FortifiedSite = true;
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1, target: 0));
            Assert.That(p.Mana.Tokens[ManaColor.White], Is.EqualTo(1));
            Assert.That(unit.IsReady, Is.True);
            Assert.That(s.Context.SkipAttackIndices, Is.Empty);
        }

        [TestCase(0, "monster_032", 2)]
        [TestCase(1, "monster_017", 3)]
        [TestCase(2, "monster_004", 5)]
        public void OriginalSorcererModesPayTheirOwnFeesAndFeedRealRangedAttack(int ability, string enemy, int fame)
        {
            var s = Create("army_014", enemy); var p = s.Player; var unit = p.Units[0];
            Assert.That(unit.Card.Level, Is.EqualTo(3)); Assert.That(unit.Card.Armor, Is.EqualTo(4));
            Assert.That(unit.Card.InfluenceCost, Is.EqualTo(9));
            Assert.That(unit.Card.Abilities, Is.EquivalentTo(new[] { Ability.FireResist, Ability.IceResist }));
            Assert.That(unit.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.MageTower | RecruitLocation.Monastery));
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Green);
            s.UseUnit(unit, ability, target: ability == 0 ? -1 : 0);
            Assert.That(s.Context.RangedPool, Is.EqualTo(3));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(ability == 1 ? 0 : 1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(ability == 2 ? 0 : 1));
            Assert.That(s.Context.FortificationsRemoved.Contains(0), Is.EqualTo(ability == 1));
            Assert.That(s.Context.ResistancesRemoved.Contains(0), Is.EqualTo(ability == 2));
            if (ability != 0) Play(s, "basic_card_011");
            var result = s.Attack(0);
            Assert.That(result.EffectivePower, Is.EqualTo(ability == 0 ? 3 : 4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(8 + fame));
            Assert.That(s.Context.RangedPool, Is.Zero);
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(p.Wounds, Is.Zero);
            Assert.That(s.Context.FortificationsRemoved, Is.Empty);
            Assert.That(s.Context.ResistancesRemoved, Is.Empty);
        }

        [TestCase(1, ManaColor.White)]
        [TestCase(2, ManaColor.Green)]
        public void ImmuneTargetKeepsProtectionsButIndependentRangedPowerRemains(int ability, ManaColor color)
        {
            var s = Create("army_014", "monster_003"); s.Player.Mana.AddToken(color);
            s.UseUnit(s.Player.Units[0], ability, target: 0);
            Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
            Assert.That(s.Context.FortificationsRemoved, Is.Empty);
            Assert.That(s.Context.ResistancesRemoved, Is.Empty);
            Assert.That(s.Context.RangedPool, Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            Assert.That(s.Context.RangedPool, Is.EqualTo(3));
        }

        [TestCase(1, ManaColor.White)]
        [TestCase(2, ManaColor.Green)]
        public void MissingFeeOrTargetNeverMutatesEnemyOrExhaustsUnit(int ability, ManaColor color)
        {
            var s = Create("army_014", "monster_017"); var unit = s.Player.Units[0];
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, ability, target: 0));
            Assert.That(unit.IsReady, Is.True);
            s.Player.Mana.AddToken(color);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, ability, target: -1));
            Assert.That(s.Player.Mana.Tokens[color], Is.EqualTo(1));
            Assert.That(unit.IsReady, Is.True); Assert.That(s.Context.RangedPool, Is.Zero);
            Assert.That(s.Context.FortificationsRemoved, Is.Empty);
            Assert.That(s.Context.ResistancesRemoved, Is.Empty);
        }
    }
}
