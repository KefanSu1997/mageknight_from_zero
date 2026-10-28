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
    public sealed class HeadbandLeadershipBorrowTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private (PveBattleSession battle, DeedCard headband, SkillCard skill) Setup(string unit = "army_007")
        {
            var p = new PlayerState { Fame = 15 };
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, unit)));
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, unit)));
            var battle = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            var h = new DeedCard("items_022", CardType.Artifact); p.Deck.Hand.Add(h);
            var s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[6];
            battle.Context.SkillSupply = new SkillSupply(); battle.Context.SkillSupply.Offer.Add(s);
            return (battle, h, s);
        }

        [Test] public void TwoActualUnitsKeepElementAndConsumerWithoutPersonalUsage()
        {
            var (b,h,s) = Setup(); var p=b.Player; var c=b.Context;
            p.Fame=35;
            var own=CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[6];p.Skills.Add(own);
            var firstUnit=new UnitState(UnitJsonLoader.Load(Folder,"army_007"));p.Units.Add(firstUnit);
            LeadershipActions.Use(p,c,own,firstUnit,0);
            Assert.That(SkillActions.IsUsed(p,own),Is.True);
            HeadbandBorrowResolution.UseLeadership(p,c,h,s,p.Units[0],0);
            var pending=c.PendingHeadbandBorrow;
            Assert.That(pending.Completed,Is.EqualTo(1)); Assert.That(pending.HasLegalInput(),Is.True);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(p,c,p.Units[1],0,leadership:s,leadershipBorrow:pending));
            Assert.That(p.Units[1].IsReady,Is.True);
            HeadbandBorrowResolution.UseLeadership(p,c,h,s,p.Units[1],0);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Skills.Single(),Is.SameAs(own));
            Assert.That(own,Is.Not.SameAs(s));Assert.That(SkillActions.IsUsed(p,own),Is.True);
            Assert.That(p.Deck.PlayedCards.Count(x=>ReferenceEquals(x,h)),Is.EqualTo(1));
            Assert.That(c.CombatPower.Attacks.Select(a=>a.Value),Is.EqualTo(new[]{4,4,4}));
            Assert.That(c.CombatPower.Attacks.All(a=>a.Element==Element.Fire && p.Units.Contains(a.SourceUnit)),Is.True);
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));
            Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
        }

        [Test] public void SecondMissingPrintedManaPreservesFirstAndCanFinishWhenNoAffordableAbility()
        {
            var (b,h,s)=Setup("army_007");var p=b.Player;var c=b.Context;b.Advance();
            // Original fire mages: red block; their free ranged ability is illegal in Block.
            int index=Array.FindIndex(p.Units[0].Card.Actions,a=>a.Kind==UnitAbilityKind.AttackOrBlock);
            Assert.That(index,Is.GreaterThanOrEqualTo(0));
            p.Mana.AddCrystal(ManaColor.Red);
            HeadbandBorrowResolution.UseLeadership(p,c,h,s,p.Units[0],index,1);
            var pending=c.PendingHeadbandBorrow;int block=c.BlockPool;
            Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(pending.HasLegalInput(),Is.False);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseLeadership(p,c,h,s,p.Units[1],index,1));
            Assert.That(c.BlockPool,Is.EqualTo(block));Assert.That(p.Units[1].IsReady,Is.True);
            Assert.That(pending.Completed,Is.EqualTo(1));Assert.That(pending.Remaining,Is.EqualTo(1));
            Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(pending.HasLegalInput(),Is.False);
            HeadbandBorrowResolution.FinishIfImpossible(p,c);Assert.That(c.PendingHeadbandBorrow,Is.Null);
            b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
        }

        [Test] public void FirstIllegalPhaseAndMastersDoNotConsumePhysicalCardOrUnits()
        {
            var (b,h,s)=Setup("army_005");var p=b.Player;var c=b.Context;b.Advance();
            for(int i=0;i<p.Units[0].Card.Actions.Length;i++)
                Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseLeadership(p,c,h,s,p.Units[0],i,target:0));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand,Does.Contain(h));
            Assert.That(p.Units.All(u=>u.IsReady),Is.True);Assert.That(c.CombatPower.Blocks,Is.Empty);
            var (r,card,skill)=Setup();r.Advance();
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseLeadership(r.Player,r.Context,card,skill,r.Player.Units[0],0));
            Assert.That(r.Context.PendingHeadbandBorrow,Is.Null);Assert.That(r.Player.Deck.Hand,Does.Contain(card));
        }
    }
}

