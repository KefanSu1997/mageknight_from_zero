using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 大地之子：提供移動力並選擇治療或格擋。
    /// </summary>
    public sealed class EarthStrengthEffect : ICardEffect
    {
        private readonly int _move;
        private readonly int _heal;
        private readonly bool _useTerrain;
        public EarthStrengthEffect(int move, int heal, bool useTerrain)
        {
            _move = move;
            _heal = heal;
            _useTerrain = useTerrain;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _move;
            if (option == 0)
            {
                CardHealing.Heal(player, ctx, _heal);
            }
            else
            {
                int block = _useTerrain
                    ? (_terrainBlock(ctx.CurrentTerrain))
                    : 2;
                ctx.BlockPool += block;
            }
        }

        private static int _terrainBlock(TerrainType t) => t switch
        {
            TerrainType.Mountain => 5,
            TerrainType.Lake => 2,
            _ => TerrainCost.GetCost(t, DayPart.Day)
        };
    }
}
