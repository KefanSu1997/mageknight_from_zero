using System;
using System.Linq;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveSpecialArtifactEntryTests
    {
        private sealed class Ordered:Random{public override int Next(int max)=>max-1;}
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(bool hero,params string[] ids)=>StartSeed(6,hero,ids);
        private static PveJourneySession StartSeed(int seed,bool hero,params string[] ids)=>StartWithSkillShuffle(seed,hero,new Ordered(),ids);
        private static PveJourneySession StartWithSkillShuffle(int seed,bool hero,Random skillShuffle,params string[] ids)
        {
            var p=new PlayerState{HeroId=hero?"Tovak":""};
            foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Places[new AxialCoord(0,0)]=new PlaceState(PlaceType.Village);
            map.Placed[new AxialCoord(1,0)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,originalSkills:hero?CardJsonLoader.LoadHeroSkills("hero_skill_pool_007"):null,progressionRandom:skillShuffle,supplyRandom:new Random(seed),unitDeck:new[]{CardJsonLoader.LoadUnit("army_2_006"),CardJsonLoader.LoadUnit("army_2_001")});
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession s,string id)=>s.Player.Deck.Hand.First(c=>c.Id==id);
        [Test]public void CommandInfluenceRecruitsWithRealBannerThenSameUnitSurvivesTurn()
        {
            var j=Start(false,"items_017","basic_card_000");var card=Card(j,"items_017");
            j.PlaySpecialArtifact(card,false);Assert.That(j.Player.BannerCommandCount,Is.Zero);Assert.That(j.Context.InfluencePool,Is.EqualTo(4));
            j.SelectCommandBanner(card);j.BeginVillageInteraction();var offer=j.Game.Globals.Units.Offer.Single(u=>u.Id=="army_2_006");j.RecruitVillageUnit(offer);
            var unit=j.Player.Units.Single();Assert.That(unit.Banner,Is.SameAs(card));Assert.That(j.Player.FreeSlots,Is.EqualTo(1));Assert.That(j.Context.InfluencePool,Is.Zero);
            j.EndTurn();j.BeginTurn();Assert.That(j.Player.Units.Single(),Is.SameAs(unit));Assert.That(unit.Banner,Is.SameAs(card));
            var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Advance();b.Advance();CardUnitActions.Use(j.Player,j.Context,unit,0);
            Assert.That(unit.CommandBannerFaceDown,Is.True);Assert.That(unit.IsReady,Is.False);
        }
        [TestCase(false)][TestCase(true)]public void BurnCommandUsesActualOfferAndAtomicDisbandWhenFull(bool full)
        {
            var j=Start(false,"items_017");var card=Card(j,"items_017");var old=new UnitState(CardJsonLoader.LoadUnit("army_2_001"));
            if(full)j.Player.Units.Add(old);var offer=j.Game.Globals.Units.Offer.Single(u=>u.Id=="army_2_006");
            if(full)Assert.Throws<InvalidOperationException>(()=>j.PlaySpecialArtifact(card,true,offer));
            j.PlaySpecialArtifact(card,true,offer,full?old:null);
            Assert.That(j.Player.Units.Single().Card,Is.SameAs(offer));Assert.That(j.Player.Fame,Is.EqualTo(2));Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(card));
            Assert.That(old.IsDestroyed,Is.EqualTo(full));
        }
        [TestCase(false)][TestCase(true)]public void HeadbandUsesActualUnchosenLevelRewardSkillAndMovementConsumer(bool burn)
        {
            var j=Start(true,"items_022","basic_card_021","basic_card_000");j.Player.Mana.AddCrystal(ManaColor.Red);
            var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_007")});b.Advance();b.Advance();b.Advance();b.Play(Card(j,"basic_card_021"),"strong");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Progression.Pending,Is.True);j.ChooseLevelReward(j.Progression.Choices[1],j.Progression.Actions[0]);j.BeginTurn();
            var skill=j.Game.Globals.Skills.Offer.Single();Assert.That(skill.SourceId,Is.EqualTo("hero_skill_pool_007:00"));var band=Card(j,"items_022");
            j.PlaySpecialArtifact(band,burn,skill:skill);
            if(burn){Assert.That(j.Game.Globals.Skills.Offer,Is.Empty);Assert.That(j.Player.Skills.Any(s=>ReferenceEquals(s,skill)),Is.True);SkillActions.Use(j.Player,j.Context,skill);}
            else Assert.That(j.Game.Globals.Skills.Offer.Single(),Is.SameAs(skill));
            Assert.That(j.Context.MovementPool,Is.EqualTo(burn?2:4));j.Move(new AxialCoord(1,0));Assert.That(j.Context.MovementPool,Is.EqualTo(burn?0:2));
            Assert.That(j.Player.Deck.RemovedCards.Contains(band),Is.EqualTo(burn));
        }
        [TestCase(false)][TestCase(true)]public void ReplaySeedHeadbandUsesRealLevelRewardAndPreservesOwnership(bool burn)
        {
            var j=StartWithSkillShuffle(0,true,new Random(0),"basic_card_000","basic_card_021","items_022");
            j.Player.Mana.AddCrystal(ManaColor.Red);
            var band=Card(j,"items_022");var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_007")});
            b.Advance();b.Advance();b.Advance();b.Play(Card(j,"basic_card_021"),"strong");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Player.Fame,Is.EqualTo(3));Assert.That(j.Player.Level,Is.EqualTo(1));
            Assert.That(j.Progression.Choices.Select(s=>s.SourceId),Is.EqualTo(new[]{"hero_skill_pool_007:00","hero_skill_pool_007:04"}));
            j.ChooseLevelReward(j.Progression.Choices[1],j.Progression.Actions[0]);
            var publicSkill=j.Game.Globals.Skills.Offer.Single();j.BeginTurn();
            j.PlaySpecialArtifact(band,burn,skill:publicSkill);
            if(burn)j.UseOutsideSkill(publicSkill,0);
            Assert.That(j.Context.MovementPool,Is.EqualTo(burn?2:4));
            Assert.That(j.Player.Skills.Contains(publicSkill),Is.EqualTo(burn));
            Assert.That(j.Game.Globals.Skills.Offer.Contains(publicSkill),Is.EqualTo(!burn));
            j.Move(new AxialCoord(1,0));Assert.That(j.Context.MovementPool,Is.EqualTo(burn?0:2));
            Assert.That(j.Player.Deck.RemovedCards.Contains(band),Is.EqualTo(burn));
            j.EndTurn();Assert.That(j.CompletedTurns,Is.EqualTo(2));
            Assert.That(j.Player.Deck.DiscardPile.Contains(band),Is.EqualTo(!burn));
            Assert.That(j.Player.Level,Is.EqualTo(2));
        }
        [TestCase(false)][TestCase(true)]public void BoxBorrowsActualResourceAndReturnsOriginalWhilePhysicalBoxMoves(bool burn)
        {
            var j=Start(false,"items_024","basic_card_021");var box=Card(j,"items_024");var original=new ArtifactCard("items_004");j.Context.ArtifactSupply.SetDeck(new[]{original});
            j.RevealBox(box);Assert.That(j.Context.MysticBox.RevealedArtifact,Is.SameAs(original));Assert.That(j.Player.Deck.PlayedCards.Single(),Is.SameAs(box));
            j.PlaySpecialArtifact(box,burn);Assert.That(j.Player.Fame,Is.EqualTo(burn?1:2));
            var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Advance();b.Advance();b.Play(Card(j,"basic_card_021"),"strong");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Context.ArtifactSupply.Draw(),Is.SameAs(original));Assert.That(j.Context.MysticBox,Is.Null);Assert.That(j.Player.Deck.RemovedCards.Contains(box),Is.EqualTo(burn));
            Assert.That(j.Player.Deck.DiscardPile.Contains(box),Is.EqualTo(!burn));
        }
        [Test]public void UnusedBoxReturnsToActualHandAndCannotBeUsedTwice()
        {
            var j=Start(false,"items_024");var box=Card(j,"items_024");j.Context.ArtifactSupply.SetDeck(new[]{new ArtifactCard("items_005")});j.RevealBox(box);
            j.EndTurn();Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(box));Assert.That(j.Context.ArtifactSupply.Count,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>j.PlaySpecialArtifact(box,false));
        }
        [Test]public void BoxMaterialStaffConsumesMaterialButNeverCreatesOwnedBorrowedCard()
        {
            var j=Start(false,"items_024","basic_card_014");var box=Card(j,"items_024");var original=new ArtifactCard("items_021");j.Context.ArtifactSupply.SetDeck(new[]{original});j.RevealBox(box);
            j.PlayMaterialArtifact(box,false,Card(j,"basic_card_014"),first:1,crystal:ManaColor.White);
            Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(2));Assert.That(j.Player.Deck.PlayedCards.Single(),Is.SameAs(box));Assert.That(j.Player.Fame,Is.EqualTo(1));
            j.EndTurn();Assert.That(j.Context.ArtifactSupply.Draw(),Is.SameAs(original));Assert.That(j.Player.Deck.DiscardPile.Any(c=>c.Id=="items_021"),Is.False);
        }
        [Test]public void ActualSeedBoxHonorRewardsSameCommandedUnitThenReturnsSupply()
        {
            var j=StartSeed(0,false,"basic_card_000","basic_card_007","basic_card_014","basic_card_021","items_017","items_022","items_024");
            j.PlayOutsideBattle(Card(j,"basic_card_000"),"base");var flag=Card(j,"items_017");j.PlaySpecialArtifact(flag,false);j.SelectCommandBanner(flag);j.BeginVillageInteraction();j.RecruitVillageUnit(j.Game.Globals.Units.Offer.Single(u=>u.Id=="army_2_006"));j.EndTurn();j.BeginTurn();
            var box=Card(j,"items_024");j.RevealBox(box);Assert.That(j.ArtifactEffectId(box),Is.EqualTo("items_000"));j.PlaySpecialArtifact(box,true);
            Assert.That(j.Player.Fame,Is.EqualTo(1));var unit=j.Player.Units.Single();var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Advance();b.Advance();CardUnitActions.Use(j.Player,j.Context,unit,0);
            Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(3));
            b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(5));b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Player.Fame,Is.EqualTo(4));Assert.That(j.Context.ArtifactSupply.Count,Is.EqualTo(22));Assert.That(unit.Banner,Is.SameAs(flag));Assert.That(unit.CommandBannerFaceDown,Is.True);Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(box));
        }
        [Test]public void BoxGrailRejectsBattleHealingBeforeAnyUseOrFame()
        {
            var j=Start(false,"items_024");var box=Card(j,"items_024");j.Context.ArtifactSupply.SetDeck(new[]{new ArtifactCard("items_012")});j.RevealBox(box);j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});
            Assert.Throws<InvalidOperationException>(()=>j.PlaySpecialArtifact(box,false));Assert.That(j.Context.MysticBox.Used,Is.False);Assert.That(j.Context.Healing.Total,Is.Zero);Assert.That(j.Player.Fame,Is.Zero);
        }
    }
}
