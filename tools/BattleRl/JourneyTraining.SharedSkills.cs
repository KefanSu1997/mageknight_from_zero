using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;

static partial class JourneyTraining
{
    static IEnumerable<Move> SharedSkillMoves()
    {
        foreach(string kind in new[]{"sharedUse","sharedClaim"})
        {
            yield return new(kind,SharedSkillArea.CalmId);
            for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)
                yield return new(kind,SharedSkillArea.NatureId,Targets:target,Option:attack);
        }
        for(int mask=0;mask<256;mask++)
            if(Enumerable.Range(0,8).Count(i=>(mask&(1<<i))!=0)<=2)
                yield return new("sharedUse",SharedSkillArea.PainId,Parts:mask);
        for(int slot=0;slot<8;slot++)foreach(var type in Enum.GetValues<SidewaysType>())
            yield return new("sharedClaim",SharedSkillArea.PainId,type.ToString(),Parts:slot);
        for(int color=0;color<4;color++)yield return new("sharedArm",SharedSkillArea.ManaEnhanceId,Option:color);
        yield return new("sharedCancel",SharedSkillArea.ManaEnhanceId);
        yield return new("sharedClaim",SharedSkillArea.ManaEnhanceId);
        foreach(var color in Enum.GetValues<ManaColor>().Where(c=>c!=ManaColor.Gold))
            yield return new("sharedUse",SharedSkillArea.ManaOverloadId,Option:(int)color);
        for(int slot=0;slot<8;slot++)foreach(var type in Enum.GetValues<SidewaysType>())
            yield return new("sharedUse",SharedSkillArea.HowlId,type.ToString(),Parts:slot);
        for(int die=-1;die<8;die++)yield return new("sharedUse",SharedSkillArea.SourceOpeningId,Parts:die);
        for(int die=0;die<8;die++)
        {
            yield return new("sharedClaim",SharedSkillArea.SourceOpeningId,Parts:die);
            foreach(int reroll in new[]{0,1})yield return new("sharedSourceReturn",SharedSkillArea.SourceOpeningId,Parts:die,Option:reroll);
            yield return new("sharedOverloadResolve",SharedSkillArea.ManaOverloadId,Parts:die);
        }
        for(int armor=0;armor<EnemySlots;armor++)for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)
            yield return new("sharedHowlResolve",SharedSkillArea.HowlId,Targets:armor,Parts:target,Option:attack);
    }
    sealed partial class Env
    {
        object SharedSkillState()=>new
        {
            tokens=Context.SharedSkills.Tokens.Select(t=>new{skill=t.Skill.SourceId,owner=t.Owner.HeroId,
                own=ReferenceEquals(t.Owner,Journey.Player),color=t.Color?.ToString()}).ToArray(),
            terrain=Enum.GetValues<TerrainType>().Select(t=>new{type=t.ToString(),reduction=Context.TerrainMoveReductionToOne.GetValueOrDefault(t)}).ToArray(),
            enhancementColor=Context.SharedSkills.PendingEnhancementColor(Context)?.ToString(),
            howls=Context.SharedSkills.PendingHowls(Context).Count,
            overload=Context.SharedSkills.PendingOverloadChoices(Context).Select(c=>new{movement=c.Movement,influence=c.Influence,
                value=c.Attack?.Value??c.Block?.Value??0,card=c.Attack?.SourceCard is DeedCard a?CardIdentity(a):c.Block?.SourceCard is DeedCard b?CardIdentity(b):-1}).ToArray(),
            sourceReturns=Context.ManaSource.PendingSourceOpeningReturns(Journey.Player).Select((d,i)=>new{index=i,face=d.Face.ToString()}).ToArray()
        };
        void AppendSharedSkillFeatures(List<double> features)
        {
            foreach(var id in Skills.Where(s=>s.SourceId.EndsWith(":09")).Select(s=>s.SourceId))
            {
                features.Add(Context.SharedSkills.Tokens.Count(t=>t.Skill.SourceId==id)/7.0);
                features.Add(Context.SharedSkills.Tokens.Any(t=>t.Skill.SourceId==id&&ReferenceEquals(t.Owner,Journey.Player))?1:0);
                foreach(var color in Enum.GetValues<ManaColor>())features.Add(Context.SharedSkills.Tokens.Any(t=>t.Skill.SourceId==id&&t.Color==color)?1:0);
            }
            foreach(var terrain in Enum.GetValues<TerrainType>())
                features.Add(Context.TerrainMoveReductionToOne.GetValueOrDefault(terrain)/4.0);
            foreach(var color in Enum.GetValues<ManaColor>())features.Add(Context.SharedSkills.PendingEnhancementColor(Context)==color?1:0);
            features.Add(Context.SharedSkills.PendingHowls(Context).Count/7.0);
            var choices=Context.SharedSkills.PendingOverloadChoices(Context);
            for(int slot=0;slot<8;slot++)
            {
                var c=slot<choices.Count?choices[slot]:null;
                features.Add(c!=null?1:0);features.Add(c?.Movement==true?1:0);features.Add(c?.Influence==true?1:0);
                features.Add((c?.Attack?.Value??c?.Block?.Value??0)/20.0);
                features.Add(c?.Block!=null?1:0);
                foreach(var element in Enum.GetValues<Element>())features.Add(c!=null&&(c.Attack?.Element??c.Block?.Element)==element?1:0);
                foreach(var type in Enum.GetValues<AttackType>())features.Add(c?.Attack!=null&&c.Attack.Type==type?1:0);
            }
            var returns=Context.ManaSource.PendingSourceOpeningReturns(Journey.Player);
            for(int slot=0;slot<8;slot++)
            {features.Add(slot<returns.Count?1:0);foreach(var color in Enum.GetValues<ManaColor>())features.Add(slot<returns.Count&&returns[slot].Face==color?1:0);}
        }
        bool ApplySharedSkill(Move action)
        {
            if(!action.Kind.StartsWith("shared",StringComparison.Ordinal))return false;
            if(!NativeSharedSkills)throw new InvalidOperationException("Native shared skills disabled");
            var player=Journey.Player;var shared=Context.SharedSkills;
            if(action.Kind=="sharedArm"){shared.ArmEnhancement(player,Context,player.Skills.Single(s=>s.SourceId==action.Card),(ManaColor)action.Option);return true;}
            if(action.Kind=="sharedCancel"){shared.CancelEnhancement(player,Context);return true;}
            if(action.Kind=="sharedOverloadResolve"){shared.ResolveOverload(player,Context,action.Parts);return true;}
            if(action.Kind=="sharedHowlResolve")
            {if(B==null)throw new InvalidOperationException("Actual howl combat required");shared.ResolveHowl(player,Context,B.Enemies[action.Targets],B.Enemies[action.Parts],action.Option);return true;}
            if(action.Kind=="sharedSourceReturn")
            {var pending=shared.PendingSourceOpeningReturns(player,Context);if(action.Parts>=pending.Count)throw new InvalidOperationException("Actual returning die required");shared.ChooseSourceOpeningReturn(player,Context,pending[action.Parts],action.Option==1);return true;}
            if(action.Kind=="sharedUse")
            {
                var owned=player.Skills.Single(s=>s.SourceId==action.Card);
                if(action.Card==SharedSkillArea.CalmId)shared.UseCalm(player,Context,owned);
                else if(action.Card==SharedSkillArea.NatureId)
                {
                    if(B==null)throw new InvalidOperationException("Nature requires actual combat enemy");
                    shared.UseNature(player,Context,owned,B.Enemies[action.Targets],action.Option);
                }
                else if(action.Card==SharedSkillArea.PainId)
                {
                    var slots=Enumerable.Range(0,8).Where(i=>(action.Parts&(1<<i))!=0).ToArray();
                    if(slots.Any(i=>i>=player.Deck.Hand.Count))throw new InvalidOperationException("Missing physical wound slot");
                    shared.UsePain(player,Context,owned,slots.Select(i=>player.Deck.Hand[i]).ToArray());
                }
                else if(action.Card==SharedSkillArea.ManaOverloadId)shared.UseOverload(player,Context,owned,(ManaColor)action.Option);
                else if(action.Card==SharedSkillArea.HowlId)
                {if(action.Parts>=player.Deck.Hand.Count)throw new InvalidOperationException("Actual howl deed required");shared.UseHowl(player,Context,owned,player.Deck.Hand[action.Parts],Enum.Parse<SidewaysType>(action.Choice));}
                else if(action.Card==SharedSkillArea.SourceOpeningId)
                {if(action.Parts>=Context.ManaSource.Dice.Count)throw new InvalidOperationException("Actual source die required");shared.UseSourceOpening(player,Context,owned,action.Parts<0?null:Context.ManaSource.Dice[action.Parts]);}
                else throw new InvalidOperationException("Unsupported native shared owner skill");
            }
            else
            {
                var token=shared.Tokens.FirstOrDefault(t=>t.Skill.SourceId==action.Card)
                    ??throw new InvalidOperationException("Actual central skill token required");
                if(action.Card==SharedSkillArea.CalmId)shared.ClaimCalm(player,Context,token);
                else if(action.Card==SharedSkillArea.NatureId)
                {
                    if(B==null)throw new InvalidOperationException("Nature requires actual combat enemy");
                    shared.ClaimNature(player,Context,token,B.Enemies[action.Targets],action.Option);
                }
                else if(action.Card==SharedSkillArea.PainId)
                {
                    if(action.Parts>=player.Deck.Hand.Count)throw new InvalidOperationException("Missing physical wound slot");
                    shared.ClaimPain(player,Context,token,player.Deck.Hand[action.Parts],Enum.Parse<SidewaysType>(action.Choice));
                }
                else if(action.Card==SharedSkillArea.ManaEnhanceId)shared.ClaimEnhancement(player,Context,token);
                else if(action.Card==SharedSkillArea.SourceOpeningId)
                {if(action.Parts>=Context.ManaSource.Dice.Count)throw new InvalidOperationException("Actual source die required");shared.ClaimSourceOpening(player,Context,token,Context.ManaSource.Dice[action.Parts]);}
                else throw new InvalidOperationException("Unsupported native central skill");
            }
            return true;
        }
        IEnumerable<Move> SharedSkillCandidates()=>SharedSkillMoves().Where(HasSharedInput);
        bool HasSharedInput(Move action)
        {
            var shared=Context.SharedSkills;
            bool available=action.Kind switch
            {
                "sharedUse" or "sharedArm"=>Journey.Player.Skills.Any(s=>s.SourceId==action.Card),
                "sharedClaim"=>shared.Tokens.Any(t=>t.Skill.SourceId==action.Card),
                "sharedCancel"=>shared.PendingEnhancementColor(Context)!=null,
                "sharedHowlResolve"=>B!=null&&shared.PendingHowls(Context).Count>0
                    &&action.Targets<B.Enemies.Count&&action.Parts<B.Enemies.Count
                    &&action.Option<B.Enemies[action.Parts].AttackCount,
                "sharedOverloadResolve"=>action.Parts<shared.PendingOverloadChoices(Context).Count,
                "sharedSourceReturn"=>action.Parts<Context.ManaSource.PendingSourceOpeningReturns(Journey.Player).Count,
                _=>false
            };
            return available&&(action.Card!=SharedSkillArea.NatureId||B!=null
                &&action.Targets<B.Enemies.Count&&action.Option<B.Enemies[action.Targets].AttackCount);
        }
    }
}
