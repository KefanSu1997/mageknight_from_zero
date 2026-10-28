using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class HeroProgressionTests
    {
        private sealed class Ordered : Random { public override int Next(int maxValue)=>maxValue-1; }
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Create()
        {
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).ToArray();
            var p=OriginalHeroes.Get("Tovak").CreatePlayer(cards,new Random(2));
            var j=new PveJourneySession(p,cards,originalSkills:CardJsonLoader.LoadHeroSkills("hero_skill_pool_007"),progressionRandom:new Ordered());
            j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        [Test] public void RealBattleFameWaitsForCleanupThenRewardEntersActualRefillAndPersists()
        {
            var j=Create();var p=j.Player;var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_007")});
            b.Advance();b.Advance();b.Advance();
            foreach(var card in p.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Take(4).ToArray())b.PlaySideways(card);
            b.Attack(0);b.Advance();Assert.That(b.Won,Is.True);Assert.That(p.Fame,Is.EqualTo(3));
            Assert.That(p.Level,Is.EqualTo(1));var draw=p.Deck.DrawPileCount;
            j.EndTurn();Assert.That(j.Game.IsEndingTurn,Is.True);Assert.That(p.Deck.PlayedCards.Count,Is.Zero);
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(draw));Assert.That(j.Progression.PendingLevel,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>j.BeginTurn());Assert.Throws<InvalidOperationException>(()=>j.EndTurn());
            Assert.Throws<InvalidOperationException>(()=>j.SidewaysMovement(p.Deck.Hand.First(c=>c.Type!=CardType.Wound)));
            var skill=j.Progression.Choices[0];var other=j.Progression.Choices[1];var action=j.Progression.Actions[1];
            j.ChooseLevelReward(skill,action);
            Assert.That(p.Level,Is.EqualTo(2));Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.Hand.Any(c=>c.Id==action.Id),Is.True);
            Assert.That(p.Skills.Single(),Is.SameAs(skill));Assert.That(j.Game.Globals.Skills.Offer.Single(),Is.SameAs(other));
            Assert.That(j.Progression.Actions.Count,Is.EqualTo(3));Assert.That(j.CompletedTurns,Is.EqualTo(1));
            j.BeginTurn();Assert.That(j.Context.AdvActionSupply,Is.SameAs(j.Game.Globals.AdvActions));
            SkillActions.Use(p,j.Context,skill);Assert.That(j.Context.MovementPool,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,j.Context,skill));
        }
        [Test] public void CommonSkillRequiresOtherHeroAndBottomOfferWithoutMutatingInvalidChoice()
        {
            var j=Create();j.Player.Fame=3;j.EndTurn(new[]{j.Player.Deck.Hand[0]});
            var foreign=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[0];j.Game.Globals.Skills.Offer.Add(foreign);
            var own=j.Progression.Choices.ToArray();var offer=j.Progression.Actions.ToArray();
            Assert.Throws<InvalidOperationException>(()=>j.ChooseLevelReward(foreign,offer[1]));
            Assert.Throws<InvalidOperationException>(()=>j.ChooseLevelReward(own[0],new AdvActionCard(offer[0].Id)));
            Assert.That(j.Player.Skills.Count,Is.Zero);Assert.That(j.Progression.Actions,Is.EqualTo(offer));
            j.ChooseLevelReward(foreign,offer[0]);Assert.That(j.Player.Skills.Single(),Is.SameAs(foreign));
            Assert.That(j.Game.Globals.Skills.Offer,Is.EqualTo(own));Assert.That(j.Player.Deck.Hand.Last().Id,Is.EqualTo(offer[0].Id));
        }
        [Test] public void MultipleLevelsApplyPrintedArmorCommandAndHandBeforeRefill()
        {
            var j=Create();var p=j.Player;var originals=p.Deck.Hand.ToArray();p.Fame=24;
            Assert.That(p.Level,Is.EqualTo(1));j.EndTurn(originals);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5)); // discard is after rewards, not before cleanup
            j.ChooseLevelReward(j.Progression.Choices[0],j.Progression.Actions[0]);
            Assert.That(p.Level,Is.EqualTo(3));Assert.That(p.CommandSlots,Is.EqualTo(2));Assert.That(p.Armor,Is.EqualTo(3));
            Assert.That(j.Progression.PendingLevel,Is.EqualTo(4));Assert.That(j.CompletedTurns,Is.Zero);
            j.ChooseLevelReward(j.Progression.Choices[0],j.Progression.Actions[0]);
            Assert.That(p.Level,Is.EqualTo(5));Assert.That(p.CommandSlots,Is.EqualTo(3));Assert.That(p.HandLimit(),Is.EqualTo(6));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(6));Assert.That(p.Skills.Count,Is.EqualTo(2));
            Assert.That(p.Deck.DiscardPile,Is.EquivalentTo(originals));Assert.That(j.Game.IsEndingTurn,Is.False);
        }
        [Test] public void MissingOwnSkillSourceAndPrematureResolutionAreRejected()
        {
            var j=Create();Assert.Throws<InvalidOperationException>(()=>j.Progression.PrepareNext());
            Assert.Throws<InvalidOperationException>(()=>new HeroProgression(j.Player,CardJsonLoader.LoadHeroSkills("hero_skill_pool_000"),j.Game.Globals.AdvActions,j.Game.Globals.Skills,()=>true));
        }
    }
}
