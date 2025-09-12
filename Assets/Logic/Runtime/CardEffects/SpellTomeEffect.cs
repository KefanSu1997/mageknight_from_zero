using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 法術寶典：棄牌施放市場法術，強效可使用強力效果。
    /// option 為目標法術在供應中的索引。
    /// </summary>
    public sealed class SpellTomeEffect : ICardEffect
    {
        private readonly bool _once;
        public SpellTomeEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToDiscard == null)
                throw new System.InvalidOperationException("未指定棄牌");
            player.Deck.Discard(ctx.CardToDiscard);
            ctx.CardToDiscard = null;

            var supply = ctx.SpellSupply ?? throw new System.InvalidOperationException("缺少法術供應");
            int idx = System.Math.Clamp(option, 0, supply.Offer.Count - 1);
            var spell = supply.Offer[idx];
            int sIdx = int.Parse(spell.Id.Split('_')[1]);
            int baseOffset = (int)ActionEffectId.EnergyFlowBase;
            var effId = (ActionEffectId)(baseOffset + sIdx * 2 + (_once ? 1 : 0));
            CardEffectFactory.Get(effId).Execute(player, ctx);
        }
    }
}
