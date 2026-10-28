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
    public sealed class PveTomeDynamicInputsTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(string payment, bool keep=false, params string[] extra)
        {
            var p=new PlayerState();
            foreach(var id in new[]{"items_023",payment}.Concat(extra))
                p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items")?CardType.Artifact:id.StartsWith("magic")?CardType.Spell:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();for(int q=0;q<3;q++)map.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,new[]{TerrainType.Plains});
            EnemyTokenSupply enemies=null;
            if(keep)
            {
                enemies=new EnemyTokenSupply(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027")},new Random(0));
                map.Places[new AxialCoord(2,0)]=new PlaceState(PlaceType.Keep){Enemy=enemies.Draw(EnemyTokenSupply.GuardColor(PlaceType.Keep))};
            }
            var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,enemySupply:enemies,
                unitDeck:new[]{CardJsonLoader.LoadUnit("army_2_006"),CardJsonLoader.LoadUnit("army_2_002")},supplyRandom:new Random(0));
            j.PrepareRound();j.SelectTactic("day_1");j.BeginTurn();return j;
        }
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.First(c=>c.Id==id);
        private static SpellCard Offer(PveJourneySession j,string id)
        {var spell=new SpellCard(id);j.Context.SpellSupply.Offer.Clear();j.Context.SpellSupply.Offer.Add(spell);return spell;}
        private static void Use(PveJourneySession j,string payment,SpellCard spell,bool burn,string choice,PveDeedSelection inputs)
            =>j.PlayMaterialArtifact(Card(j,"items_023"),burn,Card(j,payment),spell:spell,spellChoice:choice,inputs:inputs);

        [Test] public void BurnedTomeTimeBendingReturnsPlayedButNotMaterialAndExpires()
        {
            var j=Start("basic_card_014",false,"basic_card_000","basic_card_008");var p=j.Player;
            var hand=p.Deck.Hand.ToArray();
            p.Deck.SetDeck(new[]{new DeedCard("basic_card_021",CardType.Action),new DeedCard("basic_card_022",CardType.Action)});
            p.Deck.Hand.AddRange(hand);
            var book=Card(j,"items_023");var material=Card(j,"basic_card_014");var march=Card(j,"basic_card_000");
            var reserve=p.Deck.DrawPile.ToArray();
            j.PlayOutsideBattle(march,"base");
            j.PlayMaterialArtifact(book,true,material,spell:Offer(j,"magic_016"),spellChoice:"strong");
            j.EndTurn();
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,march)),Is.True);
            Assert.That(p.Deck.DiscardPile.Any(c=>ReferenceEquals(c,material)),Is.True);
            Assert.That(p.Deck.RemovedCards.Any(c=>ReferenceEquals(c,book)),Is.True);
            Assert.That(p.Deck.RoundAsideCards,Is.Empty);
            Assert.That(p.Deck.DrawPile.ToArray(),Is.EqualTo(reserve));
            Assert.That(j.Context.TimeBendingReturnsPlayed,Is.False);
            j.BeginTurn();j.PlayOutsideBattle(march,"base");j.EndTurn();
            Assert.That(p.Deck.DiscardPile.Any(c=>ReferenceEquals(c,march)),Is.True);
            Assert.That(p.Deck.DrawPileCount,Is.Zero);
        }

        [Test] public void TranceReturnsSelectedActualDiscardAndFormalDrawConsumesIt()
        {
            var j=Start("basic_card_000",false,"basic_card_014","basic_card_008");var p=j.Player;
            var recovered=new[]{Card(j,"basic_card_014"),Card(j,"basic_card_008")};
            foreach(var c in recovered)p.Deck.TryDiscardExact(c);
            var spell=Offer(j,"magic_002");var book=Card(j,"items_023");
            Use(j,"basic_card_000",spell,true,"strong_top",new PveDeedSelection{Cards=recovered});
            Assert.That(p.Deck.DrawPile.Take(2),Is.EqualTo(recovered));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(book));
            Assert.That(j.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));Assert.That(j.Context.CardsFromDiscard,Is.Empty);
            j.EndTurn();foreach(var c in recovered)Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(c,x)),Is.True);
        }
        [Test] public void NonDefaultReadCrystalPaysRealOriginalSpellAndMovement()
        {
            var j=Start("basic_card_008",false,"magic_014");var spell=Offer(j,"magic_018");
            Use(j,"basic_card_008",spell,false,"base",new PveDeedSelection{Colors=new[]{ManaColor.Blue}});
            Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));
            j.PlayOutsideBattle(Card(j,"magic_014"),"base");j.Move(new AxialCoord(1,0));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(j.Player.Position,Is.EqualTo(new AxialCoord(1,0)));
        }
        [Test] public void SacrificeSeparatesTomePaymentAndMaterialsBeforeAnyMutation()
        {
            var j=Start("basic_card_021",false,"items_008","magic_014");var p=j.Player;var spell=Offer(j,"magic_011");
            var book=Card(j,"items_023");var payment=Card(j,"basic_card_021");var sword=Card(j,"items_008");
            foreach(var forbidden in new[]{book,payment})
            {
                Assert.Throws<InvalidOperationException>(()=>Use(j,"basic_card_021",spell,false,"base",new PveDeedSelection{Cards=new[]{forbidden},BonusChoices=forbidden==book?new[]{1}:Array.Empty<int>()}));
                Assert.That(p.Deck.Hand,Does.Contain(book));Assert.That(p.Deck.Hand,Does.Contain(payment));Assert.That(p.Deck.DiscardPile,Is.Empty);
            }
            Use(j,"basic_card_021",spell,false,"base",new PveDeedSelection{Cards=new[]{sword},BonusChoices=new[]{(int)ManaColor.Blue}});
            Assert.That(p.Deck.DiscardPile,Is.EquivalentTo(new[]{payment,sword}));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(book));
            Assert.That(j.Context.CardsToDiscard,Is.Empty);Assert.That(j.Context.CardToDiscard,Is.Null);
            j.PlayOutsideBattle(Card(j,"magic_014"),"base");Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);
        }
        [Test] public void HonorSummonsSelectsSecondActualOfferAndUnitActs()
        {
            var j=Start("basic_card_008");var spell=Offer(j,"magic_020");var offered=j.Context.UnitOffer.ElementAt(1);
            Use(j,"basic_card_008",spell,true,"strong",new PveDeedSelection{OfferedUnit=offered});
            var unit=j.Player.Units.Single();Assert.That(unit.Card,Is.SameAs(offered));Assert.That(j.Context.UnitOffer.Any(c=>ReferenceEquals(c,offered)),Is.False);
            Assert.That(j.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));
            int ability=Array.FindIndex(offered.Actions,a=>a.Kind==UnitAbilityKind.Influence || a.Kind==UnitAbilityKind.GainToken);
            Assert.That(ability,Is.GreaterThanOrEqualTo(0));j.UseOutsideUnit(unit,ability);
            Assert.That(j.Context.InfluencePool+j.Player.Mana.GetTotal(),Is.GreaterThan(0));
            Assert.That(unit.IsReady,Is.False);
        }
        [Test] public void TunnelValidatesActualGuardThenAttachesSameWorldBattleWithoutPrintedMana()
        {
            var j=Start("basic_card_000",true);var spell=Offer(j,"magic_004");var site=j.Game.Map.Places[new AxialCoord(2,0)];var guard=site.Enemy;
            var path=new PveDeedSelection{TravelPath=new[]{new AxialCoord(1,0),new AxialCoord(2,0)}};
            site.Enemy=guard with{};
            Assert.Throws<InvalidOperationException>(()=>Use(j,"basic_card_000",spell,true,"strong",path));
            Assert.That(j.Player.Deck.Hand.Count,Is.EqualTo(2));Assert.That(j.Player.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(j.Battle,Is.Null);
            site.Enemy=guard;Use(j,"basic_card_000",spell,true,"strong",path);
            Assert.That(j.Battle,Is.Not.Null);Assert.That(j.Battle.Context,Is.SameAs(j.Context));Assert.That(j.Battle.Enemies.Single(),Is.SameAs(guard));
            Assert.That(j.Player.Position,Is.EqualTo(new AxialCoord(2,0)));Assert.That(j.Player.Mana.GetTotal(),Is.Zero);
            Assert.That(j.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));Assert.That(j.Context.TravelPath,Is.Empty);
        }
    }
}
