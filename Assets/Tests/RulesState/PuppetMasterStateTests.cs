using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PuppetMasterStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static SkillCard Learn(PlayerState player)
        {
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[6];
            player.Skills.Add(skill); return skill;
        }
        private static Monster Original(Func<Monster,bool> predicate = null)
            => CardJsonLoader.LoadMonsters().First(m => m.TokenColor != EnemyTokenColor.Unknown && (predicate == null || predicate(m)));
        private static ActionContext Battle(EnemyTokenSupply supply, Monster enemy)
            => new ActionContext { InBattle=true, CombatPhase=Phase.Melee, EnemySupply=supply, Enemies=new[] {enemy} };
        private static void Kill(PlayerState player, ActionContext context, Monster enemy)
        {
            context.CombatPower.AddAttack(100,Element.ColdFire);
            CardCombatActions.Attack(player,context,context.Enemies,new[]{0},Phase.Melee);
            // The production result hook must populate this; the test never fabricates a kill index.
            Assert.That(player.PuppetTokens.DefeatedThisTurn.Any(t=>ReferenceEquals(t,enemy)),Is.True);
        }
        private static void NextTurn(PlayerState player,ActionContext context)
        {
            context.InBattle=false;
            var engine=new PlayerTurnEngine();engine.EndTurn(player,DayPart.Day,new ManaSource(1,()=>DayPart.Day),context);
            engine.StartTurn(player,DayPart.Day);
        }
        [Test] public void RealDefeatIsReservedKeptAcrossTurnAndReturnedOnlyWhenConsumed()
        {
            var p=new PlayerState();new PlayerTurnEngine().StartTurn(p,DayPart.Day);var skill=Learn(p);var original=Original();
            var supply=new EnemyTokenSupply(new[]{original});var enemy=supply.Draw(original.TokenColor);var ctx=Battle(supply,enemy);
            Kill(p,ctx,enemy);
            Assert.That(supply.CanDraw(enemy.TokenColor),Is.False);Assert.That(supply.InUse,Is.Empty);
            supply.Discard(enemy); // Same release used by the actual dungeon after the combat result.
            Assert.That(supply.DiscardPile,Is.Empty);
            PuppetMasterActions.Keep(p,ctx,skill,enemy);NextTurn(p,ctx);
            Assert.That(p.PuppetTokens.KeptTokens.Single(),Is.SameAs(enemy));Assert.That(p.PuppetTokens.DefeatedThisTurn,Is.Empty);
            ctx=Battle(supply,enemy);
            PuppetMasterActions.Consume(p,ctx,skill,enemy,false);
            Assert.That(p.PuppetTokens.KeptTokens,Is.Empty);Assert.That(supply.DiscardPile.Single(),Is.SameAs(enemy));
            Assert.That(ctx.CombatPower.Attacks.Select(a=>a.Value),Is.EqualTo(Enumerable.Range(0,enemy.AttackCount).Select(i=>(enemy.GetAttack(i).Value+1)/2).Where(v=>v>0)));
            Assert.That(SkillActions.IsUsed(p,skill),Is.True);
        }
        [Test] public void UnkeptDefeatedTokenReturnsAtFormalEndTurn()
        {
            var p=new PlayerState();new PlayerTurnEngine().StartTurn(p,DayPart.Day);Learn(p);var original=Original();
            var supply=new EnemyTokenSupply(new[]{original});var enemy=supply.Draw(original.TokenColor);var ctx=Battle(supply,enemy);Kill(p,ctx,enemy);
            NextTurn(p,ctx);
            Assert.That(p.PuppetTokens.DefeatedThisTurn,Is.Empty);Assert.That(supply.PuppetReservations,Is.Empty);
            Assert.That(supply.DiscardPile.Single(),Is.SameAs(enemy));
        }
        [Test] public void InventoryKeepsMultiplePhysicalTokensWithoutReplacingPreviousOne()
        {
            var p=new PlayerState();new PlayerTurnEngine().StartTurn(p,DayPart.Day);var skill=Learn(p);
            var originals=CardJsonLoader.LoadMonsters().Where(m=>m.TokenColor!=EnemyTokenColor.Unknown).Take(2).ToArray();
            var supply=new EnemyTokenSupply(originals);Monster first=null;
            foreach(var original in originals)
            {
                var token=supply.Draw(original.TokenColor);first??=token;var ctx=Battle(supply,token);Kill(p,ctx,token);
                PuppetMasterActions.Keep(p,ctx,skill,token);NextTurn(p,ctx);
            }
            Assert.That(p.PuppetTokens.KeptTokens.Count,Is.EqualTo(2));Assert.That(p.PuppetTokens.KeptTokens[0],Is.SameAs(first));
            Assert.That(supply.DrawPile,Is.Empty);Assert.That(supply.DiscardPile,Is.Empty);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void InvalidCaptureDoesNotSpendSkillOrTakeSupply(int invalid)
        {
            var p=new PlayerState();var skill=Learn(p);var original=Original();var supply=new EnemyTokenSupply(new[]{original});
            var enemy=supply.Draw(original.TokenColor);var ctx=Battle(supply,enemy);
            if(invalid!=0)Kill(p,ctx,enemy);
            var selected=invalid==1 ? enemy with{} : enemy;
            if(invalid==2)p.Skills.Remove(skill);
            Assert.Throws<InvalidOperationException>(()=>PuppetMasterActions.Keep(p,ctx,skill,selected));
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(p.PuppetTokens.KeptTokens,Is.Empty);
        }
        [Test] public void MultipleAttackTokenProducesSeparateOriginalElements()
        {
            var enemy=Original(m=>m.AttackCount>1);var p=new PlayerState();new PlayerTurnEngine().StartTurn(p,DayPart.Day);var skill=Learn(p);
            var supply=new EnemyTokenSupply(new[]{enemy});supply.Draw(enemy.TokenColor);var ctx=Battle(supply,enemy);Kill(p,ctx,enemy);
            PuppetMasterActions.Keep(p,ctx,skill,enemy);NextTurn(p,ctx);ctx=Battle(supply,enemy);
            PuppetMasterActions.Consume(p,ctx,skill,enemy,false);
            var expected=Enumerable.Range(0,enemy.AttackCount).Select(enemy.GetAttack).Where(a=>a.Value>0).ToArray();
            Assert.That(ctx.CombatPower.Attacks.Count,Is.EqualTo(expected.Length));
            for(int i=0;i<expected.Length;i++)
            {
                Assert.That(ctx.CombatPower.Attacks[i].Value,Is.EqualTo((expected[i].Value+1)/2));
                Assert.That(ctx.CombatPower.Attacks[i].Element,Is.EqualTo(expected[i].Element));
                Assert.That(ctx.CombatPower.Attacks[i].Type,Is.EqualTo(AttackType.Melee));
            }
        }
        [TestCase(false,false,Element.Physical)] [TestCase(true,false,Element.Ice)]
        [TestCase(false,true,Element.Fire)] [TestCase(true,true,Element.ColdFire)]
        public void ActualResistanceSelectsOppositeBlockElement(bool fire,bool ice,Element expected)
        {
            // The original Golden Dragon has both resistances, but its JSON omits token_color.
            // Supply color is verified against Assets/GameData/cards/monster (37).jpg;
            // armor, attack and resistance values remain those of the original definition.
            var enemy=fire && ice
                ? CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_037") with { TokenColor=EnemyTokenColor.Red }
                : Original(m=>(m.Abilities.Contains(Ability.FireResist)||m.Abilities.Contains(Ability.ColdFireResist))==fire
                    && (m.Abilities.Contains(Ability.IceResist)||m.Abilities.Contains(Ability.ColdFireResist))==ice);
            var p=new PlayerState();new PlayerTurnEngine().StartTurn(p,DayPart.Day);var skill=Learn(p);var supply=new EnemyTokenSupply(new[]{enemy});
            supply.Draw(enemy.TokenColor);var ctx=Battle(supply,enemy);Kill(p,ctx,enemy);PuppetMasterActions.Keep(p,ctx,skill,enemy);NextTurn(p,ctx);
            ctx=Battle(supply,enemy);ctx.CombatPhase=Phase.Block;PuppetMasterActions.Consume(p,ctx,skill,enemy,true);
            Assert.That(ctx.CombatPower.Blocks.Single().Element,Is.EqualTo(expected));
            Assert.That(ctx.CombatPower.BlockTotal,Is.EqualTo((enemy.Armor+1)/2));
        }
    }
}

