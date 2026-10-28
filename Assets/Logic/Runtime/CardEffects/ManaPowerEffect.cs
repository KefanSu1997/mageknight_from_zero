using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Power of Crystals: missing-color crystal, or main effect plus independently chosen set bonuses.</summary>
    public sealed class ManaPowerEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaPowerEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_enh)
            {
                if (option < 0 || option > 3 || player.Mana.Crystals.GetValueOrDefault((ManaColor)option) != 0)
                    throw new InvalidOperationException("魔晶力量必须选择库存中没有的基本色魔晶");
                return;
            }
            if (option < 0 || option > 2 || ctx.ManaPowerBonusChoices.Any(c => c < 0 || c > 2))
                throw new InvalidOperationException("魔晶力量必须选择移动、治疗或抽牌");
            if (ctx.InBattle && (option == 1 || ctx.ManaPowerBonusChoices.Contains(1)))
                throw new InvalidOperationException("战斗中不能使用治疗效果");
        }

        public void ValidatePayment(PlayerState player, ActionContext ctx, int option, ManaColor[] cost)
        {
            Validate(player, ctx, option);
            if (!_enh) return;
            // Use the formal payment algorithm, including infinite mana and tokens before crystals.
            if (!player.Mana.TryPreviewExactColors(cost, ctx, out var remaining))
                throw new InvalidOperationException("法力不足，不能支付所选效果");
            ValidateSets(ctx, remaining);
        }

        private static void ValidateSets(ActionContext ctx, Dictionary<ManaColor, int> crystals)
        {
            int sets = Enumerable.Range(0, 4).Min(i => crystals.GetValueOrDefault((ManaColor)i));
            if (ctx.ManaPowerBonusChoices.Count != sets)
                throw new InvalidOperationException("请为付款后每套四色魔晶分别选择一个奖励");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh) player.Mana.AddCrystal((ManaColor)option, 1);
            else
            {
                ValidateSets(ctx, player.Mana.Crystals);
                Apply(option, 2);
                foreach (int choice in ctx.ManaPowerBonusChoices) Apply(choice, 1);
            }
            ctx.ManaPowerBonusChoices.Clear();

            void Apply(int choice, int amount)
            {
                if (choice == 0) ctx.MovementPool += amount * 2;
                else if (choice == 1) ctx.Healing.Add(amount);
                else player.Deck.DrawExact(amount);
            }
        }
    }
}
