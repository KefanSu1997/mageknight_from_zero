using System;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime
{
    /// <summary>魔法天赋的供给施法：使用一次同色许可，正常支付原法术费用，保留供给牌。</summary>
    public static class CardSpellActions
    {
        public static void PlayFromOffer(PlayerState player, ActionContext context, int index, bool enhanced = false, int option = 0,
            SpellCardData[] spells = null, ActionSystem actionSystem = null)
        {
            if (context.SpellSupply == null || index < 0 || index >= context.SpellSupply.Offer.Count)
                throw new InvalidOperationException("供给施法必须选择当前供给中的法术");
            var offered = context.SpellSupply.Offer[index];
            var originals = spells ?? CardJsonLoader.LoadSpells().ToArray();
            var spell = originals.SingleOrDefault(s => s.Id == offered.Id)
                ?? throw new InvalidOperationException("供给法术缺少原版定义");
            int permission = context.SpellColors.IndexOf(spell.ManaColor);
            if (permission < 0) throw new InvalidOperationException("没有本回合该颜色的供给施法许可");
            var data = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath, spell.EnImagePath,
                spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            using var source = CardContributionSource.Scope(context, offered);
            context.EnsureBattleWounds(player);
            (actionSystem ?? new ActionSystem(false, originals)).Play(data, player, context, enhanced, option);
            context.SpellColors.RemoveAt(permission);
            context.ResolveBattleWounds(player);
        }
    }
}
