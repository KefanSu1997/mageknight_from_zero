using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 能量流轉 / 能量竊取：重整自身部隊並令其他玩家的低級部隊橫置。
    /// 需於 <see cref="ActionContext.TargetUnit"/> 指定欲重整的部隊。
    /// </summary>
    public sealed class EnergyFlowEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public EnergyFlowEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0) throw new InvalidOperationException("能量流转和能量窃取没有互斥选项");
            if (ctx.InBattle) throw new InvalidOperationException("战斗中不能施放能量流转或能量窃取");
            var unit = ctx.TargetUnit;
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.IsReady)
                throw new InvalidOperationException("能量法术必须实际重整一支耗尽且未销毁的自有部队");
            var selectedOwners = new HashSet<PlayerState>();
            foreach (var target in ctx.EnergyExhaustTargets)
            {
                var owners = ctx.OtherPlayers.Where(p => !ReferenceEquals(p,player) && p.Units.Contains(target)).Distinct().ToArray();
                if (target == null || owners.Length != 1 || target.IsDestroyed || !target.IsReady
                    || target.Card.Level < 1 || target.Card.Level > (_enh ? 3 : 2) || !selectedOwners.Add(owners[0]))
                    throw new InvalidOperationException("耗尽目标须为每位其他玩家至多一支等级范围内就绪且未销毁的部队");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player,ctx,option);
            var unit = ctx.TargetUnit!;
            unit.Ready();
            if (_enh && unit.Wounds > 0)
            {
                unit.Heal(int.MaxValue);
                ctx.HealedUnitsThisTurn.Add(unit);
            }
            foreach (var target in ctx.EnergyExhaustTargets) target.Exhaust();
        }
    }
}
