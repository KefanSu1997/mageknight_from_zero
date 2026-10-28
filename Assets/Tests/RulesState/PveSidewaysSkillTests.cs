using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveSidewaysSkillTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static SkillCard Skill(PlayerState p, bool universal)
        {
            var s = CardJsonLoader.LoadHeroSkills(universal ? "hero_skill_pool_005" : "hero_skill_pool_007")[universal ? 7 : 6];
            p.Skills.Add(s); return s;
        }
        private static DeedCard Card(int kind) => kind switch
        {
            0 => new DeedCard("basic_card_000", CardType.Action),
            1 => new DeedCard(CardJsonLoader.LoadAdvancedActions().First(c => c.RequiredCrystals.Length > 0).Id, CardType.Action),
            2 => new DeedCard(CardJsonLoader.LoadSpells()[0].Id, CardType.Spell),
            _ => new DeedCard("items_000", CardType.Artifact)
        };
        [TestCase(0, 2)] [TestCase(1, 3)] [TestCase(2, 3)] [TestCase(3, 3)]
        public void CarelessUsesActualOriginalTypeAndOnlyOneCard(int kind, int expected)
        {
            var p = new PlayerState(); var ctx = new ActionContext(); var s = Skill(p, false); var c = Card(kind);
            p.Deck.Hand.Add(c); var other = Card(0); p.Deck.Hand.Add(other);
            SkillActions.PlaySideways(p, ctx, s, c, SidewaysType.Move);
            Assert.That(ctx.MovementPool, Is.EqualTo(expected)); Assert.That(ctx.SidewaysCardValue, Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(c)); Assert.That(SkillActions.IsUsed(p,s), Is.True);
            Assert.Throws<InvalidOperationException>(() => SkillActions.PlaySideways(p,ctx,s,other,SidewaysType.Move));
            Assert.That(new ActionSystem(false).PlaySideways(other,p,ctx,SidewaysType.Move), Is.True);
            Assert.That(ctx.MovementPool, Is.EqualTo(expected+1));
        }
        [TestCase(0, true)] [TestCase(0, false)] [TestCase(1, true)] [TestCase(1, false)]
        [TestCase(2, true)] [TestCase(2, false)] [TestCase(3, false)]
        public void UniversalPaysActualColorAndDerivesValue(int kind, bool match)
        {
            var p = new PlayerState(); var ctx = new ActionContext(); var s = Skill(p,true); var c=Card(kind); p.Deck.Hand.Add(c);
            var printed=CardColorHelper.Get(c.Id); var color=match ? printed.Value : (printed==ManaColor.Red ? ManaColor.Blue : ManaColor.Red);
            p.Mana.AddToken(color); p.Mana.AddCrystal(color);
            SkillActions.PlaySideways(p,ctx,s,c,SidewaysType.Attack,color);
            Assert.That(ctx.MeleePool,Is.EqualTo(match ? 4 : 3)); Assert.That(p.Mana.Tokens.ContainsKey(color),Is.False);
            Assert.That(p.Mana.Crystals[color],Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(c));
            Assert.That(ctx.SidewaysCardValue,Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidSelectionPreservesCardManaUsage(int invalid)
        {
            var p = new PlayerState(); var ctx = new ActionContext(); var s = Skill(p,true); var c=Card(0); p.Deck.Hand.Add(c);
            if(invalid!=0)p.Mana.AddToken(ManaColor.Green);
            var selected=invalid==1 ? Card(0) : c;
            if(invalid==2){selected=new DeedCard(c.Id,CardType.Spell);p.Deck.Hand.Add(selected);}
            if(invalid==3){selected=new DeedCard("w",CardType.Wound);p.Deck.Hand.Add(selected);}
            int hand=p.Deck.Hand.Count, mana=p.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(()=>SkillActions.PlaySideways(p,ctx,s,selected,invalid==4?(SidewaysType)99:SidewaysType.Block,invalid==5?(ManaColor)99:ManaColor.Green));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(hand));Assert.That(p.Mana.GetTotal(),Is.EqualTo(mana));
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(ctx.BlockPool,Is.Zero);
        }
        [TestCase(true)] [TestCase(false)]
        public void LegacyOptionCannotMintUnpaidPermission(bool universal)
        {
            var p=new PlayerState();var ctx=new ActionContext();var s=Skill(p,universal);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,ctx,s,1));
            Assert.That(ctx.SidewaysCardValue,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [TestCase(SidewaysType.Block)] [TestCase(SidewaysType.Influence)]
        public void CarelessUsesFormalResourceAndEndTurnDiscard(SidewaysType type)
        {
            var p=new PlayerState();var ctx=new ActionContext();var engine=new PlayerTurnEngine();
            engine.StartTurn(p,DayPart.Day);var s=Skill(p,false);var c=Card(0);p.Deck.Hand.Add(c);
            SkillActions.PlaySideways(p,ctx,s,c,type);
            Assert.That(type==SidewaysType.Block?ctx.BlockPool:ctx.InfluencePool,Is.EqualTo(2));
            engine.EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),ctx);
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,c)),Is.True);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [Test] public void SessionRejectsWrongPhaseThenConsumesActualBlockAndJourneyMove()
        {
            var p=new PlayerState{Armor=3};var skill=Skill(p,true);var card=Card(0);p.Deck.Hand.Add(card);p.Mana.AddCrystal(ManaColor.Green);
            var j=new MK.Logic.Runtime.Battles.PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var b=j.BeginBattle(new[]{enemy});
            Assert.Throws<InvalidOperationException>(()=>b.PlaySidewaysWithSkill(skill,card,ManaColor.Green));
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.Hand.Contains(card),Is.True);
            b.Advance();b.PlaySidewaysWithSkill(skill,card,ManaColor.Green);
            Assert.That(b.Context.BlockPool,Is.EqualTo(4));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            b.Block(0);Assert.That(b.Context.BlockPool,Is.Zero);
        }
        [Test] public void JourneyRejectsMovementAfterActionWithoutSpendingSkillOrCard()
        {
            var p=new PlayerState();var skill=Skill(p,false);var card=Card(0);p.Deck.Hand.Add(card);p.Deck.Hand.Add(Card(0));
            var j=new MK.Logic.Runtime.Battles.PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            j.Context.TurnActionCommitted=true;
            Assert.Throws<InvalidOperationException>(()=>j.PlaySidewaysWithSkill(skill,card,SidewaysType.Move));
            Assert.That(p.Deck.Hand.Contains(card),Is.True);Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            j.Context.TurnActionCommitted=false;j.PlaySidewaysWithSkill(skill,card,SidewaysType.Move);
            Assert.That(j.Context.MovementPool,Is.EqualTo(2));j.EndTurn();j.BeginTurn();
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(p.Deck.DiscardPile.Any(c=>ReferenceEquals(c,card)),Is.True);
        }
        [Test] public void ClosedBattleCannotOfferOrSpendSidewaysSkill()
        {
            var p=new PlayerState{Armor=3};var skill=Skill(p,false);var card=Card(0);p.Deck.Hand.Add(card);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b=new MK.Logic.Runtime.Battles.PveBattleSession(p,new[]{enemy},CardJsonLoader.LoadBasicActions());
            for(int i=0;i<4;i++)b.Advance();
            Assert.That(b.SkillChoices(skill),Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>b.PlaySidewaysWithSkill(skill,card));
            Assert.That(p.Deck.Hand.Contains(card),Is.True);Assert.That(SkillActions.IsUsed(p,skill),Is.False);
        }
    }
}

