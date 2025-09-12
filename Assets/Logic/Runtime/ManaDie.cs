using MK.Logic.Core;
using System;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 表示公共魔力池中的一顆骰子。
    /// 按照日夜結果決定可用顏色，黑夜禁止金色，白晝禁止黑色。
    /// </summary>
    public sealed class ManaDie
    {
        private readonly Random _rnd;
        public ManaColor Face { get; private set; } = ManaColor.Red;

        public ManaDie(Random? rnd = null)
        {
#if NETSTANDARD2_1
            // Random.Shared 在 netstandard2.1 不可用，改用單例 Random
            _rnd = rnd ?? new Random();
#else
            _rnd = rnd ?? Random.Shared;
#endif
        }

        /// <summary>
        /// 依當前日夜擲骰，排除禁用的顏色。
        /// </summary>
        public void Roll(DayPart part)
        {
#if NETSTANDARD2_1
            // netstandard2.1 沒有泛型 Enum.GetValues
            var colors = System.Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>().ToList();
#else
            var colors = Enum.GetValues<ManaColor>().ToList();
#endif
            if (part == DayPart.Day)
                colors.Remove(ManaColor.Black);
            else
                colors.Remove(ManaColor.Gold);
            int idx = _rnd.Next(colors.Count);
            Face = colors[idx];
        }
    }
}
