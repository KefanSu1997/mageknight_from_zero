using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class TunnelTravelStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static AxialCoord At(int q) => new(q,0);
        private static (PlayerState p,ActionContext c,DeedCard s) Start(int distance=3)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Night,MovementPool=5,MovementMap=new MapState()};
            for(int i=0;i<=4;i++)c.MovementMap.Placed[At(i)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Forest});
            c.TravelPath.AddRange(Enumerable.Range(1,distance).Select(At));
            var s=new DeedCard("magic_004",CardType.Spell);p.Deck.Hand.Add(s);
            p.Mana.AddToken(ManaColor.Green,2);p.Mana.AddToken(ManaColor.Black,2);p.Mana.AddToken(ManaColor.Red,2);
            return(p,c,s);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard s,bool strong=false)
        {
            var d=CardJsonLoader.LoadSpells().Single(x=>x.Id==s.Id);
            CardPlayActions.Play(p,c,s,new ActionCardData(d.Id,d.Name,d.Set,d.ImagePath,d.EnImagePath,d.Top.Effect,d.Bottom.Effect,d.Top.ManaCost),strong);
        }
        private static void Site(ActionContext c,PlaceType type,bool intrinsic=false)
            => c.MovementMap.Places[At(3)]=new PlaceState(type){Enemy=new Monster("guard",5,4,Element.Physical,3,intrinsic?new[]{Ability.Fortified}:Array.Empty<Ability>())};

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void BaseActuallyMovesWithoutMovementCostAndCanContinueWalking(int distance)
        {
            var(p,c,s)=Start(distance);Play(p,c,s);
            Assert.That(p.Position.Q,Is.EqualTo(distance));Assert.That(c.MovementPool,Is.EqualTo(5));Assert.That(c.TravelPath,Is.Empty);
            Assert.That(c.TeleportRange,Is.Zero);Assert.That(c.IgnoreRampaging,Is.False);Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            Assert.That(new MovementService().TryMoveUsingPool(p,At(distance+1),c.MovementMap,c),Is.True);
            Assert.That(c.MovementPool,Is.Zero);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(s));
            c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(s));
        }

        [TestCase(false)] [TestCase(true)]
        public void BothModesCrossIntermediateMountainsAndEnemiesWithoutProvoking(bool strong)
        {
            var(p,c,s)=Start();c.MovementMap.Placed[At(1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Mountain});
            var enemy=new Monster("orc",3,9,Element.Physical,2,Array.Empty<Ability>());
            c.MovementMap.Places[At(2)]=new PlaceState(PlaceType.OrcRampaging){Enemy=enemy};
            if(strong)Site(c,PlaceType.Keep);Play(p,c,s,strong);
            Assert.That(p.Position.Q,Is.EqualTo(3));Assert.That(p.Wounds,Is.Zero);Assert.That(c.MovementMap.Places[At(2)].Enemy,Is.SameAs(enemy));
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(false,2)] [TestCase(false,3)] [TestCase(false,4)]
        [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)] [TestCase(true,3)] [TestCase(true,4)]
        public void InvalidPathIsAtomic(bool strong,int reason)
        {
            var(p,c,s)=Start();if(strong)Site(c,PlaceType.Keep);
            if(reason<2)c.MovementMap.Placed[At(2)]=new MapTile(TileSet.Countryside,2,new[]{reason==0?TerrainType.Swamp:TerrainType.Lake});
            if(reason==2)c.TravelPath.RemoveAt(0);
            if(reason==3)c.MovementMap.Placed.Remove(At(2));
            if(reason==4)c.TravelPath.Add(At(4));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,s,strong));
            Assert.That(p.Position.Q,Is.Zero);Assert.That(c.MovementPool,Is.EqualTo(5));Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));Assert.That(p.Deck.Hand.Single(),Is.SameAs(s));Assert.That(c.TunnelAssaultActive,Is.False);
        }

        [TestCase(PlaceType.Keep)] [TestCase(PlaceType.City)] [TestCase(PlaceType.MageTower)]
        public void BaseCannotLandOnDefendedFortifiedSite(PlaceType type)
        {
            var(p,c,s)=Start();Site(c,type);Assert.Throws<InvalidOperationException>(()=>Play(p,c,s));Assert.That(p.Position.Q,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void OccupiedPlayerSpaceDistinguishesTravelFromAttack(bool strong)
        {
            var(p,c,s)=Start();var other=new PlayerState{Position=At(3)};c.OtherPlayers.Add(other);
            if(!strong){Assert.Throws<InvalidOperationException>(()=>Play(p,c,s));Assert.That(p.Position.Q,Is.Zero);}
            else{Play(p,c,s,true);Assert.That(c.TunnelTargetPlayer,Is.SameAs(other));Assert.That(p.Position.Q,Is.EqualTo(3));Assert.That(c.TurnActionCommitted,Is.True);}
            // This verifies target routing only. Full player-vs-player combat remains a separate acceptance scope.
        }

        [TestCase(PlaceType.Keep,false)] [TestCase(PlaceType.Keep,true)] [TestCase(PlaceType.City,false)] [TestCase(PlaceType.MageTower,false)]
        public void RealAssaultIgnoresOnlySiteFortificationAndWithdrawsToOrigin(PlaceType type,bool intrinsic)
        {
            var(p,c,s)=Start();Site(c,type,intrinsic);Play(p,c,s,true);
            Assert.That(c.IgnoreSiteFortifications,Is.True);Assert.That(c.IgnoreFortified,Is.False);Assert.That(c.TurnActionCommitted,Is.True);
            Assert.That(new MovementService().TryMoveUsingPool(p,At(4),c.MovementMap,c),Is.False);
            Assert.Throws<InvalidOperationException>(()=>new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c));
            Assert.Throws<InvalidOperationException>(()=>CardTravelActions.FinishTunnelAssault(p,c,true));
            var g=new GameState();g.Players.Add(p);var t=new TurnMachine(g);t.BeginBattle(c);
            var fire=new DeedCard("magic_009",CardType.Spell);p.Deck.Hand.Add(fire);Play(p,c,fire);
            if(intrinsic)Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged));
            else Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged).Kills,Is.EqualTo(1));
            t.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);t.SetBlock(0,Element.Physical);t.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);t.FinishBattle(c);
            if(intrinsic)Assert.Throws<InvalidOperationException>(()=>CardTravelActions.FinishTunnelAssault(p,c,false));
            CardTravelActions.FinishTunnelAssault(p,c,intrinsic);
            Assert.That(p.Position.Q,Is.EqualTo(intrinsic?0:3));Assert.That(c.IgnoreSiteFortifications,Is.False);Assert.That(c.ReturnAfterRetreat,Is.False);
            Assert.That(c.TunnelAssaultActive,Is.False);Assert.That(p.Fame,Is.EqualTo(intrinsic?0:3));
            Assert.That(p.Wounds,Is.EqualTo(intrinsic?2:0));
            Assert.That(new MovementService().TryMoveUsingPool(p,At(intrinsic?1:4),c.MovementMap,c),Is.False);
            if(type==PlaceType.MageTower)
            {
                c.SpellSupply=new SpellSupply();c.SpellSupply.SetDeck(new[]{new SpellCard("magic_009")});c.SpellSupply.Refill();
                FortifiedSiteActions.SelectSpellReward(c,0);
            }
            c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
        }

        private static (PlayerState p,ActionContext c,PlaceState site) ResolveSite(PlaceType type,bool win=true,bool compact=false)
        {
            var(p,c,s)=Start();Site(c,type,!win);var site=c.MovementMap.Places[At(3)];
            c.SpellSupply=new SpellSupply();c.SpellSupply.SetDeck(new[]{"magic_009","magic_015","magic_005","magic_002"}.Select(id=>new SpellCard(id)));c.SpellSupply.Refill();
            Play(p,c,s,true);var game=new GameState();game.Players.Add(p);var turn=new TurnMachine(game);turn.BeginBattle(c);
            if(win)
            {
                var fire=new DeedCard("magic_009",CardType.Spell);p.Deck.Hand.Add(fire);Play(p,c,fire);
                if(compact)
                    turn.ApplyRanged(c.Enemies,new[]{new AttackAllocation(new[]{0},5,Element.Fire)},c);
                else CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged);
            }
            if(!compact)turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);
            turn.SetBlock(0,Element.Physical);turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);turn.FinishBattle(c);
            return(p,c,site);
        }

        [TestCase(PlaceType.Keep)] [TestCase(PlaceType.City)] [TestCase(PlaceType.MageTower)]
        public void VictoryUpdatesActualSiteOnceAndOnlyAwardsPrintedEnemyFame(PlaceType type)
        {
            var(p,c,site)=ResolveSite(type);CardTravelActions.FinishTunnelAssault(p,c,false);
            Assert.That(site.Conquered,Is.True);Assert.That(site.Enemy,Is.Null);Assert.That(site.OwnerId,Is.EqualTo(p.Id));
            Assert.That(p.Fame,Is.EqualTo(3));Assert.That(p.Reputation,Is.EqualTo(type==PlaceType.City?0:-1));
            Assert.That(site.CityShields.Count,Is.EqualTo(type==PlaceType.City?1:0));
            Assert.That(c.PendingSpellReward,Is.EqualTo(type==PlaceType.MageTower));
            Assert.Throws<InvalidOperationException>(()=>CardTravelActions.FinishTunnelAssault(p,c,false));
            Assert.That(p.Fame,Is.EqualTo(3));Assert.That(site.CityShields.Count,Is.EqualTo(type==PlaceType.City?1:0));
        }

        [TestCase(PlaceType.Keep)] [TestCase(PlaceType.City)] [TestCase(PlaceType.MageTower)]
        public void DefeatKeepsSiteHostileAndReplacesOnlyKeepOrTowerGuards(PlaceType type)
        {
            var(p,c,site)=ResolveSite(type,false);var guard=site.Enemy;
            CardTravelActions.FinishTunnelAssault(p,c,true);
            Assert.That(p.Position.Q,Is.Zero);Assert.That(site.OwnerId,Is.Null);Assert.That(site.Conquered,Is.False);
            Assert.That(site.GuardNeedsReplacement,Is.EqualTo(type!=PlaceType.City));
            if(type==PlaceType.City)Assert.That(site.Enemy,Is.SameAs(guard));else Assert.That(site.Enemy,Is.Null);
            Assert.That(CardTravelActions.IsSafeDestination(p,c,At(3)),Is.False);
            Assert.That(p.Reputation,Is.EqualTo(type==PlaceType.City?0:-1));Assert.That(c.PendingSpellReward,Is.False);
            Assert.That(FortifiedSiteActions.HandBonus(p,c.MovementMap),Is.Zero);
        }

        [Test] public void RangedCompactionStillRecordsTheActualWorldGuardDefeat()
        {
            var(p,c,site)=ResolveSite(PlaceType.Keep,true,true);Assert.That(c.Enemies,Is.Empty);
            CardTravelActions.FinishTunnelAssault(p,c,false);Assert.That(site.Conquered,Is.True);Assert.That(site.OwnerId,Is.EqualTo(p.Id));
        }

        [Test] public void ReplacedWorldGuardCannotReceiveAnotherGuardsVictory()
        {
            var(p,c,site)=ResolveSite(PlaceType.Keep);site.Enemy=new Monster("other",5,4,Element.Physical,3,Array.Empty<Ability>());
            Assert.Throws<InvalidOperationException>(()=>CardTravelActions.FinishTunnelAssault(p,c,false));
            Assert.That(site.OwnerId,Is.Null);Assert.That(site.Conquered,Is.False);Assert.That(c.TunnelAssaultActive,Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void MeleeOrDirectDestroyAlsoUpdatesActualSite(bool destroy)
        {
            var(p,c,s)=Start();Site(c,PlaceType.Keep);Play(p,c,s,true);
            var game=new GameState();game.Players.Add(p);var turn=new TurnMachine(game);turn.BeginBattle(c);
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);
            var attack=new DeedCard(destroy?"magic_007":"magic_009",CardType.Spell);p.Deck.Hand.Add(attack);
            Play(p,c,attack,destroy);
            if(!destroy)turn.Attack(new[]{0},c);
            turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,false);
            Assert.That(c.MovementMap.Places[At(3)].Conquered,Is.True);Assert.That(p.Fame,Is.EqualTo(3));
        }

        [TestCase(false)] [TestCase(true)]
        public void TowerSpellIsSelectedFromRealOfferAndOnlyGrantedAtEndTurn(bool refill)
        {
            var(p,c,site)=ResolveSite(PlaceType.MageTower);CardTravelActions.FinishTunnelAssault(p,c,false);
            var untouched=c.SpellSupply.Offer[0];var chosen=c.SpellSupply.Offer[1];
            Assert.Throws<InvalidOperationException>(()=>FortifiedSiteActions.SelectSpellReward(c,3));
            int played=p.Deck.PlayedCards.Count;
            Assert.Throws<InvalidOperationException>(()=>new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(played));Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            FortifiedSiteActions.SelectSpellReward(c,1);
            Assert.That(p.Deck.DrawPileCount,Is.Zero);Assert.That(c.SpellSupply.Offer[1],Is.SameAs(chosen));
            c.SkipDraw=!refill;new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That((refill?p.Deck.Hand:p.Deck.DrawPile).Single().Id,Is.EqualTo("magic_015"));
            Assert.That(c.SpellSupply.Offer.Select(x=>x.Id),Is.EqualTo(new[]{"magic_009","magic_005","magic_002"}));
            Assert.That(c.SpellSupply.Offer[0],Is.SameAs(untouched));Assert.That(c.SpellSupply.DiscardPile,Is.Empty);
            Assert.That(c.PendingSpellReward,Is.False);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>FortifiedSiteActions.SelectSpellReward(c,0));
        }

        [Test] public void StaleRewardSelectionRejectsBeforeAnyEndTurnMutation()
        {
            var(p,c,site)=ResolveSite(PlaceType.MageTower);CardTravelActions.FinishTunnelAssault(p,c,false);
            FortifiedSiteActions.SelectSpellReward(c,1);c.SpellSupply.Claim(1);
            Assert.Throws<InvalidOperationException>(()=>new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2));Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
        }

        [TestCase(PlaceType.Keep,6)] [TestCase(PlaceType.City,7)]
        public void ConqueredLocationActuallyIncreasesEndTurnRefill(PlaceType type,int limit)
        {
            var(p,c,site)=ResolveSite(type);CardTravelActions.FinishTunnelAssault(p,c,false);
            for(int i=0;i<10;i++)p.Deck.PutOnTop(new DeedCard("basic_card_014",CardType.Action));
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);Assert.That(p.Deck.Hand.Count,Is.EqualTo(limit));
        }

        [Test] public void NearbyKeepAndCityBonusesUseHigherAndExpireOutOfRange()
        {
            var(p,c,site)=ResolveSite(PlaceType.Keep);CardTravelActions.FinishTunnelAssault(p,c,false);
            c.MovementMap.Places[At(0)]=new PlaceState(PlaceType.Keep){OwnerId=p.Id};
            var city=new PlaceState(PlaceType.City){OwnerId=p.Id};city.CityShields.Add(p.Id);c.MovementMap.Places[At(4)]=city;
            Assert.That(FortifiedSiteActions.HandBonus(p,c.MovementMap),Is.EqualTo(2));
            p.Position=At(7);Assert.That(FortifiedSiteActions.HandBonus(p,c.MovementMap),Is.Zero);
        }

        [TestCase(false,0)] [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        public void FeesAreAtomicAndCrystalPaymentUsesOriginalCosts(bool strong,int mode)
        {
            var(p,c,s)=Start();if(strong)Site(c,PlaceType.Keep);
            p.Mana.Tokens[ManaColor.Green]=0;if(mode==1||mode==3)p.Mana.AddCrystal(ManaColor.Green);
            if(mode==1)p.Mana.Tokens[ManaColor.Black]=0;
            if(mode==2){c.DayPart=DayPart.Day;p.Mana.Tokens[ManaColor.Green]=2;}
            if(mode==3){Play(p,c,s,strong);Assert.That(p.Position.Q,Is.EqualTo(3));Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);}
            else{Assert.Throws<InvalidOperationException>(()=>Play(p,c,s,strong));Assert.That(p.Position.Q,Is.Zero);Assert.That(p.Deck.Hand.Single(),Is.SameAs(s));Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(mode==1?1:0));}
        }
    }
}
