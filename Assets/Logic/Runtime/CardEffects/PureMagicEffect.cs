using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 純淨魔法：支付選定顏色魔力以獲得對應數值。
    /// option 為顏色索引 0=Red 1=Blue 2=Green 3=White。
    /// </summary>
    public sealed class PureMagicEffect : ICardEffect, ICardEffectValidator, ICardAdditionalManaCost
    {
        private readonly bool _enh;
        public PureMagicEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 3)
                throw new System.InvalidOperationException("纯净魔法必须选择红蓝绿白之一");
        }

        public ManaColor[] GetAdditionalManaCost(int option) => new[] { (ManaColor)option };

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var color = (ManaColor)option;

            int val = _enh ? 7 : 4;
            switch (color)
            {
                case ManaColor.Green:
                    ctx.MovementPool += val;
                    break;
                case ManaColor.White:
                    ctx.InfluencePool += val;
                    break;
                case ManaColor.Blue:
                    ctx.BlockPool += val;
                    break;
                case ManaColor.Red:
                    ctx.MeleePool += val;
                    break;
            }
        }

    }
}
