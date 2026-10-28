namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 血脈賁張：新傷進手後抽牌；強效移除首次新傷，另留三次抽牌。
    /// </summary>
    public sealed class BloodSurgeEffect : ICardEffect
    {
        private readonly bool _enh;
        public BloodSurgeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.BeginBloodSurge(player, _enh);
        }
    }
}
