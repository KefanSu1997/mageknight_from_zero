using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloSkillInput(int SkillSlot,int Option,int EnemySlot=-1,int AttackIndex=0,int UnitSlot=-1,int WoundSlot=-1,
        int HandSlot=-1,SidewaysType? Sideways=null,ManaColor? Color=null,bool ForgoSourceDice=false,int AttackSourceSlot=-1,
        int PuppetSlot=-1,int[] EnemySlots=null);
    public static class SoloSkillInputs
    {
        // These require their own complete physical-parameter contracts, not a bare skill toggle.
        private static bool Specialized(string id)=>id is "hero_skill_pool_001:05" or "hero_skill_pool_003:06"
            or "hero_skill_pool_004:04" or "hero_skill_pool_002:06" or "hero_skill_pool_001:06"
            or "hero_skill_pool_005:06" or "hero_skill_pool_005:07" or "hero_skill_pool_007:06" or "hero_skill_pool_007:07";
        private static bool Wound(string id)=>id is "hero_skill_pool_001:08" or "hero_skill_pool_003:04" or "hero_skill_pool_007:05";
        public static void Execute(PveJourneySession j,SoloSkillInput input)
        {
            if(j==null||input==null)throw new ArgumentNullException();
            var p=j.Player;
            if(input.SkillSlot<0||input.SkillSlot>=p.Skills.Count)throw new InvalidOperationException("Invalid owned skill slot.");
            var skill=p.Skills[input.SkillSlot];
            if(input.EnemySlots!=null)
            {
                if(skill.SourceId!=ForkedLightningActions.SourceId||!j.Context.InBattle||j.Battle==null
                    ||input.Option!=0||input.PuppetSlot!=-1||input.AttackSourceSlot!=-1||input.EnemySlot!=-1
                    ||input.AttackIndex!=0||input.UnitSlot!=-1||input.WoundSlot!=-1||input.HandSlot!=-1
                    ||input.Sideways.HasValue||input.Color.HasValue||input.ForgoSourceDice
                    ||input.EnemySlots.Length<1||input.EnemySlots.Length>3
                    ||input.EnemySlots.Distinct().Count()!=input.EnemySlots.Length
                    ||input.EnemySlots.Any(i=>i<0||i>=j.Battle.Enemies.Count))
                    throw new InvalidOperationException("Invalid complete forked lightning targets.");
                j.Battle.UseForkedLightning(skill,input.EnemySlots);
                return;
            }
            if(input.PuppetSlot>=0)
            {
                if(skill.SourceId!=PuppetMasterActions.SkillId||input.Option<0||input.Option>2
                    ||!j.Game.HasActiveTurn||j.Game.IsEndingTurn
                    ||input.AttackSourceSlot!=-1||input.EnemySlot!=-1||input.AttackIndex!=0||input.UnitSlot!=-1
                    ||input.WoundSlot!=-1||input.HandSlot!=-1||input.Sideways.HasValue||input.Color.HasValue||input.ForgoSourceDice)
                    throw new InvalidOperationException("Invalid complete puppet operation.");
                var tokens=input.Option==0?p.PuppetTokens.DefeatedThisTurn:p.PuppetTokens.KeptTokens;
                if(input.PuppetSlot>=tokens.Count)throw new InvalidOperationException("Invalid physical puppet slot.");
                if(j.Context.InBattle)j.Battle.UsePuppetToken(skill,tokens[input.PuppetSlot],input.Option);
                else if(input.Option==0)PuppetMasterActions.Keep(p,j.Context,skill,tokens[input.PuppetSlot]);
                else throw new InvalidOperationException("Puppet contribution requires battle.");
                return;
            }
            if(input.PuppetSlot!=-1)throw new InvalidOperationException("Invalid puppet slot.");
            if(input.AttackSourceSlot>=0)
            {
                if(skill.SourceId!="hero_skill_pool_004:04"||!j.Context.InBattle||j.Battle==null
                    ||input.Option!=0||input.EnemySlot!=-1||input.AttackIndex!=0||input.UnitSlot!=-1||input.WoundSlot!=-1
                    ||input.HandSlot!=-1||input.Sideways.HasValue||input.Color.HasValue||input.ForgoSourceDice
                    ||input.AttackSourceSlot>=j.Context.CombatPower.Attacks.Count)
                    throw new InvalidOperationException("Invalid actual card attack contribution selection.");
                j.Battle.BoostCardAttack(skill,j.Context.CombatPower.Attacks[input.AttackSourceSlot]);
                return;
            }
            if(input.AttackSourceSlot!=-1)throw new InvalidOperationException("Invalid attack contribution slot.");
            if(input.Sideways.HasValue)
            {
                if(input.Option!=0||input.EnemySlot!=-1||input.AttackIndex!=0||input.UnitSlot!=-1||input.WoundSlot!=-1
                    ||input.HandSlot<0||input.HandSlot>=p.Deck.Hand.Count)
                    throw new InvalidOperationException("Invalid complete sideways skill parameters.");
                if(j.Context.InBattle)
                {
                    var expected=j.Battle.Phase==Phase.Block?SidewaysType.Block:SidewaysType.Attack;
                    if(input.Sideways.Value!=expected)throw new InvalidOperationException("Sideways purpose differs from battle phase.");
                    j.Battle.PlaySidewaysWithSkill(skill,p.Deck.Hand[input.HandSlot],input.Color,input.ForgoSourceDice);
                }
                else j.PlaySidewaysWithSkill(skill,p.Deck.Hand[input.HandSlot],input.Sideways.Value,input.Color,input.ForgoSourceDice);
                return;
            }
            if(input.HandSlot!=-1||input.Color.HasValue||input.ForgoSourceDice)
                throw new InvalidOperationException("Unrelated sideways skill parameters.");
            if(Specialized(skill.SourceId))throw new InvalidOperationException("Skill requires specialized complete parameters.");
            bool needsWound=Wound(skill.SourceId),needsUnit=skill.SourceId=="hero_skill_pool_002:02";
            if(needsWound?(input.WoundSlot<0||input.WoundSlot>=p.Deck.Hand.Count||p.Deck.Hand[input.WoundSlot].Type!=CardType.Wound):input.WoundSlot!=-1)
                throw new InvalidOperationException("Invalid actual wound parameter.");
            if(needsUnit?(input.UnitSlot<0||input.UnitSlot>=p.Units.Count):input.UnitSlot!=-1)
                throw new InvalidOperationException("Invalid actual unit parameter.");
            var wound=needsWound?p.Deck.Hand[input.WoundSlot]:null;
            if(j.Context.InBattle)
            {
                bool target=BattleSkillChoice.NeedsEnemy(skill.SourceId)&&(skill.SourceId!="hero_skill_pool_001:01"||input.Option==1);
                if(!target&&input.EnemySlot!=-1||!BattleSkillChoice.NeedsAttack(skill.SourceId,input.Option)&&input.AttackIndex!=0)
                    throw new InvalidOperationException("Unrelated enemy/attack parameters.");
                j.Battle.UseSkill(skill,input.Option,input.EnemySlot,wound,input.AttackIndex);
            }
            else
            {
                if(input.EnemySlot!=-1||input.AttackIndex!=0)throw new InvalidOperationException("Outside skill cannot target an enemy attack.");
                j.UseOutsideSkill(skill,input.Option,needsUnit?p.Units[input.UnitSlot]:null,wound);
            }
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloSkillInput input)=>e.Apply(j=>Execute(j,input));
        public static IEnumerable<SoloSkillInput> SidewaysLegal(SoloConquestEpisode e)
        {
            var j=e.Journey;var p=j.Player;
            if(e.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn||j.Context==null)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(e);
            for(int s=0;s<p.Skills.Count;s++)
            {
                string id=p.Skills[s].SourceId;
                if(id is not ("hero_skill_pool_005:07" or "hero_skill_pool_007:06" or "hero_skill_pool_007:07"))continue;
                var colors=id=="hero_skill_pool_005:07"?Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>().Select(x=>(ManaColor?)x):new ManaColor?[]{null};
                for(int h=0;h<p.Deck.Hand.Count;h++)
                foreach(var type in Enum.GetValues(typeof(SidewaysType)).Cast<SidewaysType>())
                foreach(var color in colors)
                foreach(bool forgo in id=="hero_skill_pool_007:07"?new[]{false,true}:new[]{false})
                {
                    var input=new SoloSkillInput(s,0,HandSlot:h,Sideways:type,Color:color,ForgoSourceDice:forgo);bool legal=false;
                    try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                    finally{checkpoint.Restore();}
                    if(legal)yield return input;
                }
            }
        }
        public static IEnumerable<SoloSkillInput> Legal(SoloConquestEpisode e)
        {
            var j=e.Journey;var p=j.Player;
            if(e.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn||j.Context==null)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(e);var seen=new HashSet<SoloSkillInput>();
            for(int s=0;s<p.Skills.Count;s++)
            {
                var skill=p.Skills[s];
                if(skill.SourceId==ForkedLightningActions.SourceId&&j.Context.InBattle)
                {
                    var live=Enumerable.Range(0,j.Battle.Enemies.Count).Where(i=>!j.Context.DefeatedEnemyIndices.Contains(i)).ToArray();
                    foreach(var targets in TargetCombinations(live,0,new List<int>()))
                    {
                        var input=new SoloSkillInput(s,0,EnemySlots:targets);bool legal=false;
                        try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                        finally{checkpoint.Restore();}
                        if(legal)yield return input;
                    }
                }
                if(skill.SourceId==PuppetMasterActions.SkillId)
                {
                    for(int mode=0;mode<3;mode++)
                    for(int token=0;token<(mode==0?p.PuppetTokens.DefeatedThisTurn.Count:p.PuppetTokens.KeptTokens.Count);token++)
                    {
                        var input=new SoloSkillInput(s,mode,PuppetSlot:token);bool legal=false;
                        try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                        finally{checkpoint.Restore();}
                        if(legal)yield return input;
                    }
                }
                if(skill.SourceId=="hero_skill_pool_004:04"&&j.Context.InBattle)
                {
                    for(int source=0;source<j.Context.CombatPower.Attacks.Count;source++)
                    {
                        var input=new SoloSkillInput(s,0,AttackSourceSlot:source);bool legal=false;
                        try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                        finally{checkpoint.Restore();}
                        if(legal)yield return input;
                    }
                }
                if(Specialized(skill.SourceId))continue;
                var enemies=j.Context.InBattle&&BattleSkillChoice.NeedsEnemy(skill.SourceId)
                    ?Enumerable.Range(-1,j.Battle.Enemies.Count+1):new[]{-1};
                foreach(int enemy in enemies)
                for(int attack=0;attack<(enemy>=0?j.Battle.Enemies[enemy].AttackCount:1);attack++)
                foreach(var choice in j.Context.InBattle?j.Battle.SkillChoices(skill,enemy,attack):j.OutsideSkillChoices(skill))
                foreach(int unit in skill.SourceId=="hero_skill_pool_002:02"?Enumerable.Range(0,p.Units.Count):new[]{-1})
                foreach(int wound in Wound(skill.SourceId)?Enumerable.Range(0,p.Deck.Hand.Count).Where(i=>p.Deck.Hand[i].Type==CardType.Wound):new[]{-1})
                {
                    var input=new SoloSkillInput(s,choice.Option,enemy,attack,unit,wound);
                    if(!seen.Add(input))continue;
                    bool legal=false;
                    try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                    finally{checkpoint.Restore();}
                    if(legal)yield return input;
                }
            }
        }
        private static IEnumerable<int[]> TargetCombinations(int[] slots,int start,List<int> selected)
        {
            if(selected.Count>0)yield return selected.ToArray();
            if(selected.Count==3)yield break;
            for(int i=start;i<slots.Length;i++)
            {
                selected.Add(slots[i]);
                foreach(var targets in TargetCombinations(slots,i+1,selected))yield return targets;
                selected.RemoveAt(selected.Count-1);
            }
        }
    }
}
