using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// GlobalResources扩展类，提供地点交互所需的方法
    /// </summary>
    public static class GlobalResourcesExtensions
    {
        /// <summary>
        /// 获取可用的法术供应
        /// </summary>
        public static List<SpellCard> GetAvailableSpells(this GlobalResources resources)
        {
            return resources.Spells?.Offer ?? new List<SpellCard>();
        }
        
        /// <summary>
        /// 获取可用的技能牌供应
        /// </summary>
public static List<SkillCard> GetAvailableSkills(this GlobalResources resources, CardSet cardSet)
{
    // 修道院提供的是AdvActionCard，不是SkillCard
    // 返回空列表作为占位符，实际应该从游戏牌供应中获取
    return new List<SkillCard>();
}
        
        /// <summary>
        /// 从牌供应中移除法术牌
        /// </summary>
public static void RemoveSpellFromSupply(this GlobalResources resources, SpellCard spell)
        {
            if (spell != null && resources.Spells?.Offer != null)
            {
                resources.Spells.Offer.Remove(spell);
            }
        }
        
        /// <summary>
        /// 从牌供应中移除高级行动牌
        /// </summary>
        public static void RemoveAdvActionFromSupply(this GlobalResources resources, AdvActionCard advAction)
        {
            if (advAction != null && resources.MonasteryOffer != null)
            {
                resources.MonasteryOffer.Remove(advAction);
            }
        }
        
        /// <summary>
        /// 初始化新游戏的全局资源
        /// </summary>
        public static void InitializeForNewGame(this GlobalResources resources)
        {
            // 这里可以根据需要初始化资源
        }
    }
}