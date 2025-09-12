using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 无尽金币包：提供影响力并获得名望。
    /// </summary>
    public sealed class GoldPouchEffect : ICardEffect
    {
        private readonly bool _once;
        public GoldPouchEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                ctx.InfluencePool += 4;
                player.Fame += 2;
            }
            else
            {
                ctx.InfluencePool += 9;
                player.Fame += 3;
            }
        }
    }
}
