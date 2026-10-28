using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloDefendInput(int Source,int Target);
    public sealed record SoloAttackInput(int[] Targets,int[] ContributionSlots,SoloDefendInput[] Defense);
    public static class SoloAttackInputs
    {
        public static void Execute(PveJourneySession j,SoloAttackInput input)
        {
            if(j==null || input==null || input.Targets==null || input.ContributionSlots==null || input.Defense==null)
                throw new ArgumentNullException();
            var b=j.Battle??throw new InvalidOperationException("No actual battle.");
            if(input.ContributionSlots.Any(i=>i<0 || i>=j.Context.CombatPower.Attacks.Count)
                ||input.ContributionSlots.Distinct().Count()!=input.ContributionSlots.Length)
                throw new InvalidOperationException("Invalid actual attack contribution slots.");
            var selected=input.ContributionSlots.Select(i=>j.Context.CombatPower.Attacks[i]).ToArray();
            b.ClearDefendChoices();
            foreach(var d in input.Defense)b.ChooseDefend(d.Source,d.Target,input.Targets);
            b.AttackSelected(input.Targets,selected);
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloAttackInput input)
            =>episode.Apply(j=>Execute(j,input));

        public static IEnumerable<SoloAttackInput> Legal(SoloConquestEpisode episode)
            =>episode==null?throw new ArgumentNullException(nameof(episode)):episode.Terminal
                ?Array.Empty<SoloAttackInput>():Legal(episode.Journey);

        public static IEnumerable<SoloAttackInput> Legal(PveJourneySession j)
        {
            if(j==null)throw new ArgumentNullException(nameof(j));
            var b=j.Battle;
            if(j.SoloClock?.ScenarioEnded==true || b==null || !j.Context.InBattle || !j.Game.HasActiveTurn
                ||j.Game.IsEndingTurn || b.Phase is not (Phase.Ranged or Phase.Melee))yield break;
            var alive=Enumerable.Range(0,b.Enemies.Count).Where(i=>!j.Context.DefeatedEnemyIndices.Contains(i)).ToArray();
            var contributions=Enumerable.Range(0,j.Context.CombatPower.Attacks.Count).ToArray();
            if(alive.Length==0 || contributions.Length==0)yield break;
            var boundary=DemoStateCheckpoint.Capture(j);
            foreach(var targets in Subsets(alive,0,new List<int>()).Where(a=>a.Length>0))
            {
                var plans=new List<SoloDefendInput[]>();
                try { b.ClearDefendChoices(); CollectDefense(b,targets,new List<SoloDefendInput>(),plans); }
                finally { boundary.Restore(); }
                var distinctPlans=plans.GroupBy(p=>string.Join(";",p.OrderBy(x=>x.Source).Select(x=>x.Source+":"+x.Target)))
                    .Select(g=>g.First()).ToArray();
                foreach(var parts in Subsets(contributions,0,new List<int>()).Where(a=>a.Length>0))
                foreach(var plan in distinctPlans)
                {
                    var input=new SoloAttackInput(targets,parts,plan);bool valid=false;
                    try { Execute(j,input);valid=true; }
                    catch(InvalidOperationException) { }
                    finally { boundary.Restore(); }
                    if(valid)yield return input;
                }
            }
        }
        private static void CollectDefense(PveBattleSession b,int[] targets,List<SoloDefendInput> path,List<SoloDefendInput[]> result)
        {
            var choices=b.DefendChoices(targets).ToArray();
            if(choices.Length==0){result.Add(path.ToArray());return;}
            // Each selected pair consumes a distinct defender and target, so recursion is finite.
            foreach(var choice in choices)
            {
                var checkpoint=DemoStateCheckpoint.Capture(b);
                try
                {
                    b.ChooseDefend(choice.SourceIndex,choice.TargetIndex,targets);
                    path.Add(new(choice.SourceIndex,choice.TargetIndex));
                    CollectDefense(b,targets,path,result);
                    path.RemoveAt(path.Count-1);
                }
                finally {checkpoint.Restore();}
            }
        }
        private static IEnumerable<int[]> Subsets(int[] values,int at,List<int> selected)
        {
            if(at==values.Length){yield return selected.ToArray();yield break;}
            foreach(var a in Subsets(values,at+1,selected))yield return a;
            selected.Add(values[at]);
            foreach(var a in Subsets(values,at+1,selected))yield return a;
            selected.RemoveAt(selected.Count-1);
        }
    }
}
