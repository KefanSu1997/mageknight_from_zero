using System;
using System.Collections.Generic;

namespace MK.Logic.Core
{
    public static class Constants
    {
        public const int FameCap = 102;

        /// <summary>元素攻击遇到某种抗性时的倍率（1 / 0.5）；不存在相克双倍伤害</summary>
        public static double Efficiency(Element atk, Ability? resist) =>
            (atk, resist) switch
            {
                // ── 抗性 ×½ ────────────────────────
                (Element.Physical, Ability.PhysicalResist) => 0.5,
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
