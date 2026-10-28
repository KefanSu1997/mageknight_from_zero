using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public PveBattleSession EnterDungeon(Random rewardRandom = null)
            => EnterUndergroundSite(PlaceType.Dungeon, EnemyTokenColor.Brown, rewardRandom);

        public PveBattleSession EnterTomb()
            => EnterUndergroundSite(PlaceType.Tomb, EnemyTokenColor.Red, null);
        public PveBattleSession BurnMonastery()
            => EnterUndergroundSite(PlaceType.Monastery, EnemyTokenColor.Purple, null);

        private PveBattleSession EnterUndergroundSite(PlaceType requiredType, EnemyTokenColor tokenColor, Random rewardRandom)
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            RequireSoloCombatCardObligation();
            if (Context.TurnActionCommitted || Context.RestedThisTurn || Context.PeacefulMomentAction
                || !Game.Map.Places.TryGetValue(Player.Position, out var site) || site.Type != requiredType
                || Context.Dungeon != null || requiredType==PlaceType.Monastery&&site.MonasteryBurned)
                throw new InvalidOperationException("须在实际地下城格且本回合尚可执行行动");
            if (!ReferenceEquals(Context.EnemySupply, EnemySupply) || !EnemySupply.CanDraw(tokenColor))
                throw new InvalidOperationException("冒险地点须从旅程实际供给抽取对应颜色敌人");
            // Every failure above precedes drawing; this new encounter may reuse a recycled token.
            var enemy = EnemySupply.Draw(tokenColor);
            var context = Context;
            var dungeon = new DungeonActions(Player, context, site, enemy, EnemySupply, rewardRandom);
            context.Dungeon = dungeon;
            if(requiredType!=PlaceType.Monastery)context.DayPart = DayPart.Night;
            else Player.Reputation-=3;
            context.UnitsAllowed = false;
            context.CombatSite = site;
            Battle = new PveBattleSession(Player, new[] { enemy }, _cards, context, _artifacts,
                canAct: () => Game.HasActiveTurn && !Game.IsEndingTurn && ReferenceEquals(Context, context), spells: _spells);
            context.MovementPool = context.InfluencePool = 0;
            context.TurnActionCommitted = true;
            return Battle;
        }

        public IReadOnlyList<ArtifactCard> RevealDungeonArtifactReward()
        {
            RequireOutsideBattle();
            return (Context.Dungeon ?? throw new InvalidOperationException("没有实际地下城遭遇")).RevealArtifacts();
        }

        public void SelectDungeonArtifactReward(ArtifactCard artifact)
        {
            RequireOutsideBattle();
            (Context.Dungeon ?? throw new InvalidOperationException("没有实际地下城遭遇")).SelectArtifact(artifact);
        }
    }
}
