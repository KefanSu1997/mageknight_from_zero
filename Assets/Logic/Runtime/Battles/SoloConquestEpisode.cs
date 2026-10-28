using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloConquestTransition(int Index, double Reward, SoloConquestScore Score,
        int Round, int CompletedTurns, int CompletedBattles, bool Terminal);
    public sealed record SoloPolicyObservation(int Version,int Step,bool Terminal,SoloPublicWorldState World,
        SoloPublicInventory Inventory,SoloPublicCombat Combat,SoloPublicOffers Offers);

    /// <summary>
    /// Full-scenario transaction boundary for the policy adapter. The dispatcher must supply
    /// an actual native command with complete parameters. No battle count or action budget
    /// ends a scenario. This does not enumerate legal commands or serialize durable saves.
    /// </summary>
    public sealed class SoloConquestEpisode
    {
        public PveJourneySession Journey { get; }
        public SoloConquestTileSupply Tiles { get; }
        private readonly HashSet<string> _advancedIds;
        private readonly SoloScoreRewardLedger _rewards;
        internal Random AdventureRandom { get; }
        private readonly Random _effectRandom;
        public int Steps { get; private set; }
        public bool Terminal => _rewards.Closed;
        public int InitialScore => _rewards.InitialScore;
        public double RewardScale => _rewards.Scale;
        // Audit-only accounting; do not append hidden-zone inventory to policy inputs.
        public SoloConquestScore CaptureAuditScore() => Journey.CaptureSoloScore(_advancedIds);

        // Typed allow-list only. Pending special effects still need explicit projection before
        // the full training adapter is admitted; never substitute serialization of Journey.
        public SoloPolicyObservation Observe() => new(1,Steps,Terminal,Journey.ObserveSoloWorld(Tiles),
            Journey.ObserveSoloInventory(),Journey.ObserveSoloCombat(),Journey.ObserveSoloOffers());

        public SoloConquestEpisode(PveJourneySession journey, SoloConquestTileSupply tiles,
            IEnumerable<string> advancedActionIds,Random adventureRandom=null,Random effectRandom=null)
        {
            if(journey==null || tiles==null || advancedActionIds==null)throw new ArgumentNullException();
            if(!journey.IsSoloConquest || journey.SoloClock==null || journey.SoloClock.ScenarioEnded
                || journey.Game.Clock.RoundIndex!=1 || journey.CompletedTurns!=0 || journey.Player.Tactic!=null
                || journey.Game.HasActiveTurn || journey.Game.Map.OriginalCells.Count!=21
                || !journey.Game.Map.OriginalTiles.ContainsKey("A") || tiles.Capture().Revealed.Length!=2)
                throw new InvalidOperationException("Episode needs the prepared original solo opening and attached dummy clock.");
            if(!tiles.Capture().Revealed.OrderBy(x=>x).SequenceEqual(
                journey.Game.Map.OriginalTiles.Keys.Where(x=>x!="A").OrderBy(x=>x)))
                throw new InvalidOperationException("Map and tile supply do not describe the same opening.");
            Journey=journey;Tiles=tiles;_advancedIds=new HashSet<string>(advancedActionIds);
            AdventureRandom=adventureRandom??new Random(0);
            _effectRandom=effectRandom??new Random(0);
            if(Journey.Context!=null)Journey.Context.EffectRandom=_effectRandom;
            _rewards=new SoloScoreRewardLedger(Journey.CaptureSoloScore(_advancedIds));
        }

        public SoloConquestTransition Apply(Action<PveJourneySession> nativeCommand)
        {
            if(Terminal)throw new InvalidOperationException("Scenario already ended.");
            if(nativeCommand==null)throw new ArgumentNullException(nameof(nativeCommand));
            var checkpoint=DemoStateCheckpoint.Capture(this);
            try
            {
                nativeCommand(Journey);
                // BeginTurn replaces Context. All subsequent random card effects must use
                // the episode-owned stream included in native checkpoints and seed replay.
                if(Journey.Context!=null)Journey.Context.EffectRandom=_effectRandom;
                // Dummy has no policy choice. Pending tactic input must be resolved before it acts.
                if(Journey.SoloClock.DummyDue && !Journey.Game.HasPendingTacticSelection)
                    Journey.SoloClock.AdvanceDummy();
                if(Journey.SoloClock.CanSkipEmptyFinalTurn)Journey.SoloClock.SkipEmptyFinalTurn();
                var score=Journey.CaptureSoloScore(_advancedIds);
                double reward=_rewards.Advance(score);
                Steps++;
                return new SoloConquestTransition(Steps,reward,score,Math.Min(Journey.Game.Clock.RoundIndex,6),
                    Journey.CompletedTurns,Journey.CompletedBattles,Terminal);
            }
            catch
            {
                checkpoint.Restore();
                throw;
            }
        }
    }
}
