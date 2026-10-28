using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class JourneyRestSkillTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Create(params int[] skills)
        {
            var p=new PlayerState {HeroId="Tovak",Fame=15};
            p.Deck.SetDeck(Enumerable.Range(0,9).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            foreach(int i in skills)p.Skills.Add(CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[i]);
            var map=new MapState();foreach(var pos in new[]{new AxialCoord(0,0),new AxialCoord(1,0)})
                map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        [Test] public void StandardRestDiscardsExactChosenCardsDoesNotHealAndRoundReturnsWounds()
        {
            var j=Create();var p=j.Player;p.Deck.GainWound();p.Deck.GainWound();
            var wounds=p.Deck.Hand.Where(c=>c.Type==CardType.Wound).ToArray();var ordinary=p.Deck.Hand[1];var kept=p.Deck.Hand[0];
            j.Rest(ordinary,wounds);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(3));Assert.That(p.Deck.RemovedCards.Count,Is.Zero);
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,kept)),Is.True);Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,ordinary)),Is.False);
            Assert.Throws<InvalidOperationException>(()=>j.Rest(kept));Assert.Throws<InvalidOperationException>(()=>j.BeginBattle(CardJsonLoader.LoadMonsters().Take(1)));
            j.EndTurn();Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));j.BeginTurn();j.EndTurn(p.Deck.Hand.ToArray());
            j.AnnounceRoundEnd();j.EndRound();Assert.That(p.Deck.DrawPile.Count(c=>wounds.Any(w=>ReferenceEquals(w,c))),Is.EqualTo(2));
        }
        [Test] public void SlowRecoveryRequiresOnlyWoundsAndOneActualSelection()
        {
            var j=Create();foreach(var c in j.Player.Deck.Hand.ToArray())j.SidewaysMovement(c);
            j.Player.Deck.GainWound();j.Player.Deck.GainWound();var wounds=j.Player.Deck.Hand.ToArray();
            Assert.Throws<InvalidOperationException>(()=>j.Rest(null,wounds));Assert.Throws<InvalidOperationException>(()=>j.Rest(null,new DeedCard("w",CardType.Wound)));
            Assert.That(j.Player.Wounds,Is.EqualTo(2));j.Rest(null,wounds[1]);Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(wounds[0]));
            Assert.That(j.Player.Deck.DiscardPile.Single(),Is.SameAs(wounds[1]));Assert.That(j.Context.RestedThisTurn,Is.True);
        }
        [Test] public void InvalidRestIsAtomicAndActualMovementPreventsRestEvenAfterReturning()
        {
            var j=Create(0);var p=j.Player;p.Deck.GainWound();var wound=p.Deck.Hand.Last();var card=p.Deck.Hand[0];
            Assert.Throws<InvalidOperationException>(()=>j.Rest(new DeedCard(card.Id,card.Type),wound));
            Assert.Throws<InvalidOperationException>(()=>j.Rest(card,wound,wound));
            Assert.That(p.Deck.DiscardPile.Count,Is.Zero);Assert.That(j.Context.TurnActionCommitted,Is.False);
            j.UseOutsideSkill(p.Skills[0]);j.Move(new AxialCoord(1,0));
            j.PlayOutsideBattle(card,"base");j.Move(new AxialCoord(0,0));
            Assert.Throws<InvalidOperationException>(()=>j.Rest(p.Deck.Hand[0],wound));Assert.That(p.Wounds,Is.EqualTo(1));
        }
        [Test] public void SkillMovementConsumesRealPositionAndUsageRefreshesNextTurn()
        {
            var j=Create(0);var skill=j.Player.Skills[0];j.UseOutsideSkill(skill);j.Move(new AxialCoord(1,0));
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill));
            j.EndTurn(new[]{j.Player.Deck.Hand[0]});j.BeginTurn();Assert.That(j.Context.MovedThisTurn,Is.False);j.UseOutsideSkill(skill);j.Move(new AxialCoord(0,0));
            Assert.That(j.Context.MovementPool,Is.Zero);
        }
        [Test] public void FearlessPainDiscardsRatherThanRemovesAndFailedUsePreservesUsage()
        {
            var j=Create(5);var p=j.Player;var skill=p.Skills[0];
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            p.Deck.GainWound();var wound=p.Deck.Hand.Last();var top=p.Deck.DrawPile.First();j.UseOutsideSkill(skill);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(wound));Assert.That(p.Deck.Hand.Last(),Is.SameAs(top));Assert.That(p.Deck.RemovedCards.Count,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill));
            j.EndTurn();j.BeginTurn();p.Deck.GainWound();j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"));
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,j.Context,skill));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
        }
        [Test] public void RestStillAllowsPrintedHealingSkillAndActualHealingConsumer()
        {
            var j=Create();var p=j.Player;var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[0];p.Skills.Add(skill);
            p.Deck.GainWound();p.Deck.GainWound();j.Rest(p.Deck.Hand[0],p.Deck.Hand.Last());
            var healed=p.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            j.UseOutsideSkill(skill);Assert.That(j.Context.Healing.Total,Is.EqualTo(1));Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));
            j.HealOutsideBattle();Assert.That(p.Wounds,Is.Zero);
            Assert.That(p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile).Concat(p.Deck.PlayedCards).Concat(p.Deck.RemovedCards).Any(c=>ReferenceEquals(c,healed)),Is.False);
            Assert.That(p.Deck.DiscardPile.Count(c=>c.Type==CardType.Wound),Is.EqualTo(1));
            j.EndTurn();j.BeginTurn();Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill));
        }
        [TestCase("hero_skill_pool_000",DayPart.Day,1)]
        [TestCase("hero_skill_pool_000",DayPart.Night,2)]
        [TestCase("hero_skill_pool_007",DayPart.Night,1)]
        public void PrintedDayNightMovementSkillsKeepTheirDifferentValues(string pool,DayPart part,int points)
        {
            var j=Create();var skill=CardJsonLoader.LoadHeroSkills(pool)[0];j.Player.Skills.Add(skill);j.Context.DayPart=part;
            j.UseOutsideSkill(skill);Assert.That(j.Context.MovementPool,Is.EqualTo(points));
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skill));
        }
        [Test] public void FullMarchCountsOnlyActualReadyUnwoundedUnitsAndCapsAtThree()
        {
            var j=Create();var p=j.Player;p.Fame=63;var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[0];p.Skills.Add(skill);
            foreach(var id in new[]{"army_2_006","army_2_002","army_2_008","army_2_001"})
                p.Units.Add(new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"),id)));
            Assert.That(p.FreeSlots,Is.Zero);j.UseOutsideSkill(skill);Assert.That(j.Context.MovementPool,Is.EqualTo(3));
            j.EndTurn(new[]{p.Deck.Hand[0]});j.BeginTurn();p.Units[0].Exhaust();p.Units[1].AddWounds(1);
            j.UseOutsideSkill(skill);Assert.That(j.Context.MovementPool,Is.EqualTo(2));
            j.Move(new AxialCoord(1,0));Assert.That(j.Context.MovementPool,Is.Zero);
        }
    }
}
