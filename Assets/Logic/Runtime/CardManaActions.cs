using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>由实际魔力源骰子产生魔力，集中处理取用额度和卡牌转换。</summary>
    public static class CardManaActions
    {
        public static void UseSourceDie(PlayerState player, ActionContext context,
            ManaDie die, ManaColor chosen, bool extra = false)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            ManaSource.ValidateUse(player);
            var source = context.ManaSource;
            if (source == null || die == null || !source.Dice.Contains(die))
                throw new InvalidOperationException("所选骰子不在公共魔力源");
            if (!Enum.IsDefined(typeof(ManaColor), chosen))
                throw new InvalidOperationException("无效的魔力颜色");
            if (extra ? context.ExtraManaDice <= 0 : player.HeldManaDie != null)
                throw new InvalidOperationException("本次取骰没有可用额度");
            // 魔力征引的黑色通用许可只属于它提供的额外骰。
            bool blackExtra = extra && context.ExtraBlackManaDice > 0;
            bool basicColor = chosen >= ManaColor.Red && chosen <= ManaColor.White;
            if (die.Face == ManaColor.Black)
            {
                if (!blackExtra && !(context.BlackManaWild && basicColor) && (chosen != ManaColor.Black
                    || context.DayPart == DayPart.Day && !context.AllowBlackAtDay))
                    throw new InvalidOperationException("此黑骰不能产生所选魔力");
            }
            else if (die.Face == ManaColor.Gold)
            {
                if (chosen != ManaColor.Gold && (chosen < ManaColor.Red || chosen > ManaColor.White)
                    || context.DayPart == DayPart.Night && !context.AllowGoldAtNight && !context.GoldManaWild)
                    throw new InvalidOperationException("此金骰不能产生所选魔力");
            }
            else if (chosen != die.Face)
                throw new InvalidOperationException("基本色骰必须产生骰面颜色");

            if (extra)
            {
                source.TakeExtra(player, die);
                context.ExtraManaDice--;
                if (blackExtra) context.ExtraBlackManaDice--;
            }
            else source.TakeOrdinary(player, die);
            player.Mana.AddToken(chosen);
        }

        public static void ValidateTransform(ActionContext context, int count, IReadOnlyList<ManaColor> colors, PlayerState player = null)
        {
            if (player != null) ManaSource.ValidateUse(player);
            var dice = context.ManaDiceToTransform;
            if (context.ManaSource == null || dice == null || dice.Count != count
                || dice.Distinct().Count() != count || dice.Any(d => !context.ManaSource.Dice.Contains(d)))
                throw new InvalidOperationException("请先选择所需数量的不同可用骰子");
            if (colors.Count != count || colors.Any(c => !Enum.IsDefined(typeof(ManaColor), c) || c == ManaColor.Gold))
                throw new InvalidOperationException("魔力转换只能选择非金色骰面");
        }

        public static void Transform(PlayerState player, ActionContext context,
            IReadOnlyList<ManaColor> colors, int tokensEach)
        {
            ValidateTransform(context, colors.Count, colors, player);
            context.ManaSource.TakeTransformed(player, context.ManaDiceToTransform, colors);
            foreach (var color in colors) player.Mana.AddToken(color, tokensEach);
            context.ManaDiceToTransform.Clear();
        }
    }
}
