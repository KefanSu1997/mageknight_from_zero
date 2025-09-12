using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔晶精通：基礎獲得同色魔晶，強效返還本回合消耗的晶體。
    /// </summary>
    public sealed class CrystalMasteryEffect : ICardEffect
    {
        private readonly bool _enh;
        public CrystalMasteryEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                var color = (ManaColor)System.Math.Clamp(option, 0, 3);
                if (player.Mana.Crystals.ContainsKey(color))
                    player.Mana.AddCrystal(color, 1);
            }
            else
            {
                ctx.ReturnSpentCrystals = true;
            }
        }
    }
}
