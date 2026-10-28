using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveMistTravelEntryTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        [Test]public void MistPaysPrintedBlueCrossesActualLakeAndCannotEnterHillsThenExpires()
        {
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();var lake=new AxialCoord(1,0);var hills=new AxialCoord(0,1);var plains=new AxialCoord(0,0);
            map.Placed[plains]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[lake]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Lake});map.Placed[hills]=new MapTile(TileSet.Countryside,2,new[]{TerrainType.Hills});
            var p=new PlayerState();var mist=new DeedCard("magic_014",CardType.Spell);p.Deck.Hand.Add(mist);p.Deck.Hand.Add(new DeedCard("basic_card_001",CardType.Action));p.Mana.AddCrystal(ManaColor.Blue);
            var j=new PveJourneySession(p,cards,map:map,spells:spells);j.SelectTactic("day_1");j.BeginTurn();j.PlayOutsideBattle(mist,"base");
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(j.MovementCost(lake),Is.EqualTo(2));Assert.That(j.MovementCost(hills),Is.EqualTo(int.MaxValue));
            Assert.Throws<InvalidOperationException>(()=>j.Move(hills));Assert.That(j.Context.MovementPool,Is.EqualTo(4));
            j.Move(lake);j.Move(plains);Assert.That(j.Context.MovementPool,Is.Zero);j.EndTurn();j.BeginTurn();Assert.That(j.MovementCost(lake),Is.EqualTo(int.MaxValue));Assert.That(j.MovementCost(hills),Is.EqualTo(3));
        }
        [Test]public void MistShroudPaysBlueBlackAndProtectsActualFarmerThenHeroFirstWound()
        {
            var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_014");var p=new PlayerState();var card=new DeedCard(spell.Id,CardType.Spell);p.Deck.Hand.Add(card);p.Mana.AddCrystal(ManaColor.Blue);p.Mana.AddToken(ManaColor.Black);
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(unit);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b=new PveBattleSession(p,new[]{enemy,enemy with{}},new[]{new ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost)},new ActionContext{DayPart=DayPart.Night},spells:new[]{spell});
            b.Play(card,"strong");Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);b.Advance();b.Advance();b.PlanDamage(0,0,unit);b.Advance();
            Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(b.Context.IgnoreNextWound,Is.False);
        }
    }
}
