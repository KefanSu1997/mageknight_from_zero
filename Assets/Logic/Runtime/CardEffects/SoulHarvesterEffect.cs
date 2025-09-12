using MK.Logic.Core;
using MK.Logic.Data;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 靈魂收割者：攻擊並依擊殺敵人獲得魔晶。
    /// </summary>
    public sealed class SoulHarvesterEffect : ICardEffect
    {
        private readonly bool _once;
        public SoulHarvesterEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MeleePool += _once ? 8 : 3;
            ctx.CrystalsPerKill = 1;
            ctx.CrystalOnKill = SelectColor;
        }

        private static ManaColor SelectColor(Monster m)
        {
            if (m.Abilities.Contains(Ability.FireResist))
                return ManaColor.Red;
            if (m.Abilities.Contains(Ability.IceResist))
                return ManaColor.Blue;
            if (m.Abilities.Contains(Ability.MagicResist))
                return ManaColor.Green;
            return ManaColor.White;
        }
    }
}
