namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 野蠻收穫：提供移動力並允許在移動時棄牌獲得魔晶。
    /// 正式单格移动命令消费次数，按实际手牌材料结算魔晶。
    /// </summary>
    public sealed class WildHarvestEffect : ICardEffect, IFixedCardOutput
    {
        private readonly int _move;
        private readonly int _maxTriggers; // -1 表示每格皆可觸發
        public WildHarvestEffect(int move, int maxTriggers)
        {
            _move = move;
            _maxTriggers = maxTriggers;
        }

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] { new FixedCardOutput(0, FixedOutputKind.Movement, _move) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            FixedCardOutputs.Add(ctx, FixedOutputs[0]);
            // 基础整回合一次，强化每次成功单格移动可选一次。
            ctx.HarvestTriggers = _maxTriggers;
        }
    }
}
