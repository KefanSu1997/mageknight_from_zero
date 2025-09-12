namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 烈焰之牆 / 烈焰浪湧：火焰攻擊或格擋，強效依交戰敵人數提升。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class FlameWallEffect : ICardEffect
    {
        private readonly bool _enh;
        public FlameWallEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int bonus = _enh ? ctx.EngagedEnemies * 2 : 0;
            if (option == 0)
                ctx.MeleePool += 5 + bonus;
            else
                ctx.BlockPool += 7 + bonus;
        }
    }
}
