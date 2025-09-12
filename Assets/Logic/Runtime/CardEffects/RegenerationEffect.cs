using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 再生：治療自身並重整低級部隊。
    /// </summary>
    public sealed class RegenerationEffect : ICardEffect
    {
        private readonly bool _enh;
        public RegenerationEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int heal = _enh ? 2 : 1;
            player.Wounds = System.Math.Max(0, player.Wounds - heal);

            var unit = ctx.TargetUnit;
            if (unit != null)
            {
                int limit = _enh ? 3 : 2;
                if (unit.Card.Level <= limit)
                    unit.Ready();
            }
        }
    }
}

