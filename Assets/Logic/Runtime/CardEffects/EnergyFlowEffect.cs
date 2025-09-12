using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 能量流轉 / 能量竊取：重整自身部隊並令其他玩家的低級部隊橫置。
    /// 需於 <see cref="ActionContext.TargetUnit"/> 指定欲重整的部隊。
    /// </summary>
    public sealed class EnergyFlowEffect : ICardEffect
    {
        private readonly bool _enh;
        public EnergyFlowEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var unit = ctx.TargetUnit;
            if (unit == null) return;
            unit.Ready();
            if (_enh)
                unit.Heal(int.MaxValue);

            int limit = _enh ? 3 : 2;
            foreach (var other in ctx.OtherPlayers)
            {
                var tgt = other.Units.FirstOrDefault(u => u.Card.Level <= limit && u.IsReady);
                tgt?.Exhaust();
            }
        }
    }
}
