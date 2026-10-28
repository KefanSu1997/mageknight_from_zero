using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original Magic Familiars: paid recruitment color, then an inventory crystal each round.</summary>
    public static class FamiliarActions
    {
        public static bool IsBasicColor(ManaColor color) => color >= ManaColor.Red && color <= ManaColor.White;

        public static int AbilityValue(UnitState unit, UnitAbility ability) => ability.Value
            + (ability.AttunementColor.HasValue && unit.AttunementColor == ability.AttunementColor
                ? ability.AttunementBonus : 0);

        internal static void ResolveUpkeep(PlayerState player, UnitState unit, ManaColor? crystal)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed
                || !unit.Card.RequiresAttunement || !unit.NeedsAttunementUpkeep)
                throw new InvalidOperationException("请选择本轮尚未维持的自有魔宠");
            if (!crystal.HasValue)
            {
                unit.Destroy();
                player.Units.Remove(unit);
                return;
            }
            var color = crystal.Value;
            if (!IsBasicColor(color) || player.Mana.Crystals.GetValueOrDefault(color) < 1)
                throw new InvalidOperationException("维持魔宠必须使用库存中的一颗基本色魔晶，不能用临时魔力");
            // Move an inventory crystal to the card; this is not a turn's mana payment/refund.
            player.Mana.Crystals[color] -= 1;
            unit.AttunementColor = color;
            unit.NeedsAttunementUpkeep = false;
        }
    }
}
