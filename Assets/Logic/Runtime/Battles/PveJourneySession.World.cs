using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using Monster = MK.Logic.Data.Monster;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        /// <summary>The same physical enemy supply survives every turn and world battle.</summary>
        public EnemyTokenSupply EnemySupply { get; }

        /// <summary>Normal paid entry into an unconquered fortified site. Site defenders
        /// and any provoked rampagers share one combat, but only defenders gain site bonuses.</summary>
        public void AssaultFortifiedSite(AxialCoord target)
        {
            RequireOutsideBattle();
            var ctx=Context;
            RepeatedEffectResolution.RequireCompleted(ctx);
            RequireSoloCombatCardObligation();
            if(ctx.TurnActionCommitted || ctx.RestedThisTurn || ctx.PeacefulMomentAction || ctx.TunnelAssaultActive
                || !Game.Map.Places.TryGetValue(target,out var site) || site.Conquered
                || site.Type!=PlaceType.Keep && site.Type!=PlaceType.MageTower && site.Type!=PlaceType.City
                || !ReferenceEquals(ctx.EnemySupply,EnemySupply))
                throw new InvalidOperationException("请选择可正式突袭的未征服城防地点");
            if(site.GuardNeedsReplacement)
            {
                if(site.Type==PlaceType.City || site.Defenders.Count!=0 || !EnemySupply.CanDraw(EnemyTokenSupply.GuardColor(site.Type)))
                    throw new InvalidOperationException("缺少实际替换守军");
            }
            else if(site.Defenders.Count==0 || site.Defenders.Any(e=>!EnemySupply.IsInUse(e))
                || site.Defenders.Where((e,i)=>site.Defenders.Take(i).Any(other=>ReferenceEquals(e,other))).Any())
                throw new InvalidOperationException("突袭须绑定实际守军标记");
            var rollback=DemoStateCheckpoint.Capture(this);
            try
            {
                var origin=Player.Position;
                if(!new MovementService().TryAssaultMoveUsingPool(Player,target,Game.Map,ctx))
                    throw new InvalidOperationException("无法支付或执行这次突袭移动");
                var provoked=ctx.RampagingBattle==null?Array.Empty<Monster>():ctx.Enemies.ToArray();
                if(site.GuardNeedsReplacement)
                {
                    site.Enemy=EnemySupply.Draw(EnemyTokenSupply.GuardColor(site.Type));
                    site.GuardNeedsReplacement=false;
                }
                // Reuse the physical defender/withdrawal ledger also used by tunnel assault.
                // Normal entry does not receive the spell's IgnoreSiteFortifications benefit.
                ctx.TunnelOrigin=origin;ctx.TunnelDestination=target;ctx.TunnelTargetSite=site;
                ctx.TunnelTargetPlayer=null;ctx.TunnelPreviousSiteIgnore=ctx.IgnoreSiteFortifications;
                ctx.TunnelAssaultActive=ctx.ReturnAfterRetreat=true;
                ctx.BeginTunnelDefenders(site.Defenders);
                ctx.CombatSite=site;ctx.FortifiedSite=true;
                ctx.TurnActionCommitted=true;ctx.MovementPool=ctx.InfluencePool=0;
                if(site.Type==PlaceType.Keep || site.Type==PlaceType.MageTower)
                    Player.Reputation=Math.Max(-7,Player.Reputation-1);
                Battle=new PveBattleSession(Player,site.Defenders.Concat(provoked),_cards,ctx,_artifacts,
                    canAct:()=>Game.HasActiveTurn&&!Game.IsEndingTurn&&ReferenceEquals(Context,ctx),spells:_spells);
            }
            catch { rollback.Restore();throw; }
        }

        public void FinishFortifiedAssault(bool withdraw) => FinishTunnelAssault(withdraw);

        private void ValidateWorldSpell(string id,BattleCardChoice choice,PveDeedSelection inputs)
        {
            if(id!="magic_004" || !choice.Enhanced)return;
            var path=inputs?.TravelPath;
            var target=path!=null && path.Length>0?path[path.Length-1]:Player.Position;
            if(Context.OtherPlayers.Any(p=>!ReferenceEquals(p,Player) && p.Position.Equals(target)))
                throw new InvalidOperationException("当前旅程只接入实际地点的PVE突袭，尚未接入玩家战斗");
            if(!Game.Map.Places.TryGetValue(target,out var site) || site.Conquered
                || site.Type!=PlaceType.Keep && site.Type!=PlaceType.MageTower && site.Type!=PlaceType.City)
                throw new InvalidOperationException("请选择尚未征服的实际城堡、法师塔或城市");
            if(!ReferenceEquals(Context.EnemySupply,EnemySupply))
                throw new InvalidOperationException("地道突袭必须使用旅程持续的实际敌人供给");
            if(site.GuardNeedsReplacement)
            {
                if(site.Type==PlaceType.City || site.Defenders.Count!=0
                    || !EnemySupply.CanDraw(EnemyTokenSupply.GuardColor(site.Type)))
                    throw new InvalidOperationException("当前实际守军供给不足或地点尚有守军，不能重新抽取");
            }
            else if(site.Defenders.Count==0 || site.Defenders.Any(m=>m==null || !EnemySupply.IsInUse(m))
                || site.Defenders.Where((m,i)=>site.Defenders.Take(i).Any(other=>ReferenceEquals(m,other))).Any())
                throw new InvalidOperationException("地点必须持有互不重复、来自实际供给的守军标记");
        }

        // The original spell has already paid, moved and committed the action. Attach only the
        // combat controller; BeginBattle would incorrectly try to start a second action.
        private void AttachTunnelBattle()
        {
            if(!Context.TunnelAssaultActive || !Context.InBattle || Battle!=null)return;
            var context=Context;
            Battle=new PveBattleSession(Player,context.TunnelDefenders,_cards,context,_artifacts,
                canAct:()=>Game.HasActiveTurn && !Game.IsEndingTurn && ReferenceEquals(Context,context),spells:_spells);
        }

        public void FinishTunnelAssault(bool withdraw)
        {
            RequireOutsideBattle();
            if(Battle==null || !ReferenceEquals(Battle.Context,Context) || Battle.Phase!=Phase.End)
                throw new InvalidOperationException("请先完成当前地道突袭的实际战斗");
            CardTravelActions.FinishTunnelAssault(Player,Context,withdraw);
        }

        public void SelectSiteSpellReward(SpellCard offered)
        {
            RequireOutsideBattle();
            int index=Game.Globals.Spells.Offer.FindIndex(c=>ReferenceEquals(c,offered));
            if(index<0 || !ReferenceEquals(Context.SpellSupply,Game.Globals.Spells))
                throw new InvalidOperationException("请选择当前实际法术供应中的奖励");
            FortifiedSiteActions.SelectSpellReward(Context,index);
        }
    }
}
