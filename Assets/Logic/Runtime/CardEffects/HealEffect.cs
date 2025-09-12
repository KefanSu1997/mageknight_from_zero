namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 治療玩家創傷的效果。
    /// </summary>
    public sealed class HealEffect : ICardEffect
    {
        private readonly int _value;
        public HealEffect(int value) => _value = value;
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Wounds = System.Math.Max(0, player.Wounds - _value);
        }
    }
}
