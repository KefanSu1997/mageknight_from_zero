using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveJourneyRecoveryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static UnitState Unit(string id="army_2_006")=>new(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"),id));
        private static PveJourneySession Create(params string[] ids)
        {
            var spells=CardJsonLoader.LoadSpells();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var p=new PlayerState();
            foreach(string id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic")?CardType.Spell:CardType.Action));
            p.Mana.AddCrystal(ManaColor.Green,3);
            var journey=new PveJourneySession(p,cards,spells:spells);journey.SelectTactic("day_1");journey.BeginTurn();return journey;
        }
        [TestCase("basic_card_001","heal",1,0)]
        [TestCase("basic_card_001","strong_heal",2,1)]
        [TestCase("basic_card_004","heal",1,0)]
        [TestCase("basic_card_005","strong_0",2,1)]
        [TestCase("advanced_card_007","base",1,0)]
        [TestCase("advanced_card_015","strong",2,1)]
        [TestCase("magic_005","base",3,1)]
        public void OriginalRecoveryGeneratesPointsThenConsumesActualHandWound(string id,string choice,int points,int greenFee)
        {
            var s=Create(id,"basic_card_000");var p=s.Player;var card=p.Deck.Hand[0];p.Deck.GainWound();
            s.PlayOutsideBattle(card,choice);Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(s.Context.Healing.Total,Is.EqualTo(points));
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(3-greenFee));
            s.HealOutsideBattle();Assert.That(p.Wounds,Is.Zero);Assert.That(s.Context.Healing.Total,Is.EqualTo(points-1));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));Assert.That(s.Context.TurnActionCommitted,Is.False);
            s.EndTurn();Assert.That(s.Context.Healing.Total,Is.Zero);Assert.Throws<InvalidOperationException>(()=>s.HealOutsideBattle());
            s.BeginTurn();Assert.That(s.Player,Is.SameAs(p));Assert.That(p.Wounds,Is.Zero);
        }
        [Test] public void TranquilityDrawAndCureRewardUseActualDeckInstances()
        {
            var s=Create("basic_card_001","magic_001","basic_card_000");var p=s.Player;var first=p.Deck.Hand[0];var cure=p.Deck.Hand[1];
            var top=new DeedCard("basic_card_016",CardType.Action);p.Deck.PutOnTop(top);
            s.PlayOutsideBattle(first,"draw");Assert.That(p.Deck.Hand.Contains(top),Is.True);Assert.That(s.Context.Healing.Total,Is.Zero);
            var reward=new DeedCard("basic_card_021",CardType.Action);p.Deck.PutOnTop(reward);p.Deck.GainWound();
            s.PlayOutsideBattle(cure,"base");s.HealOutsideBattle();
            Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.Hand.Contains(reward),Is.True);Assert.That(p.Deck.DrawPileCount,Is.Zero);
        }
        [Test] public void RegenerationReadiesButSeparateLevelCostHealingMakesUnitUsable()
        {
            var s=Create("advanced_card_015","basic_card_000","basic_card_014");var p=s.Player;var u=Unit();p.Units.Add(u);u.Exhaust();u.AddWounds(1);
            s.PlayOutsideBattle(p.Deck.Hand[0],"strong",u);
            Assert.That(u.IsReady,Is.True);Assert.That(u.Wounds,Is.EqualTo(1));Assert.That(u.CanActivate,Is.False);
            Assert.That(u.Card.Level,Is.EqualTo(1));s.HealOutsideBattle(u);Assert.That(s.Context.Healing.Total,Is.EqualTo(1));Assert.That(u.CanActivate,Is.True);
            var b=s.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});b.Advance();b.UseUnit(u,0,1);
            foreach(var card in p.Deck.Hand.ToArray())b.PlaySideways(card);
            b.Block(0);
            Assert.That(u.IsReady,Is.False);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
        }
        [Test] public void InvalidReadyTargetAndCombatTimingRejectBeforeFeeOrCardMovement()
        {
            var s=Create("basic_card_005","basic_card_000");var p=s.Player;var c=p.Deck.Hand[0];
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideBattle(c,"strong_3",Unit("army_2_003")));
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(3));Assert.That(p.Deck.Hand.Contains(c),Is.True);
            s.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideBattle(c,"base_0"));
            Assert.Throws<InvalidOperationException>(()=>s.HealOutsideBattle());Assert.That(p.Deck.PlayedCards.Count,Is.Zero);
        }
        [Test] public void EnergyFlowAndMultiTargetRebirthKeepUnitIdentitiesAndWoundsSeparate()
        {
            var s=Create("magic_000","magic_005","basic_card_000");var p=s.Player;var flow=p.Deck.Hand[0];var rebirth=p.Deck.Hand[1];
            var a=Unit();var b=Unit("army_2_011");p.Units.Add(a);p.Units.Add(b);a.Exhaust();a.AddWounds(1);b.Exhaust();
            s.PlayOutsideBattle(flow,"base",a);Assert.That(a.IsReady,Is.True);Assert.That(a.Wounds,Is.EqualTo(1));a.Exhaust();
            s.Context.DayPart=DayPart.Night;s.Context.CurrentTerrain=TerrainType.Forest;p.Mana.AddToken(ManaColor.Black);
            s.PlayOutsideBattle(rebirth,"strong",a,b);Assert.That(a.IsReady&&b.IsReady,Is.True);Assert.That(a.Wounds,Is.EqualTo(1));
            Assert.That(s.Context.Healing.Total,Is.EqualTo(5));s.HealOutsideBattle(a);Assert.That(s.Context.Healing.Total,Is.EqualTo(4));
            Assert.That(s.Context.TargetUnit,Is.Null);Assert.That(s.Context.ReadyUnitTargets,Is.Null);
        }
    }
}
