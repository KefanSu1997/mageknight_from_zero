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
    public sealed class PveAttackSelectionTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PveBattleSession Create()
        {
            var player = new PlayerState(0, "诺洛瓦斯");
            player.Deck.Hand.Add(new DeedCard("basic_card_021", CardType.Action));
            player.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            player.Mana.AddCrystal(ManaColor.Red);
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            return new PveBattleSession(player, new[] { enemy, enemy with { } }, CardJsonLoader.LoadBasicActions());
        }

        [Test] public void OriginalRageKillsFirstAndSidewaysPlusOriginalSkillKillsSecondWithoutSpendingReservedSources()
        {
            var s = Create();
            s.Player.HeroId = "Arythea";
            s.Player.Name = "阿瑞希亚";
            s.Player.Fame = 3;
            Assert.That(s.Player.Level, Is.EqualTo(2));
            s.Advance(); s.Advance(); s.Advance();
            var wounds = s.Player.Wounds;
            var rage = s.Player.Deck.Hand.Single(c => c.Id == "basic_card_021");
            var sideways = s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000");
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[2];
            HeroSkillAcquisition.Acquire(s.Player, skill);
            s.Play(rage, "strong"); s.PlaySideways(sideways); s.UseSkill(skill, 0);
            var parts = s.Context.CombatPower.Attacks.ToArray();
            Assert.That(parts.Select(p => p.Value), Is.EqualTo(new[] { 4, 1, 2 }));
            var first = s.AttackSelected(new[] { 0 }, new[] { parts[0] });
            Assert.That(first.PrintedPower, Is.EqualTo(4)); Assert.That(first.Kills, Is.EqualTo(1));
            Assert.That(s.Context.CombatPower.Attacks.Count, Is.EqualTo(2));
            Assert.That(s.Context.CombatPower.Attacks[0], Is.SameAs(parts[1]));
            Assert.That(s.Context.CombatPower.Attacks[1], Is.SameAs(parts[2]));
            Assert.That(s.Context.DefeatedEnemyIndices.SetEquals(new[] { 0 }), Is.True);
            Assert.Throws<InvalidOperationException>(() => s.AttackSelected(new[] { 1 }, new[] { parts[0] }));
            var second = s.AttackSelected(new[] { 1 }, new[] { parts[1], parts[2] });
            Assert.That(second.PrintedPower, Is.EqualTo(3)); Assert.That(second.Kills, Is.EqualTo(1));
            Assert.That(s.Context.CombatPower.Attacks.Count, Is.Zero);
            Assert.That(s.Player.Fame, Is.EqualTo(7)); Assert.That(s.Player.Wounds, Is.EqualTo(wounds));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(s.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, rage)), Is.True);
            Assert.That(s.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, sideways)), Is.True);
            s.Advance(); Assert.That(s.Won, Is.True);
        }

        [Test] public void InvalidSelectionCannotConsumeOrSubstituteActualContributions()
        {
            var s = Create(); s.Advance(); s.Advance(); s.Advance();
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_021"), "strong");
            var part = s.Context.CombatPower.Attacks.Single();
            foreach (var selected in new[] { Array.Empty<AttackProfile>(), new[] { part, part }, new[] { part with { } }, new AttackProfile[] { null } })
            {
                Assert.Throws<InvalidOperationException>(() => s.AttackSelected(new[] { 0 }, selected));
                Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(part));
                Assert.That(s.Context.DefeatedEnemyIndices.Count, Is.Zero);
                Assert.That(s.Player.Fame, Is.Zero);
            }
        }

        [Test] public void TargetRestrictedContributionCannotBeSelectedForDifferentEnemy()
        {
            var s = Create(); s.Advance(); s.Advance(); s.Advance();
            s.Context.CombatPower.AddAttack(3, requiredEnemy: s.Enemies[0]);
            var part = s.Context.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(() => s.AttackSelected(new[] { 1 }, new[] { part }));
            Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(part));
            Assert.That(s.AttackSelected(new[] { 0 }, new[] { part }).Kills, Is.EqualTo(1));
        }

        [Test] public void RangedSelectionPreservesOtherActualRangedContribution()
        {
            var s = Create();
            s.Context.CombatPower.AddAttack(3, type: AttackType.Ranged);
            s.Context.CombatPower.AddAttack(3, type: AttackType.Siege);
            s.Context.CombatPower.AddAttack(4);
            var parts = s.Context.CombatPower.Attacks.ToArray();
            Assert.Throws<InvalidOperationException>(() => s.AttackSelected(new[] { 0 }, new[] { parts[2] }));
            Assert.That(s.AttackSelected(new[] { 0 }, new[] { parts[0] }).Kills, Is.EqualTo(1));
            Assert.That(s.Context.CombatPower.Attacks[0], Is.SameAs(parts[1]));
            Assert.That(s.Context.CombatPower.Attacks[1], Is.SameAs(parts[2]));
            Assert.That(s.AttackSelected(new[] { 1 }, new[] { parts[1] }).Kills, Is.EqualTo(1));
            Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(parts[2]));
        }

        [TestCase(Phase.Ranged)]
        [TestCase(Phase.Melee)]
        public void InsufficientPhysicalAttackRetainsActualContributionsForAdditionalCards(Phase phase)
        {
            var original = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_004");
            var player = new PlayerState(0, "原规则攻击回归");
            var s = new PveBattleSession(player, new[] { original with { } }, CardJsonLoader.LoadBasicActions());
            if (phase == Phase.Melee) { s.Advance(); s.Advance(); s.Advance(); }
            var type = phase == Phase.Ranged ? AttackType.Siege : AttackType.Melee;
            s.Context.CombatPower.AddAttack(6, type:type);
            var part = s.Context.CombatPower.Attacks.Single();
            bool actionStarted = s.Context.CombatActionStarted;
            Assert.Throws<InvalidOperationException>(() => s.AttackSelected(new[] { 0 }, new[] { part }));
            Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(part));
            Assert.That(player.Fame, Is.Zero);
            Assert.That(s.Context.DefeatedEnemyIndices, Is.Empty);
            Assert.That(s.Context.CombatActionStarted, Is.EqualTo(actionStarted));
            s.Context.CombatPower.AddAttack(2, type:type);
            Assert.That(s.AttackSelected(new[] { 0 }, s.Context.CombatPower.Attacks.ToArray()).Kills, Is.EqualTo(1));
        }

        [Test] public void InsufficientAttackDoesNotCommitProvisionalPrintedDefendBonus()
        {
            var originals = CardJsonLoader.LoadMonsters();
            var bat = originals.Single(m => m.Id == "monster_057");
            var orc = originals.Single(m => m.Id == "monster_032");
            var s = new PveBattleSession(new PlayerState(0,"防御回归"),new[]{bat,orc},CardJsonLoader.LoadBasicActions());
            s.ChooseDefend(0,1,0,1);
            var selection=s.Context.EnemyDefense.Selected.Single();
            s.Context.CombatPower.AddAttack(1,type:AttackType.Siege);
            var part=s.Context.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(()=>s.AttackSelected(new[]{0,1},new[]{part}));
            Assert.That(s.Context.EnemyDefense.Applied,Is.Empty);
            Assert.That(s.Context.EnemyDefense.Selected.Single(),Is.SameAs(selection));
            Assert.That(s.Context.CombatPower.Attacks.Single(),Is.SameAs(part));
        }
    }
}
