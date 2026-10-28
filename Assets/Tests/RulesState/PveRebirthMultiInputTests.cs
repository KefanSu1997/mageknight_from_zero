using System;
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
    public sealed class PveRebirthMultiInputTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PveJourneySession j,DeedCard book,DeedCard pay,SpellCard spell,UnitState[] units) Start(TerrainType terrain)
        {
            var p=new PlayerState{ExtraCommandSlots=2};
            var book=new DeedCard("items_023",CardType.Artifact);var pay=new DeedCard("basic_card_000",CardType.Action);
            p.Deck.Hand.AddRange(new[]{book,pay});
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{terrain});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,supplyRandom:new Random(0));
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();
            var units=new[]{new UnitState(CardJsonLoader.LoadUnit("army_2_006")),new UnitState(CardJsonLoader.LoadUnit("army_2_001")),new UnitState(CardJsonLoader.LoadUnit("army_2_001"))};
            foreach(var u in units){p.Units.Add(u);u.Exhaust();}
            var spell=new SpellCard("magic_005");j.Context.SpellSupply.Offer.Clear();j.Context.SpellSupply.Offer.Add(spell);
            return(j,book,pay,spell,units);
        }
        [TestCase(TerrainType.Plains,2,3,4)][TestCase(TerrainType.Forest,3,5,6)]
        public void TomeRebirthUsesOriginalTotalLevelsAndUnitsActuallyBlock(TerrainType terrain,int count,int healing,int block)
        {
            var(j,book,pay,spell,units)=Start(terrain);var selected=units.Take(count).ToArray();
            j.PlayMaterialArtifact(book,true,pay,spell:spell,spellChoice:"strong",inputs:new PveDeedSelection{Units=selected});
            foreach(var u in selected)Assert.That(u.IsReady,Is.True);
            if(count<3)Assert.That(units[2].IsReady,Is.False);
            Assert.That(j.Context.Healing.Total,Is.EqualTo(healing));Assert.That(j.Player.Mana.GetTotal(),Is.Zero);
            Assert.That(j.Player.Deck.DiscardPile.Single(),Is.SameAs(pay));Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(book));
            Assert.That(j.Context.ReadyUnitTargets,Is.Null);Assert.That(j.Context.TargetUnit,Is.Null);
            var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();
            foreach(var u in selected)b.UseUnit(u,0,1);
            Assert.That(j.Context.BlockPool,Is.EqualTo(block));b.Block(0);
            foreach(var u in selected)Assert.That(u.IsReady,Is.False);
            Assert.That(j.Context.BlockPool,Is.Zero);Assert.That(j.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));
        }
        [TestCase("duplicate")][TestCase("foreign")][TestCase("levels")]
        public void InvalidUnitsRejectBeforeTomeOrGreenPaymentMoves(string failure)
        {
            var(j,book,pay,spell,units)=Start(TerrainType.Plains);
            var selected=failure=="duplicate"?new[]{units[0],units[0]}:failure=="foreign"?new[]{units[0],new UnitState(units[1].Card)}:units;
            var old=new System.Collections.Generic.List<UnitState>{units[0]};j.Context.ReadyUnitTargets=old;j.Context.TargetUnit=units[0];
            Assert.Throws<InvalidOperationException>(()=>j.PlayMaterialArtifact(book,true,pay,spell:spell,spellChoice:"strong",inputs:new PveDeedSelection{Units=selected}));
            Assert.That(j.Player.Deck.Hand,Is.EqualTo(new[]{book,pay}));Assert.That(j.Player.Deck.DiscardPile,Is.Empty);Assert.That(j.Player.Deck.RemovedCards,Is.Empty);
            Assert.That(j.Context.Healing.Total,Is.Zero);Assert.That(j.Context.ReadyUnitTargets,Is.SameAs(old));Assert.That(j.Context.TargetUnit,Is.SameAs(units[0]));
            Assert.That(units.All(u=>!u.IsReady),Is.True);Assert.That(j.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));
        }
        [Test]public void OrdinaryTomeRestoreOnlyHealsAndExplicitEmptyStrongLeavesUnitsExhausted()
        {
            foreach(bool strong in new[]{false,true})
            {
                var(j,book,pay,spell,units)=Start(TerrainType.Plains);j.Player.Deck.GainWound();
                j.PlayMaterialArtifact(book,strong,pay,spell:spell,spellChoice:strong?"strong":"base",unit:units[0],inputs:new PveDeedSelection{Units=Array.Empty<UnitState>()});
                Assert.That(units.All(u=>!u.IsReady),Is.True);j.HealOutsideBattle();Assert.That(j.Player.Wounds,Is.Zero);
                Assert.That(j.Context.Healing.Total,Is.EqualTo(2));
            }
        }
    }
}
