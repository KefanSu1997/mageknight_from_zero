using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveAltemMagesTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string enemy = "monster_029", bool supply = false)
        {
            var p = new PlayerState { Fame = 8 };
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_001")));
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, supply ? "army_001" : "army_2_014")));
            if (!supply) { p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2); p.Mana.AddToken(ManaColor.Black, 1); }
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string mode = "strong") => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), mode);
        private void MageAbsorbs(PveBattleSession s)
        {
            s.Advance(); s.Advance(); s.PlanDamage(0, 0, s.Player.Units[0]); s.Advance();
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.Units[0].Wounds, Is.Zero);
        }
        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)]
        public void OriginalColdFireAttackBlockFiveSevenNinePaysExactSelectedColors(int boost, bool block)
        {
            var s = Create(); var p = s.Player; var u = p.Units[0];
            Assert.That(u.Card.Level, Is.EqualTo(4)); Assert.That(u.Card.Armor, Is.EqualTo(5));
            Assert.That(u.Card.InfluenceCost, Is.EqualTo(12)); Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.City));
            Assert.That(u.Card.Abilities, Is.EquivalentTo(new[] { Ability.FireResist, Ability.IceResist }));
            int power = boost == 3 ? 9 : boost == 0 ? 5 : 7;
            if (block) s.Advance(); else MageAbsorbs(s);
            s.UseUnit(u, 1, block ? 1 : 0, boost: boost);
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(2 - (boost & 1)));
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(2 - ((boost >> 1) & 1)));
            Assert.That(u.IsReady, Is.False);
            if (block)
            {
                Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(power)); s.Block(0); s.Advance(); s.Advance();
                s.UseUnit(p.Units[1], 0); Play(s, 21); Play(s, 16, "attack");
                Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(4));
            }
            else Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(power));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(p.Fame, Is.EqualTo(13)); Assert.That(p.Wounds, Is.Zero);
        }

        [Test] public void ColdFireConversionAppliesToPriorAndFutureContributionsAndRetainsOriginalKillReward()
        {
            var s = Create(); MageAbsorbs(s); Play(s, 11);
            var original = s.Context.CombatPower.Attacks.Single();
            s.UseUnit(s.Player.Units[0], 2, 0);
            Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(original), "不得克隆贡献丢失消费身份或奖励");
            Play(s, 21); var result = s.Attack(0);
            Assert.That(result.EffectivePower, Is.EqualTo(7)); Assert.That(s.Player.Fame, Is.EqualTo(14));
            Assert.That(s.Context.CombatPower.Attacks, Is.Empty); Assert.That(s.Context.AllAttacksColdFire, Is.True);
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Context.AllAttacksColdFire, Is.False);
            Assert.That(s.Player.Mana.Tokens.ContainsKey(ManaColor.Black), Is.False);
        }

        [Test] public void SiegeConversionLetsOriginalUnitCardsAndSidewaysAttackFortifiedEnemyInRangedPhase()
        {
            var s = Create("monster_017"); Play(s, 11);
            Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            s.UseUnit(s.Player.Units[0], 2, 1);
            s.UseUnit(s.Player.Units[1], 0); Play(s, 16, "attack");
            s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
            Assert.That(s.AttackTotal(AttackType.Siege), Is.EqualTo(9)); Assert.That(s.AttackTotal(AttackType.Melee), Is.Zero);
            Assert.That(s.Context.CombatPower.Attacks.Select(a => a.Element), Is.All.EqualTo(Element.Physical));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(9)); Assert.That(s.Context.CombatPower.Attacks, Is.Empty);
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(12));
            Assert.That(s.Context.AllAttacksSiege, Is.False); Assert.That(s.Player.Wounds, Is.Zero);
        }

        [Test] public void TwoChosenTokensPayActualOriginalCardsInsteadOfGrantingCrystals()
        {
            var s = Create("monster_032", true); var p = s.Player;
            s.UseUnit(p.Units[0], 0, manaColors: new[] { ManaColor.Red, ManaColor.Blue });
            Assert.That(p.Mana.Crystals, Is.Empty); s.Advance(); Play(s, 16); s.Block(0); s.Advance(); s.Advance();
            Play(s, 21); Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); s.Advance();
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(s.Won, Is.True); Assert.That(p.Fame, Is.EqualTo(10));
        }

        [TestCase(ManaColor.Gold, ManaColor.Black)] [TestCase(ManaColor.Black, ManaColor.Black)]
        [TestCase(ManaColor.Green, ManaColor.White)]
        public void AnyColorsCanRepeatAndBlackTokenPaysDaytimeUnitTransformation(ManaColor first, ManaColor second)
        {
            var s = Create("monster_017", true); var p = s.Player;
            s.UseUnit(p.Units[0], 0, manaColors: new[] { first, second });
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(2)); Assert.That(p.Mana.Crystals, Is.Empty);
            if (second == ManaColor.Black)
            {
                s.UseUnit(p.Units[1], 2, 1); Play(s, 16, "attack"); Play(s, 21, "attack");
                Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
                while (s.Phase != Phase.End) s.Advance(); Assert.That(s.Won, Is.True);
                Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(11));
            }
        }

        [Test] public void InvalidColorsModeBoostAndMissingCombinedCostAreAtomic()
        {
            var s = Create("monster_032", true); var p = s.Player; var u = p.Units[0];
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 0));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 0, manaColors: new[] { ManaColor.Red, (ManaColor)6 }));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, 2));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, 0));
            s.Advance(); p.Mana.AddCrystal(ManaColor.Red, 1);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, 1, boost: 3));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, 1, boost: 4));
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.That(s.Context.BlockPool, Is.Zero); Assert.That(s.Context.AllAttacksColdFire, Is.False);
            Assert.That(s.Context.AllAttacksSiege, Is.False); Assert.That(u.IsReady, Is.True);
        }

        [Test] public void ConversionCannotStartOutsideBattleAndCannotLeakIntoNextCombat()
        {
            var s = Create("monster_032"); var p = s.Player;
            s.UseUnit(p.Units[0], 2, 1); s.Context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, s.Context);
            Assert.That(s.Context.AllAttacksSiege, Is.False);
            UnitDamageResolver.EndBattle(p, s.Context);
            p.Units[0].Ready(); p.Mana.AddToken(ManaColor.Black, 1);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p, s.Context, p.Units[0], 2));
            Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(1)); Assert.That(p.Units[0].IsReady, Is.True);
        }

        [Test] public void IceResistanceActuallyAbsorbsOriginalIceLichAttack()
        {
            var s = Create("monster_004"); MageAbsorbs(s); s.UseUnit(s.Player.Units[0], 1);
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(5)); s.Advance(); Assert.That(s.Won, Is.True);
        }
    }
}
