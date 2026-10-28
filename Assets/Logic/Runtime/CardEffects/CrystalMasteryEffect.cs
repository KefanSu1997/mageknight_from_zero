using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔晶精通：基礎獲得同色魔晶，強效返還本回合消耗的晶體。
    /// </summary>
    public sealed class CrystalMasteryEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public CrystalMasteryEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh) return;
            if (option < 0 || option > 3 || !player.Mana.Crystals.TryGetValue((ManaColor)option, out int count) || count <= 0)
                throw new System.InvalidOperationException("魔晶精通必须选择当前实际持有的魔晶颜色");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                player.Mana.AddCrystal((ManaColor)option, 1);
            }
            else
            {
                ctx.ReturnSpentCrystals = true;
            }
        }
    }
}
