namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 烈焰之牆 / 烈焰浪湧：火焰攻擊或格擋，強效依交戰敵人數提升。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class FlameWallEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public FlameWallEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 1) throw new System.InvalidOperationException("烈焰之墙必须选择攻击或格挡");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            int bonus = _enh ? ctx.EngagedEnemies * 2 : 0;
            if (option == 0)
                ctx.CombatPower.AddAttack(5 + bonus, MK.Logic.Core.Element.Fire);
            else
                ctx.CombatPower.AddBlock(7 + bonus, MK.Logic.Core.Element.Fire);
        }
    }
}
