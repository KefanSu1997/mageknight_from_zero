using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 亡君之盾：提供強大或分散的格擋。
    /// option 0 為單次格擋，1 為多次格擋。
    /// </summary>
    public sealed class NecroShieldEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public NecroShieldEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 1) throw new System.InvalidOperationException("亡君之盾必须选择单次或分次格挡");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var element = _once ? Element.ColdFire : Element.Physical;
            if (option == 0) ctx.CombatPower.AddBlock(_once ? 8 : 6, element);
            else ctx.CombatPower.AddDistinctBlocks(4, element, _once ? 3 : 2);
        }
    }
}
