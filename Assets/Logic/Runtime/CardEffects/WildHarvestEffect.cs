namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 野蠻收穫：提供移動力並允許在移動時棄牌獲得魔晶。
    /// 具體棄牌與獲取由外部系統處理，此處僅標記次數。
    /// </summary>
    public sealed class WildHarvestEffect : ICardEffect
    {
        private readonly int _move;
        private readonly int _maxTriggers; // -1 表示每格皆可觸發
        public WildHarvestEffect(int move, int maxTriggers)
        {
            _move = move;
            _maxTriggers = maxTriggers;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _move;
            // 使用 BlockPool 暫存允許觸發次數，待移動系統實作
            if (_maxTriggers != 0)
                ctx.BlockPool += _maxTriggers; // 先佔用現有欄位
        }
    }
}
