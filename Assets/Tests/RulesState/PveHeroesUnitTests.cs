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
    public sealed class PveHeroesUnitTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private UnitState Unit(string id) => new(UnitJsonLoader.Load(Folder, id));
        private PveBattleSession Create(string id, string enemy = "monster_032")
        {
            var p = new PlayerState { Fame = 8 }; p.Units.Add(Unit(id));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private static void Promise(PlayerState p, ActionContext c)
        {
            var card = new DeedCard("basic_card_008", CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), false);
        }

        [TestCase("army_008", 4, Ability.IceResist, ManaColor.Blue)]
        [TestCase("army_009", 4, Ability.FireResist, ManaColor.Red)]
        [TestCase("army_010", 3, Ability.PhysicalResist, ManaColor.Green)]
        [TestCase("army_011", 6, null, ManaColor.White)]
        public void FourOriginalDefinitionsKeepPassiveFeeSeparateFromThreeSelectableAbilities(string id, int armor, Ability? resistance, ManaColor specialFee)
        {
            var card = Unit(id).Card;
            Assert.That(card.Level, Is.EqualTo(3)); Assert.That(card.InfluenceCost, Is.EqualTo(9));
            Assert.That(card.Armor, Is.EqualTo(armor));
            Assert.That(card.Abilities, Is.EquivalentTo(resistance.HasValue ? new[] { resistance.Value } : Array.Empty<Ability>()));
            Assert.That(card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village | RecruitLocation.Keep | RecruitLocation.City));
            Assert.That(card.AssaultActivationInfluenceCost, Is.EqualTo(2));
            Assert.That(card.DoubleRecruitReputation, Is.True);
            Assert.That(card.Actions.Length, Is.EqualTo(3));
            Assert.That(card.Actions[0].Kind, Is.EqualTo(UnitAbilityKind.AttackOrBlock));
            Assert.That(card.Actions[0].Value, Is.EqualTo(5)); Assert.That(card.Actions[0].Cost, Is.Empty);
            Assert.That(card.Actions[1].Cost, Is.EqualTo(new[] { specialFee }));
            Assert.That(card.Actions[2].Value, Is.EqualTo(5)); Assert.That(card.Actions[2].Cost, Is.Empty);
            Assert.That(card.Actions[2].InteractionReputationChange, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryAttackOrBlockUsesFiveWithoutAssaultSurcharge(bool block)
        {
            var s = Create("army_009"); var unit = s.Player.Units[0]; s.Advance();
            if (block)
            {
                s.UseUnit(unit, 0, 1); Assert.That(s.Context.BlockPool, Is.EqualTo(5));
                s.Block(0); s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
            }
            else
            {
                s.Advance(); s.Advance(); s.UseUnit(unit, 0, 0);
                Assert.That(s.Context.MeleePool, Is.EqualTo(5)); Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            }
            Assert.That(s.Context.InfluencePool, Is.Zero); Assert.That(unit.IsReady, Is.False);
        }

        [Test]
        public void BlueColdFireBlockHandlesOriginalColdFireAttackWithoutTreatingEnemyFortificationAsAssault()
        {
            var s = Create("army_008", "monster_003"); var unit = s.Player.Units[0];
            s.Player.Mana.AddCrystal(ManaColor.Blue); s.Advance();
            Assert.That(CombatMath.IsFortified(s.Enemies[0], 0, s.Context), Is.True);
            Assert.That(CardUnitActions.ActivationInfluenceCost(s.Context, unit), Is.Zero);
            s.UseUnit(unit, 1); Assert.That(s.Context.CombatPower.Blocks.Single().Element, Is.EqualTo(Element.ColdFire));
            Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(8)); s.Block(0); s.Advance(); s.Advance();
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.Zero);
        }

        [Test]
        public void RedColdFireAttackConsumesRedAndDefeatsOriginalIceResistantEnemy()
        {
            var s = Create("army_009", "monster_004"); s.Player.Mana.AddCrystal(ManaColor.Red);
            s.Advance(); s.Advance(); s.Advance(); s.UseUnit(s.Player.Units[0], 1);
            Assert.That(s.Context.CombatPower.Attacks.Single().Element, Is.EqualTo(Element.ColdFire));
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(6)); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.Zero); Assert.That(s.Player.Fame, Is.EqualTo(13));
        }

        [Test]
        public void WhiteRangedSevenPaysWhiteAndKillsBeforeEnemyAttack()
        {
            var s = Create("army_011"); s.Player.Mana.AddCrystal(ManaColor.White); s.UseUnit(s.Player.Units[0], 1);
            Assert.That(s.Context.RangedPool, Is.EqualTo(7)); Assert.That(s.Context.CombatPower.Attacks.Single().Type, Is.EqualTo(AttackType.Ranged));
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.That(s.Player.Mana.Crystals[ManaColor.White], Is.Zero);
        }

        [Test]
        public void GreenHealingPaysForActualLevelThreeUnitAndOneHandWoundWithoutReadying()
        {
            var s = Create("army_010"); var p = s.Player; var other = Unit("army_014");
            p.Units.Add(other); other.Exhaust(); other.AddWounds(1); p.Mana.AddCrystal(ManaColor.Green);
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(p.Wounds, Is.EqualTo(2));
            var originalWounds = p.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            s.UseUnit(p.Units[0], 1); Assert.That(s.Context.Healing.Total, Is.EqualTo(4));
            s.HealWound(1); Assert.That(other.Wounds, Is.Zero); Assert.That(other.IsReady, Is.False);
            Assert.That(s.Context.Healing.Total, Is.EqualTo(1)); s.HealWound();
            Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(s.Context.Healing.Total, Is.Zero);
            Assert.That(originalWounds.Count(w => p.Deck.Hand.Any(c => ReferenceEquals(c, w))), Is.EqualTo(1));
            Assert.That(p.Mana.Crystals[ManaColor.Green], Is.Zero); Assert.That(p.Units[0].IsReady, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AssaultManaAndInfluenceAreBothRequiredBeforeEitherIsSpent(bool hasBlue)
        {
            var s = Create("army_008", "monster_025"); var p = s.Player; var c = s.Context; var unit = p.Units[0];
            Assert.That(s.Enemies[0].TokenColor, Is.EqualTo(EnemyTokenSupply.GuardColor(PlaceType.Keep)));
            c.CombatSite = new PlaceState(PlaceType.Keep) { Enemy = s.Enemies[0] };
            c.FortifiedSite = true; c.IgnoreFortified = true; // Removing fortification cannot remove the assault fee.
            s.Advance();
            if (hasBlue) p.Mana.AddToken(ManaColor.Blue); else Promise(p, c);
            Assert.That(CardUnitActions.ActivationInfluenceCost(c, unit), Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1));
            Assert.That(c.InfluencePool, Is.EqualTo(hasBlue ? 0 : 2));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(hasBlue ? 1 : 0));
            Assert.That(unit.IsReady, Is.True); Assert.That(c.BlockPool, Is.Zero);
            if (hasBlue) Promise(p, c); else p.Mana.AddToken(ManaColor.Blue);
            s.UseUnit(unit, 1); s.Block(0); s.Advance(); s.Advance();
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(p.Wounds, Is.Zero); Assert.That(unit.IsReady, Is.False);
        }

        [Test]
        public void InfluenceAbilityCannotPayItsOwnActivationFeeRetroactively()
        {
            var s = Create("army_009", "monster_002"); var p = s.Player; var c = s.Context; var unit = p.Units[0];
            Assert.That(s.Enemies[0].TokenColor, Is.EqualTo(EnemyTokenSupply.GuardColor(PlaceType.MageTower)));
            c.CombatSite = new PlaceState(PlaceType.MageTower) { Enemy = s.Enemies[0] };
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 2));
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(unit.IsReady, Is.True);
            Promise(p, c); s.UseUnit(unit, 2);
            Assert.That(c.InfluencePool, Is.EqualTo(5)); Assert.That(p.Reputation, Is.Zero);
            Assert.That(unit.IsReady, Is.False);
        }

        [TestCase(false, 1)]
        [TestCase(true, 1)]
        [TestCase(true, -1)]
        public void ReputationRewardRequiresRealInteractionAndPreservesItsOriginalPriceModifier(bool duringInteraction, int initialReputation)
        {
            var p = new PlayerState { Fame = 8, Reputation = initialReputation }; var c = new ActionContext();
            var unit = Unit("army_011"); p.Units.Add(unit);
            if (duringInteraction) RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            CardUnitActions.Use(p, c, unit, 2);
            Assert.That(p.Reputation, Is.EqualTo(initialReputation + (duringInteraction ? 1 : 0)));
            var peasants = UnitJsonLoader.Load(Folder, "army_2_006"); var offer = new List<UnitCard> { peasants };
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(initialReputation < 0 ? 0 : 1)); Assert.That(offer, Is.Empty);
            Assert.That(p.Units[1].Card, Is.SameAs(peasants)); Assert.That(unit.IsReady, Is.False);
        }

        [Test]
        public void ExistingTunnelPlayerAttackIdentityAlsoRequiresInfluence()
        {
            var p = new PlayerState(); var unit = Unit("army_009"); p.Units.Add(unit);
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Melee, TunnelTargetPlayer = new PlayerState() };
            Assert.That(CardUnitActions.ActivationInfluenceCost(c, unit), Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p, c, unit, 0));
            Promise(p, c); CardUnitActions.Use(p, c, unit, 0);
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(c.MeleePool, Is.EqualTo(5));
            // This checks the activation fee; it is not evidence of a complete PvP battle implementation.
        }
    }
}
