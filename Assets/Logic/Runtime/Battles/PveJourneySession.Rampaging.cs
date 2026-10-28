using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public PveBattleSession ChallengeRampaging(AxialCoord target)
        {
            RequireOutsideBattle();
            RequireSoloCombatCardObligation();
            RampagingEncounter.Challenge(Player, Game.Map, target, Context);
            AttachRampagingBattle();
            return Battle;
        }

        private void AttachRampagingBattle()
        {
            var encounter = Context.RampagingBattle;
            if (encounter == null) return;
            RequireSoloCombatCardObligation();
            var context = Context;
            Battle = new PveBattleSession(Player, context.Enemies, _cards, context, _artifacts,
                canAct: () => Game.HasActiveTurn && !Game.IsEndingTurn && ReferenceEquals(Context, context),
                spells: _spells, rampaging: encounter);
            context.MovementPool = context.InfluencePool = 0;
        }
    }
}
