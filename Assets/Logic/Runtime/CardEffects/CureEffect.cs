using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 痊癒 / 感染：基礎治療並本回合治療手牌傷牌時抽牌，強效使被格擋敵人護甲降至1。
    /// </summary>
    public sealed class CureEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public CureEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_enh && ctx.InBattle) throw new InvalidOperationException("战斗中不能施放痊愈");
            if (_enh && !ctx.InBattle) throw new InvalidOperationException("感染只能在战斗中施放");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                // Printed "this turn" includes healing completed before this cast.
                player.Deck.DrawExact(ctx.HealedHandWoundsThisTurn);
                ctx.DrawPerHeal += 1;
                ctx.ReadyUnitOnHeal = true;
                foreach (var unit in ctx.HealedUnitsThisTurn)
                    if (player.Units.Contains(unit) && !unit.IsDestroyed) unit.Ready();
                ctx.Healing.Add(2);
            }
            else
            {
                ctx.BlockedArmorOne = true;
            }
        }
    }
}
