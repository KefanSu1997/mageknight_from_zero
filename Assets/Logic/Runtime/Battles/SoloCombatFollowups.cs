using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloCombatFollowupKind { ReduceAttack, ExplosiveArmor, MoveToAttack, MoveToRanged, InfluenceToBlock }
    public sealed record SoloCombatFollowup(SoloCombatFollowupKind Kind,int EnemySlot,int AttackIndex=0,int Amount=1);
    public static class SoloCombatFollowups
    {
        public static void Execute(PveJourneySession j,SoloCombatFollowup input)
        {
            if(j==null||input==null)throw new ArgumentNullException();
            var b=j.Battle??throw new InvalidOperationException("Actual battle required.");
            bool conversion=input.Kind is SoloCombatFollowupKind.MoveToAttack or SoloCombatFollowupKind.MoveToRanged or SoloCombatFollowupKind.InfluenceToBlock;
            if(conversion?(input.EnemySlot!=-1||input.AttackIndex!=0||input.Amount<=0):input.Amount!=1)
                throw new InvalidOperationException("Followup parameters do not match operation.");
            switch(input.Kind)
            {
                case SoloCombatFollowupKind.MoveToAttack:b.ConvertMovement(false,input.Amount);break;
                case SoloCombatFollowupKind.MoveToRanged:b.ConvertMovement(true,input.Amount);break;
                case SoloCombatFollowupKind.InfluenceToBlock:b.ConvertInfluence(input.Amount);break;
                case SoloCombatFollowupKind.ReduceAttack:b.ReduceAttack(input.EnemySlot,input.AttackIndex);break;
                case SoloCombatFollowupKind.ExplosiveArmor:
                    if(input.EnemySlot< -1||input.AttackIndex!=0)throw new InvalidOperationException("Invalid armor followup parameters.");
                    b.ResolveExplosiveArmor(input.EnemySlot);break;
                default:throw new InvalidOperationException("Unknown combat followup.");
            }
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloCombatFollowup input)=>e.Apply(j=>Execute(j,input));
        public static IEnumerable<SoloCombatFollowup> Legal(PveJourneySession j)
        {
            if(j?.Battle==null||j.Context?.InBattle!=true||!j.Game.HasActiveTurn||j.Game.IsEndingTurn)yield break;
            var proposals=new List<SoloCombatFollowup>();
            if(j.Context.MoveCostAttack>0)
                for(int n=1;n<=j.Context.MovementPool/j.Context.MoveCostAttack;n++)proposals.Add(new(SoloCombatFollowupKind.MoveToAttack,-1,Amount:n));
            if(j.Context.MoveCostRanged>0)
                for(int n=1;n<=j.Context.MovementPool/j.Context.MoveCostRanged;n++)proposals.Add(new(SoloCombatFollowupKind.MoveToRanged,-1,Amount:n));
            if(j.Context.InfluenceAsBlock)
                for(int n=1;n<=j.Context.InfluencePool;n++)proposals.Add(new(SoloCombatFollowupKind.InfluenceToBlock,-1,Amount:n));
            if(j.Context.AttackReductionTimes>0)
                for(int e=0;e<j.Battle.Enemies.Count;e++)
                    for(int a=0;a<j.Battle.Enemies[e].AttackCount;a++)proposals.Add(new(SoloCombatFollowupKind.ReduceAttack,e,a));
            if(j.Context.ExplosiveBoltArmorUses>0)
                for(int e=-1;e<j.Battle.Enemies.Count;e++)proposals.Add(new(SoloCombatFollowupKind.ExplosiveArmor,e));
            var checkpoint=DemoStateCheckpoint.Capture(j);
            foreach(var input in proposals)
            {
                bool legal=false;
                try {Execute(j,input);legal=true;}
                catch(InvalidOperationException) { }
                finally {checkpoint.Restore();}
                if(legal)yield return input;
            }
        }
    }
}
