using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using Monster = MK.Logic.Data.Monster;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandTargetMovementBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static Monster Enemy()=>CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_057");
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(bool flight)
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply(),MovementMap=new MapState(),DayPart=DayPart.Day};
            for(int q=0;q<4;q++)c.MovementMap.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,new[]{TerrainType.Plains});
            c.MovementMap.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,4,new[]{TerrainType.Plains});
            var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills(flight?"hero_skill_pool_005":"hero_skill_pool_001")[flight?6:5];c.SkillSupply.Offer.Add(s);
            return(p,c,h,s);
        }
        private static void MoveCard(PlayerState p,ActionContext c)
        {
            var a=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(a);
            CardPlayActions.Play(p,c,a,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==a.Id));
        }
        [Test] public void TwoIndependentForkChoicesKeepRealTargetBindingsThroughActualKills()
        {
            var(p,c,h,s)=Setup(false);var a=Enemy();var b=Enemy();var enemies=new[]{a,b};c.Enemies=enemies;c.InBattle=true;c.CombatPhase=Phase.Ranged;
            HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,new[]{a,b});Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            int count=c.CombatPower.Attacks.Count;Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(count));
            HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,new[]{a});Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(3));
            Assert.That(c.CombatPower.Attacks.Count(x=>ReferenceEquals(x.RequiredEnemy,a)),Is.EqualTo(2));
            foreach(var part in c.CombatPower.Attacks){Assert.That(part.Element,Is.EqualTo(Element.ColdFire));Assert.That(part.Type,Is.EqualTo(AttackType.Ranged));Assert.That(part.SourceSkill,Is.SameAs(s));Assert.That(part.Value,Is.EqualTo(1));}
            c.EnemyDefense.Choose(enemies,new[]{0},c,0,0);c.CombatPower.AddAttack(1,Element.Fire,AttackType.Ranged);
            var first=CardCombatActions.Attack(p,c,enemies,new[]{0},Phase.Ranged);Assert.That(first.RequiredPower,Is.EqualTo(3));Assert.That(first.Kills,Is.EqualTo(1));Assert.That(c.CombatPower.Attacks.Single().RequiredEnemy,Is.SameAs(b));
            c.CombatPower.AddAttack(2,Element.Fire,AttackType.Ranged);
            var second=CardCombatActions.Attack(p,c,enemies,new[]{1},Phase.Ranged);Assert.That(second.RequiredPower,Is.EqualTo(3));Assert.That(second.Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(a.Fame+b.Fame));
            Assert.That(p.Skills,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
        }
        [TestCase(false)] [TestCase(true)] public void FailedSecondForkPreservesFirstPartsAndCanRetryDifferentActualTarget(bool duplicate)
        {
            var(p,c,h,s)=Setup(false);var a=Enemy();var b=Enemy();c.Enemies=new[]{a,b};c.InBattle=true;c.CombatPhase=Phase.Melee;
            HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,new[]{a});var first=c.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,duplicate?new[]{b,b}:new[]{Enemy()}));
            Assert.That(c.CombatPower.Attacks.Single(),Is.SameAs(first));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,h)),Is.False);
            HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,new[]{b});Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.CombatPower.Attacks.Last().RequiredEnemy,Is.SameAs(b));
        }
        [Test] public void WrongForkPhaseCannotMoveHeadbandOrAppendContributions()
        {
            var(p,c,h,s)=Setup(false);var a=Enemy();c.Enemies=new[]{a};c.InBattle=true;c.CombatPhase=Phase.Block;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseForkedLightning(p,c,h,s,new[]{a}));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(c.CombatPower.Attacks,Is.Empty);
        }
        [TestCase(1,0)] [TestCase(2,2)] public void FlightBorrowsOnceAndSpendsActualOriginalMovePool(int distance,int cost)
        {
            var(p,c,h,s)=Setup(true);MoveCard(p,c);var target=new AxialCoord(distance,0);
            HeadbandBorrowResolution.UseFlight(p,c,h,s,target);
            Assert.That(p.Position,Is.EqualTo(target));Assert.That(c.MovementPool,Is.EqualTo(2-cost));Assert.That(c.MovedThisTurn,Is.True);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Skills,Is.Empty);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
            Assert.That(p.Deck.PlayedCards.Any(x=>ReferenceEquals(x,h)),Is.True);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlight(p,c,h,s,new AxialCoord(0,0)));
            Assert.That(p.Position,Is.EqualTo(target));Assert.That(c.TeleportRange,Is.Zero);Assert.That(c.IgnoreRampaging,Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void InvalidFlightPreservesActualPositionCardPoolAndUsage(int invalid)
        {
            var(p,c,h,s)=Setup(true);if(invalid!=0)MoveCard(p,c);var target=new AxialCoord(2,0);
            if(invalid==1)c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Mountain});
            if(invalid==2)c.TurnActionCommitted=true;if(invalid==3)c.InBattle=true;
            var others=invalid==4?new[]{new PlayerState(2,"other"){Position=target}}:Array.Empty<PlayerState>();int pool=c.MovementPool;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlight(p,c,h,s,target,others));
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(c.MovementPool,Is.EqualTo(pool));Assert.That(c.PendingHeadbandBorrow,Is.Null);
            Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(c.MovedThisTurn,Is.False);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void FlightDoesNotLeaveRampagingImmunityForNextOrdinaryMove()
        {
            var(p,c,h,s)=Setup(true);var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032");c.MovementMap.Places[new AxialCoord(0,1)]=new PlaceState(PlaceType.OrcRampaging){Enemy=enemy};MoveCard(p,c);
            HeadbandBorrowResolution.UseFlight(p,c,h,s,new AxialCoord(1,0));Assert.That(c.RampagingBattle,Is.Null);Assert.That(c.IgnoreRampaging,Is.False);
            Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(0,0),c.MovementMap,c),Is.True);
            Assert.That(c.RampagingBattle,Is.Not.Null);Assert.That(c.Enemies.Single(),Is.SameAs(enemy));Assert.That(c.MovementPool,Is.Zero);
        }
    }
}
