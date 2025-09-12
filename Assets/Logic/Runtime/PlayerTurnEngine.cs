using MK.Logic.Runtime.Map;
using MK.Logic.Runtime;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 負責處理單位玩家回合開始與結束時的共通行為。
    /// </summary>
    public sealed class PlayerTurnEngine
    {
        private readonly IGameLogger? _logger;

        public PlayerTurnEngine(IGameLogger? logger = null)
        {
            _logger = logger;
        }
        /// <summary>
        /// 回合開始：僅讓部隊就緒，不進行抽牌（抽牌在回合結束）。
        /// </summary>
        public void StartTurn(
            PlayerState p,
            DayPart part,
            GlobalResources? globals = null,
            Map.MapState? map = null)
        {
            _logger?.Log($"StartTurn P{p.Id}");
            // 開啟回合時重置持有骰，防止上一回合遺留
            p.HeldManaDie = null;
            foreach (var u in p.Units)
                u.NewRound();

            // 發放因法術獲得的每回合法力標記
            foreach (var pair in p.TokensPerTurn)
                p.Mana.AddToken(pair.Key, pair.Value);
            p.ManaCurseTriggered = false;
            if (globals != null && map != null)
                _logger?.LogFullState(p, globals, map);
        }

        /// <summary>
        /// 與舊版相容的簡化入口。
        /// </summary>
        public void StartTurn(PlayerState p, DayPart part) =>
            StartTurn(p, part, null, null);

        /// <summary>
        /// 回合結束：清空臨時法力並依照上限摸牌。
        /// </summary>
        public void EndTurn(PlayerState p, DayPart part, ManaSource source, ActionContext? ctx = null)
        {
            _logger?.Log($"EndTurn P{p.Id}");
            if (p.HeldManaDie != null)
            {
                source.Return(p.HeldManaDie);
                p.HeldManaDie = null;
            }
            if (ctx != null)
            {
                if (ctx.ReturnSpentCrystals)
                {
                    foreach (var pair in ctx.SpentCrystals)
                        p.Mana.AddCrystal(pair.Key, pair.Value);
                }
                if (ctx.CardToRecycle != null)
                {
                    p.Deck.RemoveFromHand(ctx.CardToRecycle);
                    if (ctx.RecycleToTop)
                        p.Deck.PutOnTop(ctx.CardToRecycle);
                    else if (ctx.RecycleToBottom && p.Deck.DrawPileCount > 0)
                        p.Deck.PutUnder(ctx.CardToRecycle);
                    ctx.CardToRecycle = null;
                }
                ctx.SpentCrystals.Clear();
                ctx.RecycleToTop = ctx.RecycleToBottom = false;
            }
            p.Mana.ResetTokens();
            if (ctx != null && ctx.SkipDraw)
            {
                ctx.SkipDraw = false;
                return;
            }
            int limit = p.Deck.HandLimit(
                part,
                p.Reputation,
                p.TacticHandBonus,
                p.KeepOrCastlePenalty,
                p.ExtraHandBonus);
            if (ctx != null && ctx.NextDrawBonus > 0)
            {
                limit += ctx.NextDrawBonus;
                ctx.NextDrawBonus = 0;
            }
            p.Deck.DrawToLimit(limit);
            _logger?.Log($"EndTurnComplete P{p.Id}");
        }
    }
}

