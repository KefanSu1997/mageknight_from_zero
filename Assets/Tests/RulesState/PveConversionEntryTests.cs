using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveConversionEntryTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState();foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(color,2);
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,manaRandom:new Random(0),supplyRandom:new Random(0));j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.Single(c=>c.Id==id);
        private static MK.Logic.Data.Monster Enemy()=>CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
        private static void Play(PveJourneySession j,string id,string choice)=>j.PlayOutsideBattle(Card(j,id),choice);

        [TestCase(false)][TestCase(true)]public void AgilitySpendsSharedMovementAndActualAttackBeforeNextTurnExpiry(bool strong)
        {
            var j=Start("advanced_card_006","basic_card_000");var b=j.BeginBattle(new[]{Enemy()});b.Play(Card(j,"advanced_card_006"),strong?"strong":"base");b.Play(Card(j,"basic_card_000"),"base");
            Assert.That(j.Context.MovementPool,Is.EqualTo(strong?6:4));Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(strong?1:2));
            Assert.Throws<InvalidOperationException>(()=>b.ConvertMovement(false));
            if(strong){b.ConvertMovement(true);Assert.That(j.Context.MovementPool,Is.EqualTo(4));Assert.That(b.Attack(0).Kills,Is.Zero);Assert.That(j.Context.RangedPool,Is.Zero);}
            else Assert.Throws<InvalidOperationException>(()=>b.ConvertMovement(true));
            b.Advance();Assert.Throws<InvalidOperationException>(()=>b.ConvertMovement(false));b.Advance();b.Advance();b.ConvertMovement(false,4);
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.Throws<InvalidOperationException>(()=>b.ConvertMovement(false));Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();j.BeginTurn();
            Assert.That(j.Context.MoveCostAttack,Is.Zero);Assert.That(j.Context.MoveCostRanged,Is.Zero);Assert.Throws<InvalidOperationException>(()=>b.ConvertMovement(false));
        }
        [TestCase("base",Element.Physical)][TestCase("strong_ice",Element.Ice)][TestCase("strong_fire",Element.Fire)]
        public void DiplomacyConsumesActualInfluenceAsTypedBlock(string choice,Element element)
        {
            var j=Start("advanced_card_018","basic_card_008");if(choice=="base")j.Player.Deck.Hand.Add(new DeedCard("basic_card_014",CardType.Action));var b=j.BeginBattle(new[]{Enemy()});b.Play(Card(j,"advanced_card_018"),choice);if(choice=="base")b.Play(Card(j,"basic_card_008"),"base");
            Assert.That(j.Context.InfluencePool,Is.EqualTo(4));Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(choice=="base"?2:1));
            Assert.Throws<InvalidOperationException>(()=>b.ConvertInfluence());b.Advance();b.ConvertInfluence(4);
            Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Context.CombatPower.Blocks.Single().Element,Is.EqualTo(element));Assert.Throws<InvalidOperationException>(()=>b.ConvertInfluence());b.Block(0);b.Advance();b.Advance();
            Assert.That(j.Player.Wounds,Is.Zero);b.Advance();j.EndTurn();j.BeginTurn();Assert.That(j.Context.InfluenceAsBlock,Is.False);Assert.Throws<InvalidOperationException>(()=>b.ConvertInfluence());
        }
        [TestCase(false)][TestCase(true)]public void PeacefulHealingSpendsInfluenceAndExcludesBattleAndMovement(bool strong)
        {
            var j=Start("advanced_card_035","basic_card_000","basic_card_014");j.Player.Wounds=1;var wound=j.Player.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            Play(j,"advanced_card_035",strong?"strong":"base");Assert.That(j.Context.TurnActionCommitted,Is.False);j.ConvertPeacefulHealing();Assert.That(j.Context.InfluencePool,Is.EqualTo(strong?4:1));
            Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(strong?1:2));j.HealOutsideBattle();Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Player.Deck.DiscardPile.Contains(wound),Is.False);
            Play(j,"basic_card_000","base");Assert.Throws<InvalidOperationException>(()=>j.Move(new AxialCoord(0,1)));Assert.Throws<InvalidOperationException>(()=>j.BeginBattle(new[]{Enemy()}));
            j.EndTurn();j.BeginTurn();Assert.Throws<InvalidOperationException>(()=>j.ConvertPeacefulHealing());Assert.That(j.Context.PeacefulMomentAvailable,Is.False);
        }
        [Test]public void PeacefulReadyUsesActualUnitLevelAndDoesNotHealItsWound()
        {
            var j=Start("advanced_card_035");var unit=new UnitState(CardJsonLoader.LoadUnit("army_003"));j.Player.Units.Add(unit);unit.Exhaust();unit.AddWounds(1);
            Play(j,"advanced_card_035","strong");j.ReadyWithInfluence(unit,true);Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(unit.IsReady,Is.True);Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(unit.CanActivate,Is.False);
            Assert.That(j.Context.PeacefulMomentAction,Is.True);Assert.Throws<InvalidOperationException>(()=>j.ReadyWithInfluence(unit,true));
        }
        [Test]public void ContinuousOriginalCrystalAgilityDiplomacyBattleAndNextTurnHealing()
        {
            var j=Start("basic_card_008","basic_card_014","basic_card_015","basic_card_021","advanced_card_006","advanced_card_018","advanced_card_035");
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.Crystals[color]=color==ManaColor.Red||color==ManaColor.Blue?1:0;
            Play(j,"basic_card_015","strong_White");Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);
            var die=j.Context.ManaSource.Dice[0];Assert.That(die.Face,Is.EqualTo(ManaColor.White));CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.White);
            var b=j.BeginBattle(new[]{Enemy(),Enemy()});b.Play(Card(j,"advanced_card_006"),"strong");b.ConvertMovement(true);Assert.That(b.Attack(0).Kills,Is.Zero);Assert.That(j.Context.MovementPool,Is.EqualTo(2));b.Advance();
            b.Play(Card(j,"advanced_card_018"),"strong_ice");b.ConvertInfluence(4);b.Block(0);b.Advance();b.Advance();Assert.That(j.Player.Wounds,Is.EqualTo(2));
            b.ConvertMovement(false,2);b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();Assert.That(j.Player.Deck.Hand.Count,Is.EqualTo(5));
            j.BeginTurn();Play(j,"advanced_card_035","base");Play(j,"basic_card_008","base");j.ConvertPeacefulHealing(2);j.HealOutsideBattle();j.HealOutsideBattle();
            Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.InfluencePool,Is.EqualTo(1));Assert.That(j.Context.PeacefulMomentAction,Is.True);Assert.Throws<InvalidOperationException>(()=>j.BeginBattle(new[]{b.Enemies[1]}));
            j.EndTurn();Assert.That(j.Player.Deck.Hand.Single().Id,Is.EqualTo("basic_card_014"));Assert.That(j.Player.Deck.DiscardPile.Count,Is.EqualTo(6));Assert.That(j.CompletedBattles,Is.EqualTo(1));Assert.That(j.CompletedTurns,Is.EqualTo(2));
        }
    }
}
