using MK.Logic.Core;
using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 愤怒号角：攻城攻击并可能因骰色受创。
    /// option 表示強效額外攻擊力，0~5。
    /// </summary>
    public sealed class WrathHornEffect : ICardEffect
    {
        private readonly bool _once;
        private readonly Random _rnd;
        public WrathHornEffect(bool once, Random? rnd = null)
        {
            _once = once;
#if NETSTANDARD2_1
            _rnd = rnd ?? new Random();
#else
            _rnd = rnd ?? Random.Shared;
#endif
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += 5;
            if (!_once)
            {
                RollAndWound(player, ctx.DayPart);
            }
            else
            {
                int extra = Math.Clamp(option, 0, 5);
                ctx.RangedPool += extra;
                for (int i = 0; i < extra; i++)
                    RollAndWound(player, ctx.DayPart);
            }
        }

        private void RollAndWound(PlayerState player, DayPart part)
        {
            var die = new ManaDie(_rnd);
            die.Roll(part);
            if (die.Face is ManaColor.Red or ManaColor.Black)
                player.Wounds += 1;
        }
    }
}
