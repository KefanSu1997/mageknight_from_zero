using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// Solo p15/p19 initiative and one-use tactics around the actual GameEngine.
    /// Dummy callback must advance its saved physical deck and return true ONLY
    /// for an empty deck at the beginning of its turn. It is never a policy action.
    /// </summary>
    public sealed class SoloTacticRound
    {
        private readonly GameEngine _game;
        private readonly Func<bool> _dummyTurn;
        private readonly Random _random;
        private readonly HashSet<string> _removed = new();
        private bool _started;
        private bool _playerTurnOpened;
        private bool _dummyFinalTurn;
        private bool _cityFinalTurn;
        public bool ScenarioEnded { get; private set; }
        public bool PlayerAnnouncedThisRound { get; private set; }
        public bool CityFinalTurnPending => _cityFinalTurn && !ScenarioEnded;
        public bool DummyDue { get; private set; }
        public TacticCard DummyTactic { get; private set; }
        public TacticCard PlayerTactic { get; private set; }
        public IReadOnlyCollection<string> RemovedTactics => _removed.ToArray();

        public SoloTacticRound(GameEngine game, Func<bool> dummyTurn, Random random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _dummyTurn = dummyTurn ?? throw new ArgumentNullException(nameof(dummyTurn));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public IReadOnlyList<TacticCard> AvailableTactics() => OriginalTactics.All
            .Select(t => t.Card).Where(t => t.Part == _game.Clock.DayPart && !_removed.Contains(t.Id)).ToArray();

        public void Start(TacticCard playerPick)
        {
            if (_started || ScenarioEnded || _game.CheckEndOfScenario() || _game.HasActiveTurn)
                throw new InvalidOperationException("No new solo round can start now.");
            var available = AvailableTactics();
            if (!available.Contains(playerPick)) throw new InvalidOperationException("Tactic is unavailable or already removed.");
            var remaining = available.Where(t => t.Id != playerPick.Id).ToArray();
            if (remaining.Length == 0) throw new InvalidOperationException("No unused dummy tactic remains.");
            // Select no dummy benefit: its card supplies initiative only.
            _game.StartNewRound(new[] { playerPick });
            PlayerTactic = playerPick;
            DummyTactic = remaining[_random.Next(remaining.Length)];
            DummyDue = DummyTactic.Number < PlayerTactic.Number;
            PlayerAnnouncedThisRound = false;
            _started = true;
        }

        public void AdvanceDummy()
        {
            if (!_started || ScenarioEnded || _cityFinalTurn || !DummyDue || _playerTurnOpened || _game.HasActiveTurn
                || _game.HasPendingTacticSelection || (_game.ClockTracker.CountdownStarted && !_dummyFinalTurn))
                throw new InvalidOperationException("It is not the dummy's scheduled turn.");
            bool empty = _dummyTurn();
            if (_dummyFinalTurn)
            {
                // A second declaration cannot grant the human another turn.
                _game.ClockTracker.NotifyTurnFinished();
                _dummyFinalTurn = false;
            }
            else if (empty) _game.AnnounceSoloDummyRoundEnd();
            DummyDue = false;
        }

        public bool CanAnnounceRoundEnd => _started && !ScenarioEnded && !_cityFinalTurn && !DummyDue && !_playerTurnOpened
            && _game.CanAnnounceRoundEnd;

        public void AnnounceRoundEnd()
        {
            if (!CanAnnounceRoundEnd)
                throw new InvalidOperationException("Only the scheduled player with an empty draw pile can declare round end.");
            _game.AnnounceSoloPlayerRoundEnd();
            PlayerAnnouncedThisRound = true;
            _dummyFinalTurn = true;
            DummyDue = true;
        }

        public PlayerState BeginPlayerTurn(ActionContext context)
        {
            if (!_started || ScenarioEnded || DummyDue || _playerTurnOpened) throw new InvalidOperationException("It is not the player's turn.");
            var player = _game.BeginNextTurn(context);
            _playerTurnOpened = true;
            return player;
        }

        public bool CanSkipEmptyFinalTurn => _started && !ScenarioEnded && !DummyDue && !_playerTurnOpened
            && (_game.CanSkipEmptyTurn || _cityFinalTurn && _game.NextPlayerHasNoCards);

        /// <summary>Consumes the native final-turn countdown without opening an impossible empty turn.</summary>
        public void SkipEmptyFinalTurn()
        {
            if (!CanSkipEmptyFinalTurn)
                throw new InvalidOperationException("Only the scheduled empty final player turn can be skipped.");
            if (_game.CanSkipEmptyTurn) _game.SkipEmptyTurn();
            if (_cityFinalTurn) ScenarioEnded = true;
            DummyDue = false;
        }

        /// <summary>Also supports the native split end-turn flow with intervening reward selections.</summary>
        public void ObserveCompletedPlayerTurn()
        {
            if (!_playerTurnOpened || _game.HasActiveTurn || _game.IsEndingTurn)
                throw new InvalidOperationException("The actual native player turn has not completed.");
            _playerTurnOpened = false;
            if (_cityFinalTurn)
            {
                ScenarioEnded = true;
                DummyDue = false;
                return;
            }
            if (_game.SoloCitiesConquered)
            {
                // The just-completed conquest turn is NOT the final extra turn.
                // An already-expired round takes precedence; never refresh another round.
                _cityFinalTurn = true;
                ScenarioEnded = _game.ClockTracker.RoundEnded;
                DummyDue = false;
                return;
            }
            DummyDue = !_game.ClockTracker.CountdownStarted && !_game.HasImmediateExtraTurn;
        }

        public void EndPlayerTurn(ActionContext context, IEnumerable<DeedCard> discard = null, SiteTurnSelection site = null)
        {
            if (!_playerTurnOpened) throw new InvalidOperationException("No player turn was opened.");
            _game.EndCurrentTurn(context, discard, site);
            ObserveCompletedPlayerTurn();
        }

        public void EndRound() => FinishRound(_game.EndRound);
        public void EndRound(SoloDummyDeck dummy, Func<string, MK.Logic.Core.ManaColor> printedColor)
            => FinishRound(() => _game.EndSoloRound(dummy, printedColor));
        public void EndRound(SoloDummyDeck dummy, Func<string, MK.Logic.Core.ManaColor[]> printedColors)
            => FinishRound(() => _game.EndSoloRound(dummy, printedColors));

        private void FinishRound(Action endNativeRound)
        {
            if (!_started || ScenarioEnded || _playerTurnOpened || !_game.ClockTracker.RoundEnded)
                throw new InvalidOperationException("The native final-turn countdown must finish first.");
            endNativeRound();
            _removed.Add(PlayerTactic.Id);
            _removed.Add(DummyTactic.Id);
            DummyDue = false;
            _started = false;
            ScenarioEnded = _game.CheckEndOfScenario();
        }
    }
}
