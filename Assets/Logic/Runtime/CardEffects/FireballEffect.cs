using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 火焰魔球 / 火焰風暴：提供遠程或攻城火焰攻擊。
    /// </summary>
    public sealed class FireballEffect : ICardEffect
    {
        private readonly bool _enh;
        public FireballEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh)
            {
                player.Wounds += 1;
                ctx.CombatPower.AddAttack(8, Element.Fire, AttackType.Siege);
            }
            else
            {
                ctx.CombatPower.AddAttack(5, Element.Fire, AttackType.Ranged);
            }
        }
    }
}
