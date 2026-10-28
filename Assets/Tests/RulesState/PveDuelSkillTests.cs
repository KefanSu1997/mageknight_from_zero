using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveDuelSkillTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private (PlayerState p, ActionContext c, SkillCard skill) Create()
        {
            var p = new PlayerState { HeroId = "Wolfhawk", Fame = 3 };
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[7]; p.Skills.Add(skill);
            var original = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block, Enemies = new[] { original, original with { } }, SelectedEnemy = original };
            return (p,c,skill);
        }
        private UnitState Unit(PlayerState p, string id = "army_001")
        { var u = new UnitState(UnitJsonLoader.Load(Folder,id)); p.Units.Add(u); return u; }
        private int Kill(PlayerState p, ActionContext c, int target)
        {
            CardCombatActions.EnterPhase(c,Phase.Melee); c.CombatPower.AddAttack(100);
            return CardCombatActions.Attack(p,c,c.Enemies,new[]{target},Phase.Melee).Fame;
        }
        [Test] public void OriginalSkillBindsBlockAndLaterAttackToExactEnemyAndPaysOnlyItsKill()
        {
            var (p,c,s)=Create(); SkillActions.Use(p,c,s);
            Assert.That(c.CombatPower.AvailableBlocks(1,0,c.Enemies[1]),Is.Empty);
            Assert.That(c.CombatPower.AvailableBlocks(0,0,c.Enemies[0]).Single().Value,Is.EqualTo(1));
            Assert.That(c.CombatPower.Attacks,Is.Empty);
            CardCombatActions.EnterPhase(c,Phase.Melee); CardCombatActions.EnterPhase(c,Phase.Melee);
            Assert.That(c.CombatPower.Attacks.Single().RequiredEnemy,Is.SameAs(c.Enemies[0]));
            Assert.That(Kill(p,c,1),Is.EqualTo(c.Enemies[1].Fame));
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame+1));
            Assert.That(DuelActions.TakeDefeatBonus(c,c.Enemies[0]),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualOwnedOrOfferedUnitBlockCancelsOnlyAffectedEnemyReward(bool offered)
        {
            var (p,c,s)=Create(); var u=Unit(p); SkillActions.Use(p,c,s);
            if(offered)
            {
                c.UnitOffer=new[]{u.Card};
                new MK.Logic.Runtime.CardEffects.BattleSummonsEffect(false).Execute(p,c);
                CardUnitActions.UseFromOffer(p,c,0,1,1);
            }
            else CardUnitActions.Use(p,c,u,1,1);
            CardCombatActions.Block(p,c,c.Enemies,0);
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
        }
        [Test] public void UnitBlockOfSameNamedOtherEnemyDoesNotCancelDuelReward()
        {
            var (p,c,s)=Create();var u=Unit(p);SkillActions.Use(p,c,s);
            CardUnitActions.Use(p,c,u,1,1);CardCombatActions.Block(p,c,c.Enemies,1);
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame+1));
        }
        [Test] public void UnitAttackBeforeDuelIsRememberedEvenWhenInsufficient()
        {
            var (p,c,s)=Create();var u=Unit(p,"army_007");
            // Original ranged fire attack is insufficient against this actual enemy's raised test armor.
            c.Enemies=new[]{c.Enemies[0] with{Armor=20}};c.SelectedEnemy=c.Enemies[0];c.CombatPhase=Phase.Ranged;
            CardUnitActions.Use(p,c,u,0);Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged).Kills,Is.Zero);
            CardCombatActions.EnterPhase(c,Phase.Block);SkillActions.Use(p,c,s);
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
        }
        [Test] public void ActualResistedDamageWithoutWoundStillDisqualifiesReward()
        {
            var (p,c,s)=Create();var u=Unit(p);SkillActions.Use(p,c,s);c.UnitsResistAll=true;
            Assert.That(UnitDamageResolver.Assign(p,c,u,c.Enemies[0],1,Element.Physical),Is.Zero);
            Assert.That(u.Wounds,Is.Zero);Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
        }
        [Test] public void FailedAndZeroDamageDoNotDisqualifyAndIllegalSkillDoesNotConsume()
        {
            var (p,c,s)=Create();var foreign=Unit(new PlayerState());
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,foreign,c.Enemies[0],1,Element.Physical));
            c.CombatPhase=Phase.Ranged;Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));Assert.That(SkillActions.IsUsed(p,s),Is.False);
            c.CombatPhase=Phase.Block;SkillActions.Use(p,c,s);var u=Unit(p);UnitDamageResolver.Assign(p,c,u,c.Enemies[0],0,Element.Physical);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame+1));
        }
        [Test] public void ActualFearBannerAndMastersTargetEffectsDisqualifyTheirEnemy()
        {
            var (p,c,s)=Create();var u=Unit(p);SkillActions.Use(p,c,s);
            var banner=new DeedCard("items_001",CardType.Artifact);p.Deck.Hand.Add(banner);BannerActions.AssignFear(p,u,banner);
            BannerActions.UseFear(p,c,u,0);Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
            (p,c,s)=Create();u=Unit(p,"army_005");SkillActions.Use(p,c,s);
            var ability=Array.FindIndex(u.Card.Actions,a=>a.Kind==UnitAbilityKind.StopEnemy);
            foreach(var color in u.Card.Actions[ability].Cost)p.Mana.AddToken(color,1);
            DelphanaMastersActions.Use(p,c,u,ability,0);Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
        }
        [Test] public void ActualUnitColdFireTransformationDisqualifiesOnlyWhenAnAttackUsesIt()
        {
            var (p,c,s)=Create();var u=Unit(p);SkillActions.Use(p,c,s);
            CardCombatActions.EnterPhase(c,Phase.Melee);
            int index=Array.FindIndex(u.Card.Actions,a=>a.Kind==UnitAbilityKind.TransformAttacks);
            foreach(var color in u.Card.Actions[index].Cost)p.Mana.AddToken(color,1);
            CardUnitActions.Use(p,c,u,index,0);
            Assert.That(Kill(p,c,0),Is.EqualTo(c.Enemies[0].Fame));
        }
        [Test] public void RealBattleEndClearsTargetAndHistoryWithoutResettingTurnUsage()
        {
            var (p,c,s)=Create();SkillActions.Use(p,c,s);UnitDamageResolver.EndBattle(p,c);
            Assert.That(DuelActions.TakeDefeatBonus(c,c.Enemies[0]),Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.True);
            Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(c.CombatPower.Blocks,Is.Empty);
        }
    }
}
