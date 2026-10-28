using MK.Logic.Core;
using System;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 表示公共魔力池中的一顆骰子。
    /// 六种骰面始终可能出现；昼夜限制由实际取用流程判断。
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

        /// <summary>Roll all six printed faces; card-effect dice are not filtered by time of day.</summary>
        public void RollUnrestricted() => Face = (ManaColor)_rnd.Next(6);

        /// <summary>卡牌转换实际骰面，不触发随机重掷。</summary>
        public void TurnTo(ManaColor color)
        {
            if (!Enum.IsDefined(typeof(ManaColor), color))
                throw new ArgumentOutOfRangeException(nameof(color));
            Face = color;
        }

        /// <summary>
        /// 正式源骰同样掷六面，不能预先排除枯竭颜色。
        /// </summary>
        public void Roll(DayPart part)
        {
            RollUnrestricted();
        }
    }
}
