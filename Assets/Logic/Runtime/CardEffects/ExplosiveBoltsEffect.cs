using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 爆裂箭矢：受創獲得兩色晶體或造成遠程攻擊並削弱護甲。
    /// </summary>
    public sealed class ExplosiveBoltsEffect : ICardEffect
    {
        private readonly bool _enh;
        public ExplosiveBoltsEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                player.Wounds += 1;
                player.Mana.AddCrystal(ManaColor.White, 1);
                player.Mana.AddCrystal(ManaColor.Red, 1);
            }
            else
            {
                ctx.RangedPool += 3;
                ctx.ArmorReducePerKill = 1;
            }
        }
    }
}
