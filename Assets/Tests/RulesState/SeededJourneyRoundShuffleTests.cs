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
    public sealed class SeededJourneyRoundShuffleTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static string NightDeck(int shuffleSeed)
        {
            var cards=CardJsonLoader.LoadBasicActions();
            var p=OriginalHeroes.All[0].CreatePlayer(cards,new Random(17));
            var journey=new PveJourneySession(p,cards,manaRandom:new Random(17),tacticRandom:new Random(17),supplyRandom:new Random(17),roundShuffleRandom:new Random(shuffleSeed));
            var original=p.Deck.Hand.Concat(p.Deck.DrawPile).ToArray();
            journey.PrepareRound();journey.SelectTactic("day_1");journey.BeginTurn();
            for(int turn=0;turn<8;turn++)
            {
                journey.EndTurn(p.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray());
                if(journey.Game.CanAnnounceRoundEnd)break;
                journey.BeginTurn();
            }
            Assert.That(journey.Game.CanAnnounceRoundEnd,Is.True);
            journey.AnnounceRoundEnd();journey.EndRound();journey.PrepareRound();journey.SelectTactic("night_1");journey.BeginTurn();
            Assert.That(journey.Context.DayPart,Is.EqualTo(DayPart.Night));
            var actual=p.Deck.Hand.Concat(p.Deck.DrawPile).ToArray();
            Assert.That(actual.Length,Is.EqualTo(16));
            Assert.That(original.All(card=>actual.Any(c=>ReferenceEquals(c,card))),Is.True);
            return string.Join(",",p.Deck.Hand.Select(c=>c.Id))+"|"+string.Join(",",p.Deck.DrawPile.Select(c=>c.Id));
        }
        [Test] public void SameSeedReplaysActualNightDeckAndDifferentRoundSeedChangesOrder()
        {
            var first=NightDeck(31);
            Assert.That(NightDeck(31),Is.EqualTo(first));
            Assert.That(NightDeck(32),Is.Not.EqualTo(first));
        }
    }
}
