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
    public sealed class PveCombatDeedEntryTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState();
            if(ids.Contains("advanced_card_042"))p.Deck.SetDeck(new[]{new DeedCard("basic_card_021",CardType.Action)});
            foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            if(ids.Contains("advanced_card_042"))foreach(var id in new[]{"basic_card_000","basic_card_016","basic_card_008"})p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(color,2);
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,manaRandom:new Random(0),supplyRandom:new Random(0));j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.Single(c=>c.Id==id);
        private static PveBattleSession Battle(PveJourneySession j,string enemy="monster_032")=>j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id==enemy)});
        private static void Melee(PveBattleSession b){b.Advance();b.Advance();b.Advance();}
        private static void Outside(PveJourneySession j,string id,string choice)=>j.PlayOutsideBattle(Card(j,id),choice);

        [TestCase(false,false,2)][TestCase(false,true,5)][TestCase(true,false,4)][TestCase(true,true,9)]
        public void BloodRageActualWoundAndPrintedRedPayRealAttack(bool strong,bool wound,int power)
        {
            var j=Start("advanced_card_004");var b=Battle(j);Melee(b);int before=j.Player.Wounds;
            b.Play(Card(j,"advanced_card_004"),strong?(wound?"strong_wound":"strong"):(wound?"wound":"base"));
            Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(power));Assert.That(j.Player.Wounds-before,Is.EqualTo(wound?1:0));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(strong?1:2));
            var result=b.Attack(0);Assert.That(result.Kills,Is.EqualTo(power>=3?1:0));Assert.That(result.RequiredPower,Is.EqualTo(3));
        }
        [TestCase(false)][TestCase(true)]public void AmbushIceShieldCounterstrikeUsesRealBlockAndExpires(bool strong)
        {
            var j=Start("advanced_card_023","advanced_card_005","advanced_card_028","basic_card_000");Outside(j,"advanced_card_023",strong?"strong":"base");
            j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.EqualTo(strong?2:0));
            var b=Battle(j);b.Advance();b.Play(Card(j,"advanced_card_005"),"strong");Assert.That(b.BlockPreview(0).Effective,Is.EqualTo(strong?7:5));b.Block(0);b.Advance();b.Advance();
            Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.BlockedEnemiesThisTurn,Is.EqualTo(1));
            b.Play(Card(j,"advanced_card_028"),strong?"strong":"base");Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(strong?7:4));var result=b.Attack(0);Assert.That(result.RequiredPower,Is.EqualTo(1));b.Advance();j.EndTurn();j.BeginTurn();
            Assert.That(j.Context.AmbushAttackBonus,Is.Zero);Assert.That(j.Context.BlockedEnemiesThisTurn,Is.Zero);Assert.That(j.Player.Deck.Hand.Any(c=>c.Id=="basic_card_000"),Is.True);
        }
        [TestCase(false)][TestCase(true)]public void ShieldBashOriginalSwiftTargetAndKnightlyReward(bool strong)
        {
            var j=Start("advanced_card_031","advanced_card_034","basic_card_021");var b=Battle(j,"monster_007");b.Advance();b.Play(Card(j,"advanced_card_031"),strong?"strong":"base");b.Block(0);b.Advance();b.Advance();
            Assert.That(j.Player.Wounds,Is.Zero);b.Play(Card(j,"advanced_card_034"),strong?"strong_reward":"reward");
            if(!strong)b.Play(Card(j,"basic_card_021"),"attack");var result=b.Attack(0);Assert.That(result.Kills,Is.EqualTo(1));Assert.That(result.RequiredPower,Is.EqualTo(strong?1:4));Assert.That(j.Player.Reputation,Is.EqualTo(1));Assert.That(j.Player.Fame,Is.EqualTo(strong?4:3));
        }
        [TestCase(false)][TestCase(true)]public void ChargeActualUnitConsumerAndLatePlayRejects(bool strong)
        {
            var j=Start("advanced_card_016","basic_card_000");var unit=new UnitState(CardJsonLoader.LoadUnit("army_003"));j.Player.Units.Add(unit);var b=Battle(j);
            b.Play(Card(j,"advanced_card_016"),strong?"strong":"base");b.UseUnit(unit,1);Assert.That(b.AttackTotal(AttackType.Ranged),Is.EqualTo(strong?9:8));Assert.That(unit.IsReady,Is.False);Assert.That(j.Context.NoUnitDamage,Is.True);b.Attack(0);b.Advance();b.Advance();b.Advance();b.Advance();j.EndTurn();j.BeginTurn();Assert.That(j.Context.NoUnitDamage,Is.False);
            var other=Start("advanced_card_016");var late=Battle(other);late.Advance();Assert.Throws<InvalidOperationException>(()=>late.Play(Card(other,"advanced_card_016"),"strong"));Assert.That(other.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));
        }
        [TestCase("base",false)][TestCase("strong_red",true)][TestCase("strong_green",true)]
        public void BloodSurgeRealSelfWoundDrawAndOriginalPayment(string choice,bool strong)
        {
            var j=Start("advanced_card_042","advanced_card_004");var drawn=j.Player.Deck.DrawPile.Single();
            Outside(j,"advanced_card_042",choice);var b=Battle(j);Melee(b);
            // Actual defense creates two wounds first; strong removes the first, both draw from the real deck.
            Assert.That(j.Player.Deck.Hand.Contains(drawn),Is.True);Assert.That(j.Player.Wounds,Is.EqualTo(strong?1:2));
            b.Play(Card(j,"advanced_card_004"),"wound");Assert.That(j.Player.Wounds,Is.EqualTo(strong?2:3));Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();j.BeginTurn();Assert.That(j.Context.WoundDrawRemaining,Is.Zero);
            Assert.That(j.Player.Mana.Crystals[choice=="strong_green"?ManaColor.Green:ManaColor.Red],Is.EqualTo(strong?1:2));
        }
        [TestCase(false)][TestCase(true)]public void DecomposeActualMaterialPaysNextOriginalCardAndStaysRemoved(bool strong)
        {
            var j=Start("advanced_card_020","basic_card_021","basic_card_000");j.Player.Mana.Crystals[ManaColor.Red]=1;j.Player.Mana.Crystals[ManaColor.Green]=0;var material=Card(j,"basic_card_021");
            j.PlayOutsideWithInputs(Card(j,"advanced_card_020"),strong?"strong":"base",new PveDeedSelection{Cards=new[]{material}});
            Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(material));Assert.That(j.Context.CardToRemove,Is.Null);Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(strong?0:3));
            if(strong){Outside(j,"basic_card_000","strong");j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.EqualTo(2));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.Zero);}
            j.EndTurn();Assert.That(j.Player.Deck.Hand.Contains(material),Is.False);Assert.That(j.Player.Deck.DiscardPile.Contains(material),Is.False);
        }
        [TestCase(false,"basic_card_021",5,Element.Physical,AttackType.Melee)]
        [TestCase(true,"basic_card_021",6,Element.Fire,AttackType.Melee)]
        [TestCase(false,"basic_card_014",3,Element.Ice,AttackType.Melee)]
        [TestCase(true,"basic_card_014",4,Element.ColdFire,AttackType.Melee)]
        [TestCase(false,"basic_card_008",3,Element.Physical,AttackType.Ranged)]
        [TestCase(true,"basic_card_008",4,Element.Fire,AttackType.Ranged)]
        [TestCase(false,"basic_card_000",2,Element.Physical,AttackType.Siege)]
        [TestCase(true,"basic_card_000",3,Element.Fire,AttackType.Siege)]
        public void RitualActualMaterialColorProducesOriginalTypedContribution(bool strong,string materialId,int power,Element element,AttackType type)
        {
            var j=Start("advanced_card_029",materialId);var material=Card(j,materialId);var b=Battle(j);if(type==AttackType.Melee)Melee(b);
            b.Play(Card(j,"advanced_card_029"),strong?"strong":"base",inputs:new PveDeedSelection{Cards=new[]{material}});
            var actual=j.Context.CombatPower.Attacks.Single();Assert.That(actual.Element,Is.EqualTo(element));Assert.That(actual.Type,Is.EqualTo(type));Assert.That(b.AttackTotal(type),Is.EqualTo(power));Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(material));if(power>=3)Assert.That(b.Attack(0).Kills,Is.EqualTo(1));
        }
        [TestCase("advanced_card_020")][TestCase("advanced_card_029")]
        public void PermanentMaterialRejectsSelfWoundForeignBeforeFee(string id)
        {
            var j=Start(id);var b=Battle(j);var card=Card(j,id);j.Player.Wounds++;var wound=j.Player.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            foreach(var material in new[]{card,wound,new DeedCard("basic_card_000",CardType.Action)})Assert.Throws<InvalidOperationException>(()=>b.Play(card,"strong",inputs:new PveDeedSelection{Cards=new[]{material}}));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));Assert.That(j.Player.Deck.RemovedCards,Is.Empty);Assert.That(j.Context.CardToRemove,Is.Null);
        }
        [Test]public void ContinuousMaterialAmbushCounterstrikeThenSameSurvivorRitual()
        {
            var j=Start("basic_card_014","basic_card_021","advanced_card_005","advanced_card_020","advanced_card_023","advanced_card_028","advanced_card_029");
            j.Player.Mana.Crystals[ManaColor.Red]=1;j.Player.Mana.Crystals[ManaColor.Blue]=1;j.Player.Mana.Crystals[ManaColor.Green]=0;j.Player.Mana.Crystals[ManaColor.White]=0;
            var redMaterial=Card(j,"basic_card_021");var blueMaterial=Card(j,"basic_card_014");
            j.PlayOutsideWithInputs(Card(j,"advanced_card_020"),"base",new PveDeedSelection{Cards=new[]{redMaterial}});Outside(j,"advanced_card_023","base");j.Move(new AxialCoord(0,1));
            var first=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var survivor=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b=j.BeginBattle(new[]{first,survivor});b.Advance();b.Play(Card(j,"advanced_card_005"),"strong");b.Block(0);b.Advance();b.Advance();
            Assert.That(j.Player.Wounds,Is.EqualTo(2));b.Play(Card(j,"advanced_card_028"),"base");Assert.That(b.Attack(0).RequiredPower,Is.EqualTo(1));b.Advance();j.EndTurn();
            Assert.That(j.Player.Deck.Hand.Count,Is.EqualTo(4));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(3));j.BeginTurn();
            var again=j.BeginBattle(new[]{survivor});Assert.That(again.Enemies.Single(),Is.SameAs(survivor));Melee(again);Assert.That(j.Player.Wounds,Is.EqualTo(4));
            again.Play(Card(j,"advanced_card_029"),"strong",inputs:new PveDeedSelection{Cards=new[]{blueMaterial}});Assert.That(again.Context.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.ColdFire));Assert.That(again.Attack(0).Kills,Is.EqualTo(1));again.Advance();j.EndTurn();
            Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));Assert.That(j.Player.Fame,Is.EqualTo(4));Assert.That(j.Player.Deck.RemovedCards,Is.EquivalentTo(new[]{redMaterial,blueMaterial}));Assert.That(j.CompletedBattles,Is.EqualTo(2));Assert.That(j.CompletedTurns,Is.EqualTo(2));
        }
    }
}
