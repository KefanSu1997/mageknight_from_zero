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
    public sealed class PveCrystalBoltEntryTests
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
        [Test]public void MissingCrystalIsActuallyPaidByFollowingOriginalCard()
        {
            var j=Start("advanced_card_040","basic_card_000");j.Player.Mana.Crystals[ManaColor.Green]=0;
            j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),"base",new PveDeedSelection{Colors=new[]{ManaColor.Green}});
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));j.PlayOutsideBattle(Card(j,"basic_card_000"),"strong");j.Move(new AxialCoord(0,1));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(j.Context.MovementPool,Is.EqualTo(2));
        }
        [TestCase(ManaColor.Blue)][TestCase(ManaColor.Green)]
        public void IndependentSetChoicesUsePostPaymentTokensAndActualDrawHealMove(ManaColor cost)
        {
            var j=Start("advanced_card_040");var next=new DeedCard("basic_card_000",CardType.Action);j.Player.Deck.PutOnTop(next);j.Player.Wounds=1;j.Player.Mana.AddToken(cost);
            var choice=BattleCardChoice.For("advanced_card_040").Single(c=>c.Id=="strong_move_"+cost);
            Assert.That(PveDeedInputs.ManaPowerSetsAfterPayment(j.Player,j.Context,choice),Is.EqualTo(2));
            j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),choice.Id,new PveDeedSelection{BonusChoices=new[]{1,2}});
            Assert.That(j.Player.Mana.Tokens.Values.Sum(),Is.Zero);Assert.That(j.Player.Mana.Crystals.Values.All(v=>v==2),Is.True);Assert.That(j.Player.Deck.Hand.Contains(next),Is.True);
            j.HealOutsideBattle();Assert.That(j.Player.Wounds,Is.Zero);j.Move(new AxialCoord(0,1));j.PlayOutsideBattle(next,"base");Assert.That(j.Context.MovementPool,Is.EqualTo(4));Assert.That(j.Context.ManaPowerBonusChoices,Is.Empty);
        }
        [Test]public void PayingLastBlueCrystalBreaksSetBeforeChoosingDrawBonus()
        {
            var j=Start("advanced_card_040");foreach(var color in j.Player.Mana.Crystals.Keys.ToArray())j.Player.Mana.Crystals[color]=1;
            var first=new DeedCard("basic_card_000",CardType.Action);var second=new DeedCard("basic_card_008",CardType.Action);j.Player.Deck.PutOnTop(second);j.Player.Deck.PutOnTop(first);
            var choice=BattleCardChoice.For("advanced_card_040").Single(c=>c.Id=="strong_draw_Blue");Assert.That(PveDeedInputs.ManaPowerSetsAfterPayment(j.Player,j.Context,choice),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),choice.Id,new PveDeedSelection{BonusChoices=new[]{2}}));Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));
            j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),choice.Id,new PveDeedSelection());Assert.That(j.Player.Deck.Hand,Is.EqualTo(new[]{first,second}));Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);j.PlayOutsideBattle(first,"base");j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.Zero);
        }
        [Test]public void CrystalPowerRejectsExistingBaseColorAndCombatHealingBeforeFees()
        {
            var j=Start("advanced_card_040");var card=Card(j,"advanced_card_040");Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"base",new PveDeedSelection{Colors=new[]{ManaColor.Red}}));
            var b=j.BeginBattle(new[]{Enemy()});Assert.Throws<InvalidOperationException>(()=>b.Play(card,"strong_move_Blue",inputs:new PveDeedSelection{BonusChoices=new[]{1}}));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(2));Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(card));
        }
        [TestCase("strong_white",ManaColor.White)][TestCase("strong_red",ManaColor.Red)]
        public void ExplosiveKillTargetsActualSurvivorThenOriginalAttackConsumesArmor(string mode,ManaColor color)
        {
            var j=Start("advanced_card_041","basic_card_021");var b=j.BeginBattle(new[]{Enemy(),Enemy()});var survivor=b.Enemies[1];b.Play(Card(j,"advanced_card_041"),mode);
            Assert.That(j.Player.Mana.Crystals[color],Is.EqualTo(1));Assert.That(b.Attack(0).Kills,Is.EqualTo(1));Assert.That(j.Context.ExplosiveBoltArmorUses,Is.EqualTo(1));Assert.That(b.Enemies[1],Is.SameAs(survivor));
            Assert.Throws<InvalidOperationException>(()=>b.Advance());Assert.Throws<InvalidOperationException>(()=>b.ResolveExplosiveArmor(0));b.ResolveExplosiveArmor(1);Assert.That(j.Context.ExplosiveBoltArmorUses,Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(survivor,1,j.Context),Is.EqualTo(2));b.Advance();b.Advance();b.Advance();b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.Attack(1).Kills,Is.EqualTo(1));Assert.That(j.Context.ExplosiveBoltArmorUses,Is.Zero);b.Advance();Assert.That(CombatMath.EffectiveArmor(survivor,1,j.Context),Is.EqualTo(3));
        }
        [Test]public void ExplosiveLastEnemyRewardCanBeDeclinedAndBaseWoundCrystalsAreReal()
        {
            var j=Start("advanced_card_041","basic_card_007");foreach(var color in j.Player.Mana.Crystals.Keys.ToArray())j.Player.Mana.Crystals[color]=0;
            j.PlayOutsideBattle(Card(j,"advanced_card_041"),"base");Assert.That(j.Player.Wounds,Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));
            var b=j.BeginBattle(new[]{Enemy()});b.Play(Card(j,"basic_card_007"),"strong");Assert.That(b.Attack(0).Kills,Is.EqualTo(1));Assert.That(j.Context.ExplosiveBoltArmorUses,Is.Zero);
            var next=Start("advanced_card_041");var last=next.BeginBattle(new[]{Enemy()});last.Play(Card(next,"advanced_card_041"),"strong_red");last.Attack(0);last.ResolveExplosiveArmor();Assert.That(next.Context.ExplosiveBoltArmorUses,Is.Zero);Assert.DoesNotThrow(()=>last.Advance());
        }
        [Test]public void ContinuousFourOriginalCardsMoveReduceArmorProtectFarmerAndHealSameUnit()
        {
            var j=Start("basic_card_006","basic_card_008","basic_card_021","advanced_card_037","advanced_card_038","advanced_card_040","advanced_card_041");
            var p=j.Player;p.Mana.Crystals[ManaColor.Red]=1;p.Mana.Crystals[ManaColor.Blue]=2;p.Mana.Crystals[ManaColor.White]=1;p.Mana.Crystals[ManaColor.Green]=0;
            var farmer=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(farmer);j.PlayOutsideBattle(Card(j,"basic_card_006"),"green_crystal");
            j.PlayOutsideWithInputs(Card(j,"advanced_card_037"),"base_move",new PveDeedSelection());j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.Zero);
            var b=j.BeginBattle(new[]{Enemy(),Enemy()});b.Play(Card(j,"advanced_card_041"),"strong_red");b.Attack(0);b.ResolveExplosiveArmor(1);
            b.Play(Card(j,"advanced_card_038"),"base",inputs:new PveDeedSelection{Unit=farmer});
            b.Advance();b.Advance();b.PlanDamage(1,0,farmer);b.Advance();Assert.That(p.Wounds,Is.Zero);Assert.That(farmer.Wounds,Is.EqualTo(1));Assert.That(farmer.IsReady,Is.True);Assert.That(j.Context.InfluencePool,Is.Zero);
            b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.Attack(1).Kills,Is.EqualTo(1));b.Advance();Assert.That(farmer.PhysicalResistTemp,Is.False);j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),"strong_heal_Blue",new PveDeedSelection());Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));b.HealWound(0);Assert.That(farmer.Wounds,Is.Zero);Assert.That(farmer.CanActivate,Is.True);Assert.That(j.Context.Healing.Total,Is.EqualTo(1));
            j.EndTurn();j.BeginTurn();var next=j.BeginBattle(new[]{Enemy()});next.Advance();next.Advance();next.Advance();next.UseUnit(farmer,0,0);Assert.That(farmer.IsReady,Is.False);Assert.That(next.Context.MeleePool,Is.EqualTo(2));Assert.That(p.Units.Single(),Is.SameAs(farmer));
            next.PlaySideways(Card(j,"basic_card_008"));Assert.That(next.Attack(0).Kills,Is.EqualTo(1));next.Advance();j.EndTurn();Assert.That(j.CompletedBattles,Is.EqualTo(2));Assert.That(j.CompletedTurns,Is.EqualTo(2));
        }
        [Test]public void ContinuousPrintedBaseWoundFundsSplitPaymentAndIndependentMoveBonusThenDiscardedWoundAttacks()
        {
            var j=Start("basic_card_006","basic_card_008","basic_card_021","advanced_card_037","advanced_card_038","advanced_card_040","advanced_card_041");
            var p=j.Player;p.Mana.Crystals[ManaColor.Red]=1;p.Mana.Crystals[ManaColor.Blue]=2;p.Mana.Crystals[ManaColor.White]=1;p.Mana.Crystals[ManaColor.Green]=0;
            j.PlayOutsideBattle(Card(j,"basic_card_006"),"green_crystal");j.PlayOutsideBattle(Card(j,"advanced_card_041"),"base");
            j.PlayOutsideWithInputs(Card(j,"advanced_card_040"),"strong_heal_Blue",new PveDeedSelection{BonusChoices=new[]{0}});Assert.That(j.Context.Healing.Total,Is.EqualTo(2));Assert.That(j.Context.MovementPool,Is.EqualTo(2));j.HealOutsideBattle();j.Move(new AxialCoord(0,1));
            var b=j.BeginBattle(new[]{Enemy(),Enemy()});Assert.That(j.Context.Healing.Total,Is.Zero);b.Advance();b.Play(Card(j,"advanced_card_038"),"strong_block");b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.EqualTo(2));
            var wound=p.Deck.Hand.First(c=>c.Type==CardType.Wound);b.Play(Card(j,"advanced_card_037"),"base_attack",inputs:new PveDeedSelection{Cards=new[]{wound}});Assert.That(j.Context.MeleePool,Is.EqualTo(3));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(wound));Assert.That(b.Attack(0).Kills,Is.EqualTo(1));
            b.Play(Card(j,"basic_card_021"),"strong");Assert.That(b.Attack(1).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(7));
        }
    }
}
