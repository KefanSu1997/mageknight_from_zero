using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 公正之劍：棄牌換取攻擊並在擊殺敵人時獲得名望；強效使物理攻擊翻倍並忽略抗性。
    /// </summary>
    public sealed class JusticeSwordEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public JusticeSwordEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_once) WeaponDiscard.Validate(player, ctx);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                int count = WeaponDiscard.Discard(player, ctx);
                ctx.CombatPower.AddAttack(count * 3);
                ctx.SwordFamePerKill += 1;
            }
            else
            {
                ctx.DoublePhysicalAttack = true;
                ctx.IgnorePhysicalResist = true;
                ctx.SwordFamePerKill += 1;
            }
        }
    }
}
