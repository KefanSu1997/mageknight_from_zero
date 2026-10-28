using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Runtime;

static partial class JourneyTraining
{
    // Shared payment inputs are selected once, rather than multiplying every
    // complete original command by all possible material/color/die tuples.
    static IEnumerable<Move> PaymentMoves()
    {
        yield return new Move("cancelPayment");
        foreach(var required in Enum.GetValues<ManaColor>())
        {
            for(int slot=0;slot<8;slot++)
                yield return new Move("payment","hero_skill_pool_000:06","material",Targets:(int)required,Parts:slot);
            foreach(var source in Enum.GetValues<ManaColor>())
                foreach(var kind in new[]{"stock","die","extra"})
                    yield return new Move("payment","hero_skill_pool_000:07",kind,Targets:(int)required,Parts:(int)source);
        }
    }

    sealed partial class Env
    {
        ManaSkillPaymentSelection PendingPayment;
        public object State()
        {
            if(!NativePayments&&!NativeConversions&&!NativeSharedSkills&&!NativeWorld)return RawState();
            var raw=JsonSerializer.SerializeToElement(RawState());
            var result=raw.EnumerateObject().ToDictionary(p=>p.Name,p=>(object)p.Value);
            if(NativeSharedSkills)result["sharedSkills"]=SharedSkillState();
            if(NativeWorld)result["world"]=WorldState();
            // Audit-only native order. RawState's sorted source faces are a
            // multiset display and cannot validate a pending native die index.
            result["auditSourceOrder"]=Context.ManaSource.Dice.Select(d=>d.Face.ToString()).ToArray();
            if(NativeConversions)result["combatConversions"]=new {
                meleeCost=Context.MoveCostAttack,rangedCost=Context.MoveCostRanged,
                influenceAllowed=Context.InfluenceAsBlock,influenceElement=Context.InfluenceBlockElement.ToString()};
            var s=PendingPayment;
            result["paymentSelection"]=s==null?null:new {
                skill=s.Skill.SourceId,required=s.RequiredColor.ToString(),
                material=s.Material==null?-1:CardIdentity(s.Material),
                materialSlot=s.Material==null?-1:Journey.Player.Deck.Hand.FindIndex(c=>ReferenceEquals(c,s.Material)),
                source=s.SourceColor?.ToString(),die=s.SourceDie==null?-1:Context.ManaSource.Dice.ToList().IndexOf(s.SourceDie),extra=s.ExtraDie};
            if(DeclaredTask!=null)result["taskProgress"]=new {
                encounterIndex=TaskEncounterCursor,groupCount=DeclaredTask.EnemyGroups.Length,
                sequenceCompleted=StopReason=="task_sequence_complete"};
            return result;
        }
        void AppendPaymentFeatures(List<double> f)
        {
            var s=PendingPayment;
            f.Add(s!=null?1:0);
            foreach(var id in new[]{"hero_skill_pool_000:06","hero_skill_pool_000:07"})f.Add(s?.Skill.SourceId==id?1:0);
            foreach(var color in Enum.GetValues<ManaColor>())f.Add(s?.RequiredColor==color?1:0);
            foreach(var color in Enum.GetValues<ManaColor>())f.Add(s?.SourceColor==color?1:0);
            for(int slot=0;slot<8;slot++)f.Add(s?.Material!=null&&slot<Journey.Player.Deck.Hand.Count&&ReferenceEquals(s.Material,Journey.Player.Deck.Hand[slot])?1:0);
            f.Add(s?.SourceDie!=null?1:0);f.Add(s?.ExtraDie==true?1:0);
            // This public predicate is checked by formal end-turn validation.
            // Combat commitment alone does not prove a card was played/discarded.
            f.Add(Journey.Player.Deck.CardsPlayedOrDiscardedThisTurn>0?1:0);
        }
        ManaSkillPaymentSelection ResolvePayment(Move a)
        {
            var p=Journey.Player;
            var skill=p.Skills.SingleOrDefault(s=>s.SourceId==a.Card)??throw new InvalidOperationException("Select an actual owned payment skill");
            if(a.Choice=="material")
            {
                if(a.Parts<0||a.Parts>=p.Deck.Hand.Count)throw new InvalidOperationException("Select an actual physical hand material");
                return new ManaSkillPaymentSelection(skill,(ManaColor)a.Targets,p.Deck.Hand[a.Parts]);
            }
            if(a.Choice is not ("stock" or "die" or "extra"))throw new InvalidOperationException("Unknown original payment source");
            var source=(ManaColor)a.Parts;
            var die=a.Choice=="stock"?null:Context.ManaSource.Dice.FirstOrDefault(d=>d.Face==source)
                ??throw new InvalidOperationException("Select an actual available source die");
            return new ManaSkillPaymentSelection(skill,(ManaColor)a.Targets,SourceColor:source,SourceDie:die,ExtraDie:a.Choice=="extra");
        }
        bool ApplyPaymentSelection(Move a)
        {
            if(a.Kind=="cancelPayment")
            {
                if(PendingPayment==null)throw new InvalidOperationException("No selected payment to cancel");
                PendingPayment=null;return true;
            }
            if(a.Kind!="payment")return false;
            if(PendingPayment!=null)throw new InvalidOperationException("Complete or cancel the current payment selection");
            var selected=ResolvePayment(a);
            // Validate only; choosing inputs must not pay or mark a skill used.
            ManaSkillPaymentActions.Execute(Journey.Player,Context,selected,()=>{});
            PendingPayment=selected;return true;
        }
        IEnumerable<int> LegalPaymentSelections(IEnumerable<Move> candidates)
        {
            var consumers=candidates.Where(a=>a.Kind is "play" or "unit").ToArray();
            foreach(var a in PaymentMoves().Where(a=>a.Kind=="payment"&&Journey.Player.Skills.Any(s=>s.SourceId==a.Card)))
            {
                bool payable=false;
                try
                {
                    ApplyPaymentSelection(a);
                    // The shared native replacement rule can reject impossible
                    // color pairs without replaying every consumer. Use its
                    // broadest spell purpose here; each real consumer below
                    // still validates its own purpose and remaining costs.
                    using(ManaSkillPaymentActions.StrongSpellScope(Context,true))
                        if(!new ManaSkillPayment(Journey.Player,PendingPayment).ReplaceCost(
                            new List<ManaColor>{PendingPayment.RequiredColor},Context))continue;
                    // Formal previews include every remaining original cost and
                    // material. An input with no actual consumer is not exposed.
                    payable=Preview(consumers,firstOnly:true).Count>0;
                }
                catch(InvalidOperationException){}
                finally{PendingPayment=null;}
                if(payable)yield return CatalogIndices[a.Key];
            }
        }
    }
}
