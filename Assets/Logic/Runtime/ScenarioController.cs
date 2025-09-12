using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 負責迭代整個劇本，直到天數上限或勝利條件達成。
    /// </summary>
    public sealed class ScenarioController
    {
        private readonly GameEngine _engine;
        private readonly int _maxDays;
        private readonly IGameLogger? _logger;

        public ScenarioController(GameEngine engine, int maxDays, IGameLogger? logger = null)
        {
            _engine = engine;
            _maxDays = maxDays;
            _logger = logger;
        }

        /// <summary>
        /// 以提供的戰術牌序列模擬劇本流程。
        /// 每輪會先呼叫 <see cref="GameEngine.StartNewRound"/>，
        /// 之後依序讓玩家行動直到 <see cref="RoundEndTracker.RoundEnded"/>。
        /// </summary>
        public void RunScenario(IList<IReadOnlyList<TacticCard>> tacticsPerRound)
        {
            int round = 0;
            while (!_engine.CheckEndOfScenario())
            {
                _logger?.Log($"ScenarioRoundStart {round + 1}");
                _engine.StartNewRound(tacticsPerRound[round]);
                while (!_engine.ClockTracker.RoundEnded)
                    _engine.RunOneTurn();
                _engine.EndRound();
                _logger?.Log($"ScenarioRoundEnd {round + 1}");
                round++;
                if (round >= tacticsPerRound.Count) break; // 測試用保護
            }
            _logger?.Log("ScenarioFinished");
        }
    }
}

