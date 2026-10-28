using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloEnemyAttack(int Index,int Power,string Element,string[] Abilities,bool Blocked,bool Cancelled);
    public sealed record SoloCombatEnemy(int Slot,string Id,bool Defeated,int PrintedArmor,int EffectiveArmor,
        string[] Abilities,SoloEnemyAttack[] Attacks);
    public sealed record SoloAttackSource(int Slot,int Value,string Element,string Type,bool Available,bool Sweep,
        int RequiredEnemy,int[] ExcludedEnemies,int UnitSlot,string Skill,int StarBowChoice,
        int FamePerKill,int ReputationPerKill,int FameIfKill,int CrystalsPerKill,int ArmorReductionsPerKill)
    {
        public string SourceCardId { get; init; }
        public int PlayedCardSlot { get; init; } = -1;
    }
    public sealed record SoloBlockSource(int Slot,int Value,string Element,int RequiredEnemy,int RequiredAttack,
        int Target,int Attack,int UnitSlot,string Skill,bool CountSymbols,int ArmorReduction,int AttackOnSuccess,
        bool KillOnSuccess,int DistinctGroup,bool DoubleSwift,bool ReduceByExcess);
    public sealed record SoloPublicCombat(bool Active,string Phase,SoloCombatEnemy[] Enemies,
        SoloAttackSource[] Attacks,SoloBlockSource[] Blocks)
    {
        public int AttackReduction { get; init; }
        public int AttackReductionTimes { get; init; }
        public int ExplosiveArmorUses { get; init; }
        public int MoveCostAttack { get; init; }
        public int MoveCostRanged { get; init; }
        public bool InfluenceAsBlock { get; init; }
        public string InfluenceBlockElement { get; init; }
    }

    public sealed partial class PveJourneySession
    {
        /// <summary>Actual battle participants and physical contribution indices. No fixed slot caps.
        /// Pending card/skill obligations require their separate structured branch.</summary>
        public SoloPublicCombat ObserveSoloCombat()
        {
            var b=Battle;var c=Context;
            if(b==null||c==null||!c.InBattle)return new(false,"Outside",Array.Empty<SoloCombatEnemy>(),
                Array.Empty<SoloAttackSource>(),Array.Empty<SoloBlockSource>());
            int EnemySlot(Monster enemy)=>enemy==null?-1:Enumerable.Range(0,b.Enemies.Count)
                .Where(i=>ReferenceEquals(b.Enemies[i],enemy)).DefaultIfEmpty(-1).First();
            int UnitSlot(UnitState unit)=>unit==null?-1:Player.Units.FindIndex(u=>ReferenceEquals(u,unit));
            var available=c.CombatPower.AvailableAttacks(b.Phase,c.AllAttacksSiege);
            var enemies=b.Enemies.Select((enemy,i)=>new SoloCombatEnemy(i,enemy.Id,c.DefeatedEnemyIndices.Contains(i),
                enemy.Armor,CombatMath.EffectiveArmor(enemy,i,c,b.Phase),enemy.Abilities.Select(x=>x.ToString()).ToArray(),
                Enumerable.Range(0,enemy.AttackCount).Select(a=>
                {
                    var profile=CardCombatActions.DefenseProfile(enemy,i,c,a);
                    return new SoloEnemyAttack(a,profile.Attack,profile.AttackElement.ToString(),
                        profile.Abilities.Select(x=>x.ToString()).ToArray(),b.IsBlocked(i,a),
                        c.IsAttackCancelled(i,a)||c.ColdStareSkips(enemy));
                }).ToArray())).ToArray();
            var attacks=c.CombatPower.Attacks.Select((a,i)=>new SoloAttackSource(i,a.Value,a.Element.ToString(),a.Type.ToString(),
                available.Any(x=>ReferenceEquals(x,a)),a.IsSweep,EnemySlot(a.RequiredEnemy),
                Enumerable.Range(0,b.Enemies.Count).Where(j=>a.Excludes(b.Enemies[j],j)).ToArray(),UnitSlot(a.SourceUnit),
                a.SourceSkill?.SourceId,a.StarBowChoice,a.FamePerDefeatedEnemy,a.ReputationPerDefeatedEnemy,
                a.FameIfAnyDefeated,a.CrystalsPerDefeatedEnemy,a.ArmorReductionsPerDefeatedEnemy)
                {SourceCardId=(a.SourceCard as DeedCard)?.Id,PlayedCardSlot=a.SourceCard==null?-1:
                    Enumerable.Range(0,Player.Deck.PlayedCards.Count).Where(k=>ReferenceEquals(Player.Deck.PlayedCards[k],a.SourceCard))
                        .DefaultIfEmpty(-1).First()}).ToArray();
            var blocks=c.CombatPower.Blocks.Select((a,i)=>new SoloBlockSource(i,a.Value,a.Element.ToString(),EnemySlot(a.RequiredEnemy),
                a.RequiredAttackIndex,a.TargetIndex,a.AttackIndex,UnitSlot(a.SourceUnit),a.SourceSkill?.SourceId,a.CountEnemySymbols,
                a.ArmorReductionOnSuccess,a.AttackOnSuccess,a.KillOnSuccess,a.DistinctAttackGroup,a.DoubleAgainstSwift,a.ReduceArmorByExcess)).ToArray();
            return new(true,b.Phase.ToString(),enemies,attacks,blocks)
                {AttackReduction=c.AttackReduction,AttackReductionTimes=c.AttackReductionTimes,
                    ExplosiveArmorUses=c.ExplosiveBoltArmorUses,MoveCostAttack=c.MoveCostAttack,MoveCostRanged=c.MoveCostRanged,
                    InfluenceAsBlock=c.InfluenceAsBlock,InfluenceBlockElement=c.InfluenceBlockElement.ToString()};
        }
    }
}
