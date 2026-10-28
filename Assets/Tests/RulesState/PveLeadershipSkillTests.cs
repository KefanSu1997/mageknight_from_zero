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
    public sealed class PveLeadershipSkillTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string unit = "army_001", string enemy = "monster_029")
        {
            var p = new PlayerState { HeroId = "Norowas", Fame = 15 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[6]);
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, unit)));
            p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2);
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Use(PveBattleSession s, int ability, int option = 0, int boost = 0) =>
            LeadershipActions.Use(s.Player, s.Context, s.Player.Skills.Single(), s.Player.Units[0], ability, option, boost: boost);

        [TestCase(false,0)] [TestCase(false,3)] [TestCase(true,0)] [TestCase(true,3)]
        public void OriginalColdFireRetainsExactBoostFeesAndActualAttackOrBlockConsumer(bool block,int boost)
        {
            var s = Create(); var p = s.Player; var u = p.Units[0]; s.Advance();
            if (!block) { s.Advance(); s.PlanDamage(0,0,u); s.Advance(); }
            Use(s, 1, block ? 1 : 0, boost);
            int value = 5 + (boost == 3 ? 4 : 0) + (block ? 3 : 2);
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(boost == 3 ? 1 : 2));
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(boost == 3 ? 1 : 2));
            Assert.That(u.IsReady,Is.False); Assert.That(u.Wounds,Is.Zero);
            Assert.That(SkillActions.IsUsed(p,p.Skills.Single()),Is.True);
            if(block)
            {
                Assert.That(s.Context.CombatPower.Blocks.Single().Element,Is.EqualTo(Element.ColdFire));
                Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(value)); s.Block(0);
                s.Advance(); s.Advance(); Assert.That(p.Wounds,Is.Zero);
            }
            else
            {
                var a=s.Context.CombatPower.Attacks.Single();
                Assert.That(a.SourceUnit,Is.SameAs(u)); Assert.That(a.Element,Is.EqualTo(Element.ColdFire));
                Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(value)); s.Advance(); Assert.That(s.Won,Is.True);
            }
        }

        [Test] public void FireRangedPlusOneKeepsPrintedFreeCostAndKillsOriginalEnemy()
        {
            var s=Create("army_007","monster_032"); Use(s,0);
            var a=s.Context.CombatPower.Attacks.Single();
            Assert.That(a.Type,Is.EqualTo(AttackType.Ranged)); Assert.That(a.Element,Is.EqualTo(Element.Fire));
            Assert.That(a.Value,Is.EqualTo(4)); Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));
        }

        [Test] public void IceSiegeAndNonContributionAbilitiesAreRejectedWithoutPaymentOrUsage()
        {
            var s=Create("army_013","monster_017"); var p=s.Player;
            Assert.Throws<InvalidOperationException>(()=>Use(s,1));
            Assert.Throws<InvalidOperationException>(()=>Use(s,2));
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(2));
            Assert.That(p.Units[0].IsReady,Is.True); Assert.That(SkillActions.IsUsed(p,p.Skills.Single()),Is.False);
            Assert.That(s.Context.CombatPower.Attacks,Is.Empty);
        }

        [Test] public void InvalidPhaseForeignSkillMissingFeeAndInvalidOptionPreserveEverything()
        {
            var s=Create();var p=s.Player;var skill=p.Skills.Single();
            Assert.Throws<InvalidOperationException>(()=>Use(s,1,1));
            s.Advance();
            Assert.Throws<InvalidOperationException>(()=>LeadershipActions.Use(p,s.Context,CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[6],p.Units[0],1,1));
            Assert.Throws<InvalidOperationException>(()=>Use(s,1,2));
            p.Mana.Crystals.Clear();p.Mana.AddCrystal(ManaColor.Red,1);
            Assert.Throws<InvalidOperationException>(()=>Use(s,1,1,3));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Units[0].IsReady,Is.True);
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(s.Context.CombatPower.Blocks,Is.Empty);
        }

        [Test] public void SwordsmanActualSelfWoundAndOtherUnitDoNotInheritBonusOrAllowReuse()
        {
            var s=Create("army_2_014","monster_007");var p=s.Player;
            var other=new UnitState(UnitJsonLoader.Load(Folder,"army_2_013"));p.Units.Add(other);
            s.Advance();Use(s,1,1);
            Assert.That(p.Units[0].Wounds,Is.EqualTo(1));Assert.That(s.Context.BlockPool,Is.EqualTo(9));
            Assert.Throws<InvalidOperationException>(()=>LeadershipActions.Use(p,s.Context,p.Skills.Single(),other,1));
            Assert.That(other.IsReady,Is.True);
            CardUnitActions.Use(p,s.Context,other,1);
            Assert.That(s.Context.CombatPower.Blocks.Select(b=>b.Value),Is.EqualTo(new[]{9,4}));
            s.Block(0); s.Advance(); s.Advance(); Assert.That(p.Wounds,Is.Zero);
        }
    }
}
