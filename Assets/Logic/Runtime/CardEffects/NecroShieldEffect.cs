using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 亡君之盾：提供強大或分散的格擋。
    /// option 0 為單次格擋，1 為多次格擋。
    /// </summary>
    public sealed class NecroShieldEffect : ICardEffect
    {
        private readonly bool _once;
        public NecroShieldEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                if (option == 0)
                    ctx.BlockPool += 6;
                else
                    ctx.BlockPool += 8; // 4+4
            }
            else
            {
                if (option == 0)
                    ctx.BlockPool += 8;
                else
                    ctx.BlockPool += 12; // 4*3
            }
        }
    }
}
