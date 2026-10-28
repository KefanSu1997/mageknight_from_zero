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
    public sealed class TezlaEnemyDefenseRulesTests
    {
        private static Monster Bat() => MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == "monster_057");
        private static Monster Orc() => MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == "monster_032");
        private static ActionContext Context(params Monster[] enemies) => new() { Enemies = enemies, InBattle = true };
        [Test] public void OriginalSixCardsDefendGroupThenBothIceSlotsBlockedAndLowArmorConsumedInFormalJourney()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
            var p = new PlayerState { HeroId = "Wolfhawk", Fame = 35, Armor = 3 };
            p.Mana.Crystals[ManaColor.Red] = 1; p.Mana.Crystals[ManaColor.Blue] = 1;
            foreach (string id in new[]{"basic_card_010","basic_card_011","advanced_card_000","basic_card_016","basic_card_021","advanced_card_034"})
                p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            var originals=p.Deck.Hand.ToArray(); DeedCard Card(string id)=>p.Deck.Hand.Single(x=>x.Id==id);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),manaRandom:new Random(0),supplyRandom:new Random(0));
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();var enemies=new[]{Bat(),Orc(),Orc()};var b=j.BeginBattle(enemies);
            b.Play(Card("basic_card_010"),"ranged");b.Play(Card("basic_card_011"),"strong");b.Play(Card("advanced_card_000"),"strong");
            Assert.That(b.DefendChoices(1,2).Count,Is.EqualTo(2));
            b.ChooseDefend(0,1,1,2);Assert.That(b.AttackArmorPreview(1,2),Is.EqualTo(7));
            Assert.That(b.Context.EnemyDefense.Applied,Is.Empty);
            var result=b.Attack(1,2);Assert.That(result.EffectivePower,Is.EqualTo(7));Assert.That(result.Kills,Is.EqualTo(2));
            Assert.That(result.Fame,Is.EqualTo(5),"Two original orcs plus Throwing Axe strong once-per-attack fame reward");
            Assert.That(p.Fame,Is.EqualTo(40));
            Assert.That(b.Context.EnemyDefense.Applied.Single().Target,Is.SameAs(enemies[1]));
            Assert.That(b.Context.EnemyDefense.Applied.Single().Source,Is.SameAs(enemies[0]));
            b.Advance();b.Play(Card("basic_card_016"),"block");
            Assert.That(b.BlockPreview(0,0).Effective,Is.EqualTo(1));b.Block(0,0);
            b.Play(Card("basic_card_021"),"block");Assert.That(b.BlockPreview(0,1).Effective,Is.EqualTo(1));b.Block(0,1);
            b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
            Assert.That(b.Context.SuccessfullyBlockedAttacks.Count,Is.EqualTo(2));
            Assert.That(b.AttackArmorPreview(0),Is.EqualTo(1));
            b.Play(Card("advanced_card_034"),"base");result=b.Attack(0);
            Assert.That(result.EffectivePower,Is.EqualTo(3));Assert.That(result.RequiredPower,Is.EqualTo(1));Assert.That(result.Kills,Is.EqualTo(1));
            b.Advance();j.EndTurn();Assert.That(j.Player,Is.SameAs(p));Assert.That(j.CompletedBattles,Is.EqualTo(1));Assert.That(j.CompletedTurns,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(41));Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(6));
            Assert.That(originals.All(card=>p.Deck.DiscardPile.Any(d=>ReferenceEquals(d,card))),Is.True);
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);
        }
        [Test] public void OriginalBatDefendsRealGroupTargetThenBothIceAttacksMustBeBlocked()
        {
            var e = new[] { Bat(), Orc(), Orc() }; var c = Context(e); var p = new PlayerState();
            c.CombatPower.AddAttack(7, Element.Fire, AttackType.Ranged);
            Assert.That(c.EnemyDefense.Choices(e,new[]{1,2},c).Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p,c,e,new[]{1,2},Phase.Ranged));
            Assert.That(c.CombatPower.Attacks.Single().Value,Is.EqualTo(7));
            c.EnemyDefense.Choose(e,new[]{1,2},c,0,2);
            Assert.That(c.EnemyDefense.Applied,Is.Empty);
            Assert.That(c.EnemyDefense.Selected.Single().Target,Is.SameAs(e[2]));
            var hit=CardCombatActions.Attack(p,c,e,new[]{1,2},Phase.Ranged);
            Assert.That(hit.RequiredPower,Is.EqualTo(7)); Assert.That(hit.Kills,Is.EqualTo(2));
            Assert.That(c.EnemyDefense.Applied.Single().Source,Is.SameAs(e[0]));
            Assert.That(c.EnemyDefense.Bonus(e[1]),Is.Zero);
            BattleResolver.Resolve(p,e,new[]{new BlockAllocation(0,1,Element.Fire,AttackIndex:0)},Array.Empty<AttackAllocation>(),ctx:c,enemyAttacks:new[]{(0,0)});
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Melee),Is.EqualTo(2));
            BattleResolver.Resolve(p,e,new[]{new BlockAllocation(0,1,Element.Fire,AttackIndex:1)},Array.Empty<AttackAllocation>(),ctx:c,enemyAttacks:new[]{(0,1)});
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Melee),Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Ranged),Is.EqualTo(2));
            c.CombatPower.AddAttack(1,Element.Fire); hit=CardCombatActions.Attack(p,c,e,new[]{0},Phase.Melee);
            Assert.That(hit.RequiredPower,Is.EqualTo(1));Assert.That(hit.Kills,Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Count,Is.Zero);Assert.That(p.Fame,Is.EqualTo(e.Sum(x=>x.Fame)));
        }
        [Test] public void CancelledAndUnblockedDamageNeverUnlockLowArmorButZeroBlockAttackDoes()
        {
            var e=new[]{Bat()};var c=Context(e);var p=new PlayerState();c.CancelledAttacks.Add((0,1));
            BattleResolver.Resolve(p,e,new[]{new BlockAllocation(0,1,Element.Fire)},Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Melee),Is.EqualTo(2));
            c=Context(e);c.PendingAttackReductions[(0,0)]=1;c.PendingAttackReductions[(0,1)]=1;
            BattleResolver.Resolve(new PlayerState(),e,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(c.SuccessfullyBlockedAttacks.Count,Is.EqualTo(2));
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Melee),Is.EqualTo(1));
            c=Context(e);BattleResolver.Resolve(new PlayerState(),e,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Melee),Is.EqualTo(2));
        }
        [Test] public void DifferentDefendersRequireRealChoicesAndDeadUnusedSourceNeverApplies()
        {
            // Official p2 example uses two Defend1 and one Defend2; generic fields, not hardcoded IDs.
            var a=Bat();var b=Bat();var protector=Orc() with {DefendValue=2,Abilities=new[]{Ability.Defend},Armor=4};
            var e=new[]{a,b,protector};var c=Context(e);var p=new PlayerState();
            c.EnemyDefense.Choose(e,new[]{0,2},c,0,0);c.EnemyDefense.Choose(e,new[]{0,2},c,1,2);
            c.CombatPower.AddAttack(8,Element.Fire,AttackType.Ranged);
            var hit=CardCombatActions.Attack(p,c,e,new[]{0,2},Phase.Ranged);
            Assert.That(hit.RequiredPower,Is.EqualTo(8));Assert.That(hit.Kills,Is.EqualTo(2));
            Assert.That(c.EnemyDefense.Applied.Count,Is.EqualTo(2));
            Assert.That(c.EnemyDefense.Choices(e,new[]{1},c),Is.Empty);
            c.CombatPower.AddAttack(2,Element.Fire,AttackType.Ranged);
            hit=CardCombatActions.Attack(p,c,e,new[]{1},Phase.Ranged);
            Assert.That(hit.RequiredPower,Is.EqualTo(2));Assert.That(hit.Kills,Is.EqualTo(1));
        }
        [Test] public void DefendPersistsAfterSourceDeathAndArmorModifiersAffectBothPrintedValues()
        {
            var e=new[]{Bat(),Orc()};var c=Context(e);var p=new PlayerState();
            c.EnemyDefense.Apply(e,new[]{1},c);CardCombatActions.DestroyEnemy(p,c,0);
            Assert.That(c.EnemyDefense.Bonus(e[1]),Is.EqualTo(1));
            var bat=Bat();e=new[]{bat};c=Context(e);c.EnemyDefense.Apply(e,new[]{0},c);
            c.ArmorReduction[0]=1;
            Assert.That(CombatMath.EffectiveArmor(bat,0,c,Phase.Melee),Is.EqualTo(2));
            c.SuccessfullyBlockedAttacks.Add((0,0));c.SuccessfullyBlockedAttacks.Add((0,1));
            Assert.That(CombatMath.EffectiveArmor(bat,0,c,Phase.Melee),Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(bat,0,c,Phase.Ranged),Is.EqualTo(2));
            UnitDamageResolver.EndBattle(p,c);Assert.That(c.EnemyDefense.Applied,Is.Empty);
        }
        [Test] public void InsightIgnoresOriginalDefenseAbilitiesAndCancelledSelectionHasNoEffect()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
            // Independent once-per-round branches, each acquired and activated by its actual hero.
            foreach (var ability in new[] { Ability.Defend, Ability.Elusive })
            {
                var e=new[]{Bat(),Orc(),Orc()};var c=Context(e);
                var p=new PlayerState { HeroId="Wolfhawk", Fame=35, Armor=3 };
                var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[5];
                HeroSkillAcquisition.Acquire(p,skill);
                var battle=new PveBattleSession(p,e,CardJsonLoader.LoadBasicActions(),c);
                c.EnemyDefense.Choose(e,new[]{1,2},c,0,1);c.EnemyDefense.ClearChoices();
                Assert.That(c.EnemyDefense.Selected,Is.Empty);Assert.That(c.EnemyDefense.Applied,Is.Empty);
                battle.UseSkill(skill,100+(int)ability,target:0);
                Assert.That(SkillActions.IsUsed(p,skill),Is.True);
                if (ability==Ability.Defend)
                {
                    Assert.That(c.EnemyDefense.Choices(e,new[]{1,2},c),Is.Empty);
                    c.EnemyDefense.Apply(e,new[]{1,2},c);Assert.That(c.EnemyDefense.Applied,Is.Empty);
                }
                else Assert.That(CombatMath.EffectiveArmor(e[0],0,c,Phase.Ranged),Is.EqualTo(1));
            }
        }
    }
}
