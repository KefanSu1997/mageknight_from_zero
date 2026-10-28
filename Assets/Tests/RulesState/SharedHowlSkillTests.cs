using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SharedHowlSkillTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private (GameEngine g,PlayerState a,PlayerState b,ActionContext c,SkillCard s) Start()
        {
            var a=new PlayerState(1,"Wolfhawk"){HeroId="Wolfhawk",Fame=15};var b=new PlayerState(2,"Tovak"){HeroId="Tovak",Fame=3};
            foreach(var p in new[]{a,b})p.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[9];a.Skills.Add(s);
            var g=new GameEngine(new[]{a,b},3);g.StartNewRound(new[]{OriginalTactics.Get("day_1").Card,OriginalTactics.Get("day_4").Card});
            var c=new ActionContext();Assert.That(g.BeginNextTurn(c),Is.SameAs(a));return(g,a,b,c,s);
        }
        private Monster Enemy()=>CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
        private PveBattleSession Battle(PlayerState p,ActionContext c,params Monster[] enemies)
            =>new PveBattleSession(p,enemies,CardJsonLoader.LoadBasicActions(),c);

        [Test] public void ActualSidewaysValueCountsEmptyCommandsAndSuccessfulCardPublishesOriginalSkill()
        {
            var(g,a,b,c,s)=Start();var card=a.Deck.Hand[0];var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));a.Units.Add(unit);unit.Exhaust();
            Assert.That(a.FreeSlots,Is.EqualTo(1));var token=g.SharedSkills.UseHowl(a,c,s,card,SidewaysType.Move);
            Assert.That(c.MovementPool,Is.EqualTo(5));Assert.That(a.Deck.PlayedCards.Single(),Is.SameAs(card));
            Assert.That(token.Owner,Is.SameAs(a));Assert.That(token.Skill,Is.SameAs(s));Assert.That(SkillActions.IsUsed(a,s),Is.True);
            Assert.That(g.SharedSkills.PendingHowls(c),Is.Empty);
        }
        [Test] public void EmptyLoyaltyCountsButFlagCommandDoesNotSpendAnotherNormalToken()
        {
            var(g,a,b,c,s)=Start();HeroSkillAcquisition.Acquire(a,CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[7],units:new UnitSupply());
            var banner=new DeedCard("items_017",CardType.Artifact);a.Deck.Hand.Add(banner);
            CardPlayActions.PlayArtifact(a,c,banner,CardJsonLoader.LoadItems().Single(i=>i.Id==banner.Id));BannerActions.SelectCommandForRecruit(a,c,banner);
            c.UnitOffer=new[]{CardJsonLoader.LoadUnit("army_2_006")}.ToList();new MK.Logic.Runtime.CardEffects.BattleSummonsEffect(true).Execute(a,c);
            Assert.That(a.FreeSlots,Is.EqualTo(3));Assert.That(a.BannerCommandCount,Is.EqualTo(1));
            g.SharedSkills.UseHowl(a,c,s,a.Deck.Hand[0],SidewaysType.Move);Assert.That(c.MovementPool,Is.EqualTo(7));
        }
        [Test] public void NegotiationForeignCardAndIllegalPhaseAreAtomic()
        {
            var(g,a,b,c,s)=Start();var card=a.Deck.Hand[0];c.InNegotiation=true;
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UseHowl(a,c,s,card,SidewaysType.Move));c.InNegotiation=false;
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UseHowl(a,c,s,new DeedCard(card.Id,card.Type),SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UseHowl(a,c,s,card,SidewaysType.Block));
            Assert.That(a.Deck.Hand.Contains(card),Is.True);Assert.That(g.SharedSkills.Tokens,Is.Empty);Assert.That(SkillActions.IsUsed(a,s),Is.False);
            Assert.That(c.MovementPool+c.BlockPool,Is.Zero);
        }
        [Test] public void FirstActualOtherPlayerBattleForcesReturnBlocksPlayAndResolvesIndependentTargets()
        {
            var(g,a,b,c,s)=Start();var token=g.SharedSkills.UseHowl(a,c,s,a.Deck.Hand[0],SidewaysType.Move);
            g.EndCurrentTurn(c);var next=new ActionContext();Assert.That(g.BeginNextTurn(next),Is.SameAs(b));
            var first=Enemy();var second=first with{};var battle=Battle(b,next,first,second);
            Assert.That(g.SharedSkills.Tokens,Is.Empty);Assert.That(g.SharedSkills.PendingHowls(next).Single(),Is.SameAs(token));
            Assert.Throws<InvalidOperationException>(()=>battle.Advance());
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(b,next,b.Deck.Hand[0],CardJsonLoader.LoadBasicActions().Single(x=>x.Id==b.Deck.Hand[0].Id)));
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ResolveHowl(b,next,first,second with{}));
            Assert.That(CombatMath.EffectiveArmor(first,0,next),Is.EqualTo(first.Armor));Assert.That(g.SharedSkills.PendingHowls(next).Count,Is.EqualTo(1));
            g.SharedSkills.ResolveHowl(b,next,first,second);Assert.That(g.SharedSkills.PendingHowls(next),Is.Empty);
            Assert.That(CombatMath.EffectiveArmor(first,0,next),Is.EqualTo(Math.Max(1,first.Armor-1)));
            Assert.That(CardCombatActions.DefenseProfile(first,0,next).Attack,Is.EqualTo(first.Attack));
            Assert.That(CardCombatActions.DefenseProfile(second,1,next).Attack,Is.EqualTo(second.Attack-1));
            battle.Advance();battle.Advance();battle.Advance();next.CombatPower.AddAttack(first.Armor-1);
            Assert.That(battle.Attack(0).Kills,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(a,s),Is.True);
        }
        [Test] public void OwnerIsEligibleAndBattlePublicationDoesNotRetroactivelyTrigger()
        {
            var(g,a,b,c,s)=Start();var first=Battle(a,c,Enemy());first.Advance();
            var token=g.SharedSkills.UseHowl(a,c,s,a.Deck.Hand[0],SidewaysType.Block);
            Assert.That(c.BlockPool,Is.EqualTo(6));Assert.That(g.SharedSkills.PendingHowls(c),Is.Empty);Assert.That(g.SharedSkills.Tokens.Single(),Is.SameAs(token));
            first.Block(0);first.Advance();first.Advance();first.Advance();
            var second=Battle(a,c,Enemy());Assert.That(g.SharedSkills.PendingHowls(c).Single(),Is.SameAs(token));
            g.SharedSkills.ResolveHowl(a,c,second.Enemies[0],second.Enemies[0]);Assert.That(g.SharedSkills.Tokens,Is.Empty);
        }
        [Test] public void MultiplePhysicalTokensReturnAndResolveInPublicationOrder()
        {
            var(g,a,b,c,s)=Start();var first=g.SharedSkills.UseHowl(a,c,s,a.Deck.Hand[0],SidewaysType.Move);g.EndCurrentTurn(c);
            var next=new ActionContext();g.BeginNextTurn(next);
            var otherSkill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[9];HeroSkillAcquisition.Acquire(b,otherSkill);
            var second=g.SharedSkills.UseHowl(b,next,otherSkill,b.Deck.Hand[0],SidewaysType.Move);g.EndCurrentTurn(next);
            var again=new ActionContext();Assert.That(g.BeginNextTurn(again),Is.SameAs(a));var e=Enemy();var battle=Battle(a,again,e);
            Assert.That(g.SharedSkills.PendingHowls(again),Is.EqualTo(new[]{first,second}));Assert.That(g.SharedSkills.Tokens,Is.Empty);
            g.SharedSkills.ResolveHowl(a,again,e,e);Assert.That(g.SharedSkills.PendingHowls(again).Single(),Is.SameAs(second));
            Assert.Throws<InvalidOperationException>(()=>battle.Advance());
            g.SharedSkills.ResolveHowl(a,again,e,e);Assert.That(g.SharedSkills.PendingHowls(again),Is.Empty);
            Assert.That(CombatMath.EffectiveArmor(e,0,again),Is.EqualTo(Math.Max(1,e.Armor-2)));
            Assert.That(CardCombatActions.DefenseProfile(e,0,again).Attack,Is.EqualTo(Math.Max(0,e.Attack-2)));battle.Advance();
        }
        [Test] public void ExactAttackSlotAndMinimumArmorArePreserved()
        {
            var(g,a,b,c,s)=Start();g.SharedSkills.UseHowl(a,c,s,a.Deck.Hand[0],SidewaysType.Move);
            var e=Enemy() with{Armor=1,Attack=1,Attacks=new[]{new EnemyAttack(1,Element.Physical),new EnemyAttack(1,Element.Fire)}};var battle=Battle(a,c,e);
            g.SharedSkills.ResolveHowl(a,c,e,e,1);Assert.That(CombatMath.EffectiveArmor(e,0,c),Is.EqualTo(1));
            Assert.That(CardCombatActions.DefenseProfile(e,0,c,0).Attack,Is.EqualTo(1));Assert.That(CardCombatActions.DefenseProfile(e,0,c,1).Attack,Is.Zero);
        }
    }
}
