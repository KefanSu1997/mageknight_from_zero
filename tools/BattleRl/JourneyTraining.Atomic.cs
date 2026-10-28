using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;

// Adapter-only composite decisions. Every component uses the original formal API.
static partial class JourneyTraining
{
    sealed partial class Env
    {
        public bool AtomicSingleBattle;
        public static bool Selection(Move a) => a.Kind is "unitDamage" or "sharedArm" or "sharedCancel"
            or "defend" or "clearDefend" or "payment" or "cancelPayment" or "bowChoice"
            or "killCrystal" or "roundReturn" or "sharedSourceReturn" or "protectionCleanup"
            || a.Kind == "bannerUse" && a.Card == "items_018";

        Action AtomicUndo()
        {
            // Env is an adapter object outside MK.Logic; capture its scalar/reference
            // fields separately from the original mutable rule graph.
            var fields=typeof(Env).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .Where(f=>!f.IsInitOnly).ToArray();
            var values=fields.Select(f=>f.GetValue(this)).ToArray();
            var saved=DemoStateCheckpoint.Capture(Journey,Acquired,identities,enemyIdentities);
            return ()=>{saved.Restore();for(int i=0;i<fields.Length;i++)fields[i].SetValue(this,values[i]);};
        }

        public List<Move[]> AtomicChoices()
        {
            if(Done)return new();
            if(B==null)throw new InvalidOperationException("Atomic policy requires formal combat entry");
            if(PendingPayment!=null||Context.SharedSkills.PendingEnhancementColor(Context)!=null)
                throw new InvalidOperationException("Unconsumed selection crossed an atomic decision boundary");
            var original=JsonSerializer.Serialize(State());
            var undo=AtomicUndo();
            var choices=new Dictionary<string,Move[]>();
            var path=new List<Move>();
            void Add(Move consumer)
            {
                var restore=AtomicUndo();
                try
                {
                    Apply(consumer);
                    if(PendingPayment!=null||Context.SharedSkills.PendingEnhancementColor(Context)!=null)return;
                    if(StopReason.StartsWith("formal_"))return;
                    var sequence=path.Append(consumer).ToArray();
                    choices.TryAdd(string.Join("|",sequence.Select(a=>a.Key)),sequence);
                }
                catch(InvalidOperationException) { }
                finally { restore(); }
            }
            void Branch(Move selection,Action next)
            {
                var restore=AtomicUndo();
                bool pushed=false;
                try { Apply(selection);path.Add(selection);pushed=true;next(); }
                catch(InvalidOperationException) { }
                finally { if(pushed)path.RemoveAt(path.Count-1);restore(); }
            }
            void Groups(List<Move[]> groups,int index,Action finish)
            {
                if(index==groups.Count){finish();return;}
                // Existing formal defaults are a genuine choice, not a separate actor action.
                Groups(groups,index+1,finish);
                foreach(var a in groups[index])Branch(a,()=>Groups(groups,index+1,finish));
            }
            try
            {
                var legal=Legal().Select(i=>Catalog[i]).ToArray();
                if(B.Phase==Phase.End)
                {
                    // Only compulsory effect resolution and formal cleanup remain.
                    foreach(var a in legal.Where(a=>a.Kind is "sharedHowlResolve" or "sharedOverloadResolve" or "puppetKeep"))Add(a);
                    var cleanup=new List<Move[]>();
                    if(Context.ProtectionBannerBurned)cleanup.Add(new[]{
                        new Move("protectionCleanup","items_002",Option:0),
                        new Move("protectionCleanup","items_002",Option:1)});
                    void Returns()
                    {
                        var pending=Context.ManaSource.PendingSourceOpeningReturns(Journey.Player);
                        if(pending.Count==0){Add(new Move("nextTurn"));return;}
                        // Choosing removes a die from the pending list: always use index zero.
                        foreach(int reroll in new[]{0,1})Branch(new Move("sharedSourceReturn",SharedSkillArea.SourceOpeningId,Parts:0,Option:reroll),Returns);
                    }
                    Groups(cleanup,0,Returns);
                }
                else
                {
                    foreach(var a in legal.Where(a=>!Selection(a)&&a.Kind!="attack"
                        &&!(a.Kind=="advance"&&B.Phase==Phase.AssignDamage)))
                        choices.TryAdd(a.Key,new[]{a}); // Already checked by the formal Legal preview.

                    // Enhancement and payment selections expose consumers that may not
                    // be legal before payment. Re-query after each formal parameter.
                    void PayingConsumers()
                    {
                        foreach(var a in Legal().Select(i=>Catalog[i]).Where(a=>!Selection(a)))Add(a);
                    }
                    void Payments()
                    {
                        foreach(var a in Legal().Select(i=>Catalog[i]).Where(a=>a.Kind=="payment").ToArray())Branch(a,PayingConsumers);
                    }
                    foreach(var payment in legal.Where(a=>a.Kind=="payment"))Branch(payment,PayingConsumers);
                    foreach(var arm in legal.Where(a=>a.Kind=="sharedArm"))
                        Branch(arm,()=>{PayingConsumers();Payments();});

                    if(B.Phase==Phase.AssignDamage)
                    {
                        var groups=legal.Where(a=>a.Kind=="bannerUse"&&a.Card=="items_018")
                            .GroupBy(a=>a.Targets).Select(g=>g.ToArray()).ToList();
                        groups.AddRange(legal.Where(a=>a.Kind=="unitDamage"&&a.Option!=1)
                            .GroupBy(a=>(a.Targets,a.Parts)).Select(g=>g.ToArray()));
                        Groups(groups,0,()=>Add(new Move("advance")));
                    }
                    if(B.Phase is Phase.Ranged or Phase.Melee)
                    {
                        int count=Math.Min(8,Context.CombatPower.AvailableAttacks(B.Phase,Context.AllAttacksSiege).Length);
                        foreach(var attack in Catalog.Where(a=>a.Kind=="attack"&&a.Targets>0&&a.Targets<(1<<B.Enemies.Count)&&a.Parts>0&&a.Parts<(1<<count)))
                        {
                            var targets=Enumerable.Range(0,B.Enemies.Count).Where(i=>(attack.Targets&(1<<i))!=0).ToArray();
                            var groups=legal.Where(a=>a.Kind=="bowChoice"&&(attack.Parts&(1<<a.Targets))!=0)
                                .GroupBy(a=>a.Targets).Select(g=>g.ToArray()).ToList();
                            groups.AddRange(legal.Where(a=>a.Kind=="killCrystal"&&targets.Contains(a.Targets))
                                .GroupBy(a=>a.Targets).Select(g=>g.ToArray()));
                            void Defense(int minimumSource=0)
                            {
                                var options=Context.EnemyDefense.Choices(B.Enemies,targets,Context);
                                if(options.Count==0){Add(attack);return;}
                                // Sources may outnumber targets: choosing only the
                                // minimum source would delete legal assignments.
                                // Increasing explicit source order avoids factorial
                                // permutations while retaining every source choice.
                                foreach(var c in options.Where(c=>c.SourceIndex>=minimumSource))
                                    Branch(new Move("defend",Targets:attack.Targets,Parts:c.SourceIndex,Option:c.TargetIndex),()=>Defense(c.SourceIndex+1));
                            }
                            try { Groups(groups,0,()=>Defense()); }
                            catch(InvalidOperationException) { /* Invalid target sets have no attack. */ }
                        }
                    }
                }
            }
            finally { undo(); }
            if(JsonSerializer.Serialize(State())!=original)throw new InvalidOperationException("Atomic enumeration mutated state");
            return choices.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Value).ToList();
        }

        public object[] ApplyAtomic(Move[] sequence,out string failure)
        {
            failure=null;
            if(sequence.Length==0||Selection(sequence[^1]))throw new InvalidOperationException("Missing formal consumer");
            var trace=new List<object>();
            foreach(var action in sequence)
            {
                var before=JsonSerializer.SerializeToElement(State());
                try { Apply(action); }
                catch(Exception error)
                {
                    failure=error.ToString();
                    trace.Add(new{before,action,after=JsonSerializer.SerializeToElement(State()),error=failure});
                    return trace.ToArray();
                }
                bool planning=action.Kind is "unitDamage" or "defend" or "clearDefend" or "bowChoice"
                    or "killCrystal" or "protectionCleanup" or "roundReturn" or "payment" or "cancelPayment"
                    || action.Kind=="bannerUse"&&action.Card=="items_018";
                PlanningRun=ArtifactBuilds&&planning?PlanningRun+1:0;
                trace.Add(new{before,action,after=JsonSerializer.SerializeToElement(State())});
            }
            Steps++;PlanningRun=0;
            return trace.ToArray();
        }
    }
}
