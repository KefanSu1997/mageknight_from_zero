using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloBlockInput(int EnemySlot,int AttackIndex,int[] ContributionSlots);
    public sealed record SoloDamageAllocation(int EnemySlot,int AttackIndex,int[] UnitSlots);
    public sealed record SoloDamageInput(SoloDamageAllocation[] Allocations);
    public static class SoloDefenseInputs
    {
        public static void Block(PveJourneySession j,SoloBlockInput input)
        {
            if(j==null || input==null || input.ContributionSlots==null)throw new ArgumentNullException();
            if(input.ContributionSlots.Any(i=>i<0 || i>=j.Context.CombatPower.Blocks.Count)
                ||input.ContributionSlots.Distinct().Count()!=input.ContributionSlots.Length)
                throw new InvalidOperationException("Invalid actual block contribution slots.");
            (j.Battle??throw new InvalidOperationException("No battle")).BlockSelected(input.EnemySlot,
                input.ContributionSlots.Select(i=>j.Context.CombatPower.Blocks[i]).ToArray(),input.AttackIndex);
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloBlockInput input)=>e.Apply(j=>Block(j,input));

        public static IEnumerable<SoloBlockInput> LegalBlocks(PveJourneySession j)
        {
            if(j==null)throw new ArgumentNullException(nameof(j));
            var b=j.Battle;
            if(b==null || b.Phase!=Phase.Block || !j.Game.HasActiveTurn || j.Game.IsEndingTurn)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(j);
            for(int enemy=0;enemy<b.Enemies.Count;enemy++)
            for(int attack=0;attack<b.Enemies[enemy].AttackCount;attack++)
            {
                if(j.Context.DefeatedEnemyIndices.Contains(enemy)||b.IsBlocked(enemy,attack))continue;
                var available=j.Context.CombatPower.AvailableBlocks(enemy,attack,b.Enemies[enemy]);
                var slots=Enumerable.Range(0,j.Context.CombatPower.Blocks.Count)
                    .Where(i=>available.Any(x=>ReferenceEquals(x,j.Context.CombatPower.Blocks[i]))).ToArray();
                foreach(var selected in Subsets(slots,0,new List<int>()).Where(s=>s.Length>0))
                {
                    var input=new SoloBlockInput(enemy,attack,selected);bool valid=false;
                    try {Block(j,input);valid=true;}
                    catch(InvalidOperationException) { }
                    finally {checkpoint.Restore();}
                    if(valid)yield return input;
                }
            }
        }
        public static void Damage(PveJourneySession j,SoloDamageInput input)
        {
            if(j==null || input==null || input.Allocations==null)throw new ArgumentNullException();
            var b=j.Battle??throw new InvalidOperationException("No battle");
            if(b.Phase!=Phase.AssignDamage)throw new InvalidOperationException("Not damage assignment phase.");
            if(input.Allocations.Any(a=>a==null || a.UnitSlots==null)
                ||input.Allocations.Select(a=>(a.EnemySlot,a.AttackIndex)).Distinct().Count()!=input.Allocations.Length)
                throw new InvalidOperationException("Duplicate or missing attack allocations.");
            // Explicitly cover every damaging attack, including an empty list for hero damage.
            var required=new List<(int EnemySlot,int AttackIndex)>();
            for(int enemy=0;enemy<b.Enemies.Count;enemy++)
            for(int attack=0;attack<b.Enemies[enemy].AttackCount;attack++)
                if(!j.Context.DefeatedEnemyIndices.Contains(enemy)&&!b.IsBlocked(enemy,attack)
                    &&!j.Context.IsAttackCancelled(enemy,attack)&&!j.Context.ColdStareSkips(b.Enemies[enemy]))
                    required.Add((enemy,attack));
            if(required.Count!=input.Allocations.Length || required.Any(k=>!input.Allocations.Any(a=>(a.EnemySlot,a.AttackIndex)==k)))
                throw new InvalidOperationException("Complete damage allocation must cover exactly the actual pending attacks.");
            foreach(var a in input.Allocations)
            {
                if(a.UnitSlots.Any(i=>i<0 || i>=j.Player.Units.Count))throw new InvalidOperationException("Invalid owned damage unit slot.");
                b.PlanDamage(a.EnemySlot,a.AttackIndex,a.UnitSlots.Select(i=>j.Player.Units[i]).ToArray());
            }
            b.Advance(); // mandatory consumer, never a separately reversible policy preselection
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloDamageInput input)=>e.Apply(j=>Damage(j,input));
        public static IEnumerable<SoloDamageInput> LegalDamage(PveJourneySession j)
        {
            if(j==null)throw new ArgumentNullException(nameof(j));
            var b=j.Battle;
            if(b==null || b.Phase!=Phase.AssignDamage || !j.Game.HasActiveTurn || j.Game.IsEndingTurn)yield break;
            var attacks=new List<(int enemy,int attack)>();
            for(int e=0;e<b.Enemies.Count;e++)
            for(int a=0;a<b.Enemies[e].AttackCount;a++)
                if(!j.Context.DefeatedEnemyIndices.Contains(e)&&!b.IsBlocked(e,a)
                    &&!j.Context.IsAttackCancelled(e,a)&&!j.Context.ColdStareSkips(b.Enemies[e]))attacks.Add((e,a));
            var checkpoint=DemoStateCheckpoint.Capture(j);
            foreach(var assignments in Allocate(attacks,0,Enumerable.Range(0,j.Player.Units.Count).ToArray(),new List<SoloDamageAllocation>()))
            {
                var input=new SoloDamageInput(assignments);bool valid=false;
                try {Damage(j,input);valid=true;}
                catch(InvalidOperationException) { }
                finally {checkpoint.Restore();}
                if(valid)yield return input;
            }
        }
        // Enumerate ordered subsets of actual units, including none; no fixed command-slot cap.
        private static IEnumerable<int[]> OrderedSubsets(int[] remaining,List<int> prefix)
        {
            yield return prefix.ToArray();
            foreach(int unit in remaining)
            {
                prefix.Add(unit);
                foreach(var order in OrderedSubsets(remaining.Where(i=>i!=unit).ToArray(),prefix))yield return order;
                prefix.RemoveAt(prefix.Count-1);
            }
        }
        private static IEnumerable<SoloDamageAllocation[]> Allocate(List<(int enemy,int attack)> attacks,int at,int[] remaining,List<SoloDamageAllocation> path)
        {
            if(at==attacks.Count){yield return path.ToArray();yield break;}
            foreach(var order in OrderedSubsets(remaining,new List<int>()))
            {
                path.Add(new(attacks[at].enemy,attacks[at].attack,order));
                foreach(var result in Allocate(attacks,at+1,remaining.Except(order).ToArray(),path))yield return result;
                path.RemoveAt(path.Count-1);
            }
        }
        private static IEnumerable<int[]> Subsets(int[] slots,int at,List<int> selected)
        {
            if(at==slots.Length){yield return selected.ToArray();yield break;}
            foreach(var a in Subsets(slots,at+1,selected))yield return a;
            selected.Add(slots[at]);foreach(var a in Subsets(slots,at+1,selected))yield return a;
            selected.RemoveAt(selected.Count-1);
        }
    }
}
