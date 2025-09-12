using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 痊癒 / 感染：基礎治療並本回合治療手牌傷牌時抽牌，強效使被格擋敵人護甲降至1。
    /// </summary>
    public sealed class CureEffect : ICardEffect
    {
        private readonly bool _enh;
        public CureEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                player.Wounds = System.Math.Max(0, player.Wounds - 2);
                ctx.DrawPerHeal = 1;
                ctx.ReadyUnitOnHeal = true;
            }
            else
            {
                ctx.BlockedArmorOne = true;
            }
        }
    }
}
