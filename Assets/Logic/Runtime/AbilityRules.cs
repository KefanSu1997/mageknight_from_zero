// MageKnightLogicProj/Runtime/AbilityRules.cs
// ------------------------------------------------
// 该文件封装對怪物能力標誌的計算邏輯，方便 BattleResolver 等模塊調用。

using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 与戰鬥相關的能力處理輔助函式集合。
    /// 目前僅實作 Swift、Brutal、Poison、Paralyze 四種進攻能力。
    /// </summary>
    public static class AbilityRules
    {
        /// <summary>
        /// 計算格擋指定敵人所需的最終 Block 值。
        /// Swift 會令需求翻倍，其餘能力不影響。
        /// </summary>
        public static int RequiredBlock(Monster m, System.Collections.Generic.ISet<Ability>? ignore = null)
        {
            bool swift = m.Abilities.Contains(Ability.Swift) && (ignore == null || !ignore.Contains(Ability.Swift));
            return swift ? m.Attack * 2 : m.Attack;
        }

        /// <summary>
        /// 若敵人帶有 Brutal 且攻擊未被完全格擋，將剩餘傷害翻倍。
        /// </summary>
        public static int BrutalizedDamage(Monster m, int raw, System.Collections.Generic.ISet<Ability>? ignore = null)
        {
            bool brutal = m.Abilities.Contains(Ability.Brutal) && (ignore == null || !ignore.Contains(Ability.Brutal));
            return brutal ? raw * 2 : raw;
        }

        /// <summary>
        /// 給目標分配創傷，同時處理 Poison 與 Paralyze 的副作用。
        /// </summary>
        public static void AssignWounds(Monster src, object target, ref int wounds, System.Collections.Generic.ISet<Ability>? ignore = null)
        {
            if (wounds <= 0) return;

            // 針對部隊的處理
            if (target is UnitState u)
            {
                bool poison = src.Abilities.Contains(Ability.Poison) && (ignore == null || !ignore.Contains(Ability.Poison));
                bool paralyze = src.Abilities.Contains(Ability.Paralyze) && (ignore == null || !ignore.Contains(Ability.Paralyze));
                int mult = poison ? 2 : 1;
                u.AddWounds(wounds * mult);
                if (paralyze)
                    u.Destroy();
            }
            // 針對英雄本身的處理
            else if (target is PlayerState p)
            {
                p.Wounds += wounds;
                bool poison = src.Abilities.Contains(Ability.Poison) && (ignore == null || !ignore.Contains(Ability.Poison));
                bool paralyze = src.Abilities.Contains(Ability.Paralyze) && (ignore == null || !ignore.Contains(Ability.Paralyze));
                if (paralyze)
                    p.DiscardNonWoundHand();              // 先棄掉非傷牌
                p.AddWoundCards(wounds);                  // 將創傷牌加入手牌
                if (poison)
                    p.AddWoundsToDiscard(wounds);         // 毒素額外進入棄牌堆
            }

            wounds = 0; // 創傷已處理，剩餘傷害清零
        }
    }
}

