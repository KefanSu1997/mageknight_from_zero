using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 圍繞日夜與輪次進行的流程控制核心。
    /// 僅涵蓋順序、回合刷新與輪次結束等邏輯。
    /// </summary>
    public sealed class GameEngine
    {
        public RoundClock Clock { get; }
        public GlobalResources Globals { get; }
        public RoundEndTracker ClockTracker { get; } = new();
        public Map.MapState Map { get; }
        private readonly IList<PlayerState> _players;
        private readonly RoundOrderService _order = new();
        private readonly PlayerTurnEngine _turn;
        private readonly int _maxDays;
        private readonly IGameLogger? _logger;

        public GameEngine(
            IList<PlayerState> players,
            int maxDays,
            IGameLogger? logger = null,
            Map.MapState? map = null)
        {
            _players = players;
            Clock = new RoundClock();
            Globals = new GlobalResources(players.Count, () => Clock.DayPart);
            Map = map ?? new Map.MapState();
            _maxDays = maxDays;
            _logger = logger;
            _turn = new PlayerTurnEngine(logger);
        }

        /// <summary>開始新輪次，重擲魔力骰並根據戰術牌確定順序。</summary>
        public void StartNewRound(IReadOnlyList<TacticCard> picks)
        {
            _logger?.Log($"StartRound {Clock.RoundIndex} {Clock.DayPart} Day{Clock.DayIndex}");
            Globals.Mana.ResetSource();
            _order.Init(_players, picks);
            ClockTracker.Reset();
            // 開局抓牌：根據手牌上限抽到滿，不會在此階段洗牌
            foreach (var p in _players)
            {
                int limit = p.Deck.HandLimit(
                    Clock.DayPart,
                    p.Reputation,
                    p.TacticHandBonus,
                    p.KeepOrCastlePenalty,
                    p.ExtraHandBonus);
                p.Deck.DrawToLimit(limit);
            }
        }

        /// <summary>執行一名玩家的回合。</summary>
        public void RunOneTurn()
        {
            var player = _order.NextPlayer();
            _logger?.Log($"TurnStart P{player.Id}");

            // 若玩家已無牌可抽且手牌為空，依規則自動宣告結束輪次
            // CountdownStarted 判斷可避免重複啟動
            if (player.Deck.Hand.Count == 0
                && player.Deck.DrawPileCount == 0
                && !ClockTracker.CountdownStarted)
            {
                ClockTracker.StartCountdown(_players.Count);
            }

            _turn.StartTurn(player, Clock.DayPart, Globals, Map);
            // 在測試中不處理具體行動，直接結束回合
            _turn.EndTurn(player, Clock.DayPart, Globals.Mana);
            _logger?.Log($"TurnEnd P{player.Id}");
            ClockTracker.NotifyTurnFinished();
        }

        /// <summary>有人宣告結束輪次時啟動倒數。</summary>
        public void AnnounceRoundEnd()
        {
            _logger?.Log("AnnounceRoundEnd");
            ClockTracker.StartCountdown(_players.Count);
        }

        /// <summary>結束當前輪次並推進到下一輪。</summary>
        public void EndRound()
        {
            _logger?.Log("EndRound");
            Globals.EndRound(coreRevealed: false);
            foreach (var p in _players)
            {
                p.TacticHandBonus = 0;
                p.Deck.ShuffleFromDiscard();
                p.TokensPerTurn.Clear();
                p.ManaCurseColor = null;
                p.ManaCurseTriggered = false;
            }
            Clock.NextRound();
            _logger?.Log($"NextRound {Clock.RoundIndex} {Clock.DayPart} Day{Clock.DayIndex}");
        }

        /// <summary>檢查整個劇本是否應該結束。</summary>
        public bool CheckEndOfScenario() => Clock.ScenarioEnded(_maxDays);
    }
}

