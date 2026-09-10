using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 風雪交加 / 狂風暴雪：提供寒冰攻擊，強效需受傷。
    /// </summary>
    public sealed class BlizzardEffect : ICardEffect
    {
        private readonly bool _enh;
        public BlizzardEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh)
            {
                player.Wounds += 1;
                ctx.CombatPower.AddAttack(8, Element.Ice, AttackType.Siege);
            }
            else
            {
                ctx.CombatPower.AddAttack(5, Element.Ice, AttackType.Ranged);
            }
        }
    }
}
