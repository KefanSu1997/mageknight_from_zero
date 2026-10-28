using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public void UseOfferedUnit(UnitCard card,int abilityIndex,int option=0,int target=-1,ManaColor[] colors=null,int boost=0,UnitState unitTarget=null,int attackIndex=0)
        {
            if(!Game.HasActiveTurn||Game.IsEndingTurn||Context==null)throw new InvalidOperationException("请在自己的实际回合发动供应能力");
            RepeatedEffectResolution.RequireCompleted(Context);
            int index=Context.UnitOffer?.ToList().FindIndex(c=>ReferenceEquals(c,card))??-1;
            if(index<0||abilityIndex<0||abilityIndex>=card.Actions.Length)throw new InvalidOperationException("请选择实际供应卡与原能力");
            var a=card.Actions[abilityIndex];bool attack=a.Kind==UnitAbilityKind.Attack||a.Kind==UnitAbilityKind.AttackOrBlock&&option==0;
            bool block=a.Kind==UnitAbilityKind.Block||a.Kind==UnitAbilityKind.AttackOrBlock&&option==1;
            if(Context.InBattle&&(block&&Context.CombatPhase!=Phase.Block||attack&&Context.CombatPhase!=Phase.Melee&&
                !(Context.CombatPhase==Phase.Ranged&&(a.AttackType!=AttackType.Melee||Context.AllAttacksSiege))))
                throw new InvalidOperationException("供应能力必须符合实际战斗阶段");
            if(target>=0&&(Context.Enemies==null||target>=Context.Enemies.Count||Context.DefeatedEnemyIndices.Contains(target)))
                throw new InvalidOperationException("请选择本场实际存活敌人");
            CardUnitActions.UseFromOffer(Player,Context,index,abilityIndex,option,target,colors,boost,unitTarget:unitTarget,attackIndex:attackIndex);
        }
    }
}
