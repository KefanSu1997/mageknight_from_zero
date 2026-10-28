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
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveSacrificeSummonsEntryTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(string unit="army_2_006",params string[] ids)
        {
            var p=new PlayerState();foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:id.StartsWith("items_")?CardType.Artifact:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,unitDeck:new[]{CardJsonLoader.LoadUnit(unit)},supplyRandom:new Random(0));j.Game.Clock.NextRound();j.SelectTactic("night_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.First(c=>c.Id==id);
        private static MK.Logic.Data.Monster Enemy()=>CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032") with{};
        [Test]public void SacrificeExactMaterialsAndIndependentArtifactColorsPayNextOriginalSpell()
        {
            var j=Start(ids:new[]{"magic_011","basic_card_000","items_008","items_019","magic_014"});var p=j.Player;p.Mana.AddCrystal(ManaColor.Red);
            var materials=p.Deck.Hand.Skip(1).Take(3).ToArray();j.PlayOutsideWithInputs(Card(j,"magic_011"),"base",new PveDeedSelection{Cards=materials,BonusChoices=new[]{(int)ManaColor.White,(int)ManaColor.Blue}});
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(3));foreach(var c in materials)Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(c,x)),Is.True);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(1));
            j.PlayOutsideBattle(Card(j,"magic_014"),"base");Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue),Is.Zero);j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.EqualTo(2));
        }
        [TestCase(ManaColor.Green,ManaColor.Red,4,1,AttackType.Siege,Element.Fire)]
        [TestCase(ManaColor.Green,ManaColor.Blue,8,2,AttackType.Siege,Element.Ice)]
        [TestCase(ManaColor.White,ManaColor.Red,6,1,AttackType.Ranged,Element.Fire)]
        [TestCase(ManaColor.White,ManaColor.Blue,12,2,AttackType.Ranged,Element.Ice)]
        public void SacrificeCountsPairsAfterPrintedPaymentAndConsumesActualTypedAttack(ManaColor first,ManaColor second,int power,int pairs,AttackType type,Element element)
        {
            var j=Start(ids:new[]{"magic_011","basic_card_008"});var p=j.Player;foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(c,2);p.Mana.AddToken(ManaColor.Black);
            var b=j.BeginBattle(new[]{Enemy()});b.Play(Card(j,"magic_011"),"strong",inputs:new PveDeedSelection{BonusChoices=new[]{(int)first,(int)second}});
            var attack=b.Context.CombatPower.Attacks.Single();Assert.That(attack.Value,Is.EqualTo(power));Assert.That(attack.Type,Is.EqualTo(type));Assert.That(attack.Element,Is.EqualTo(element));Assert.That(p.Mana.Tokens[first],Is.EqualTo(pairs));Assert.That(p.Mana.Tokens[second],Is.EqualTo(pairs));
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(2));Assert.That(b.Context.RangedPool+b.Context.SiegePool,Is.Zero);
        }
        [Test]public void SacrificeRejectsSelfMaterialAndInvalidColorsBeforePayment()
        {
            var j=Start(ids:new[]{"magic_011","items_008"});var p=j.Player;p.Mana.AddCrystal(ManaColor.Red);var card=Card(j,"magic_011");
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"base",new PveDeedSelection{Cards=new[]{card}}));
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"base",new PveDeedSelection{Cards=new[]{Card(j,"items_008")}}));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [Test]public void OfferedFarmerRemainsInSupplyAndCannotAttackBeforeMeleeThenHitsActualEnemy()
        {
            var j=Start(ids:new[]{"magic_020","basic_card_021"});var p=j.Player;p.Mana.AddCrystal(ManaColor.White);j.PlayOutsideBattle(Card(j,"magic_020"),"base");var unit=j.Context.UnitOffer.Single();var b=j.BeginBattle(new[]{Enemy()});
            Assert.Throws<InvalidOperationException>(()=>j.UseOfferedUnit(unit,0));Assert.That(j.Context.OfferedUnitUses,Is.EqualTo(1));b.Advance();b.Advance();b.Advance();
            j.UseOfferedUnit(unit,0);Assert.That(j.Context.MeleePool,Is.EqualTo(2));Assert.That(j.Context.UnitOffer.Single(),Is.SameAs(unit));Assert.That(p.Units,Is.Empty);Assert.That(j.Context.OfferedUnitUses,Is.Zero);
            b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();Assert.That(j.Game.Globals.Units.Offer.Single(),Is.SameAs(unit));
        }
        [Test]public void OfferedHerbalistReadiesActualWoundedOwnedUnitWithoutHealingIt()
        {
            var j=Start("army_2_002","magic_020","basic_card_008");var p=j.Player;var own=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));own.Exhaust();own.AddWounds(1);p.Units.Add(own);p.Mana.AddCrystal(ManaColor.White);p.Mana.AddCrystal(ManaColor.Green);
            j.PlayOutsideBattle(Card(j,"magic_020"),"base");var offer=j.Context.UnitOffer.Single();int ability=Array.FindIndex(offer.Actions,a=>a.Kind==UnitAbilityKind.ReadyUnit);
            j.UseOfferedUnit(offer,ability,unitTarget:own);Assert.That(own.IsReady,Is.True);Assert.That(own.Wounds,Is.EqualTo(1));Assert.That(own.CanActivate,Is.False);Assert.That(j.Context.OfferedUnitUses,Is.Zero);
        }
        [Test]public void HonorSummonsValidatesFullSlotBeforePaymentThenReplacesAndActivatesActualUnit()
        {
            var j=Start(ids:new[]{"magic_020","basic_card_008"});var p=j.Player;var old=new UnitState(CardJsonLoader.LoadUnit("army_2_002"));p.Units.Add(old);p.Mana.AddCrystal(ManaColor.White);p.Mana.AddToken(ManaColor.Black);var offer=j.Context.UnitOffer.Single();var card=Card(j,"magic_020");
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"strong",new PveDeedSelection{OfferedUnit=offer}));Assert.That(p.Mana.Crystals[ManaColor.White],Is.EqualTo(1));
            j.PlayOutsideWithInputs(card,"strong",new PveDeedSelection{OfferedUnit=offer,DisbandUnit=old});Assert.That(old.IsDestroyed,Is.True);Assert.That(j.Context.UnitOffer,Is.Empty);var gained=p.Units.Single();Assert.That(gained.Card,Is.SameAs(offer));
            var b=j.BeginBattle(new[]{Enemy()});b.Advance();b.Advance();b.Advance();b.UseUnit(gained,0);b.PlaySideways(Card(j,"basic_card_008"));Assert.That(b.Attack(0).Kills,Is.EqualTo(1));Assert.That(gained.IsReady,Is.False);
        }
    }
}
