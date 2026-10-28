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
    // New public entry coverage; formal effects, original cards and original enemy tokens only.
    public sealed class PveShieldCharmEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static ActionCardData[] Originals(SpellCardData[] spells)=>CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
            .Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost))).ToArray();
        private static PveBattleSession Start(string[] cards,params string[] enemies)
        {
            var p=new PlayerState();
            foreach(string id in cards)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();var catalog=CardJsonLoader.LoadMonsters();
            return new PveBattleSession(p,enemies.Select(id=>catalog.Single(e=>e.Id==id) with {}),Originals(spells),new ActionContext{DayPart=DayPart.Night},spells:spells);
        }
        private static DeedCard Card(PveBattleSession b,string id)=>b.Player.Deck.Hand.First(c=>c.Id==id);
        private static int Tokens(PlayerState p,ManaColor c)=>p.Mana.Tokens.TryGetValue(c,out int n)?n:0;
        private static int Crystals(PlayerState p,ManaColor c)=>p.Mana.Crystals.TryGetValue(c,out int n)?n:0;
        private static void Melee(PveBattleSession b){b.Advance();b.Advance();b.Advance();}
        private static PveJourneySession Village(params string[] ids)
        {
            var p=new PlayerState();p.Deck.SetDeck(ids.Select(id=>new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:CardType.Action)));
            var map=new MapState();
            foreach(var pos in new[]{new AxialCoord(0,0),new AxialCoord(0,1)})
                map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            map.Places.Add(new AxialCoord(0,1),new PlaceState(PlaceType.Village));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var j=new PveJourneySession(p,Originals(spells),map:map,spells:spells,
                unitDeck:new[]{"army_2_006","army_2_002","army_2_001"}.Select(CardJsonLoader.LoadUnit));
            j.SelectTactic("day_1");j.BeginTurn();
            j.PlayOutsideBattle(p.Deck.Hand.Single(c=>c.Id=="basic_card_000"),"base");j.Move(new AxialCoord(0,1));j.BeginVillageInteraction();return j;
        }

        [Test] public void BurningShieldActuallyBlocksThenItsFireAttackKillsDifferentOriginalEnemy()
        {
            var b=Start(new[]{"magic_008"},"monster_032","monster_032");var card=Card(b,"magic_008");b.Player.Mana.AddToken(ManaColor.Red);
            b.Advance();b.Play(card,"base");Assert.That(b.BlockPreview(0),Is.EqualTo((4,4)));b.Block(0);
            Assert.That(b.Context.MeleePool,Is.Zero);b.Advance();b.Advance();
            Assert.That(b.Player.Wounds,Is.EqualTo(2));Assert.That(b.Context.MeleePool,Is.EqualTo(4));
            Assert.That(b.Context.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Fire));
            Assert.That(b.Attack(1).Kills,Is.EqualTo(1));Assert.That(b.Context.DefeatedEnemyIndices,Is.EquivalentTo(new[]{1}));
            Assert.That(b.Player.Fame,Is.EqualTo(2));Assert.That(b.Context.MeleePool,Is.Zero);
            Assert.That(b.Player.Deck.PlayedCards.Single(),Is.SameAs(card));Assert.That(Tokens(b.Player,ManaColor.Red),Is.Zero);
        }
        [Test] public void ExplodingShieldPaysPrintedNightCostAndDestroysOnlySuccessfullyBlockedEnemyOnce()
        {
            var b=Start(new[]{"magic_008"},"monster_032","monster_032");var card=Card(b,"magic_008");
            b.Player.Mana.AddToken(ManaColor.Red);b.Player.Mana.AddToken(ManaColor.Black);
            b.Advance();b.Play(card,"strong");b.Block(0);b.Advance();b.Advance();
            Assert.That(b.Context.DefeatedEnemyIndices,Is.EquivalentTo(new[]{0}));Assert.That(b.Player.Fame,Is.EqualTo(2));
            Assert.That(b.Player.Wounds,Is.EqualTo(2));Assert.That(b.Context.MeleePool,Is.Zero);
            Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.Zero);Assert.That(b.Player.Deck.PlayedCards.Single(),Is.SameAs(card));
            b.Advance();Assert.That(b.Player.Fame,Is.EqualTo(2));Assert.Throws<InvalidOperationException>(()=>b.Advance());
        }
        [TestCase(false)] [TestCase(true)] public void ShieldWrongPhaseOrDayStrongRejectsWithoutSpending(bool day)
        {
            var b=Start(new[]{"magic_008"},"monster_032");var card=Card(b,"magic_008");
            b.Player.Mana.AddToken(ManaColor.Red);b.Player.Mana.AddToken(ManaColor.Black);
            if(day){b.Context.DayPart=DayPart.Day;b.Advance();}
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,day?"strong":"base"));
            Assert.That(b.Player.Deck.Hand.Single(),Is.SameAs(card));Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.EqualTo(2));
            Assert.That(b.Context.BlockPool,Is.Zero);Assert.That(b.Player.Deck.PlayedCards,Is.Empty);
        }
        [Test] public void CharmOutsideNegotiationHasInfluenceButNoFreeCrystalOrDiscount()
        {
            var b=Start(new[]{"magic_021","magic_021"},"monster_032");b.Player.Mana.AddToken(ManaColor.White,2);
            b.Play(Card(b,"magic_021"),"base_Red");b.Play(Card(b,"magic_021"),"base_discount");
            Assert.That(b.Context.InfluencePool,Is.EqualTo(8));Assert.That(b.Player.Mana.Crystals.Values.Sum(),Is.Zero);
            Assert.That(b.Context.CharmRecruitDiscount,Is.Zero);Assert.That(Tokens(b.Player,ManaColor.White),Is.Zero);
        }
        [Test] public void CharmVillageDiscountIsConsumedByRealOriginalFarmerRecruitment()
        {
            var j=Village("basic_card_000","magic_021");var p=j.Player;p.Mana.AddToken(ManaColor.White);
            var charm=p.Deck.Hand.Single();var actual=j.Game.Globals.Units.Offer[0];
            j.PlayOutsideBattle(charm,"base_discount");Assert.That(j.Context.InfluencePool,Is.EqualTo(4));Assert.That(j.Context.CharmRecruitDiscount,Is.EqualTo(3));
            j.RecruitVillageUnit(actual);Assert.That(p.Units.Single().Card,Is.SameAs(actual));
            Assert.That(j.Context.InfluencePool,Is.EqualTo(3));Assert.That(j.Context.CharmRecruitDiscount,Is.Zero);
            Assert.That(j.Game.Globals.Units.Offer.Any(c=>ReferenceEquals(c,actual)),Is.False);Assert.That(Tokens(p,ManaColor.White),Is.Zero);
        }
        [Test] public void CharmChosenCrystalActuallyPaysNextOriginalSpellInSameVillageInteraction()
        {
            var j=Village("basic_card_000","magic_021","magic_021");var p=j.Player;p.Mana.AddToken(ManaColor.White);
            var first=p.Deck.Hand[0];var second=p.Deck.Hand[1];
            j.PlayOutsideBattle(first,"base_White");Assert.That(Crystals(p,ManaColor.White),Is.EqualTo(1));Assert.That(Tokens(p,ManaColor.White),Is.Zero);
            j.PlayOutsideBattle(second,"base_discount");Assert.That(Crystals(p,ManaColor.White),Is.Zero);Assert.That(j.Context.InfluencePool,Is.EqualTo(8));
            Assert.That(p.Deck.PlayedCards.Any(c=>ReferenceEquals(c,first)),Is.True);Assert.That(p.Deck.PlayedCards.Any(c=>ReferenceEquals(c,second)),Is.True);
            j.RecruitVillageUnit(j.Game.Globals.Units.Offer[0]);Assert.That(j.Context.InfluencePool,Is.EqualTo(7));Assert.That(j.Context.CharmRecruitDiscount,Is.Zero);
        }
        [Test] public void PossessionKeepsOriginalSourceForbiddenAndUsesItsActualNormalAttackAgainstOtherEnemy()
        {
            var b=Start(new[]{"magic_021"},"monster_032","monster_032");var source=b.Enemies[0];var other=b.Enemies[1];
            Assert.That(ReferenceEquals(source,other),Is.False);b.Player.Mana.AddToken(ManaColor.White);b.Player.Mana.AddToken(ManaColor.Black);
            b.Play(Card(b,"magic_021"),"strong",0);Assert.That(b.Context.RangedPool,Is.Zero);Assert.That(b.Context.MeleePool,Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(()=>b.Attack(1));Assert.That(b.Context.MeleePool,Is.EqualTo(4));
            Melee(b);Assert.That(b.Player.Wounds,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>b.Attack(0));Assert.That(b.Context.MeleePool,Is.EqualTo(4));
            Assert.That(b.Context.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Physical));Assert.That(b.Attack(1).Kills,Is.EqualTo(1));
            Assert.That(b.Context.DefeatedEnemyIndices,Is.EquivalentTo(new[]{1}));Assert.That(b.Player.Fame,Is.EqualTo(2));
            Assert.That(b.Enemies[0],Is.SameAs(source));Assert.That(b.Enemies[1],Is.SameAs(other));Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.Zero);
        }
        [Test] public void PossessionArcaneImmuneSourceRejectsBeforeAnyManaCardOrSkipMutation()
        {
            var b=Start(new[]{"magic_021"},"monster_056","monster_032");var card=Card(b,"magic_021");
            Assert.That(b.Enemies[0].Abilities,Does.Contain(Ability.MagicResist));b.Player.Mana.AddToken(ManaColor.White);b.Player.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,"strong",0));Assert.That(b.Player.Deck.Hand.Single(),Is.SameAs(card));
            Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.EqualTo(2));Assert.That(b.Context.SkipAttackIndices,Is.Empty);
            Assert.That(b.Context.PossessedEnemyIndices,Is.Empty);Assert.That(b.Context.CombatPower.Attacks,Is.Empty);Assert.That(b.Player.Deck.PlayedCards,Is.Empty);
        }
    }
}
