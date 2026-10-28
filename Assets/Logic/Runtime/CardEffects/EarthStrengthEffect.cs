using System;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>大地之子三选一：0治疗、1格挡、2移动。强效格挡使用所在格未修正的费用。</summary>
    public sealed class EarthStrengthEffect : ICardEffect, IFixedCardOutput, ICardEffectValidator
    {
        private readonly int _move;
        private readonly int _heal;
        private readonly bool _useTerrain;
        public EarthStrengthEffect(int move, int heal, bool useTerrain)
        { _move = move; _heal = heal; _useTerrain = useTerrain; }

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => _useTerrain
            ? new[] { new FixedCardOutput(2, FixedOutputKind.Movement, _move) }
            : new[] { new FixedCardOutput(1, FixedOutputKind.Block, 2), new FixedCardOutput(2, FixedOutputKind.Movement, _move) };
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > 2) throw new InvalidOperationException("大地之子必须选择治疗、格挡或移动");
            if (option == 0 && context.InBattle) throw new InvalidOperationException("战斗中不能使用治疗效果");
        }

        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            if (option == 0) context.Healing.Add(_heal);
            else if (option == 2) FixedCardOutputs.Add(context, new FixedCardOutput(2, FixedOutputKind.Movement, _move));
            else
            {
                int value = !_useTerrain ? 2 : context.CurrentTerrain switch
                {
                    TerrainType.Mountain => 5,
                    TerrainType.Lake => 2,
                    _ => TerrainCost.GetCost(context.CurrentTerrain, context.DayPart)
                };
                Element element = !_useTerrain ? Element.Physical
                    : context.DayPart == DayPart.Day ? Element.Fire : Element.Ice;
                if (_useTerrain) context.CombatPower.AddBlock(value, element);
                else FixedCardOutputs.Add(context, new FixedCardOutput(1, FixedOutputKind.Block, value, element));
            }
        }
    }
}
