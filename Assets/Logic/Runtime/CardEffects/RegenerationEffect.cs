using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 再生：獲得治療點並重整一支自有低級部隊，兩個目標可分別選擇。
    /// </summary>
    public sealed class RegenerationEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public RegenerationEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0) throw new InvalidOperationException("再生没有互斥选项");
            if (ctx.InBattle) throw new InvalidOperationException("战斗中不能使用再生");
            var unit = ctx.TargetUnit;
            if (unit != null && (!player.Units.Contains(unit) || unit.IsDestroyed
                || unit.Card.Level < 1 || unit.Card.Level > (_enh ? 3 : 2)))
                throw new InvalidOperationException("再生只能重整等级范围内未销毁的自有部队");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.Healing.Add(_enh ? 2 : 1);
            ctx.TargetUnit?.Ready();
        }
    }
}

