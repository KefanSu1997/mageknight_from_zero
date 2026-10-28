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
 public sealed class PveSupplyPrimaryModesTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static PveJourneySession Start(params string[] ids)
  {
   var p=new PlayerState{HeroId="Arythea"};foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
   p.Mana.AddCrystal(ManaColor.Red,1);p.Mana.AddCrystal(ManaColor.Blue,1);
   var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
   var map=new MapState();map.Placed[new AxialCoord(0,0)]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Placed[new AxialCoord(0,1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
   var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,manaRandom:new Random(0),supplyRandom:new Random(0));j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
  }
  static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.Single(c=>c.Id==id);
  static void Play(PveJourneySession j,string id,string choice)=>j.PlayOutsideBattle(Card(j,id),choice);
  [Test] public void ForgeStrongLearningStrongStormBaseActualContinuousConsumers()
  {
   var j=Start("basic_card_000","basic_card_008","basic_card_014","basic_card_022","advanced_card_027","advanced_card_026","advanced_card_033");var p=j.Player;
   var spells=j.Game.Globals.Spells.Offer.ToArray();TestContext.WriteLine("Supplies "+string.Join(",",j.Game.Globals.AdvActions.Offer.Select(a=>a.Id))+" / "+string.Join(",",spells.Select(s=>s.Id+":"+CardColorHelper.Get(s.Id)))+" / source "+string.Join(",",j.Context.ManaSource.Dice.Select(d=>d.Face)));Assert.That(spells.Select(s=>s.Id),Is.EqualTo(new[]{"magic_004","magic_023","magic_000"}));
   j.PlayOutsideWithInputs(Card(j,"advanced_card_033"),"strong",new PveDeedSelection{Spells=new[]{spells[0],spells[1]}});
   Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Mana.Crystals[ManaColor.White],Is.EqualTo(1));Assert.That(j.Game.Globals.Spells.Offer,Is.EqualTo(spells));
   Play(j,"advanced_card_026","strong");var die=j.Context.ManaSource.Dice[0];Assert.That(die.Face,Is.EqualTo(ManaColor.White));CardManaActions.UseSourceDie(p,j.Context,die,ManaColor.White);
   Play(j,"basic_card_008","strong");Play(j,"basic_card_022","base");Assert.That(j.Context.InfluencePool,Is.EqualTo(10));
   var offer=j.Game.Globals.AdvActions.Offer[1];Assert.That(offer.Id,Is.EqualTo("advanced_card_022"));var storm=j.PurchaseLearnedCard(offer,true);Assert.That(p.Deck.Hand.Contains(storm),Is.True);Assert.That(j.Context.InfluencePool,Is.EqualTo(1));
   TestContext.WriteLine("actual source="+string.Join(",",j.Context.ManaSource.Dice.Select(d=>d.Face)));
   var stormDie=j.Context.ManaSource.Dice.First(d=>d.Face==ManaColor.Blue);
   j.PlayOutsideWithInputs(storm,"base",new PveDeedSelection{Dice=new[]{stormDie}});Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));Assert.That(j.Context.ManaSource.Dice.Contains(stormDie),Is.True);
   Play(j,"basic_card_000","strong");Play(j,"basic_card_014","strong");j.Move(new AxialCoord(0,1));Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(j.Context.MovementPool,Is.EqualTo(6));
   j.EndTurn(p.Deck.Hand.ToArray());Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DiscardPile.Contains(storm),Is.True);Assert.That(p.Fame,Is.Zero);Assert.That(j.Context.MovementPool,Is.Zero);

  }
  [Test] public void PracticeBaseOriginalMaterialRemovedActualShieldSurvivesRoundAndConsumes()
  {
   var j=Start("basic_card_014","basic_card_021","advanced_card_026","advanced_card_027","advanced_card_033");var p=j.Player;
   var material=Card(j,"basic_card_014");var offer=j.Game.Globals.AdvActions.Offer[0];Assert.That(offer.Id,Is.EqualTo("advanced_card_005"));
   j.PlayOutsideWithInputs(Card(j,"advanced_card_027"),"base",new PveDeedSelection{Cards=new[]{material},Advanced=offer});
   var shield=p.Deck.DiscardPile.Single();Assert.That(shield.Id,Is.EqualTo("advanced_card_005"));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(material));Assert.That(p.Mana.Crystals.ContainsKey(ManaColor.Green)?p.Mana.Crystals[ManaColor.Green]:0,Is.Zero);
   j.EndTurn(p.Deck.Hand.ToArray());Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(5));j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();
   Assert.That(p.Deck.Hand.Count,Is.EqualTo(5));Assert.That(p.Deck.Hand.Contains(shield),Is.True);Assert.That(p.Deck.DrawPileCount,Is.Zero);Assert.That(p.Deck.Hand.Contains(material),Is.False);j.SelectTactic("night_1");j.BeginTurn();
   var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Play(shield,"strong");b.PlaySideways(Card(j,"advanced_card_033"));b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);
   b.Play(Card(j,"basic_card_021"),"strong");Assert.That(b.Attack(0).RequiredPower,Is.EqualTo(1));b.Advance();Assert.That(b.Won,Is.True);Assert.That(p.Fame,Is.EqualTo(2));j.EndTurn(p.Deck.Hand.ToArray());Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(material));Assert.That(p.Deck.DiscardPile.Contains(shield),Is.True);
  }
 }
}

