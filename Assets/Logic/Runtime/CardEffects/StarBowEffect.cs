using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 星辰之弓：棄牌換取遠程攻擊，擊殺獲名望；強效令遠程及攻城攻擊翻倍或互換。
    /// </summary>
    public sealed class StarBowEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public StarBowEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_once) WeaponDiscard.Validate(player, ctx);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                int count = WeaponDiscard.Discard(player, ctx);
                ctx.CombatPower.AddAttack(count * 2, type: AttackType.Ranged);
                ctx.BowFamePerKill += 1;
            }
            else
            {
                ctx.StarBowActive = true;
            }
        }
    }
}
