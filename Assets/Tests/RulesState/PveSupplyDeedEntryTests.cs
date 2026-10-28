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
    public sealed class PveSupplyDeedEntryTests
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
        private static void Play(PveJourneySession j,string id,string choice)=>j.PlayOutsideBattle(Card(j,id),choice);
        [TestCase(false)][TestCase(true)]public void LearningOptionalPurchasePaysOriginalInfluenceAndPreservesSupply(bool strong)
        {
            var j=Start("advanced_card_026","basic_card_008","basic_card_022");var offer=j.Game.Globals.AdvActions.Offer.ToArray();Play(j,"advanced_card_026",strong?"strong":"base");
            Assert.That(j.Context.InfluencePool,Is.EqualTo(strong?4:2));Assert.That(j.Game.Globals.AdvActions.Offer,Is.EqualTo(offer));
            Assert.Throws<InvalidOperationException>(()=>j.PurchaseLearnedCard(offer[0],strong));Assert.That(j.Context.LearnUsed,Is.False);
            Play(j,"basic_card_008","strong");if(strong)Play(j,"basic_card_022","base");var gained=j.PurchaseLearnedCard(offer[0],strong);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(strong?1:0));Assert.That(gained.Id,Is.EqualTo(offer[0].Id));Assert.That(strong?j.Player.Deck.Hand.Contains(gained):j.Player.Deck.DiscardPile.Contains(gained),Is.True);
            Assert.That(j.Game.Globals.AdvActions.Offer.Count,Is.EqualTo(3));Assert.That(j.Game.Globals.AdvActions.Offer.Take(2),Is.EqualTo(offer.Skip(1)));Assert.Throws<InvalidOperationException>(()=>j.PurchaseLearnedCard(j.Game.Globals.AdvActions.Offer[0],strong));
            j.EndTurn();j.BeginTurn();Assert.That(j.Context.LearningModes,Is.Zero);Assert.Throws<InvalidOperationException>(()=>j.PurchaseLearnedCard(j.Game.Globals.AdvActions.Offer[0],strong));
        }
        [TestCase(false)][TestCase(true)]public void PracticeOriginalSameColorMaterialAndSupplyGoToRealZones(bool strong)
        {
            var j=Start("advanced_card_027","basic_card_000","basic_card_014","basic_card_008","basic_card_021");var offered=j.Game.Globals.AdvActions.Offer[0];var oldOffer=j.Game.Globals.AdvActions.Offer.ToArray();
            var material=j.Player.Deck.Hand.First(c=>c.Id!="advanced_card_027"&&CardColorHelper.Get(c.Id)==CardColorHelper.Get(offered.Id));
            j.PlayOutsideWithInputs(Card(j,"advanced_card_027"),strong?"strong":"base",new PveDeedSelection{Cards=new[]{material},Advanced=offered});
            Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(material));Assert.That(j.Context.CardToRemove,Is.Null);Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(strong?1:2));
            var gained=(strong?j.Player.Deck.Hand:j.Player.Deck.DiscardPile).Single(c=>c.Id==offered.Id);Assert.That(gained.Type,Is.EqualTo(CardType.Action));Assert.That(j.Game.Globals.AdvActions.Offer.Take(2),Is.EqualTo(oldOffer.Skip(1)));Assert.That(j.Game.Globals.AdvActions.Offer.Count,Is.EqualTo(3));
            j.EndTurn();j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();Assert.That(j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).Any(c=>ReferenceEquals(c,gained)),Is.True);Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(material));
        }
        [Test]public void PracticeWrongColorForeignOfferAndSelfRejectBeforeGreenFee()
        {
            var j=Start("advanced_card_027","basic_card_000","basic_card_014","basic_card_008","basic_card_021");var card=Card(j,"advanced_card_027");var offered=j.Game.Globals.AdvActions.Offer[0];
            var wrong=j.Player.Deck.Hand.First(c=>c!=card&&CardColorHelper.Get(c.Id)!=CardColorHelper.Get(offered.Id));
            foreach(var input in new[]{new PveDeedSelection{Cards=new[]{wrong},Advanced=offered},new PveDeedSelection{Cards=new[]{wrong},Advanced=new AdvActionCard(offered.Id)},new PveDeedSelection{Cards=new[]{card},Advanced=offered}})
                Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"strong",input));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));Assert.That(j.Player.Deck.RemovedCards,Is.Empty);Assert.That(j.Context.CardToRemove,Is.Null);
        }
        [TestCase(false)][TestCase(true)]public void SpellForgeUsesActualDifferentSpellsWithoutTakingThem(bool strong)
        {
            var j=Start("advanced_card_033","basic_card_000","basic_card_014","basic_card_008","basic_card_021");var spells=j.Game.Globals.Spells.Offer.ToArray();var selected=spells.Take(strong?2:1).ToArray();
            j.PlayOutsideWithInputs(Card(j,"advanced_card_033"),strong?"strong":"base",new PveDeedSelection{Spells=selected});
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
            {
                int amount=2-(strong&&color==ManaColor.Blue?1:0)+selected.Count(s=>CardColorHelper.Get(s.Id)==color);
                Assert.That(j.Player.Mana.Crystals[color],Is.EqualTo(Math.Min(3,amount)));
            }
            Assert.That(j.Game.Globals.Spells.Offer,Is.EqualTo(spells));
            var chosen=CardColorHelper.Get(selected[0].Id).Value;
            string consumer=chosen switch{ManaColor.Red=>"basic_card_021",ManaColor.Blue=>"basic_card_014",ManaColor.Green=>"basic_card_000",_=>"basic_card_008"};int before=j.Player.Mana.Crystals[chosen];
            if(chosen==ManaColor.Red){var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Advance();b.Advance();b.Play(Card(j,consumer),"strong");Assert.That(b.Attack(0).Kills,Is.EqualTo(1));}
            else Play(j,consumer,"strong");Assert.That(j.Player.Mana.Crystals[chosen],Is.EqualTo(before-1));
        }
        [Test]public void SpellForgeDuplicateAndStaleOfferRejectBeforeBlueFee()
        {
            var j=Start("advanced_card_033");var card=Card(j,"advanced_card_033");var spell=j.Game.Globals.Spells.Offer[0];
            foreach(var selected in new[]{new[]{spell,spell},new[]{spell,new SpellCard(spell.Id)}})
                Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(card,"strong",new PveDeedSelection{Spells=selected}));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(2));Assert.That(j.Player.Deck.Hand.Single(),Is.SameAs(card));
        }
        [Test]public void SeedZeroOriginalSupplyForContinuousRoute()
        {
            var j=Start("advanced_card_026","advanced_card_027","advanced_card_033","basic_card_000","basic_card_014","basic_card_008","basic_card_021");
            TestContext.WriteLine("Advanced: "+string.Join(",",j.Game.Globals.AdvActions.Offer.Select(c=>c.Id+":"+CardColorHelper.Get(c.Id))));
            TestContext.WriteLine("Spells: "+string.Join(",",j.Game.Globals.Spells.Offer.Select(c=>c.Id+":"+CardColorHelper.Get(c.Id))));
            Assert.That(j.Game.Globals.AdvActions.Offer.Count,Is.EqualTo(3));Assert.That(j.Game.Globals.Spells.Offer.Count,Is.EqualTo(3));
        }
        [Test]public void ContinuousForgeLearningPracticeOriginalGainedShieldWinsBattleAndRoundKeepsZones()
        {
            var j=Start("basic_card_000","basic_card_008","basic_card_014","basic_card_021","advanced_card_026","advanced_card_027","advanced_card_033");
            j.Player.Mana.Crystals[ManaColor.Red]=1;j.Player.Mana.Crystals[ManaColor.Blue]=1;j.Player.Mana.Crystals[ManaColor.Green]=0;j.Player.Mana.Crystals[ManaColor.White]=0;
            var forge=j.Game.Globals.Spells.Offer[0];Assert.That(forge.Id,Is.EqualTo("magic_004"));
            j.PlayOutsideWithInputs(Card(j,"advanced_card_033"),"base",new PveDeedSelection{Spells=new[]{forge}});Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
            Play(j,"advanced_card_026","base");var source=j.Context.ManaSource.Dice[0];Assert.That(source.Face,Is.EqualTo(ManaColor.White));CardManaActions.UseSourceDie(j.Player,j.Context,source,ManaColor.White);
            Play(j,"basic_card_008","strong");var learned=j.Game.Globals.AdvActions.Offer[1];Assert.That(learned.Id,Is.EqualTo("advanced_card_022"));var gained=j.PurchaseLearnedCard(learned,false);Assert.That(j.Context.InfluencePool,Is.Zero);Assert.That(j.Player.Deck.DiscardPile.Single(),Is.SameAs(gained));
            var blue=Card(j,"basic_card_014");var offered=j.Game.Globals.AdvActions.Offer[0];Assert.That(offered.Id,Is.EqualTo("advanced_card_005"));
            j.PlayOutsideWithInputs(Card(j,"advanced_card_027"),"strong",new PveDeedSelection{Cards=new[]{blue},Advanced=offered});Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.Zero);
            var shield=Card(j,"advanced_card_005");var b=j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.Play(shield,"strong");b.PlaySideways(Card(j,"basic_card_000"));b.Block(0);b.Advance();b.Advance();
            Assert.That(j.Player.Wounds,Is.Zero);b.Play(Card(j,"basic_card_021"),"attack");Assert.That(b.Attack(0).RequiredPower,Is.EqualTo(1));b.Advance();j.EndTurn();
            Assert.That(j.Player.Deck.Hand,Is.Empty);Assert.That(j.Player.Deck.DiscardPile.Count,Is.EqualTo(8));Assert.That(j.Player.Deck.RemovedCards.Single(),Is.SameAs(blue));
            j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();Assert.That(j.Player.Deck.Hand.Count,Is.EqualTo(5));Assert.That(j.Player.Deck.DrawPile.Count,Is.EqualTo(3));
            var all=j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).ToArray();Assert.That(all.Any(c=>ReferenceEquals(c,shield)),Is.True);Assert.That(all.Any(c=>ReferenceEquals(c,gained)),Is.True);Assert.That(all.Contains(blue),Is.False);
        }
    }
}
