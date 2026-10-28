using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveDeedInputTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var p=new PlayerState();foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(c);
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,manaRandom:new Random(0),supplyRandom:new Random(0));
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.Single(c=>c.Id==id);
        private static void Play(PveJourneySession j,string id,string choice,PveDeedSelection input=null)=>j.PlayOutsideWithInputs(Card(j,id),choice,input);
        private static PveBattleSession Melee(PveJourneySession j)
        {var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Advance();b.Advance();return b;}

        [TestCase("basic_card_009",1,2)][TestCase("basic_card_013",2,1)]
        public void ConvertedActualDicePayOriginalAttackAndReturnUnrerolled(string id,int count,int tokens)
        {
            var j=Start(id,"basic_card_021");var dice=j.Context.ManaSource.Dice.Take(count).ToArray();
            Play(j,id,"strong",new PveDeedSelection{Dice=dice,Colors=Enumerable.Repeat(ManaColor.Red,count).ToArray()});
            Assert.That(j.Player.Mana.Tokens[ManaColor.Red],Is.EqualTo(count*tokens));Assert.That(j.Context.ManaSource.TurnClaims.Count,Is.EqualTo(count));
            var b=Melee(j);b.Play(Card(j,"basic_card_021"),"strong");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            Assert.That(dice.All(d=>j.Context.ManaSource.Dice.Contains(d)&&d.Face==ManaColor.Red),Is.True);Assert.That(j.Context.ManaSource.TurnClaims,Is.Empty);
        }
        [TestCase("basic_card_009")][TestCase("basic_card_013")]
        public void BaseExtraDieUsesActualBudgetAndCannotBeClaimedTwice(string id)
        {
            var j=Start(id);Play(j,id,"base");var die=j.Context.ManaSource.Dice[0];die.TurnTo(id=="basic_card_013"?ManaColor.Black:ManaColor.Red);
            CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.Red,true);
            Assert.That(j.Context.ExtraManaDice,Is.Zero);Assert.That(j.Player.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.Red,true));
            j.EndTurn();Assert.That(j.Context.ManaSource.Dice.Contains(die),Is.True);
        }
        [Test]public void DuplicateOrGoldTransformRejectsBeforePrintedFeeAndRestoresInput()
        {
            var j=Start("basic_card_013");var card=Card(j,"basic_card_013");var die=j.Context.ManaSource.Dice[0];
            foreach(var input in new[]{new PveDeedSelection{Dice=new[]{die,die},Colors=new[]{ManaColor.Red,ManaColor.Blue}},new PveDeedSelection{Dice=j.Context.ManaSource.Dice.Take(2).ToArray(),Colors=new[]{ManaColor.Red,ManaColor.Gold}}})
                Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"strong",input));
            Assert.That(j.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));Assert.That(j.Context.ManaSource.TurnClaims,Is.Empty);Assert.That(j.Context.ManaDiceToTransform,Is.Empty);Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(card));
        }
        [TestCase(false)][TestCase(true)]public void BloodRitualRealWoundAndIndependentColorsPayMovement(bool strong)
        {
            var j=Start("advanced_card_012","advanced_card_013");int oldRed=j.Player.Mana.Crystals[ManaColor.Red];
            Play(j,"advanced_card_012",strong?"strong":"base",new PveDeedSelection{Colors=strong?new[]{ManaColor.Green,ManaColor.Blue,ManaColor.White}:new[]{ManaColor.Green},Crystal=strong?ManaColor.Blue:null});
            Assert.That(j.Player.Wounds,Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(strong?oldRed-1:oldRed+1));
            if(strong){Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(2));Assert.That(j.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue),Is.Zero);}
            Play(j,"advanced_card_013","move");Assert.That(j.Context.MovementPool,Is.EqualTo(4));j.Move(new AxialCoord(0,1));Assert.That(j.Context.MovementPool,Is.EqualTo(2));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
        }
        [Test]public void MasteryTracksPrintedFeeAndLaterAttackButDoesNotLeakNextTurn()
        {
            var j=Start("advanced_card_021","basic_card_021","basic_card_000");Play(j,"advanced_card_021","strong");Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);
            var b=Melee(j);b.Play(Card(j,"basic_card_021"),"strong");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            j.BeginTurn();j.PlayOutsideBattle(Card(j,"basic_card_000"),"strong");j.EndTurn();Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(j.Context.ReturnSpentCrystals,Is.False);
        }
        [Test]public void MasteryBaseRequiresOwnedColorAndCapsActualInventory()
        {
            var j=Start("advanced_card_021");j.Player.Mana.TryPayExactColors(new[]{ManaColor.Blue},j.Context,j.Player);
            Assert.Throws<InvalidOperationException>(()=>Play(j,"advanced_card_021","base",new PveDeedSelection{Colors=new[]{ManaColor.Blue}}));
            Play(j,"advanced_card_021","base",new PveDeedSelection{Colors=new[]{ManaColor.Red}});Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));
        }
        [TestCase(false)][TestCase(true)]public void StormUsesActualSourceAndExtraClaims(bool strong)
        {
            var j=Start("advanced_card_022","basic_card_021");var dice=j.Context.ManaSource.Dice.ToArray();dice[0].TurnTo(ManaColor.Red);
            Play(j,"advanced_card_022",strong?"strong":"base",new PveDeedSelection{Dice=strong?Array.Empty<ManaDie>():new[]{dice[0]}});
            if(strong)
            {
                Assert.That(j.Context.ExtraManaDice,Is.EqualTo(3));Assert.That(j.Context.BlackManaWild,Is.True);Assert.That(j.Context.GoldManaWild,Is.True);
                dice[0].TurnTo(ManaColor.Black);CardManaActions.UseSourceDie(j.Player,j.Context,dice[0],ManaColor.Red,true);
                Assert.That(j.Context.ExtraManaDice,Is.EqualTo(2));
            }
            else Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));
            Assert.That(j.Context.ManaSource.Dice.Concat(j.Context.ManaSource.TurnClaims.Keys).ToArray(),Is.EquivalentTo(dice));
        }
        [TestCase(false)][TestCase(true)]public void ImprovisationExactMaterialCreatesRealAttack(bool strong)
        {
            var j=Start("basic_card_023","basic_card_000");var actual=Card(j,"basic_card_000");var b=Melee(j);
            b.Play(Card(j,"basic_card_023"),strong?"strong_attack":"attack",inputs:new PveDeedSelection{Cards=new[]{actual}});
            Assert.That(j.Player.Deck.DiscardPile.Single(),Is.SameAs(actual));Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(strong?5:3));b.Attack(0);Assert.That(b.Won,Is.False);b.Advance();Assert.That(b.Won,Is.True);
        }
        [Test]public void ImprovisationRejectsSelfWoundAndForeignMaterialBeforeRedFee()
        {
            var j=Start("basic_card_023");var card=Card(j,"basic_card_023");j.Player.Wounds++;var wound=j.Player.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            foreach(var material in new[]{card,wound,new DeedCard("basic_card_000",CardType.Action)})
                Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"strong_move",new PveDeedSelection{Cards=new[]{material}}));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(j.Player.Deck.Hand.Count,Is.EqualTo(2));Assert.That(j.Context.CardToDiscard,Is.Null);
        }
        [TestCase(false)][TestCase(true)]public void MeditationReturnsActualEarlierCardsThenFormalNextDrawConsumesBonus(bool strong)
        {
            var j=Start("basic_card_000","basic_card_014","magic_002");
            if(strong){j.PlayOutsideBattle(Card(j,"basic_card_000"),"base");j.EndTurn();j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();j.SelectTactic("night_1");j.BeginTurn();}
            var a=Card(j,"basic_card_000");var b=Card(j,"basic_card_014");
            j.PlayOutsideBattle(a,"base");j.PlayOutsideBattle(b,"base");j.EndTurn();j.BeginTurn();
            if(strong){var die=j.Context.ManaSource.Dice[0];die.TurnTo(ManaColor.Black);CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.Black);}
            Play(j,"magic_002",strong?"strong_top":"top",new PveDeedSelection{Cards=strong?new[]{a,b}:Array.Empty<DeedCard>()});
            Assert.That(j.Player.Deck.DrawPile,Is.EquivalentTo(new[]{a,b}));if(strong)Assert.That(j.Player.Deck.DrawPile,Is.EqualTo(new[]{a,b}));Assert.That(j.Context.NextDrawBonus,Is.EqualTo(2));
            j.EndTurn();Assert.That(j.Player.Deck.Hand,Is.EquivalentTo(new[]{a,b}));Assert.That(j.Player.Deck.DrawPileCount,Is.Zero);
        }
        [Test]public void ContinuousSourceAllowancesAndStormThenMeditationRecoversRealPreviousTurnCards()
        {
            var j=Start("basic_card_000","basic_card_009","basic_card_014","basic_card_023","advanced_card_012","advanced_card_022","magic_002");
            j.Player.Mana.Crystals[ManaColor.Green]=0;j.Player.Mana.Crystals[ManaColor.White]=0;
            var source=j.Context.ManaSource;Assert.That(source.Dice.Select(d=>d.Face),Is.EqualTo(new[]{ManaColor.White,ManaColor.Blue,ManaColor.White}));
            j.PlayOutsideBattle(Card(j,"basic_card_000"),"base");j.PlayOutsideBattle(Card(j,"basic_card_014"),"base");
            Play(j,"basic_card_009","base");CardManaActions.UseSourceDie(j.Player,j.Context,source.Dice[0],ManaColor.White);
            CardManaActions.UseSourceDie(j.Player,j.Context,source.Dice[0],ManaColor.Blue,true);
            Play(j,"advanced_card_022","strong");Assert.That(source.Dice.Single().Face,Is.EqualTo(ManaColor.Black));
            CardManaActions.UseSourceDie(j.Player,j.Context,source.Dice[0],ManaColor.Green,true);
            Assert.That(j.Context.ExtraManaDice,Is.EqualTo(2));Assert.That(j.Player.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));j.EndTurn();
            var prior=j.Player.Deck.DiscardPile.ToArray();Assert.That(prior.Length,Is.EqualTo(4));j.BeginTurn();
            Assert.That(j.Player.Mana.Tokens,Is.Empty);Assert.That(j.Context.ExtraManaDice,Is.Zero);
            Play(j,"advanced_card_012","base",new PveDeedSelection{Colors=new[]{ManaColor.Green}});
            Play(j,"magic_002","bottom");Assert.That(j.Player.Deck.DrawPile.Count,Is.EqualTo(2));
            var returned=j.Player.Deck.DrawPile.ToArray();Assert.That(returned.All(c=>prior.Any(old=>ReferenceEquals(old,c))),Is.True);
            Assert.That(j.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);j.EndTurn();
            Assert.That(returned.All(c=>j.Player.Deck.Hand.Any(h=>ReferenceEquals(c,h))),Is.True);Assert.That(j.Context.NextDrawBonus,Is.Zero);Assert.That(j.Player.Wounds,Is.EqualTo(1));
        }
        [Test]public void ContinuousResourceChainUsesOnePlayerAndLeavesLaterImprovisationMaterials()
        {
            var j=Start("basic_card_009","basic_card_013","basic_card_023","advanced_card_012","advanced_card_013","advanced_card_021","advanced_card_022");
            j.Player.Mana.Crystals[ ManaColor.Green]=0;j.Player.Mana.Crystals[ManaColor.White]=0;
            var p=j.Player;var source=j.Context.ManaSource;
            Play(j,"basic_card_009","strong",new PveDeedSelection{Dice=source.Dice.Take(1).ToArray(),Colors=new[]{ManaColor.Blue}});
            Play(j,"advanced_card_021","strong");Play(j,"advanced_card_012","base",new PveDeedSelection{Colors=new[]{ManaColor.White}});
            Play(j,"basic_card_013","strong",new PveDeedSelection{Dice=source.Dice.Take(2).ToArray(),Colors=new[]{ManaColor.Red,ManaColor.Green}});
            var b=Melee(j);b.Play(Card(j,"advanced_card_013"),"strong_attack");b.Attack(0);b.Advance();j.EndTurn();
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(source.Dice.Count,Is.EqualTo(3));
            j.BeginTurn();Play(j,"basic_card_023","strong_move",new PveDeedSelection{Cards=new[]{Card(j,"advanced_card_022")}});j.Move(new AxialCoord(0,1));j.EndTurn();
            Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(j.CompletedTurns,Is.EqualTo(2));
        }
    }
}
