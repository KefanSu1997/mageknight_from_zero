using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class TurnMinimumActivityTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (GameEngine,PlayerState,ActionContext) Start()
        {
            var p=new PlayerState();p.Deck.SetDeck(Enumerable.Range(0,8).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var g=new GameEngine(new[]{p},3);g.StartNewRound(new[]{OriginalTactics.Get("day_1").Card});
            var c=new ActionContext();g.BeginNextTurn(c);return(g,p,c);
        }
        [Test] public void SkillOnlyTurnCannotResetUsageOrCleanResourcesWithoutSpendingACard()
        {
            var(g,p,c)=Start();var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[0];p.Skills.Add(skill);
            SkillActions.Use(p,c,skill);p.Mana.AddToken(ManaColor.Red);var hand=p.Deck.Hand.ToArray();
            Assert.Throws<InvalidOperationException>(()=>g.EndCurrentTurn(c));
            Assert.That(g.IsEndingTurn,Is.False);Assert.That(g.CurrentPlayer,Is.SameAs(p));Assert.That(c.MovementPool,Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Deck.Hand,Is.EqualTo(hand));Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            Assert.Throws<InvalidOperationException>(()=>g.BeginNextTurn(new ActionContext()));
            g.EndCurrentTurn(c,new[]{hand[1]});Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(hand[1]));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Mana.Tokens.Values.Sum(),Is.Zero);
            var next=new ActionContext();g.BeginNextTurn(next);Assert.That(p.Deck.CardsPlayedOrDiscardedThisTurn,Is.Zero);
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.Throws<InvalidOperationException>(()=>g.EndCurrentTurn(next));
        }
        [Test] public void EndSelectionIsValidatedBeforeCleanupAndDiscardedOnlyAtCompletion()
        {
            var(g,p,c)=Start();var card=p.Deck.Hand[1];p.Deck.GainWound();var wound=p.Deck.Hand.Last();p.Mana.AddToken(ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(()=>g.BeginEndCurrentTurn(c,new[]{card,wound}));
            Assert.Throws<InvalidOperationException>(()=>g.BeginEndCurrentTurn(c,new[]{card,card}));
            Assert.Throws<InvalidOperationException>(()=>g.BeginEndCurrentTurn(c,new[]{new DeedCard(card.Id,card.Type)}));
            Assert.That(p.Mana.Tokens[ManaColor.Blue],Is.EqualTo(1));Assert.That(p.Deck.DiscardPile,Is.Empty);
            g.BeginEndCurrentTurn(c,new[]{card});Assert.That(p.Deck.Hand.Any(h=>ReferenceEquals(h,card)),Is.True);
            Assert.That(g.IsEndingTurn,Is.True);g.CompleteEndCurrentTurn(c);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));Assert.That(p.Deck.Hand.Any(h=>ReferenceEquals(h,wound)),Is.True);
        }
        [Test] public void BeforeTurnDiscardOrNewWoundDoesNotCountAsCurrentTurnActivity()
        {
            var(g,p,c)=Start();p.Deck.TryDiscardExact(p.Deck.Hand[0]);g.EndCurrentTurn(c);
            p.Deck.TryDiscardExact(p.Deck.Hand[0]);var next=new ActionContext();g.BeginNextTurn(next);p.Deck.GainWound(true);
            Assert.That(p.Deck.CardsPlayedOrDiscardedThisTurn,Is.Zero);Assert.Throws<InvalidOperationException>(()=>g.EndCurrentTurn(next));
        }
        [Test] public void CompletelyHealedEmptyHandMayFinishAndRefillFromExistingDeck()
        {
            var p=new PlayerState();p.Deck.SetDeck(new[]{new DeedCard("basic_card_000",CardType.Action)});
            for(int i=0;i<5;i++)p.Deck.GainWound();
            var g=new GameEngine(new[]{p},3);g.StartNewRound(new[]{OriginalTactics.Get("day_1").Card});var c=new ActionContext();g.BeginNextTurn(c);
            c.Healing.Add(5);CardHealing.Spend(p,c,5);Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.CardsPlayedOrDiscardedThisTurn,Is.Zero);
            g.EndCurrentTurn(c);Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo("basic_card_000"));Assert.That(p.Wounds,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void AttachedOrBurnedArtifactCountsEvenWhenNothingRemainsInPlayedZone(bool burn)
        {
            var(g,p,c)=Start();var artifact=new DeedCard(burn?"items_010":"items_003",CardType.Artifact);p.Deck.Hand.Add(artifact);
            if(burn)CardPlayActions.PlayArtifact(p,c,artifact,CardJsonLoader.LoadItems().Single(i=>i.Id==artifact.Id),true);
            else
            {
                var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(unit);BannerActions.AssignCourage(p,unit,artifact);
                Assert.That(unit.Banner,Is.SameAs(artifact));
            }
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.CardsPlayedOrDiscardedThisTurn,Is.EqualTo(1));g.EndCurrentTurn(c);
        }
        [Test] public void EmptyFinalPlayerSkipsWithoutStartTurnManaSkillResetOrRefill()
        {
            var a=new PlayerState();var b=new PlayerState();b.TokensPerTurn[ManaColor.Red]=1;
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[0];b.Skills.Add(skill);
            var g=new GameEngine(new[]{a,b},3);g.StartNewRound(new[]{OriginalTactics.Get("day_1").Card,OriginalTactics.Get("day_4").Card});
            SkillActions.Use(b,new ActionContext(),skill);g.AnnounceRoundEnd();Assert.That(g.CanSkipEmptyTurn,Is.True);
            Assert.Throws<InvalidOperationException>(()=>g.BeginNextTurn(new ActionContext()));Assert.That(b.Mana.Tokens,Is.Empty);
            g.SkipEmptyTurn();Assert.That(g.ClockTracker.RoundEnded,Is.True);Assert.That(g.HasActiveTurn,Is.False);
            Assert.That(SkillActions.IsUsed(b,skill),Is.True);Assert.That(b.Mana.Tokens,Is.Empty);Assert.Throws<InvalidOperationException>(()=>g.SkipEmptyTurn());
        }
    }
}
