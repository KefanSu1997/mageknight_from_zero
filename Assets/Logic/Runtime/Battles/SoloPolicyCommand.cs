using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Versioned command envelope for the implemented world branches. Exactly one
    /// complete payload is required. This is not yet the full card/combat action vocabulary.</summary>
    public sealed record SoloPolicyCommand(int SchemaVersion,int ExpectedStep,
        SoloSpatialCommand Spatial=null,SoloClockCommand Clock=null,SoloTacticInput TacticInput=null,
        SoloTacticAbility TacticAbility=null,SoloEndTurnInput EndTurn=null,SoloRestInput Rest=null,
        SoloLevelRewardInput LevelReward=null,SoloHealingInput Healing=null,SoloSiteInput Site=null,
        SoloAssaultSettlementInput AssaultSettlement=null,SoloDeedInput Deed=null,SoloAttackInput Attack=null,
        SoloBlockInput Block=null,SoloDamageInput Damage=null,SoloBasicInput Basic=null,SoloUnitInput Unit=null,
        SoloCombatFollowup CombatFollowup=null,SoloSkillInput Skill=null,SoloAdventureInput Adventure=null,SoloArtifactInput Artifact=null,
        SoloMaterialArtifactInput MaterialArtifact=null,SoloBannerInput Banner=null,SoloShapeshiftInput Shapeshift=null,SoloCityArtifactReward CityArtifactReward=null,
        SoloRepeatedInput Repeated=null,SoloLearningInput Learning=null)
    {
        public SoloConquestTransition Execute(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            if(SchemaVersion!=1)throw new InvalidOperationException("Unsupported solo command schema.");
            if(ExpectedStep!=episode.Steps)throw new InvalidOperationException("Stale command: physical slots belong to a different decision boundary.");
            int count=(Spatial==null?0:1)+(Clock==null?0:1)+(TacticInput==null?0:1)
                +(TacticAbility==null?0:1)+(EndTurn==null?0:1)+(Rest==null?0:1)+(LevelReward==null?0:1)+(Healing==null?0:1)+(Site==null?0:1)
                +(AssaultSettlement==null?0:1)+(Deed==null?0:1)+(Attack==null?0:1)+(Block==null?0:1)+(Damage==null?0:1)+(Basic==null?0:1)+(Unit==null?0:1)+(CombatFollowup==null?0:1);
            count+=Skill==null?0:1;
            count+=Adventure==null?0:1;
            count+=Artifact==null?0:1;
            count+=MaterialArtifact==null?0:1;
            count+=Banner==null?0:1;
            count+=Shapeshift==null?0:1;
            count+=CityArtifactReward==null?0:1;
            count+=Repeated==null?0:1;
            count+=Learning==null?0:1;
            if(count!=1)throw new InvalidOperationException("Exactly one complete command payload is required.");
            if(episode.Journey.Context?.PendingRepeatedEffect!=null&&Repeated==null)
                throw new InvalidOperationException("Resolve committed repeated effect first.");
            if(Repeated!=null)return SoloRepeatedInputs.Apply(episode,Repeated);
            if(episode.Journey.CommittedRewardEnd!=null&&EndTurn==null)
                throw new InvalidOperationException("Only committed end-turn reward resolution is permitted.");
            if(CityArtifactReward!=null)return episode.Apply(j=>j.ClaimPurchasedCityArtifacts(CityArtifactReward.KeepSlots));
            if(episode.Journey.CityArtifactChoices.Count>0)throw new InvalidOperationException("Resolve committed city artifact reward first.");
            if(Learning!=null)return SoloLearningInputs.Apply(episode,Learning);
            if(Spatial!=null)return SoloSpatialCommands.Apply(episode,Spatial);
            if(Clock!=null)return SoloClockCommands.Apply(episode,Clock);
            if(TacticInput!=null)return SoloTacticInputs.Apply(episode,TacticInput);
            if(TacticAbility!=null)return SoloTacticAbilities.Apply(episode,TacticAbility);
            if(EndTurn!=null)return SoloEndTurnInputs.Apply(episode,EndTurn);
            if(Rest!=null)return SoloRestInputs.Apply(episode,Rest);
            if(Healing!=null)return SoloHealingInputs.Apply(episode,Healing);
            if(Site!=null)return SoloSiteInputs.Apply(episode,Site);
            if(AssaultSettlement!=null)return SoloAssaultSettlement.Apply(episode,AssaultSettlement);
            if(Deed!=null)return SoloDeedInputs.Apply(episode,Deed);
            if(Attack!=null)return SoloAttackInputs.Apply(episode,Attack);
            if(Block!=null)return SoloDefenseInputs.Apply(episode,Block);
            if(Damage!=null)return SoloDefenseInputs.Apply(episode,Damage);
            if(Basic!=null)return SoloBasicOperations.Apply(episode,Basic);
            if(Unit!=null)return SoloUnitInputs.Apply(episode,Unit);
            if(CombatFollowup!=null)return SoloCombatFollowups.Apply(episode,CombatFollowup);
            if(Skill!=null)return SoloSkillInputs.Apply(episode,Skill);
            if(Adventure!=null)return SoloAdventureInputs.Apply(episode,Adventure);
            if(Artifact!=null)return SoloArtifactInputs.Apply(episode,Artifact);
            if(MaterialArtifact!=null)return SoloMaterialArtifactInputs.Apply(episode,MaterialArtifact);
            if(Banner!=null)return SoloBannerInputs.Apply(episode,Banner);
            if(Shapeshift!=null)return SoloShapeshiftInputs.Apply(episode,Shapeshift);
            return SoloLevelRewardInputs.Apply(episode,LevelReward);
        }

        /// <summary>All currently implemented branches, without truncation. Each returned
        /// command is tied to this episode's decision index; card/combat branches are pending.</summary>
        public static IEnumerable<SoloPolicyCommand> ImplementedChoices(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            int step=episode.Steps;
            if(episode.Journey.Context?.PendingRepeatedEffect!=null)
            {
                foreach(var repeated in SoloRepeatedInputs.Legal(episode))yield return new(1,step,Repeated:repeated);
                yield break;
            }
            if(episode.Journey.CommittedRewardEnd!=null)
            {
                foreach(var end in SoloEndTurnInputs.Legal(episode))yield return new(1,step,EndTurn:end);
                yield break;
            }
            if(episode.Journey.CityArtifactChoices.Count>0)
            {
                foreach(var slots in episode.Journey.CityArtifactSelections())yield return new(1,step,CityArtifactReward:new(slots));
                yield break;
            }
            foreach(var x in SoloClockCommands.Legal(episode))yield return new(1,step,Clock:x);
            foreach(var x in SoloLearningInputs.Legal(episode))yield return new(1,step,Learning:x);
            foreach(var x in SoloAdventureInputs.Legal(episode))yield return new(1,step,Adventure:x);
            foreach(var x in SoloArtifactInputs.Legal(episode))yield return new(1,step,Artifact:x);
            foreach(var x in SoloMaterialArtifactInputs.Legal(episode))yield return new(1,step,MaterialArtifact:x);
            foreach(var x in SoloBannerInputs.Legal(episode))yield return new(1,step,Banner:x);
            foreach(var x in SoloTacticInputs.Legal(episode))yield return new(1,step,TacticInput:x);
            foreach(var x in SoloTacticAbilities.Legal(episode))yield return new(1,step,TacticAbility:x);
            foreach(var x in SoloLevelRewardInputs.Legal(episode))yield return new(1,step,LevelReward:x);
            foreach(var x in SoloSpatialCommands.Legal(episode.Journey,episode.Tiles))yield return new(1,step,Spatial:x);
            foreach(var x in SoloRestInputs.Legal(episode))yield return new(1,step,Rest:x);
            foreach(var x in SoloHealingInputs.Legal(episode))yield return new(1,step,Healing:x);
            foreach(var x in SoloSiteInputs.Legal(episode))yield return new(1,step,Site:x);
            foreach(var x in SoloAssaultSettlement.Legal(episode.Journey))yield return new(1,step,AssaultSettlement:x);
            foreach(var x in SoloAttackInputs.Legal(episode))yield return new(1,step,Attack:x);
            foreach(var x in SoloDefenseInputs.LegalBlocks(episode.Journey))yield return new(1,step,Block:x);
            foreach(var x in SoloDefenseInputs.LegalDamage(episode.Journey))yield return new(1,step,Damage:x);
            foreach(var x in SoloDeedInputs.BasicChoices(episode))yield return new(1,step,Deed:x);
            foreach(var x in SoloDeedInputs.AdvancedChoices(episode))yield return new(1,step,Deed:x);
            foreach(var x in SoloDeedInputs.SpellChoices(episode))yield return new(1,step,Deed:x);
            foreach(var x in SoloBasicOperations.Legal(episode))yield return new(1,step,Basic:x);
            foreach(var x in SoloUnitInputs.Legal(episode))yield return new(1,step,Unit:x);
            foreach(var x in SoloCombatFollowups.Legal(episode.Journey))yield return new(1,step,CombatFollowup:x);
            foreach(var x in SoloSkillInputs.Legal(episode))yield return new(1,step,Skill:x);
            foreach(var x in SoloSkillInputs.SidewaysLegal(episode))yield return new(1,step,Skill:x);
            foreach(var x in SoloShapeshiftInputs.Legal(episode))yield return new(1,step,Shapeshift:x);
            foreach(var x in SoloEndTurnInputs.Legal(episode))yield return new(1,step,EndTurn:x);
        }
    }
}
