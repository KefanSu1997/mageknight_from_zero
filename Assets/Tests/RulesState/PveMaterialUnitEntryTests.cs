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
    public sealed class PveMaterialUnitEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState();foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            p.Mana.AddCrystal(ManaColor.Green,2);
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();
            map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});
            map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Forest});
            map.Places[new AxialCoord(0,0)]=new PlaceState(PlaceType.Village);
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,manaRandom:new Random(0),supplyRandom:new Random(0));
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.First(c=>c.Id==id);
        private static MK.Logic.Data.Monster Enemy(string id="monster_032")=>CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);
        private static DeedCard Wound(PveJourneySession j)
        {
            var wound=new DeedCard("w",CardType.Wound);j.Player.Deck.Hand.Add(wound);return wound;
        }
        private static void AssertMaterials(PveJourneySession j,params DeedCard[] discarded)
        {
            foreach(var card in discarded)
            {
                Assert.That(j.Player.Deck.DiscardPile.Count(c=>ReferenceEquals(c,card)),Is.EqualTo(1));
                Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,card)),Is.False);
                Assert.That(j.Player.Deck.RemovedCards.Any(c=>ReferenceEquals(c,card)),Is.False);
            }
            Assert.That(j.Context.HealedHandWoundsThisTurn,Is.Zero,"弃掉伤牌不算治疗");
            Assert.That(j.Context.CardsToDiscard,Is.Empty);Assert.That(j.Context.CardToDiscard,Is.Null);
        }

        [TestCase(false)][TestCase(true)]
        public void PerseveranceMovementOptionalMaterialPaysActualForest(bool strong)
        {
            var j=Start("advanced_card_037");var wound=strong?null:Wound(j);
            j.PlayOutsideWithInputs(Card(j,"advanced_card_037"),strong?"strong_move":"base_move",
                new PveDeedSelection{Cards=strong?Array.Empty<DeedCard>():new[]{wound}});
            Assert.That(j.Context.MovementPool,Is.EqualTo(3));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(strong?1:2));
            AssertMaterials(j,strong?Array.Empty<DeedCard>():new[]{wound});
            j.Move(new AxialCoord(0,1));Assert.That(j.Player.Position,Is.EqualTo(new AxialCoord(0,1)));Assert.That(j.Context.MovementPool,Is.Zero);
        }

        [TestCase(false)][TestCase(true)]
        public void PerseveranceInfluenceActualDiscardFundsVillageHealing(bool strong)
        {
            var j=Start("advanced_card_037","basic_card_000");var wound=Wound(j);var toHeal=Wound(j);
            // Both modes discard one actual old wound; strong also discards an ordinary card.
            var materials=strong?new[]{wound,Card(j,"basic_card_000")}:new[]{wound};
            j.PlayOutsideWithInputs(Card(j,"advanced_card_037"),strong?"strong_influence":"base_influence",new PveDeedSelection{Cards=materials});
            AssertMaterials(j,materials);Assert.That(j.Context.InfluencePool,Is.EqualTo(strong?7:3));
            j.BeginVillageInteraction();j.BuyVillageHealing();j.HealOutsideBattle();
            Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,toHeal)),Is.False);Assert.That(j.Player.Deck.DiscardPile.Any(c=>ReferenceEquals(c,toHeal)),Is.False);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(strong?4:0));Assert.That(j.Context.HealedHandWoundsThisTurn,Is.EqualTo(1));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(strong?1:2));
        }

        [TestCase(false)][TestCase(true)]
        public void PerseveranceAttackConsumesRealMaterialAndAttackPower(bool strong)
        {
            var j=Start("advanced_card_037","basic_card_021","basic_card_008","basic_card_014");var old=Wound(j);
            var b=j.BeginBattle(new[]{Enemy()});b.Advance();b.Advance();b.Advance();
            var materials=strong?new[]{old,Card(j,"basic_card_008")}:new[]{old};
            b.Play(Card(j,"advanced_card_037"),strong?"strong_attack":"base_attack",inputs:new PveDeedSelection{Cards=materials});
            AssertMaterials(j,materials);Assert.That(j.Context.MeleePool,Is.EqualTo(strong?7:3));
            if(!strong)b.Play(Card(j,"basic_card_021"),"attack");
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));Assert.That(j.Context.MeleePool,Is.Zero);
            Assert.That(j.Player.Wounds,Is.EqualTo(2),"保留本次承伤产生的新伤牌");
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(strong?1:2));
        }

        [TestCase(false)][TestCase(true)]
        public void PerseveranceBlockConsumesOptionalOrdinaryCardsAndPreventsWounds(bool strong)
        {
            var j=Start("advanced_card_037","basic_card_000","basic_card_008","basic_card_016");
            var materials=strong?new[]{Card(j,"basic_card_000"),Card(j,"basic_card_008")}:Array.Empty<DeedCard>();
            var b=j.BeginBattle(new[]{Enemy()});b.Advance();
            b.Play(Card(j,"advanced_card_037"),strong?"strong_block":"base_block",inputs:new PveDeedSelection{Cards=materials});
            Assert.That(j.Context.BlockPool,Is.EqualTo(strong?7:2));AssertMaterials(j,materials);
            if(!strong)b.Play(Card(j,"basic_card_016"),"block");
            b.Block(0);b.Advance();b.Advance();Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.BlockPool,Is.Zero);
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(strong?1:2));
        }

        [Test]
        public void PerseveranceIllegalMaterialsRejectBeforeFeeOrCardMutation()
        {
            var j=Start("advanced_card_037","basic_card_008");var source=Card(j,"advanced_card_037");var ordinary=Card(j,"basic_card_008");
            var wound=Wound(j);var another=Wound(j);var before=j.Player.Deck.Hand.ToArray();
            foreach(var materials in new[]{new[]{source},new[]{wound,another},new[]{ordinary,ordinary},new[]{new DeedCard(ordinary.Id,CardType.Action)}})
            {
                Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(source,"strong_move",new PveDeedSelection{Cards=materials}));
                Assert.That(j.Player.Deck.Hand,Is.EqualTo(before));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));
                Assert.That(j.Player.Deck.PlayedCards,Is.Empty);Assert.That(j.Player.Deck.DiscardPile,Is.Empty);
            }
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(source,"base_move",new PveDeedSelection{Cards=new[]{ordinary}}));
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.That(j.Player.Deck.Hand,Is.EqualTo(before));
        }

        [Test]
        public void NatureChosenFarmerResistanceExpiresForSameSurvivingEnemyNextTurn()
        {
            var j=Start("advanced_card_038","basic_card_008","basic_card_008");
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));j.Player.Units.Add(unit);
            var enemy=Enemy("monster_007");j.PlayOutsideBattle(Card(j,"basic_card_008"),"base");
            var b=j.BeginBattle(new[]{enemy});b.Play(Card(j,"advanced_card_038"),"base",inputs:new PveDeedSelection{Unit=unit});
            Assert.That(unit.PhysicalResistTemp,Is.True);Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));
            b.Advance();b.Advance();b.PlanDamage(0,0,unit);b.Advance();
            Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsReady,Is.True);Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.InfluencePool,Is.EqualTo(2));
            b.Advance();Assert.That(unit.PhysicalResistTemp,Is.False);j.EndTurn();j.BeginTurn();
            j.PlayOutsideBattle(Card(j,"basic_card_008"),"base");var second=j.BeginBattle(new[]{enemy});
            Assert.That(second.Enemies.Single(),Is.SameAs(enemy));second.Advance();second.Advance();second.PlanDamage(0,0,unit);second.Advance();
            Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(unit.IsReady,Is.True);Assert.That(j.Player.Wounds,Is.Zero);
            Assert.That(j.Player.Units.Single(),Is.SameAs(unit));Assert.That(j.Context.InfluencePool,Is.EqualTo(2));
        }

        [TestCase(false)][TestCase(true)]
        public void NatureStrongOriginalGreenFeeAndActualSiegeOrSwiftBlockConsumer(bool block)
        {
            var j=Start("advanced_card_038");var b=j.BeginBattle(new[]{Enemy(block?"monster_007":"monster_032")});
            if(block)b.Advance();
            b.Play(Card(j,"advanced_card_038"),block?"strong_block":"strong_siege");
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
            if(block)
            {
                Assert.That(j.Context.BlockPool,Is.EqualTo(6));b.Block(0);b.Advance();b.Advance();
                Assert.That(j.Player.Wounds,Is.Zero);Assert.That(j.Context.BlockPool,Is.Zero);
            }
            else
            {
                Assert.That(j.Context.SiegePool,Is.EqualTo(3));var hit=b.Attack(0);
                Assert.That(hit.PrintedPower,Is.EqualTo(3));Assert.That(hit.RequiredPower,Is.EqualTo(3));Assert.That(hit.Kills,Is.EqualTo(1));Assert.That(j.Context.SiegePool,Is.Zero);
            }
        }

        [Test]
        public void NatureRequiresActualOwnedUnitBeforePlaying()
        {
            var j=Start("advanced_card_038");var source=Card(j,"advanced_card_038");var b=j.BeginBattle(new[]{Enemy()});
            Assert.Throws<InvalidOperationException>(()=>b.Play(source,"base",inputs:new PveDeedSelection()));
            Assert.Throws<InvalidOperationException>(()=>b.Play(source,"base",inputs:new PveDeedSelection{Unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"))}));
            Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(source));Assert.That(j.Player.Deck.PlayedCards,Is.Empty);
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));
        }
    }
}
