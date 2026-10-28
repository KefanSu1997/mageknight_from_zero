using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloUnitInput(int UnitSlot,int Ability,int Option=0,int TargetSlot=-1,
        int AttackIndex=0,int Boost=0,ManaColor[] Colors=null,ScoutTarget Scout=null,int LeadershipSlot=-1);
    public static class SoloUnitInputs
    {
        public static void Execute(PveJourneySession j,SoloUnitInput input)
        {
            if(j==null || input==null)throw new ArgumentNullException();
            if(input.UnitSlot<0 || input.UnitSlot>=j.Player.Units.Count)throw new InvalidOperationException("Invalid actual unit slot.");
            var unit=j.Player.Units[input.UnitSlot];
            if(input.Ability<0 || input.Ability>=unit.Card.Actions.Length)throw new InvalidOperationException("Invalid printed unit ability.");
            if(input.LeadershipSlot < -1 || input.LeadershipSlot>=j.Player.Skills.Count
                ||input.LeadershipSlot>=0&&(!j.Context.InBattle||j.Player.Skills[input.LeadershipSlot].SourceId!="hero_skill_pool_002:06"))
                throw new InvalidOperationException("Leadership requires an actual owned leadership skill and combat ability.");
            var leadership=input.LeadershipSlot<0?null:j.Player.Skills[input.LeadershipSlot];
            if(j.Context.InBattle)
            {
                if(input.Scout!=null)throw new InvalidOperationException("Scouting is an outside-world operation.");
                (j.Battle??throw new InvalidOperationException("Missing battle")).UseUnit(unit,input.Ability,input.Option,
                    input.TargetSlot,input.Colors,input.Boost,input.AttackIndex,leadership);
            }
            else
            {
                var kind=unit.Card.Actions[input.Ability].Kind;
                if(input.Boost!=0 || input.AttackIndex!=0 || input.TargetSlot < -1
                    ||kind!=UnitAbilityKind.ReadyUnit&&input.TargetSlot!=-1)
                    throw new InvalidOperationException("Outside unit parameters do not match the printed ability.");
                UnitState target=null;
                if(kind==UnitAbilityKind.ReadyUnit)
                {
                    if(input.TargetSlot<0 || input.TargetSlot>=j.Player.Units.Count)throw new InvalidOperationException("Invalid owned ready target.");
                    target=j.Player.Units[input.TargetSlot];
                }
                j.UseOutsideUnit(unit,input.Ability,input.Option,target,input.Colors,input.Scout);
            }
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloUnitInput input)=>e.Apply(j=>Execute(j,input));
        /// <summary>Native printed abilities with dynamic targets and optional owned leadership in one operation. Borrowed payment remains a separate branch.</summary>
        public static IEnumerable<SoloUnitInput> Legal(SoloConquestEpisode e)
        {
            if(e==null)throw new ArgumentNullException(nameof(e));
            var j=e.Journey;
            if(e.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(e);
            for(int u=0;u<j.Player.Units.Count;u++)
            for(int a=0;a<j.Player.Units[u].Card.Actions.Length;a++)
            {
                var ability=j.Player.Units[u].Card.Actions[a];var kind=ability.Kind;
                if(kind==UnitAbilityKind.ScoutToken)
                {
                    if(j.Context.InBattle)continue;
                    // Propose only visible locations/token counts and supply colors, never faces.
                    var scouts=new List<ScoutTarget>();
                    foreach(var color in Enum.GetValues(typeof(EnemyTokenColor)).Cast<EnemyTokenColor>())
                        if(color!=EnemyTokenColor.Unknown)scouts.Add(new(SupplyColor:color));
                    foreach(var entry in j.Game.Map.Places.OrderBy(p=>p.Key.Q).ThenBy(p=>p.Key.R))
                        for(int token=0;token<Math.Max(1,entry.Value.Defenders.Count);token++)
                            scouts.Add(new(entry.Key,token));
                    foreach(var scout in scouts)
                    {
                        var proposal=new SoloUnitInput(u,a,Scout:scout);bool accepted=false;
                        try {Apply(e,proposal);accepted=true;}
                        catch(InvalidOperationException) { }
                        finally {checkpoint.Restore();}
                        if(accepted)yield return proposal;
                    }
                    continue;
                }
                int options=kind is UnitAbilityKind.AttackOrBlock or UnitAbilityKind.MovementOrInfluence or UnitAbilityKind.TransformAttacks?2:1;
                bool enemy=kind is UnitAbilityKind.FreezeArmor or UnitAbilityKind.StopUnfortifiedEnemy or UnitAbilityKind.StopEnemy
                    or UnitAbilityKind.DestroyBlockedEnemy or UnitAbilityKind.ReduceEnemyArmor or UnitAbilityKind.RemoveEnemyDefenses
                    or UnitAbilityKind.WeakenEnemyAttack or UnitAbilityKind.InterceptEnemyAttack
                    ||ability.RemoveTargetFortifications||ability.RemoveTargetResistances;
                bool attack=kind is UnitAbilityKind.WeakenEnemyAttack or UnitAbilityKind.InterceptEnemyAttack;
                var targets=kind==UnitAbilityKind.ReadyUnit?Enumerable.Range(0,j.Player.Units.Count)
                    :enemy?Enumerable.Range(0,j.Context.InBattle?j.Battle.Enemies.Count:0):new[]{-1};
                var colors=new List<ManaColor[]>{Array.Empty<ManaColor>()};
                if(kind==UnitAbilityKind.GainChosenTokens)
                {
                    colors.Clear();
                    foreach(var c1 in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black})
                    foreach(var c2 in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black})colors.Add(new[]{c1,c2});
                }
                for(int option=0;option<options;option++)foreach(int target in targets)
                foreach(int index in attack?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                for(int boost=0;boost<(ability.RedBlueBoost?4:1);boost++)foreach(var color in colors)
                foreach(int leader in new[]{-1}.Concat(j.Context.InBattle
                    ?Enumerable.Range(0,j.Player.Skills.Count).Where(s=>j.Player.Skills[s].SourceId=="hero_skill_pool_002:06")
                    :Enumerable.Empty<int>()))
                {
                    var input=new SoloUnitInput(u,a,option,target,index,boost,color,LeadershipSlot:leader);bool valid=false;
                    try {Apply(e,input);valid=true;}
                    catch(InvalidOperationException) { }
                    finally {checkpoint.Restore();}
                    if(valid)yield return input;
                }
            }
        }
    }
}
