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
    public sealed class PveRecoverySkillJourneyTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));

        [TestCase("000","basic_card_022",ManaColor.Red,5)]
        [TestCase("002","basic_card_008",ManaColor.White,4)]
        [TestCase("005","basic_card_000",ManaColor.Green,4)]
        [TestCase("007","basic_card_014",ManaColor.Blue,4)]
        public void InspirationDrawsSameOriginalInstanceThenPaysItsEnhancedEffect(string pool,string cardId,ManaColor color,int value)
        {
            var p=new PlayerState{Fame=3};var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_"+pool)[8];p.Skills.Add(skill);
            var cards=Enumerable.Range(0,6).Select(_=>new DeedCard("basic_card_021",CardType.Action)).ToArray();
            p.Deck.SetDeck(cards);var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            var actual=new DeedCard(cardId,CardType.Action);p.Deck.PutOnTop(actual);
            int hand=p.Deck.Hand.Count,deck=p.Deck.DrawPileCount;
            j.UseOutsideSkill(skill);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(hand+2));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(deck-2));
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,actual)),Is.True);
            j.PlayOutsideBattle(actual,"strong");
            Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(color==ManaColor.Green || color==ManaColor.Blue ? j.Context.MovementPool:j.Context.InfluencePool,Is.EqualTo(value));
            Assert.That(p.Deck.PlayedCards.Any(c=>ReferenceEquals(c,actual)),Is.True);
        }

        [Test] public void RallyRestoresSameFarmerForSecondFormalMovementAndRestoresTemporaryInput()
        {
            var p=new PlayerState{Fame=3};var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[2];p.Skills.Add(skill);
            p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(unit);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            j.UseOutsideUnit(unit,2);Assert.That(j.Context.MovementPool,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill,0));
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            j.UseOutsideSkill(skill,0,unit);Assert.That(j.Context.TargetUnit,Is.Null);
            j.UseOutsideUnit(unit,2);Assert.That(j.Context.MovementPool,Is.EqualTo(4));
            Assert.That(unit.IsReady,Is.False);Assert.That(unit.Wounds,Is.Zero);
        }
    }
}
