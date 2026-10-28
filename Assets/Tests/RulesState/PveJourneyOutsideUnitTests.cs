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
    public sealed class PveJourneyOutsideUnitTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static UnitState Add(PveJourneySession j,string id)
        {var u=new UnitState(CardJsonLoader.LoadUnit(id));j.Player.Units.Add(u);return u;}
        private static PveJourneySession Start(string unitId)
        {
            var p=new PlayerState{Fame=8};
            p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));
            p.Deck.Hand.Add(new DeedCard("basic_card_001",CardType.Action));
            p.Deck.Hand.Add(new DeedCard("magic_009",CardType.Spell));
            var map=new MapState();
            map.Placed[new(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});
            map.Placed[new(1,0)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Forest});
            map.Places[new(0,0)]=new PlaceState(PlaceType.Village);
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var j=new PveJourneySession(p,cards,map:map,spells:spells,manaRandom:new Random(0),tacticRandom:new Random(0));
            Add(j,unitId);j.SelectTactic("day_1");j.BeginTurn();return j;
        }

        [TestCase("army_2_000",0,2)]
        [TestCase("army_2_006",2,3)]
        [TestCase("army_2_009",2,3)]
        public void OriginalMovementEntersSharedPoolAndMovesSamePlayer(string id,int ability,int forestCost)
        {
            var j=Start(id);var u=j.Player.Units.Single();j.UseOutsideUnit(u,ability);
            Assert.That(j.Context.MovementPool,Is.EqualTo(2));Assert.That(u.IsReady,Is.False);
            Assert.That(j.MovementCost(new(1,0)),Is.EqualTo(forestCost));
            if(forestCost>2)j.SidewaysMovement(j.Player.Deck.Hand[0]);
            j.Move(new(1,0));Assert.That(j.Player.Position,Is.EqualTo(new AxialCoord(1,0)));
            Assert.That(j.Context.MovementPool,Is.Zero);
            if(id=="army_2_009")Assert.That(j.Context.ScoutExplorationRange,Is.EqualTo(2));
        }

        [TestCase("army_2_006",1,2,0)]
        [TestCase("army_2_011",2,4,-1)]
        [TestCase("army_2_003",0,4,0)]
        [TestCase("army_008",2,5,1)]
        [TestCase("army_009",2,5,1)]
        [TestCase("army_010",2,5,1)]
        [TestCase("army_011",2,5,1)]
        public void OriginalInfluenceRunsDuringRealInteractionAndBuysHealing(string id,int ability,int value,int reputation)
        {
            var j=Start(id);var u=j.Player.Units.Single();j.Player.Deck.GainWound();
            j.BeginVillageInteraction();j.UseOutsideUnit(u,ability);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(value));Assert.That(j.Player.Reputation,Is.EqualTo(reputation));
            if(value<3){j.SidewaysInfluence(j.Player.Deck.Hand[0]);value++;}
            j.BuyVillageHealing();j.HealOutsideBattle();
            Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.InfluencePool,Is.EqualTo(value-3));
            Assert.That(u.IsReady,Is.False);
        }

        [Test] public void FireMageManaBeforeBattlePaysOriginalFireballAndKeepsCrystal()
        {
            var j=Start("army_007");var u=j.Player.Units.Single();j.UseOutsideUnit(u,2);
            Assert.That(j.Player.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var battle=j.BeginBattle(new[]{enemy});
            battle.Play(j.Player.Deck.Hand.Single(c=>c.Id=="magic_009"),"base");
            Assert.That(battle.Attack(0).Kills,Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            Assert.That(j.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);
        }

        [TestCase("army_2_002",0,2)]
        [TestCase("army_010",1,4)]
        public void PrintedGreenHealingWorksAfterRestAndConsumesActualWound(string id,int ability,int points)
        {
            var j=Start(id);var u=j.Player.Units.Single();j.Player.Deck.GainWound();j.Player.Mana.AddToken(ManaColor.Green);
            j.Rest(j.Player.Deck.Hand[0]);j.UseOutsideUnit(u,ability);
            Assert.That(j.Context.Healing.Total,Is.EqualTo(points));j.HealOutsideBattle();
            Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.Healing.Total,Is.EqualTo(points-1));
            Assert.That(j.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);Assert.That(u.IsReady,Is.False);
        }

        [Test] public void HerbalistReadiesSameWoundedUnitWithoutHealingAndItRemainsAcrossTurn()
        {
            var j=Start("army_2_002");var herb=j.Player.Units.Single();var target=Add(j,"army_2_006");
            target.Exhaust();target.AddWounds(1);j.UseOutsideUnit(herb,1,unitTarget:target);
            Assert.That(target.IsReady,Is.True);Assert.That(target.Wounds,Is.EqualTo(1));Assert.That(target.CanActivate,Is.False);
            j.SidewaysInfluence(j.Player.Deck.Hand[0]);j.EndTurn();j.BeginTurn();
            Assert.That(j.Player.Units[1],Is.SameAs(target));Assert.That(target.Wounds,Is.EqualTo(1));Assert.That(herb.IsReady,Is.False);
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(herb,2));
        }

        [Test] public void ScoutPrivatelyObservesActualMapGuardWithoutPublicRevealOrSupplyDraw()
        {
            var j=Start("army_2_009");var u=j.Player.Units.Single();
            var guard=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            var site=new PlaceState(PlaceType.Keep){Enemy=guard};j.Game.Map.Places[new(1,0)]=site;
            j.UseOutsideUnit(u,1,scoutTarget:new ScoutTarget(new AxialCoord(1,0)));
            Assert.That(j.Context.LastScoutObservation.Enemy,Is.SameAs(guard));
            Assert.That(j.Game.Map.RevealedSites.Contains(site),Is.False);Assert.That(site.Enemy,Is.SameAs(guard));Assert.That(u.IsReady,Is.False);
            int fameBefore=j.Player.Fame;
            j.Player.Mana.AddCrystal(ManaColor.Red);
            var battle=j.BeginBattle(new[]{guard});
            battle.Play(j.Player.Deck.Hand.Single(c=>c.Id=="magic_009"),"base");
            Assert.That(battle.Attack(0).Kills,Is.EqualTo(1));
            Assert.That(j.Player.Fame,Is.EqualTo(fameBefore+guard.Fame+1),"同一私看守军真实击败时给予额外名望");
        }

        [Test] public void InvalidOwnedChoiceTimingFeeAndTargetsRejectBeforeExhaustion()
        {
            var j=Start("army_2_002");var herb=j.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(herb,0));
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(herb,1,unitTarget:new UnitState(CardJsonLoader.LoadUnit("army_2_006"))));
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(herb,2,unitTarget:herb));
            Assert.That(herb.IsReady,Is.True);Assert.That(j.Context.Healing.Total,Is.Zero);
            var farmer=Add(j,"army_2_006");Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(farmer,0));
            j.BeginVillageInteraction();Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(farmer,2));Assert.That(farmer.IsReady,Is.True);
        }

        [Test] public void NewFacadeRejectsCombatAndClosedTurnInsteadOfUsingOldContext()
        {
            var j=Start("army_007");var unit=j.Player.Units.Single();
            var battle=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(unit,2));Assert.That(unit.IsReady,Is.True);
            while(battle.Phase!=Phase.End)battle.Advance();j.SidewaysInfluence(j.Player.Deck.Hand[0]);j.EndTurn();
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideUnit(unit,2));Assert.That(unit.IsReady,Is.True);
        }
    }
}
