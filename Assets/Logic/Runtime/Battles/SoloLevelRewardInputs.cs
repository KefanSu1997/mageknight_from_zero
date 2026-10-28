using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloSkillOffer { Own, Common }
    public sealed record SoloLevelRewardInput(SoloSkillOffer Offer,int SkillSlot,int ActionSlot);

    public static class SoloLevelRewardInputs
    {
        public static SoloLevelRewardInput[] Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;var progress=j.Progression;
            if(episode.Terminal || !j.Game.IsEndingTurn || progress?.Pending!=true)
                return Array.Empty<SoloLevelRewardInput>();
            var proposals=new List<SoloLevelRewardInput>();
            for(int skill=0;skill<progress.Choices.Count;skill++)
                for(int action=0;action<progress.Actions.Count;action++)proposals.Add(new(SoloSkillOffer.Own,skill,action));
            for(int skill=0;skill<progress.CommonSkills.Count;skill++)proposals.Add(new(SoloSkillOffer.Common,skill,0));
            var checkpoint=DemoStateCheckpoint.Capture(episode);var legal=new List<SoloLevelRewardInput>();
            foreach(var input in proposals)
            {
                try { Apply(episode,input);legal.Add(input); }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
            }
            return legal.ToArray();
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloLevelRewardInput input)
        {
            if(episode==null || input==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                var progress=j.Progression;
                if(!j.Game.IsEndingTurn || progress?.Pending!=true)throw new InvalidOperationException("No native pending level reward.");
                var skills=input.Offer switch
                {
                    SoloSkillOffer.Own=>progress.Choices,
                    SoloSkillOffer.Common=>progress.CommonSkills,
                    _=>throw new ArgumentOutOfRangeException(nameof(input.Offer))
                };
                if(input.SkillSlot<0 || input.SkillSlot>=skills.Count || input.ActionSlot<0 || input.ActionSlot>=progress.Actions.Count)
                    throw new InvalidOperationException("Invalid actual reward offer slots.");
                j.ChooseLevelReward(skills[input.SkillSlot],progress.Actions[input.ActionSlot]);
            });
        }
    }
}
