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
    public sealed class OriginalHeroJourneyTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        [TestCase("Arythea","024","013","hero_skill_pool_000")]
        [TestCase("Tovak","020","027","hero_skill_pool_007")]
        [TestCase("Goldyx","006","019","hero_skill_pool_005")]
        [TestCase("Norowas","012","005","hero_skill_pool_002")]
        [TestCase("Wolfhawk","010","017","hero_skill_pool_004")]
        [TestCase("Krang","003","026","hero_skill_pool_003")]
        [TestCase("Braevalar","004","018","hero_skill_pool_001")]
        public void SevenPrintedDecksBeginWithRealFiveOfSixteenAndNoFreeResources(string id,string first,string second,string pool)
        {
            var hero=OriginalHeroes.Get(id);var original=CardJsonLoader.LoadBasicActions();
            var p=hero.CreatePlayer(original,new Random(3));var all=p.Deck.DrawPile.ToArray();
            Assert.That(all.Length,Is.EqualTo(16));Assert.That(p.Deck.Hand.Count,Is.Zero);
            Assert.That(hero.UniqueCards,Is.EquivalentTo(new[]{"basic_card_"+first,"basic_card_"+second}));
            Assert.That(all.Count(c=>c.Id=="basic_card_"+first),Is.EqualTo(1));Assert.That(all.Count(c=>c.Id=="basic_card_"+second),Is.EqualTo(1));
            Assert.That(all.Select(c=>c.Id),Is.EquivalentTo(hero.StartingCards));
            Assert.That(all.Any(c=>c.Id is "basic_card_011" or "basic_card_025"),Is.False);
            Assert.That(all.Where((c,i)=>all.Take(i).Any(x=>ReferenceEquals(c,x))).Count(),Is.Zero);
            Assert.That(hero.SkillPoolId,Is.EqualTo(pool));Assert.That(CardJsonLoader.LoadHeroSkills(pool).Count,Is.EqualTo(10));
            Assert.That(p.HeroId,Is.EqualTo(id));Assert.That(p.Fame,Is.Zero);Assert.That(p.Level,Is.EqualTo(1));Assert.That(p.Armor,Is.EqualTo(2));
            Assert.That(p.CommandSlots,Is.EqualTo(1));Assert.That(p.Units.Count,Is.Zero);Assert.That(p.Skills.Count,Is.Zero);
            Assert.That(p.Mana.Crystals.Values.Sum()+p.Mana.Tokens.Values.Sum(),Is.Zero);
            var journey=new PveJourneySession(p,original);journey.PrepareRound();
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(11));
            Assert.That(all.All(c=>p.Deck.Hand.Concat(p.Deck.DrawPile).Any(x=>ReferenceEquals(c,x))),Is.True);
            journey.SelectTactic("day_1");journey.BeginTurn();
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(11));
        }
        [Test] public void OriginalSourceRequiredAndReplayRandomIsExplicit()
        {
            var hero=OriginalHeroes.Get("Tovak");var source=CardJsonLoader.LoadBasicActions();
            Assert.Throws<InvalidOperationException>(()=>OriginalHeroes.Get("Ymirgh"));
            Assert.Throws<InvalidOperationException>(()=>hero.CreatePlayer(source.Where(c=>c.Id!="basic_card_020")));
            var a=hero.CreatePlayer(source,new Random(2));var b=hero.CreatePlayer(source,new Random(2));var c=hero.CreatePlayer(source,new Random(4));
            Assert.That(a.Deck.DrawPile.Select(x=>x.Id),Is.EqualTo(b.Deck.DrawPile.Select(x=>x.Id)));
            Assert.That(a.Deck.DrawPile.Select(x=>x.Id),Is.Not.EqualTo(c.Deck.DrawPile.Select(x=>x.Id)));
        }
        [Test] public void EndTurnSelectionDiscardsExactDuplicateThenRefillsWithoutReplacingKeptCards()
        {
            var p=new PlayerState();var a=new DeedCard("basic_card_000",CardType.Action);var b=new DeedCard(a.Id,a.Type);
            var source=CardJsonLoader.LoadBasicActions();p.Deck.SetDeck(Enumerable.Range(0,8).Select(_=>new DeedCard("basic_card_021",CardType.Action)));
            p.Deck.Hand.Add(a);p.Deck.Hand.Add(b);var j=new PveJourneySession(p,source);j.SelectTactic("day_1");j.BeginTurn();
            var hand=p.Deck.Hand.ToArray();j.EndTurn(new[]{b});
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,a)),Is.True);
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,b)),Is.False);Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(4));
            foreach(var kept in hand.Where(c=>!ReferenceEquals(c,b)))Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,kept)),Is.True);
            Assert.Throws<InvalidOperationException>(()=>j.EndTurn(new[]{a}));Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,a)),Is.True);
        }
        [Test] public void EndTurnInvalidSelectionIsAtomicAndDoesNotDiscardWounds()
        {
            var p=OriginalHeroes.Get("Braevalar").CreatePlayer(CardJsonLoader.LoadBasicActions(),new Random(2));
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            var card=p.Deck.Hand[0];p.Deck.GainWound();var wound=p.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            Assert.Throws<InvalidOperationException>(()=>j.EndTurn(new[]{card,wound}));
            Assert.Throws<InvalidOperationException>(()=>j.EndTurn(new[]{card,card}));
            Assert.Throws<InvalidOperationException>(()=>j.EndTurn(new[]{new DeedCard(card.Id,card.Type)}));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(6));Assert.That(p.Deck.DiscardPile.Count,Is.Zero);Assert.That(j.Game.HasActiveTurn,Is.True);
            j.EndTurn(new[]{card});Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,wound)),Is.True);
        }
    }
}
