using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力風暴：操作公共魔力源並獲取晶體或額外使用骰子。
    /// option 參數為欲選取的骰子索引。
    /// </summary>
    public sealed class ManaStormEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaStormEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var source = ctx.ManaSource ?? throw new System.InvalidOperationException("缺少魔力源");
            if (!_enh)
            {
                int idx = System.Math.Clamp(option, 0, source.Dice.Count - 1);
                var die = source.Dice[idx];
                var color = die.Face;
                if (color is ManaColor.Red or ManaColor.Blue or ManaColor.Green or ManaColor.White)
                    player.Mana.AddCrystal(color, 1);
                die.Roll(ctx.DayPart);
            }
            else
            {
                source.RollAll();
                ctx.ExtraManaDice += 3;
                ctx.BlackManaWild = true;
                ctx.GoldManaWild = true;
            }
        }
    }
}
