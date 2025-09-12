namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 旋風 / 颶風：使敵人不攻擊或直接消滅。
    /// option 為敵人索引。
    /// </summary>
    public sealed class WhirlwindEffect : ICardEffect
    {
        private readonly bool _enh;
        public WhirlwindEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh)
                ctx.KillEnemyIndex = option;
            else
                ctx.SkipAttackIndices.Add(option);
        }
    }
}
