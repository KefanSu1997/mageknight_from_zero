using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 德魯伊之道：移動並降低移動成本。
    /// </summary>
    public sealed class DruidicPathsEffect : ICardEffect
    {
        private readonly int _move;
        private readonly bool _terrain;
        public DruidicPathsEffect(int move, bool byTerrain)
        {
            _move = move;
            _terrain = byTerrain;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _move;
            if (_terrain)
                ctx.MoveBonusAll += 0; // placeholder
            else
                ctx.MoveBonusNext += 0; // placeholder for single hex
            // 具體移動減費未實作，留待 MovementService 擴充
        }
    }
}
