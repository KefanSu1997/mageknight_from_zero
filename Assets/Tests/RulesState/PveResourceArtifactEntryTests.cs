using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using Monster=MK.Logic.Data.Monster;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveResourceArtifactEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState();
            foreach(string id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var s=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),spells:spells);
            s.PrepareRound();s.SelectTactic("day_1");s.BeginTurn();return s;
        }
        private static DeedCard Card(PveJourneySession s,string id)=>s.Player.Deck.Hand.First(c=>c.Id==id);
        private static Monster Enemy()=>CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
        private sealed class GoldRolls:Random { public override int Next(int n) { Assert.That(n,Is.EqualTo(6));return (int)ManaColor.Gold; } }

        [TestCase("items_004",ManaColor.Red)] [TestCase("items_005",ManaColor.Blue)]
        [TestCase("items_006",ManaColor.White)] [TestCase("items_007",ManaColor.Green)]
        public void OriginalRingOutsideKeepsCrystalButClearsTemporaryTokenAtFormalEnd(string id,ManaColor color)
        {
            var s=Start(id);var card=Card(s,id);s.PlayOutsideArtifact(card,false);
            Assert.That(s.Player.Mana.Tokens[color],Is.EqualTo(1));Assert.That(s.Player.Mana.Crystals[color],Is.EqualTo(1));
            Assert.That(s.Player.Fame,Is.EqualTo(1));Assert.That(s.Player.Deck.PlayedCards.Single(),Is.SameAs(card));
            s.EndTurn();Assert.That(s.Player.Mana.Tokens.Values.Sum(),Is.Zero);Assert.That(s.Player.Mana.Crystals[color],Is.EqualTo(1));
            Assert.That(s.Player.Deck.DiscardPile.Single(),Is.SameAs(card));
        }
        [TestCase("items_004",ManaColor.Red,"magic_009")]
        [TestCase("items_005",ManaColor.Blue,"magic_015")]
        [TestCase("items_006",ManaColor.White,"magic_022")]
        [TestCase("items_007",ManaColor.Green,"magic_003")]
        public void BurnedRingPaysActualOriginalSpellAndExpiresNextTurn(string id,ManaColor color,string spell)
        {
            var s=Start(id,spell,"basic_card_000");var ring=Card(s,id);s.PlayOutsideArtifact(ring,true);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(ring));
            var b=s.BeginBattle(new[]{Enemy()});b.Play(Card(s,spell),"base",0);
            Assert.That(s.Player.Fame,Is.EqualTo(1));Assert.That(s.Player.Mana.Tokens.Values.Sum(),Is.Zero);
            Assert.That(s.Context.InfiniteMana.Contains(color),Is.True);Assert.That(s.Context.InfiniteMana.Contains(ManaColor.Black),Is.True);
            b.Advance();b.Advance();b.Advance();b.Advance();s.EndTurn();s.BeginTurn();
            Assert.That(s.Context.InfiniteMana,Is.Empty);Assert.That(s.Context.FamePerSpell,Is.Empty);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(ring));
        }
        [Test] public void DarkAmuletAllowsActualDayLowerSpellAndPreservesThreeIndependentColors()
        {
            var s=Start("items_015","magic_007");var a=Card(s,"items_015");
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideArtifact(a,true,null,ManaColor.Red));
            Assert.That(s.Player.Deck.Hand.Contains(a),Is.True);Assert.That(s.Player.Mana.Tokens,Is.Empty);
            s.PlayOutsideArtifact(a,true,null,ManaColor.Red,ManaColor.Black,ManaColor.White);
            var b=s.BeginBattle(new[]{Enemy()});b.Advance();b.Advance();b.Advance();
            b.Play(Card(s,"magic_007"),"strong",0);
            Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
            Assert.That(s.Player.Mana.Tokens[ManaColor.White],Is.EqualTo(1));Assert.That(s.Player.Fame,Is.EqualTo(2));
            b.Advance();s.EndTurn();s.BeginTurn();Assert.That(s.Context.AllowBlackAtDay,Is.False);
        }
        [Test] public void GoldBagChoiceIsValidatedBeforeRollAndEachGoldUsesOwnColor()
        {
            var s=Start("items_011");var a=Card(s,"items_011");s.Context.EffectRandom=new GoldRolls();
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideArtifact(a,false,null,ManaColor.Black,ManaColor.Blue));
            Assert.That(s.Context.LastManaRolls,Is.Empty);Assert.That(s.Player.Deck.Hand.Single(),Is.SameAs(a));
            s.PlayOutsideArtifact(a,false,null,ManaColor.Green,ManaColor.White);
            Assert.That(s.Context.LastManaRolls,Is.EqualTo(new[]{ManaColor.Gold,ManaColor.Gold}));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(s.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));
            s.EndTurn();Assert.That(s.Player.Mana.Crystals.Values.Sum(),Is.EqualTo(2));
        }
        [Test] public void BurnedGemBagTokensActuallyPayBothIndependentOriginalSpells()
        {
            var s=Start("items_011","magic_009","magic_015");var b=s.BeginBattle(new[]{Enemy(),Enemy() with{}});
            b.PlayResourceArtifact(Card(s,"items_011"),true);
            Assert.That(s.Player.Mana.Tokens.Values.Sum(),Is.EqualTo(5));Assert.That(s.Context.LastManaRolls,Is.Empty);
            b.Play(Card(s,"magic_009"),"base");b.Attack(0);b.Play(Card(s,"magic_015"),"base");b.Attack(1);
            Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue),Is.Zero);
            Assert.That(s.Player.Fame,Is.EqualTo(4));Assert.That(s.Player.Deck.RemovedCards.Single().Id,Is.EqualTo("items_011"));
        }
        [Test] public void SunTokenPaysOriginalSpellAndOutsideGrailHealsActualWound()
        {
            var s=Start("items_014","magic_009","items_012");s.PlayOutsideArtifact(Card(s,"items_014"),false);
            s.ConvertGold(ManaColor.Red);
            var b=s.BeginBattle(new[]{Enemy()});b.Play(Card(s,"magic_009"),"base");b.Attack(0);
            Assert.That(s.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.Zero);
            b.Advance();b.Advance();b.Advance();b.Advance();
            var wound=new DeedCard("wound",CardType.Wound);s.Player.Deck.Hand.Add(wound);
            s.PlayOutsideArtifact(Card(s,"items_012"),false);s.HealOutsideBattle();
            Assert.That(s.Player.Wounds,Is.Zero);Assert.That(s.Context.Healing.Total,Is.EqualTo(1));Assert.That(s.Player.Fame,Is.EqualTo(3));
        }
        [TestCase(false,4,2)] [TestCase(true,9,3)]
        public void PouchOutsideUsesExactCardAndActualInfluencePool(bool burn,int influence,int fame)
        {
            var s=Start("items_010");var a=Card(s,"items_010");s.PlayOutsideArtifact(a,burn);
            Assert.That(s.Context.InfluencePool,Is.EqualTo(influence));Assert.That(s.Player.Fame,Is.EqualTo(fame));
            Assert.That((burn?s.Player.Deck.RemovedCards:s.Player.Deck.PlayedCards).Single(),Is.SameAs(a));
            s.EndTurn();Assert.That(s.Context.InfluencePool,Is.Zero);
        }
        [Test] public void ProtectionAttachesSameCardToOwnedUnitBeforeBattle()
        {
            var s=Start("items_002");var a=Card(s,"items_002");var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));s.Player.Units.Add(unit);
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideArtifact(a,false,new UnitState(unit.Card)));
            s.PlayOutsideArtifact(a,false,unit);Assert.That(unit.Banner,Is.SameAs(a));Assert.That(BannerActions.ProtectionArmor(unit),Is.EqualTo(1));
            Assert.That(BannerActions.ProtectionResists(unit,Element.Fire),Is.True);Assert.That(s.Context.TargetUnit,Is.Null);
            s.EndTurn();Assert.That(unit.Banner,Is.SameAs(a));Assert.That(s.Player.Deck.Hand,Is.Empty);
        }
        [Test] public void PouchBuysActualFarmerThenSameProtectedUnitAbsorbsDamageNextTurn()
        {
            var s=Start("items_010","items_002","items_011","items_014","basic_card_021");var p=s.Player;
            s.Game.Map.Placed.Add(p.Position,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            s.Game.Map.Places.Add(p.Position,new PlaceState(PlaceType.Village));
            var offered=CardJsonLoader.LoadUnit("army_2_006");s.Game.Globals.Units.Offer.Add(offered);
            s.BeginVillageInteraction();s.PlayOutsideArtifact(Card(s,"items_010"),false);s.RecruitVillageUnit(offered);
            var unit=p.Units.Single();Assert.That(unit.Card,Is.SameAs(offered));Assert.That(s.Context.InfluencePool,Is.Zero);
            var flag=Card(s,"items_002");s.PlayOutsideArtifact(flag,false,unit);s.EndTurn();s.BeginTurn();
            s.PlayOutsideArtifact(Card(s,"items_011"),true);s.PlayOutsideArtifact(Card(s,"items_014"),false);s.ConvertGold(ManaColor.Red);
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(2));Assert.That(p.Mana.Tokens[ManaColor.Gold],Is.EqualTo(1));
            var b=s.BeginBattle(new[]{Enemy()});b.Advance();b.Advance();b.PlanDamage(0,0,unit);b.Advance();
            Assert.That(p.Wounds,Is.Zero);Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(unit.Banner,Is.SameAs(flag));
            b.Play(Card(s,"basic_card_021"),"strong");Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();s.EndTurn();
            Assert.That(p.Units.Single(),Is.SameAs(unit));Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(p.Mana.Tokens.Values.Sum(),Is.Zero);
        }
    }
}
