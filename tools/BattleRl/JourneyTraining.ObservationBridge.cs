using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;

// Read-only projection for isolated A alignment gates. It never resets a journey,
// chooses a future encounter, or advances native rules. Not a complete solo schema.
public static class BaselineAObservation
{
    public sealed record Counters(int Decisions, int Turns, int Rounds,
        int ConsecutivePlanning, bool RoundWindow);

    public static double[] Capture(PveJourneySession journey, Counters counters) =>
        JourneyTraining.CaptureBaselineObservation(journey, counters);

    public static object SevenHeroEntryGate() => JourneyTraining.BaselineObservationGate();
}

static partial class JourneyTraining
{
    static void RequireBaselineObservationConfiguration()
    {
        if (!(NativePayments && NativeConversions && NativeEnergyTargets &&
              NativeSharedSkills && NativeShapeshift && NativeWorld))
            throw new InvalidOperationException("Set every baseline A native feature flag before loading this type");
        VariedBuilds = UnitBuilds = PartyBuilds = ArtifactBuilds = true;
        IncludeOriginalOpenings = true;
    }

    public static double[] CaptureBaselineObservation(PveJourneySession journey,
        BaselineAObservation.Counters counters)
    {
        RequireBaselineObservationConfiguration();
        if (journey == null || counters == null) throw new ArgumentNullException();
        if (counters.Decisions < 0 || counters.Turns < 0 || counters.Rounds < 0 ||
            counters.ConsecutivePlanning < 0) throw new ArgumentOutOfRangeException(nameof(counters));
        var values = new Env(journey, counters).Features();
        if (values.Length != 6314 || values.Any(x => !double.IsFinite(x)))
            throw new InvalidOperationException("Baseline numerical schema changed or produced nonfinite values");
        return values;
    }

    sealed partial class Env
    {
        readonly ActionContext ObservationOnlyContext;
        // No random source, generated inventory, task seed, reset or pending enemy.
        public Env(PveJourneySession journey, BaselineAObservation.Counters counters)
        {
            Journey = journey;
            PendingEnemies = Array.Empty<Monster>();
            Steps = counters.Decisions;
            Turns = counters.Turns;
            Rounds = counters.Rounds;
            PlanningRun = counters.ConsecutivePlanning;
            RoundWindow = counters.RoundWindow;
            // Before the first native turn there is no context. Read the actual
            // public supplies with empty turn-local effects; never install this
            // view in Journey or call BeginTurn (which would mutate the game).
            if (journey.Context == null)
                ObservationOnlyContext = new ActionContext {
                    DayPart = journey.Game.Clock.DayPart,
                    ManaSource = journey.Game.Globals.Mana,
                    SharedSkills = journey.Game.SharedSkills,
                    AdvActionSupply = journey.Game.Globals.AdvActions,
                    SpellSupply = journey.Game.Globals.Spells,
                    ArtifactSupply = journey.Game.Globals.Artifacts,
                    SkillSupply = journey.Game.Globals.Skills,
                    UnitOffer = journey.Game.Globals.Units.Offer
                };
        }
    }

    public static object BaselineObservationGate()
    {
        RequireBaselineObservationConfiguration();
        var entries = Enumerable.Range(0, 7).Select(seed =>
        {
            var original = new Env(seed);
            original.Apply(new Move("fight"));
            var expected = original.Features();
            var actual = CaptureBaselineObservation(original.Journey,
                new(original.Steps, original.Turns, original.Rounds,
                    original.PlanningRun, original.RoundWindow));
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException("Existing battle coordinates changed");
            return new { hero = original.Journey.Player.HeroId, width = actual.Length,
                maximum_difference = actual.Zip(expected, (a,b) => Math.Abs(a-b)).Max() };
        }).ToArray();
        return new { passed = true, entries,
            scope = "Seven actual battle entries; not full solo coverage or PPO acceptance" };
    }
}
