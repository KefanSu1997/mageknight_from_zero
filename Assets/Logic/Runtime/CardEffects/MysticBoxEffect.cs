using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 神秘寶盒：翻開神器並可暫時使用其效果。
    /// 基礎僅翻開後放回，強效則立即套用一次該神器的強效並獲得名望。
    /// </summary>
    public sealed class MysticBoxEffect : ICardEffect
    {
        private readonly bool _once;
        public MysticBoxEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var supply = ctx.ArtifactSupply ?? throw new System.InvalidOperationException("缺少神器供應");
            var art = supply.Draw();
            supply.PutUnder(art);
            if (_once)
            {
                int idx = int.Parse(art.Id.Split('_')[1]);
                int baseOffset = (int)ActionEffectId.HonorBannerAssign;
                var effId = (ActionEffectId)(baseOffset + idx * 2 + 1);
                CardEffectFactory.Get(effId).Execute(player, ctx);
                player.Fame += 1;
            }
        }
    }
}
