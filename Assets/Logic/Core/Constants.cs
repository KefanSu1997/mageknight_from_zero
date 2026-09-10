/***************************************************************
 * 元素攻防效率表（规则书 UE p.25）                              *
 * - Fire vs IceResist  → ×2   (弱点)                           *
 * - Ice  vs FireResist → ×2                                     *
 * - 同元素攻/抗         → ×0.5 (抗性)                          *
 * - Cold-Fire 仅当目标同时具有 FireResist+IceResist             *
 *   或显式 ColdFireResist 时 ×0.5                              *
 *-------------------------------------------------------------*
 * Block 效率（p.18-19）：                                       *
 *   Fire Attack  ⇒  Ice / ColdFire Block 全效；其余 ×0.5       *
 *   Ice  Attack  ⇒  Fire / ColdFire Block 全效；其余 ×0.5      *
 *   ColdFire Attack ⇒  仅 ColdFire Block 全效；其余 ×0.5       *
 *   Physical 不区分元素，任意 Block 全效                       *
 **************************************************************/
using System;
using System.Collections.Generic;

namespace MK.Logic.Core
{
    public static class Constants
    {
        public const int FameCap = 102;

        /// <summary>元素攻击遇到某种抗性时的倍率（1 / 0.5 / 2）</summary>
        public static double Efficiency(Element atk, Ability? resist) =>
            (atk, resist) switch
            {
                // ── 弱点 ×2 ─────────────────────────
                (Element.Fire, Ability.IceResist) => 2.0,
                (Element.Ice,  Ability.FireResist) => 2.0,

                // ── 抗性 ×½ ────────────────────────
                (Element.Fire, Ability.FireResist) => 0.5,
                (Element.Ice,  Ability.IceResist)  => 0.5,
                // Cold-Fire 只有在同时具 Fire+Ice 抗或显式 ColdFireResist 时减半
                (Element.ColdFire, Ability.ColdFireResist) => 0.5,

                _ => 1.0
            };

        /// <summary>不同元素格挡的效率（1 或 0.5）</summary>
        public static double BlockEfficiency(Element attackEle, Element blockEle) =>
            (attackEle, blockEle) switch
            {
                // 物理攻击 & 物理格挡或元素格挡 —— 全效
                (Element.Physical, _)                     => 1,
                (_, Element.Physical)                     => 0.5,

                // Fire 被 Ice / ColdFire 全效，其余半效
                (Element.Fire, Element.Ice)               => 1,
                (Element.Fire, Element.ColdFire)          => 1,

                // Ice 被 Fire / ColdFire 全效，其余半效
                (Element.Ice, Element.Fire)               => 1,
                (Element.Ice, Element.ColdFire)           => 1,

                // ColdFire 仅 ColdFire 格挡全效
                (Element.ColdFire, Element.ColdFire)      => 1,

                // 其余皆半效
                _                                          => 0.5
            };
    }
}
