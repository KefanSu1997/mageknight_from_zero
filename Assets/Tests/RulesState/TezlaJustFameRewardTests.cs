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
    public sealed class TezlaJustFameRewardTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Original(string id) => CardJsonLoader.LoadMonsters().Single(e=>e.Id==id);
        [Test] public void OriginalFactionKillsUseRangedMeleeAndDirectPathsExactlyOnce()
        {
            var enemies=new[]{Original("monster_015"),Original("monster_054"),Original("monster_057")};
            Assert.That(enemies.Select(e=>e.Faction),Is.All.EqualTo(EnemyFaction.Elementalist));
            Assert.That(enemies.Select(e=>e.Fame),Is.EqualTo(new[]{3,4,1}));
            var player=new PlayerState();
            var battle=new PveBattleSession(player,enemies,CardJsonLoader.LoadBasicActions(),enemyRewardMode:EnemyRewardMode.TezlaJustFame);
            var c=battle.Context;
            Assert.That(EnemyRewardRules.GetFactionFame(enemies[0],c),Is.EqualTo(1));
            // The living original bat defends the first actually attacked token.
            c.CombatPower.AddAttack(5,Element.Physical,AttackType.Ranged);
            var ranged=battle.Attack(0);Assert.That(ranged.RequiredPower,Is.EqualTo(5));Assert.That(ranged.Fame,Is.EqualTo(4));
            Assert.That(c.EnemyRewards.Grants.Single().Enemy,Is.SameAs(enemies[0]));
            Assert.That(c.EnemyRewards.Grants.Single().Source,Is.EqualTo(EnemyDefeatSource.Ranged));
            CardCombatActions.DestroyEnemy(player,c,2);
            Assert.That(player.Fame,Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.DestroyEnemy(player,c,2));
            battle.Advance();c.CombatPower.AddBlock(6,Element.Fire);battle.Block(1);battle.Advance();battle.Advance();
            c.CombatPower.AddAttack(7);var melee=battle.Attack(1);
            Assert.That(melee.RequiredPower,Is.EqualTo(7));Assert.That(melee.Fame,Is.EqualTo(5));
            Assert.That(c.EnemyRewards.Count,Is.EqualTo(3));Assert.That(c.EnemyRewards.TotalFame,Is.EqualTo(3));
            Assert.That(c.EnemyRewards.Grants.Select(g=>g.Source),Is.EqualTo(new[]{EnemyDefeatSource.Ranged,EnemyDefeatSource.DirectEffect,EnemyDefeatSource.Melee}));
            Assert.That(player.Fame,Is.EqualTo(11));Assert.That(player.Wounds,Is.Zero);
            battle.Advance();Assert.That(c.EnemyRewards.TotalFame,Is.EqualTo(3));
            Assert.That(enemies.Select(e=>e.Fame),Is.EqualTo(new[]{3,4,1}),"No printed fame mutation");
            Assert.Throws<InvalidOperationException>(()=>battle.Attack(1));Assert.That(player.Fame,Is.EqualTo(11));
        }
        [Test] public void SuccessfulBlockDestructionAlsoGrantsOfficialFactionBonusOnce()
        {
            var enemy=Original("monster_057");var player=new PlayerState();
            var c=new ActionContext { InBattle=true,Enemies=new[]{enemy},EnemyRewardMode=EnemyRewardMode.TezlaJustFame };
            var result=BattleResolver.Resolve(player,c.Enemies,new[]{new BlockAllocation(0,1,Element.Fire,KillOnSuccess:true)},Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(result.KilledIndices,Is.EqualTo(new[]{0}));Assert.That(result.FameGain,Is.EqualTo(2));
            Assert.That(c.EnemyRewards.Grants.Single().Source,Is.EqualTo(EnemyDefeatSource.BlockEffect));
            Assert.That(player.Fame,Is.EqualTo(2));Assert.That(player.Wounds,Is.Zero);
            var repeated=BattleResolver.Resolve(player,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(repeated.FameGain,Is.Zero);Assert.That(c.EnemyRewards.Count,Is.EqualTo(1));
        }
        [Test] public void DefaultModeAndNonFactionEnemyKeepPrintedRewardsAndIdentityIsNotRemovableAbility()
        {
            var enemy=Original("monster_015");var player=new PlayerState();
            var battle=new PveBattleSession(player,new[]{enemy},CardJsonLoader.LoadBasicActions());
            Assert.That(battle.Context.EnemyRewardMode,Is.EqualTo(EnemyRewardMode.PrintedFameOnly));
            Assert.That(EnemyRewardRules.GetFactionFame(enemy,battle.Context),Is.Zero);
            CardCombatActions.DestroyEnemy(player,battle.Context,0);Assert.That(player.Fame,Is.EqualTo(3));
            Assert.That(battle.Context.EnemyRewards.Count,Is.Zero);
            var ordinary=Original("monster_032");player=new PlayerState();
            battle=new PveBattleSession(player,new[]{ordinary},CardJsonLoader.LoadBasicActions(),enemyRewardMode:EnemyRewardMode.TezlaJustFame);
            CardCombatActions.DestroyEnemy(player,battle.Context,0);Assert.That(player.Fame,Is.EqualTo(ordinary.Fame));
            Assert.That(battle.Context.EnemyRewards.Count,Is.Zero);Assert.That(ordinary.Faction,Is.EqualTo(EnemyFaction.None));
            Assert.That(EnemyRewardRules.PreviewBonus(enemy with {Abilities=Array.Empty<Ability>()},EnemyRewardMode.TezlaJustFame),Is.EqualTo(1));
            Assert.That(enemy.Fame,Is.EqualTo(3));
        }
        [Test] public void JustFameOriginalSixCardBattleAndNextFormalRoundKeepSamePlayerWithoutOldRewardReplay()
        {
            var p=new PlayerState {HeroId="Wolfhawk",Fame=35,Armor=3};
            p.Mana.Crystals[ManaColor.Red]=1;p.Mana.Crystals[ManaColor.Blue]=1;
            foreach(string id in new[]{"basic_card_010","basic_card_011","advanced_card_000","basic_card_016","basic_card_021","advanced_card_034"})
                p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            var originalCards=p.Deck.Hand.ToArray();DeedCard Card(string id)=>p.Deck.Hand.Single(x=>x.Id==id);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),manaRandom:new Random(0),supplyRandom:new Random(0),enemyRewardMode:EnemyRewardMode.TezlaJustFame);
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();var enemies=new[]{Original("monster_057"),Original("monster_032"),Original("monster_032")};var b=j.BeginBattle(enemies);
            b.Play(Card("basic_card_010"),"ranged");b.Play(Card("basic_card_011"),"strong");b.Play(Card("advanced_card_000"),"strong");b.ChooseDefend(0,1,1,2);
            Assert.That(b.Attack(1,2).Fame,Is.EqualTo(5));Assert.That(b.Context.EnemyRewards.Count,Is.Zero);
            b.Advance();b.Play(Card("basic_card_016"),"block");b.Block(0,0);b.Play(Card("basic_card_021"),"block");b.Block(0,1);b.Advance();b.Advance();
            b.Play(Card("advanced_card_034"),"base");Assert.That(b.Attack(0).Fame,Is.EqualTo(2));
            var firstContext=b.Context;Assert.That(firstContext.EnemyRewards.Grants.Single().Enemy,Is.SameAs(enemies[0]));
            b.Advance();j.EndTurn();Assert.That(p.Fame,Is.EqualTo(42));Assert.That(firstContext.EnemyRewards.TotalFame,Is.EqualTo(1));
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DrawPileCount,Is.Zero);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(6));
            j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();j.SelectTactic("night_1");j.BeginTurn();
            Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Night));
            Assert.That(j.Player,Is.SameAs(p));Assert.That(j.Context.EnemyRewardMode,Is.EqualTo(EnemyRewardMode.TezlaJustFame));
            Assert.That(j.Context.EnemyRewards.Count,Is.Zero);Assert.Throws<InvalidOperationException>(()=>j.BeginBattle(new[]{enemies[0]}));
            Assert.Throws<InvalidOperationException>(()=>b.Attack(0));Assert.That(p.Fame,Is.EqualTo(42));
            Assert.That(j.CompletedTurns,Is.EqualTo(1));Assert.That(j.CompletedBattles,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(42));Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.Hand.Count,Is.EqualTo(6));
            Assert.That(originalCards.All(card=>p.Deck.Hand.Any(d=>ReferenceEquals(card,d))),Is.True);
            Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.DrawPileCount,Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);
        }
    }
}
