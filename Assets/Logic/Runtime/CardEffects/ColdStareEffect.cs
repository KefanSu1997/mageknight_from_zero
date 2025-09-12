using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冷峻目光：影響力或使敵人失去攻擊能力。
    /// option 0 影響力，1 對敵人
    /// </summary>
    public sealed class ColdStareEffect : ICardEffect
    {
        private readonly bool _enh;
        public ColdStareEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
            {
                ctx.InfluencePool += _enh ? 5 : 3;
            }
            else
            {
                if (_enh)
                    ctx.EnemySkipAttack = true;
                else
                    ctx.AttackDisabled = true;
            }
        }
    }
}
