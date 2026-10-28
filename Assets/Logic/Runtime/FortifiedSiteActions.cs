using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime
{
    /// <summary>城防地点的真实奖励选择和回合结束补牌加成。</summary>
    public static class FortifiedSiteActions
    {
        public static void SelectSpellReward(ActionContext context, int offerIndex)
        {
            if (!context.PendingSpellReward || context.InBattle || context.TunnelAssaultActive)
                throw new InvalidOperationException("必须先征服法师塔才能选择回合结束奖励");
            if (context.SpellSupply == null || offerIndex < 0 || offerIndex >= context.SpellSupply.Offer.Count)
                throw new InvalidOperationException("奖励必须选择真实法术供给中的卡牌");
            context.SelectedSpellReward = context.SpellSupply.Offer[offerIndex];
        }

        internal static void ValidateSpellReward(ActionContext context)
        {
            if (context?.PendingSpellReward != true) return;
            if (context.SelectedSpellReward == null || context.SpellSupply == null
                || !context.SpellSupply.Offer.Any(c => ReferenceEquals(c, context.SelectedSpellReward)))
                throw new InvalidOperationException("结束回合前必须选择仍在供给中的法术奖励");
        }

        internal static void GrantSpellReward(PlayerState player, ActionContext context)
        {
            if (context?.PendingSpellReward != true) return;
            int index = context.SpellSupply.Offer.FindIndex(c => ReferenceEquals(c, context.SelectedSpellReward));
            var spell = context.SpellSupply.Claim(index);
            player.Deck.PutOnTop(new DeedCard(spell.Id, CardType.Spell));
            context.SpellSupply.FillVacancies();
            context.PendingSpellReward = false;
            context.SelectedSpellReward = null;
        }

        public static int HandBonus(PlayerState player, MapState map)
        {
            if (map == null) return 0;
            bool Near(AxialCoord position)
            {
                int q = position.Q - player.Position.Q, r = position.R - player.Position.R;
                return (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2 <= 1;
            }
            var keeps = map.Places.Where(p => p.Value.Type == PlaceType.Keep
                && p.Value.Conquered && p.Value.OwnerId == player.Id).ToArray();
            int keepBonus = keeps.Any(p => Near(p.Key)) ? keeps.Length : 0;
            int cityBonus = map.Places.Where(p => p.Value.Type == PlaceType.City && p.Value.Conquered
                && Near(p.Key) && p.Value.CityShields.Contains(player.Id))
                .Select(p => p.Value.OwnerId == player.Id ? 2 : 1).DefaultIfEmpty(0).Max();
            return Math.Max(keepBonus, cityBonus);
        }
    }
}
