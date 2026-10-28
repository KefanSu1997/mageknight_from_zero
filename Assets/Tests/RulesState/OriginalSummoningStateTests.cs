using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Data.Cards;

namespace MageKnight.Tests.RulesState
{
    public sealed class OriginalSummoningStateTests
    {
        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);

        [Test] public void OriginalIconIsSummonNotTwoZeroElementalAttacks()
        {
            var summoner=Enemy("monster_049");
            Assert.That(summoner.SummonAttack, Is.True);
            Assert.That(summoner.AttackCount, Is.Zero);
            Assert.That(summoner.Armor, Is.EqualTo(5));
            Assert.That(summoner.Fame, Is.EqualTo(3));
            Assert.That(summoner.Abilities, Does.Contain(Ability.Fortified));
            Assert.Throws<ArgumentOutOfRangeException>(() => summoner.GetAttack(0));
        }

        [Test] public void ActualBrownTokenHasOneOwnerAndReturnsSamePhysicalToken()
        {
            var summoner=Enemy("monster_049");var brown=Enemy("monster_066");
            var originals=new List<Monster>{summoner};var supply=new EnemyTokenSupply(new[]{brown},new Random(49));
            var state=new SummoningState();
            state.BeginDefense(originals,supply,new HashSet<int>(),new HashSet<int>());
            Assert.That(state.Bindings.Single().Summoner, Is.SameAs(summoner));
            Assert.That(state.DefenderAt(0,originals), Is.SameAs(brown));
            Assert.That(originals[0], Is.SameAs(summoner));
            Assert.That(brown.AttackCount, Is.EqualTo(3));
            Assert.That(supply.DrawPile, Is.Empty);
            Assert.That(supply.InUse.Single(), Is.SameAs(brown));
            Assert.Throws<InvalidOperationException>(() => state.BeginDefense(originals,supply,new HashSet<int>(),new HashSet<int>()));
            Assert.That(supply.InUse.Count, Is.EqualTo(1));
            state.EndDefense();
            Assert.That(state.DefenderAt(0,originals), Is.SameAs(summoner));
            Assert.That(supply.InUse, Is.Empty);
            Assert.That(supply.DiscardPile.Single(), Is.SameAs(brown));
            Assert.Throws<InvalidOperationException>(() => state.EndDefense());
            state.BeginDefense(originals,supply,new HashSet<int>(),new HashSet<int>());
            Assert.That(state.DefenderAt(0,originals), Is.SameAs(brown));
            state.EndDefense();
        }

        [TestCase(true)] [TestCase(false)] public void DeadOrStoppedSummonerDoesNotDraw(bool dead)
        {
            var original=Enemy("monster_049");var brown=Enemy("monster_066");
            var supply=new EnemyTokenSupply(new[]{brown},new Random(49));var state=new SummoningState();
            state.BeginDefense(new[]{original},supply,dead?new HashSet<int>{0}:new HashSet<int>(),dead?new HashSet<int>():new HashSet<int>{0});
            Assert.That(state.Bindings, Is.Empty);Assert.That(supply.DrawPile.Single(), Is.SameAs(brown));
            state.EndDefense();Assert.That(supply.DiscardPile, Is.Empty);
        }

        [Test] public void MissingSupplyCannotSilentlyBecomeZeroAttack()
        {
            var state=new SummoningState();var supply=new EnemyTokenSupply();
            Assert.Throws<InvalidOperationException>(() => state.BeginDefense(new[]{Enemy("monster_049")},supply,new HashSet<int>(),new HashSet<int>()));
            Assert.That(state.IsActive, Is.False);Assert.That(state.Bindings, Is.Empty);Assert.That(supply.InUse, Is.Empty);
            var battle=new PveBattleSession(new PlayerState(0,"test"),new[]{Enemy("monster_049")},Array.Empty<ActionCardData>());
            Assert.Throws<InvalidOperationException>(() => battle.Advance());
            Assert.That(battle.Phase,Is.EqualTo(Phase.Ranged));
            Assert.That(battle.Context.CombatPhase,Is.EqualTo(Phase.Ranged));
            Assert.Throws<InvalidOperationException>(() => battle.Advance());
            Assert.That(battle.Context.Summoning.IsActive,Is.False);
        }

        [Test] public void TemporaryDefenderDoesNotInheritOrOverwriteSummonerModifiers()
        {
            var summoner=Enemy("monster_049");var other=Enemy("monster_032");var brown=Enemy("monster_066");
            var ctx=new ActionContext { Enemies=new List<Monster>{summoner,other},
                EnemySupply=new EnemyTokenSupply(new[]{brown},new Random(49)) };
            ctx.ArmorReduction[0]=2;ctx.ArmorReduction[1]=1;
            ctx.ResistancesRemoved.Add(0);ctx.AttackElementChange[0]=Element.Fire;
            ctx.PendingAttackReductions[(0,0)]=2;
            ctx.Summoning.ReplaceDefenders(ctx);
            Assert.That(ctx.Enemies[0],Is.SameAs(brown));
            Assert.That(ctx.ArmorReduction.ContainsKey(0),Is.False);
            Assert.That(ctx.ResistancesRemoved,Is.Empty);
            Assert.That(ctx.AttackElementChange.ContainsKey(0),Is.False);
            Assert.That(ctx.PendingAttackReductions,Is.Empty);
            ctx.ArmorReduction[0]=7;ctx.ArmorReduction[1]=3;
            ctx.DefeatedEnemyIndices.Add(0);ctx.SkipAttackIndices.Add(0);
            ctx.CancelledAttacks.Add((0,2));ctx.SuccessfullyBlockedAttacks.Add((0,1));
            ctx.Summoning.RestoreSummoners(ctx);
            Assert.That(ctx.Enemies[0],Is.SameAs(summoner));
            Assert.That(ctx.Enemies[1],Is.SameAs(other));
            Assert.That(ctx.ArmorReduction[0],Is.EqualTo(2));
            Assert.That(ctx.ArmorReduction[1],Is.EqualTo(3));
            Assert.That(ctx.ResistancesRemoved,Does.Contain(0));
            Assert.That(ctx.AttackElementChange[0],Is.EqualTo(Element.Fire));
            Assert.That(ctx.PendingAttackReductions[(0,0)],Is.EqualTo(2));
            Assert.That(ctx.DefeatedEnemyIndices,Is.Empty);
            Assert.That(ctx.SkipAttackIndices,Is.Empty);
            Assert.That(ctx.CancelledAttacks,Is.Empty);
            Assert.That(ctx.SuccessfullyBlockedAttacks,Is.Empty);
            Assert.That(ctx.EnemySupply.DiscardPile.Single(),Is.SameAs(brown));
        }

        [Test] public void FormalPhasesDefendAgainstActualBrownThenAttackOriginalSummoner()
        {
            var summoner=Enemy("monster_049");var brown=Enemy("monster_066");
            var player=new PlayerState(0,"test");
            var gs=new GameState();gs.Players.Add(player);
            var ctx=new ActionContext { Enemies=new List<Monster>{summoner},
                EnemySupply=new EnemyTokenSupply(new[]{brown},new Random(49)) };
            var turn=new TurnMachine(gs);turn.BeginBattle(ctx);
            turn.ApplyRanged(ctx.Enemies,Array.Empty<AttackAllocation>(),ctx);
            Assert.That(gs.CurrentPhase,Is.EqualTo(Phase.Block));
            Assert.That(ctx.Enemies[0],Is.SameAs(brown));
            Assert.That(ctx.Enemies[0].AttackCount,Is.EqualTo(3));
            turn.SetBlock(0,Element.Physical);
            var defense=turn.ResolveDefense(ctx.Enemies,Array.Empty<BlockAllocation>(),ctx);
            Assert.That(defense.TotalWounds,Is.GreaterThan(0));
            Assert.That(gs.CurrentPhase,Is.EqualTo(Phase.Melee));
            Assert.That(ctx.Enemies[0],Is.SameAs(summoner));
            Assert.That(ctx.EnemySupply.DiscardPile.Single(),Is.SameAs(brown));
            Assert.That(player.Fame,Is.Zero);
            Assert.That(ctx.DefeatedEnemyIndices,Is.Empty);
            ctx.CombatPower.AddAttack(summoner.Armor,Element.Physical,AttackType.Melee);
            var kill=turn.Attack(new[]{0},ctx);
            Assert.That(kill.Kills,Is.EqualTo(1));Assert.That(player.Fame,Is.EqualTo(summoner.Fame));
            turn.FinishBattle(ctx);Assert.That(gs.CurrentPhase,Is.EqualTo(Phase.End));
        }

        [Test] public void DestroyingTemporaryDefenderDoesNotRewardOrKillSummoner()
        {
            var original=Enemy("monster_049");var brown=Enemy("monster_066");
            var player=new PlayerState(0,"test");
            var ctx=new ActionContext { InBattle=true,Enemies=new List<Monster>{original},
                EnemySupply=new EnemyTokenSupply(new[]{brown},new Random(49)),SwordFamePerKill=2 };
            CardCombatActions.EnterPhase(ctx,Phase.Block);
            CardCombatActions.DestroyEnemy(player,ctx,0);
            Assert.That(ctx.DefeatedEnemyIndices,Does.Contain(0));
            Assert.That(player.Fame,Is.Zero);Assert.That(player.PuppetTokens.DefeatedThisTurn,Is.Empty);
            CardCombatActions.EnterPhase(ctx,Phase.Melee);
            Assert.That(ctx.DefeatedEnemyIndices,Is.Empty);
            Assert.That(ctx.Enemies[0],Is.SameAs(original));
            Assert.That(ctx.EnemySupply.DiscardPile.Single(),Is.SameAs(brown));
        }

        [Test] public void CombinedDefenseAndMeleeApiRestoresOriginalBeforeAttack()
        {
            var original=Enemy("monster_049");var brown=Enemy("monster_066");var player=new PlayerState(0,"test");
            var gs=new GameState();gs.Players.Add(player);
            var ctx=new ActionContext { Enemies=new List<Monster>{original},
                EnemySupply=new EnemyTokenSupply(new[]{brown},new Random(49)) };
            var turn=new TurnMachine(gs);turn.BeginBattle(ctx);
            turn.ApplyRanged(ctx.Enemies,Array.Empty<AttackAllocation>(),ctx);turn.SetBlock(0,Element.Physical);
            var result=turn.ResolveBattle(ctx.Enemies,Array.Empty<BlockAllocation>(),
                new[]{new AttackAllocation(new[]{0},original.Armor,Element.Physical)},ctx:ctx);
            Assert.That(result.TotalWounds,Is.GreaterThan(0));
            Assert.That(result.KilledIndices,Is.EqualTo(new[]{0}));
            Assert.That(result.FameGain,Is.EqualTo(original.Fame));
            Assert.That(ctx.Enemies[0],Is.SameAs(original));
            Assert.That(ctx.EnemySupply.DiscardPile.Single(),Is.SameAs(brown));
            Assert.That(gs.CurrentPhase,Is.EqualTo(Phase.End));
        }

        [Test] public void BlockKillOnTemporaryDefenderDoesNotCountAsBattleKill()
        {
            var original=Enemy("monster_049");var brown=Enemy("monster_066");var player=new PlayerState(0,"test");
            var ctx=new ActionContext { InBattle=true,Enemies=new List<Monster>{original},
                EnemySupply=new EnemyTokenSupply(new[]{brown},new Random(49)),SwordFamePerKill=2 };
            CardCombatActions.EnterPhase(ctx,Phase.Block);
            var blocks=Enumerable.Range(0,brown.AttackCount).Select(i=>new BlockAllocation(0,
                brown.GetAttack(i).Value*2,Element.ColdFire,KillOnSuccess:true,AttackIndex:i)).ToArray();
            var result=BattleResolver.Resolve(player,ctx.Enemies,blocks,Array.Empty<AttackAllocation>(),ctx:ctx);
            Assert.That(result.KilledIndices,Is.Empty);Assert.That(result.FameGain,Is.Zero);
            Assert.That(result.AllKilled,Is.False);
            Assert.That(player.Fame,Is.Zero);Assert.That(result.TotalWounds,Is.Zero);
            CardCombatActions.EnterPhase(ctx,Phase.Melee);
            Assert.That(ctx.DefeatedEnemyIndices,Is.Empty);Assert.That(ctx.Enemies[0],Is.SameAs(original));
        }

        [TestCase("monster_003",1)] [TestCase("monster_023",2)]
        [TestCase("monster_031",1)] [TestCase("monster_049",1)]
        public void AllPrintedSummonIconsHaveActualCounts(string id,int count)
        {
            var enemy=Enemy(id);Assert.That(enemy.SummonAttack,Is.True);
            Assert.That(enemy.SummonCount,Is.EqualTo(count));Assert.That(enemy.AttackCount,Is.Zero);
            if(id=="monster_003"||id=="monster_023")
            {
                Assert.That(enemy.Abilities,Does.Contain(Ability.PhysicalResist));
                Assert.That(enemy.Abilities.Contains(Ability.Fortified),Is.False);
                Assert.That(enemy.Abilities.Contains(Ability.ColdFireResist),Is.False);
            }
        }

        [Test] public void DoubleSummonOwnsTwoDistinctBrownTokensAndRestoresFollowingEnemyIndex()
        {
            var original=Enemy("monster_023");var other=Enemy("monster_032");
            var brown=Enemy("monster_066");var second=Enemy("monster_011");
            var ctx=new ActionContext { Enemies=new List<Monster>{original,other},
                EnemySupply=new EnemyTokenSupply(new[]{brown,second},new Random(23)) };
            ctx.ArmorReduction[0]=2;ctx.ArmorReduction[1]=1;
            CardCombatActions.EnterPhase(ctx,Phase.Block);
            Assert.That(ctx.Enemies.Count,Is.EqualTo(3));
            Assert.That(ctx.Summoning.Bindings.Count,Is.EqualTo(2));
            Assert.That(ctx.Summoning.Bindings.Select(b=>b.Token),Is.EquivalentTo(new[]{brown,second}));
            Assert.That(ctx.EnemySupply.InUse.Count,Is.EqualTo(2));
            Assert.That(ctx.Enemies[2],Is.SameAs(other));
            Assert.That(ctx.ArmorReduction.ContainsKey(0),Is.False);
            Assert.That(ctx.ArmorReduction[2],Is.EqualTo(1));
            ctx.ArmorReduction[2]=3;ctx.DefeatedEnemyIndices.Add(0);ctx.DefeatedEnemyIndices.Add(1);
            CardCombatActions.EnterPhase(ctx,Phase.Melee);
            Assert.That(ctx.Enemies,Is.EqualTo(new[]{original,other}));
            Assert.That(ctx.Enemies[0],Is.SameAs(original));Assert.That(ctx.Enemies[1],Is.SameAs(other));
            Assert.That(ctx.ArmorReduction[0],Is.EqualTo(2));Assert.That(ctx.ArmorReduction[1],Is.EqualTo(3));
            Assert.That(ctx.DefeatedEnemyIndices,Is.Empty);Assert.That(ctx.EnemySupply.InUse,Is.Empty);
            Assert.That(ctx.EnemySupply.DiscardPile,Is.EquivalentTo(new[]{brown,second}));
        }

        [Test] public void DoubleSummonMissingSecondTokenRejectsBeforeTakingFirst()
        {
            var brown=Enemy("monster_066");var state=new SummoningState();
            var supply=new EnemyTokenSupply(new[]{brown},new Random(23));
            Assert.Throws<InvalidOperationException>(()=>state.BeginDefense(new[]{Enemy("monster_023")},supply,new HashSet<int>(),new HashSet<int>()));
            Assert.That(supply.InUse,Is.Empty);Assert.That(supply.DrawPile.Single(),Is.SameAs(brown));
            Assert.That(state.Bindings,Is.Empty);Assert.That(state.IsActive,Is.False);
        }
    }
}
