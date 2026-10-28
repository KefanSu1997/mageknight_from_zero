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
    public sealed class PveSiteSpellPurchaseTests
    {
        private static readonly AxialCoord SiteHex = new AxialCoord(0,1);
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private PveJourneySession Start(PlaceType type=PlaceType.MageTower,CityColor color=CityColor.None)
        {
            var p=new PlayerState{HeroId="Tovak",Fame=35};
            p.Deck.SetDeck(new[]{"basic_card_000","basic_card_008","basic_card_008","advanced_card_008","basic_card_021","basic_card_016"}
                .Select(id=>new DeedCard(id,CardType.Action)));
            var map=new MapState();
            foreach(var hex in new[]{new AxialCoord(0,0),SiteHex})
                map.Placed.Add(hex,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            map.Places.Add(SiteHex,new PlaceState(type){OwnerId=p.Id+1,CityColor=color});
            var spells=CardJsonLoader.LoadSpells();
            var deeds=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(card=>new ActionCardData(card.Id,card.Name,card.Set,card.ImagePath,card.EnImagePath,
                    card.Top.Effect,card.Bottom.Effect,card.Top.ManaCost)));
            var j=new PveJourneySession(p,deeds,map:map,spells:spells);
            j.SelectTactic("day_1");j.BeginTurn();
            // Finite original supply fixture; Claim must draw the untouched next physical supply card.
            var supply=j.Game.Globals.Spells;supply.Offer.Clear();
            supply.SetDeck(new[]{"magic_015","magic_001","magic_002","magic_003","magic_004"}.Select(id=>new SpellCard(id)));
            supply.FillVacancies();
            Play(j,"basic_card_000");j.Move(SiteHex);
            return j;
        }
        private void Play(PveJourneySession j,string id,string choice="base")
            =>j.PlayOutsideBattle(j.Player.Deck.Hand.First(c=>c.Id==id),choice);
        private void Promise(PveJourneySession j)
        {j.Player.Mana.AddToken(ManaColor.White);Play(j,"basic_card_008","strong");}
        private ManaColor Color(SpellCard card)=>CardJsonLoader.LoadSpells().Single(x=>x.Id==card.Id).ManaColor;
        [TestCase(false)] [TestCase(true)]
        public void RealPurchasePaysPrintedFeeReplenishesThenSameAcquiredSpellKillsNextTurn(bool blueCity)
        {
            var j=Start(blueCity?PlaceType.City:PlaceType.MageTower,blueCity?CityColor.Blue:CityColor.None);
            Assert.That(j.CanBuySpellAtCurrentSite,Is.True);j.BeginSiteInteraction();Promise(j);Promise(j);
            var supply=j.Game.Globals.Spells;var target=supply.Offer[0];var unchosen=supply.Offer.Skip(1).ToArray();
            Assert.That(Color(target),Is.EqualTo(ManaColor.Blue));j.Player.Mana.AddCrystal(ManaColor.Blue,2);
            var acquired=j.BuySiteSpell(target);
            Assert.That(acquired.Id,Is.EqualTo("magic_015"));Assert.That(acquired.Type,Is.EqualTo(CardType.Spell));
            Assert.That(j.Player.Deck.DrawPile[0],Is.SameAs(acquired));
            Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,acquired)),Is.False);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(1));Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));
            Assert.That(supply.Offer.Count,Is.EqualTo(3));Assert.That(supply.Offer[0],Is.SameAs(unchosen[0]));
            Assert.That(supply.Offer[1],Is.SameAs(unchosen[1]));Assert.That(supply.Offer[2].Id,Is.EqualTo("magic_003"));
            Assert.Throws<InvalidOperationException>(()=>j.BuySiteSpell(target));
            j.EndTurn();j.BeginTurn();Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,acquired)),Is.True);
            var battle=j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"));
            battle.Play(acquired,"base");
            Assert.That(battle.Context.CombatPower.Attacks.Single().Value,Is.EqualTo(5));
            Assert.That(battle.Context.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Ice));
            Assert.That(battle.Attack(0).Kills,Is.EqualTo(1));
            Assert.That(j.Player.Mana.GetTotal(),Is.Zero);
            Assert.That(j.Player.Deck.PlayedCards.Any(c=>ReferenceEquals(c,acquired)),Is.True);
        }
        [Test]
        public void TwoBlueCityPurchasesUseShieldsOnlyOnceAndStackRealAcquiredCardsOnTop()
        {
            var j=Start(PlaceType.City,CityColor.Blue);j.CurrentInteractionSite.CityShields.AddRange(new[]{j.Player.Id,j.Player.Id});
            j.BeginSiteInteraction();Promise(j);Promise(j);Play(j,"advanced_card_008");
            foreach(var card in j.Player.Deck.Hand.ToArray())j.SidewaysInfluence(card);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(16));
            var first=j.Game.Globals.Spells.Offer[0];var second=j.Game.Globals.Spells.Offer[1];
            j.Player.Mana.AddToken(Color(first));j.Player.Mana.AddToken(Color(second));
            var a=j.BuySiteSpell(first);var b=j.BuySiteSpell(second);
            Assert.That(j.Context.InfluencePool,Is.EqualTo(2));Assert.That(j.Context.InteractionCityInfluence,Is.EqualTo(2));
            Assert.That(j.Player.Mana.GetTotal(),Is.Zero);
            Assert.That(j.Player.Deck.DrawPile[0],Is.SameAs(b));Assert.That(j.Player.Deck.DrawPile[1],Is.SameAs(a));
            Assert.That(j.Game.Globals.Spells.Offer.Select(c=>c.Id),Is.EqualTo(new[]{"magic_002","magic_003","magic_004"}));
        }
        [TestCase("copy")] [TestCase("influence")] [TestCase("mana")] [TestCase("whiteCity")]
        [TestCase("closed")] [TestCase("position")] [TestCase("supply")] [TestCase("unstarted")]
        public void InvalidPurchasePreservesOfferManaInfluenceAndDeck(string failure)
        {
            var j=Start(failure=="whiteCity"?PlaceType.City:PlaceType.MageTower,CityColor.White);
            if(failure!="unstarted")j.BeginSiteInteraction();Promise(j);
            if(failure!="influence")Promise(j);
            if(failure!="mana")j.Player.Mana.AddCrystal(ManaColor.Blue);
            var target=j.Game.Globals.Spells.Offer[0];
            if(failure=="copy")target=new SpellCard(target.Id);
            if(failure=="closed")j.EndSiteInteraction();
            if(failure=="position")j.Player.Position=new AxialCoord(0,0);
            if(failure=="supply")j.Context.SpellSupply=new SpellSupply();
            var offer=j.Game.Globals.Spells.Offer.ToArray();int influence=j.Context.InfluencePool,mana=j.Player.Mana.GetTotal(),deck=j.Player.Deck.DrawPileCount;
            Assert.Throws<InvalidOperationException>(()=>j.BuySiteSpell(target));
            Assert.That(j.Game.Globals.Spells.Offer.Count,Is.EqualTo(offer.Length));
            for(int i=0;i<offer.Length;i++)Assert.That(j.Game.Globals.Spells.Offer[i],Is.SameAs(offer[i]));
            Assert.That(j.Context.InfluencePool,Is.EqualTo(influence));Assert.That(j.Player.Mana.GetTotal(),Is.EqualTo(mana));
            Assert.That(j.Player.Deck.DrawPileCount,Is.EqualTo(deck));
        }
    }
}
