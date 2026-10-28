using System;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>One-time acquisition effects over the actual acquired skill entity.</summary>
    public static class HeroSkillAcquisition
    {
        public static void Validate(PlayerState player, SkillCard skill, UnitSupply units = null)
        {
            if (player == null || skill == null || string.IsNullOrEmpty(skill.SourceId))
                throw new InvalidOperationException("取得技能需要实际玩家和原技能实体");
            if (player.Skills.Any(s => s.SourceId == skill.SourceId && !ReferenceEquals(s, skill)))
                throw new InvalidOperationException("不能重复取得同一原技能");
            if (skill.SourceId == LoyaltyActions.SourceId && !player.Skills.Any(s => ReferenceEquals(s,skill)) && units == null)
                throw new InvalidOperationException("取得忠诚契约需要实际部队供应");
        }

        public static void Acquire(PlayerState player, SkillCard skill, Random random = null, UnitSupply units = null)
        {
            Validate(player, skill, units);
            if (player.Skills.Any(s => ReferenceEquals(s, skill))) return;
            if (skill.SourceId == ChaosSkillRules.SourceId && player.ChaosColor == null)
            {
                var die = new ManaDie(random);
                die.RollUnrestricted();
                player.ChaosColor = die.Face;
            }
            if (skill.SourceId == LoyaltyActions.SourceId)
            {
                units.AppendRegularToOffer(2);
                player.LoyaltySkill = skill;
            }
            player.Skills.Add(skill);
        }
    }
}
