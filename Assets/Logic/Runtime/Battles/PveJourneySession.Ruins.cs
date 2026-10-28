using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public PveBattleSession EnterOriginalEnemyRuin()
        {
            RequireOutsideBattle();RepeatedEffectResolution.RequireCompleted(Context);
            RequireSoloCombatCardObligation();
            if(Context.TurnActionCommitted||Context.RestedThisTurn||Context.PeacefulMomentAction||Context.Ruin!=null
                ||!Game.Map.Places.TryGetValue(Player.Position,out var site)||site.Type!=PlaceType.AncientRuin
                ||site.RuinConquered||site.OriginalRuinToken?.Kind!=RuinTokenType.Enemy
                ||Game.Globals.Ruins==null||!Game.Globals.Ruins.IsInUse(site.OriginalRuinToken)
                ||!Game.Map.RevealedSites.Contains(site))throw new InvalidOperationException("Actual revealed enemy ruin required");
            if(site.Defenders.Count==0)
            {
                foreach(var group in site.OriginalRuinToken.Enemies.GroupBy(c=>c))
                    if(EnemySupply.DrawPile.Concat(EnemySupply.DiscardPile).Count(e=>e.TokenColor==group.Key)<group.Count())
                        throw new InvalidOperationException("Insufficient actual ruin enemies");
            }
            else if(site.Defenders.Any(e=>!EnemySupply.IsInUse(e)))throw new InvalidOperationException("Ruin survivor identity mismatch");
            var rollback=DemoStateCheckpoint.Capture(Player,Context,site,EnemySupply);
            try
            {
                if(site.Defenders.Count==0)
                {
                    var guards=site.OriginalRuinToken.Enemies.Select(EnemySupply.Draw).ToArray();
                    site.Enemy=guards[0];site.AdditionalGuards.AddRange(guards.Skip(1));
                }
                var context=Context;
                context.Ruin=new RuinActions(Player,context,site,EnemySupply,Game.Globals.Ruins);
                context.CombatSite=site;
                Battle=new PveBattleSession(Player,site.Defenders,_cards,context,_artifacts,
                    canAct:()=>Game.HasActiveTurn&&!Game.IsEndingTurn&&ReferenceEquals(Context,context),spells:_spells);
                context.MovementPool=context.InfluencePool=0;context.TurnActionCommitted=true;
                return Battle;
            }
            catch{rollback.Restore();throw;}
        }

        public void ActivateOriginalAltar()
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            if(Context.TurnActionCommitted||Context.RestedThisTurn||Context.PeacefulMomentAction
                ||!Game.Map.Places.TryGetValue(Player.Position,out var site)
                ||site.Type!=PlaceType.AncientRuin||site.RuinConquered
                ||site.OriginalRuinToken==null||site.OriginalRuinToken.Kind!=RuinTokenType.Altar
                ||Game.Globals.Ruins==null||!Game.Globals.Ruins.IsInUse(site.OriginalRuinToken)
                ||!Game.Map.RevealedSites.Contains(site))
                throw new InvalidOperationException("Requires a revealed, unconquered actual altar at the player's position");
            var token=site.OriginalRuinToken;
            var rollback=DemoStateCheckpoint.Capture(Player,Context,Game.Globals,site);
            try
            {
                if(!Player.Mana.TryPayExactColors(token.ManaCost,Context,Player))
                    throw new InvalidOperationException("Insufficient actual altar mana");
                Player.Fame+=token.Fame;
                site.RuinConquered=true;site.OwnerId=Player.Id;
                Game.Globals.Ruins.Discard(token);
                Context.MovementPool=Context.InfluencePool=0;
                Context.TurnActionCommitted=true;
            }
            catch { rollback.Restore(); throw; }
        }
    }
}
