using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SourceOpeningSharedSkillTests
    {
        private sealed class DiceRandom:Random
        {
            public ManaColor Color=ManaColor.Red;
            public int Calls;
            public override int Next(int maxValue) { Calls++;return (int)Color; }
        }
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (GameEngine G,PlayerState A,PlayerState B,ActionContext C,SkillCard S,DiceRandom R) Start()
        {
            var a=new PlayerState(1,"Goldyx"){HeroId="Goldyx",Fame=3};var b=new PlayerState(2,"Tovak"){HeroId="Tovak",Fame=3};
            foreach(var p in new[]{a,b})p.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_005")[9];a.Skills.Add(skill);var random=new DiceRandom();
            var game=new GameEngine(new[]{a,b},3,manaRandom:random);
            game.StartNewRound(new[]{OriginalTactics.Get("day_1").Card,OriginalTactics.Get("day_4").Card});
            var context=new ActionContext();game.BeginNextTurn(context);return(game,a,b,context,skill,random);
        }
        [TestCase(false)] [TestCase(true)]
        public void AnotherPlayerUsesExtraActualDieAndChoosesItsReturn(bool reroll)
        {
            var(g,a,b,c,s,r)=Start();int initial=r.Calls;
            var token=g.SharedSkills.UseSourceOpening(a,c,s);Assert.That(r.Calls,Is.EqualTo(initial));
            g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});var next=new ActionContext();g.BeginNextTurn(next);
            var ordinary=g.Globals.Mana.Dice[0];CardManaActions.UseSourceDie(b,next,ordinary,ordinary.Face);
            var extra=g.Globals.Mana.Dice[0];g.SharedSkills.ClaimSourceOpening(b,next,token,extra);
            Assert.That(b.HeldManaDie,Is.SameAs(ordinary));Assert.That(next.ExtraManaDice,Is.Zero);
            Assert.That(a.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(b.Mana.Tokens[ManaColor.Red],Is.EqualTo(2));
            Assert.That(g.SharedSkills.PendingSourceOpeningReturns(b,next).Single(),Is.SameAs(extra));
            var hand=b.Deck.Hand.ToArray();Assert.Throws<InvalidOperationException>(()=>g.EndCurrentTurn(next,new[]{hand[0]}));
            Assert.That(b.Deck.Hand,Is.EqualTo(hand));Assert.That(b.Mana.Tokens[ManaColor.Red],Is.EqualTo(2));Assert.That(g.IsEndingTurn,Is.False);
            Assert.That(g.Globals.Mana.TurnClaims.Count,Is.EqualTo(2));
            r.Color=ManaColor.Blue;int calls=r.Calls;
            g.SharedSkills.ChooseSourceOpeningReturn(b,next,extra,reroll);g.EndCurrentTurn(next,new[]{hand[0]});
            Assert.That(r.Calls-calls,Is.EqualTo(reroll?2:1));Assert.That(extra.Face,Is.EqualTo(reroll?ManaColor.Blue:ManaColor.Red));
            Assert.That(g.Globals.Mana.Dice.Any(d=>ReferenceEquals(d,extra)),Is.True);Assert.That(g.Globals.Mana.TurnClaims,Is.Empty);
            Assert.That(g.Globals.Mana.PendingSourceOpeningReturns(b),Is.Empty);
        }
        [Test] public void OptionalRerollUsesOnlyAvailableOriginalDieAndOwnerMayClaim()
        {
            var(g,a,b,c,s,r)=Start();int calls=r.Calls;
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UseSourceOpening(a,c,s,new ManaDie()));
            Assert.That(SkillActions.IsUsed(a,s),Is.False);Assert.That(r.Calls,Is.EqualTo(calls));
            var die=g.Globals.Mana.Dice[0];r.Color=ManaColor.Green;
            var token=g.SharedSkills.UseSourceOpening(a,c,s,die);Assert.That(r.Calls,Is.EqualTo(calls+1));
            g.SharedSkills.ClaimSourceOpening(a,c,token,die);Assert.That(a.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            Assert.That(a.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(a.HeldManaDie,Is.Null);
            Assert.Throws<InvalidOperationException>(()=>g.Globals.Mana.Return(die));
            g.SharedSkills.ChooseSourceOpeningReturn(a,c,die,false);g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});
            Assert.That(die.Face,Is.EqualTo(ManaColor.Green));
        }
        [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)]
        public void InvalidColorOrForbiddenUseCannotConsumeSharedSkill(ManaColor invalid)
        {
            var(g,a,b,c,s,r)=Start();var token=g.SharedSkills.UseSourceOpening(a,c,s);
            g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});var next=new ActionContext();g.BeginNextTurn(next);
            var die=g.Globals.Mana.Dice[0];die.TurnTo(invalid);
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimSourceOpening(b,next,token,die));
            die.TurnTo(ManaColor.Red);var repel=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[7];b.Skills.Add(repel);
            SkillActions.PlaySideways(b,next,repel,b.Deck.Hand[0],SidewaysType.Move,forgoSourceDice:true);
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimSourceOpening(b,next,token,die));
            Assert.That(g.SharedSkills.Tokens.Single(),Is.SameAs(token));Assert.That(a.Mana.Tokens,Is.Empty);Assert.That(a.Mana.Crystals,Is.Empty);
            Assert.That(g.Globals.Mana.Dice.Any(d=>ReferenceEquals(d,die)),Is.True);
        }
    }
}
