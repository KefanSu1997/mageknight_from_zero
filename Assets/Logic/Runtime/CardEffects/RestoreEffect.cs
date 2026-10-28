using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 復原 / 重生：治療並依地形重整部隊。
    /// </summary>
    public sealed class RestoreEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public RestoreEffect(bool enhanced) => _enh = enhanced;

        private static IEnumerable<UnitState> Targets(ActionContext ctx)
            => ctx.ReadyUnitTargets ?? (ctx.TargetUnit == null ? Enumerable.Empty<UnitState>() : new[] { ctx.TargetUnit });

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0) throw new InvalidOperationException("复原和重生没有互斥选项");
            if (ctx.InBattle) throw new InvalidOperationException("战斗中不能使用复原或重生");
            if (!_enh) return;
            var seen = new HashSet<UnitState>();
            int levels = 0;
            foreach (var unit in Targets(ctx))
            {
                if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.Card.Level < 1 || !seen.Add(unit))
                    throw new InvalidOperationException("重生必须选择不重复且未销毁的自有部队");
                levels = checked(levels + unit.Card.Level);
            }
            if (levels > (ctx.CurrentTerrain == TerrainType.Forest ? 5 : 3))
                throw new InvalidOperationException("重生所选部队总等级超过地形上限");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.Healing.Add(ctx.CurrentTerrain == TerrainType.Forest ? 5 : 3);
            if (_enh) foreach (var unit in Targets(ctx)) unit.Ready();
        }
    }
}
