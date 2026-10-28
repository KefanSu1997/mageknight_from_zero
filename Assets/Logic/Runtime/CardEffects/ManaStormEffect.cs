using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力風暴：操作公共魔力源並獲取晶體或額外使用骰子。
    /// option 參數為欲選取的骰子索引。
    /// </summary>
    public sealed class ManaStormEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaStormEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var source = ctx.ManaSource ?? throw new System.InvalidOperationException("缺少魔力源");
            if (!_enh && (option < 0 || option >= source.Dice.Count
                || source.Dice[option].Face < ManaColor.Red || source.Dice[option].Face > ManaColor.White))
                throw new System.InvalidOperationException("魔力风暴必须选择源中实际可用的基本色骰");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var source = ctx.ManaSource ?? throw new System.InvalidOperationException("缺少魔力源");
            if (!_enh)
            {
                var die = source.Dice[option];
                var color = die.Face;
                if (color is ManaColor.Red or ManaColor.Blue or ManaColor.Green or ManaColor.White)
                    player.Mana.AddCrystal(color, 1);
                die.RollUnrestricted();
            }
            else
            {
                foreach (var die in source.Dice) die.RollUnrestricted();
                ctx.ExtraManaDice += 3;
                ctx.BlackManaWild = true;
                ctx.GoldManaWild = true;
            }
        }
    }
}
