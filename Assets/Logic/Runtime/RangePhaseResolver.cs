// MageKnightLogicProj/Runtime/RangedPhaseResolver.cs
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 解析遠程/攻城階段，計算擊殺目標並回傳名望。
    /// 僅供 <see cref="TurnMachine"/> 在戰鬥流程中呼叫。
    /// </summary>
    internal static class RangedPhaseResolver
    {
        public static RangedResult Resolve(
            IList<Monster> enemies,
            IReadOnlyList<AttackAllocation> attacks,
            ActionContext? ctx = null)
        {
            var killed = new HashSet<int>();
            int fame   = 0;

            foreach (var atk in attacks)
            {
                /* 1️⃣ 目标集合 */
                var targetIdx = (atk.TargetIndices == null || atk.TargetIndices.Count == 0)
                    ? enemies.Select((_, i) => i).ToList()
                    : atk.TargetIndices.Where(i => i < enemies.Count).ToList();
                if (targetIdx.Count == 0) continue;

                /* 2️⃣ 组攻护甲 + 抗性 */
                int armorSum = 0;
                bool fireRes = false, iceRes = false, coldRes = false;
                bool anyFortified = false;

                foreach (int ti in targetIdx)
                {
                    var m = enemies[ti];
                    int armor = m.Armor;
                    if (ctx != null && ctx.ArmorReduction.TryGetValue(ti, out int red))
                        armor = System.Math.Max(1, armor - red);
                    armorSum += armor;
                    // FireResist 或 MagicResist 均視為火抗
                    fireRes  |= m.Abilities.Contains(Ability.FireResist)
                                || m.Abilities.Contains(Ability.MagicResist);
                    // IceResist 或 MagicResist 均視為冰抗
                    iceRes   |= m.Abilities.Contains(Ability.IceResist)
                                || m.Abilities.Contains(Ability.MagicResist);
                    coldRes  |= m.Abilities.Contains(Ability.ColdFireResist);
                    anyFortified |= m.Abilities.Contains(Ability.Fortified);
                }

                bool siege = atk.IsSiege;
                if (ctx != null)
                {
                    if (!siege && ctx.RangedAttackAsSiege) siege = true;
                    if (siege && ctx.SiegeAttackAsRanged) siege = false;
                }

                if (anyFortified && !siege) continue;      // Fortified 需 Siege

                if (!coldRes && fireRes && iceRes) coldRes = true;

                Ability? resist =
                    atk.Element == Element.Fire      && fireRes ? Ability.FireResist :
                    atk.Element == Element.Ice       && iceRes  ? Ability.IceResist  :
                    atk.Element == Element.ColdFire  && coldRes ? Ability.ColdFireResist : null;

                int val = atk.Value;
                if (ctx != null)
                {
                    if (!siege && ctx.DoubleRangedAttack) val *= 2;
                    if (siege && ctx.DoubleSiegeAttack) val *= 2;
                }
                int eff = (int)(val * Constants.Efficiency(atk.Element, resist));

                /* 3️⃣ 判断击杀 */
                if (eff >= armorSum)
                {
                    foreach (int ti in targetIdx)
                    {
                        killed.Add(ti);
                        fame += enemies[ti].Fame;            // 统计 Fame
                    }
                }
            }
            return new RangedResult(killed.OrderBy(i => i).ToArray(), fame);
        }
    }
}
