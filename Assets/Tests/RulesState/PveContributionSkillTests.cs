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
    public sealed class PveContributionSkillTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static SkillCard Own(PlayerState p,string pool,int index)
        {var skill=CardJsonLoader.LoadHeroSkills(pool)[index];p.Skills.Add(skill);return skill;}
        [TestCase(Element.Physical)] [TestCase(Element.Fire)] [TestCase(Element.Ice)] [TestCase(Element.ColdFire)]
        public void SoulGuideSelectsOneLiveElementalBlockAndPreservesRewards(Element element)
        {
            var p=new PlayerState();var c=new ActionContext();var skill=Own(p,"hero_skill_pool_003",0);
            SkillActions.Use(p,c,skill);Assert.That(c.MovementPool,Is.EqualTo(1));Assert.That(c.CombatPower.Blocks,Is.Empty);
            c.InBattle=true;c.CombatPhase=Phase.Block;
            c.CombatPower.AddBlock(2,element,armorReductionOnSuccess:1,attackOnSuccess:2);
            c.CombatPower.AddBlock(2,element,armorReductionOnSuccess:1,attackOnSuccess:2);
            var first=c.CombatPower.Blocks[0];var second=c.CombatPower.Blocks[1];
            ContributionSkillActions.ApplySoulGuideBlock(p,c,second);
            Assert.That(c.CombatPower.Blocks[0],Is.SameAs(first));var boosted=c.CombatPower.Blocks[1];
            Assert.That(boosted.Value,Is.EqualTo(3));Assert.That(boosted.Element,Is.EqualTo(element));
            Assert.That(boosted.AttackOnSuccess,Is.EqualTo(2));Assert.That(boosted.ArmorReductionOnSuccess,Is.EqualTo(1));
            Assert.That(c.SoulGuideBlockPending,Is.False);
            Assert.Throws<InvalidOperationException>(()=>ContributionSkillActions.ApplySoulGuideBlock(p,c,first));
        }
        [Test] public void SoulGuideInvalidTargetPreservesPermissionAndFormalTurnExpiresIt()
        {
            var p=new PlayerState();var c=new ActionContext();var skill=Own(p,"hero_skill_pool_003",0);
            SkillActions.Use(p,c,skill);c.InBattle=true;c.CombatPhase=Phase.Block;
            Assert.Throws<InvalidOperationException>(()=>ContributionSkillActions.ApplySoulGuideBlock(p,c,new BlockAllocation(0,2,Element.Ice)));
            Assert.That(c.SoulGuideBlockPending,Is.True);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.SoulGuideBlockPending,Is.False);
        }
        [Test] public void SoulGuideBonusHasActualElementalBlockConsumer()
        {
            var p=new PlayerState{Armor=3};var skill=Own(p,"hero_skill_pool_003",0);
            var e=new Monster("fire",3,3,Element.Fire,1,Array.Empty<Ability>());
            var b=new PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions());b.Advance();
            b.UseSkill(skill,0);b.Context.CombatPower.AddBlock(2,Element.Ice);
            b.ApplySoulGuideBlock(b.Context.CombatPower.Blocks.Single());b.Block(0);b.Advance();b.Advance();
            Assert.That(p.Wounds,Is.Zero);Assert.That(b.Context.CombatPower.Blocks,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void BullseyeRealOriginalRangedKeepsElementAndCost(bool fire)
        {
            var p=new PlayerState();var skill=Own(p,"hero_skill_pool_004",4);
            var id=fire?"advanced_card_000":"advanced_card_001";var card=new DeedCard(id,CardType.Action);p.Deck.Hand.Add(card);
            var color=fire?ManaColor.Red:ManaColor.Blue;p.Mana.AddCrystal(color);
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b=new PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()));
            b.Play(card,"strong");var old=b.Context.CombatPower.Attacks.Single();b.BoostCardAttack(skill,old);
            var boosted=b.Context.CombatPower.Attacks.Single();Assert.That(boosted.Value,Is.EqualTo(old.Value+1));
            Assert.That(boosted.Element,Is.EqualTo(old.Element));Assert.That(boosted.SourceCard,Is.SameAs(card));
            Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            b.Attack(0);Assert.That(b.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));
        }
        [Test] public void BullseyeSidewaysOnlyRealCardNotSkillOrUnitAndNoStaleTarget()
        {
            var p=new PlayerState{Armor=3};var skill=Own(p,"hero_skill_pool_004",4);var card=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(card);
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var b=new PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions());
            b.Advance();b.Advance();b.Advance();b.Context.CombatPower.AddAttack(1);
            Assert.Throws<InvalidOperationException>(()=>b.BoostCardAttack(skill,b.Context.CombatPower.Attacks.Single()));
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);b.PlaySideways(card);
            var part=b.Context.CombatPower.Attacks.Last();b.BoostCardAttack(skill,part);
            Assert.That(b.Context.MeleePool,Is.EqualTo(4));Assert.That(b.Context.CombatPower.Attacks.First().Value,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>b.Context.CombatPower.BoostExactAttack(part,2));
            b.Attack(0);Assert.That(b.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));
        }
    }
}
