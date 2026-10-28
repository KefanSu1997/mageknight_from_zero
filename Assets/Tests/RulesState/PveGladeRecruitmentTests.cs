using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveGladeRecruitmentTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PveJourneySession journey, UnitCard offered) Start(bool village = false)
        {
            var map = new MapState();
            var supply = new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Where(m => m.TokenColor == EnemyTokenColor.Green));
            OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map, supply);
            var p = new PlayerState { Reputation = -7, Position = map.Places.First(x => x.Value.Type == (village ? PlaceType.Village : PlaceType.MagicalGlade)).Key };
            var influence = new DeedCard("basic_card_008", CardType.Action); var second = new DeedCard("basic_card_008", CardType.Action);
            p.Deck.Hand.Add(influence); p.Deck.Hand.Add(second); p.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            var j = new PveJourneySession(p, CardJsonLoader.LoadBasicActions(), map: map, enemySupply: supply);
            var offered = CardJsonLoader.LoadUnit("army_2_004"); j.Game.Globals.Units.SetDeck(new[] { offered });
            j.SelectTactic("day_1"); j.BeginTurn();
            // The original glade start token pays the printed white strong Influence cost.
            if (village) p.Mana.AddToken(ManaColor.White);
            else j.ConvertGold(ManaColor.White);
            j.PlayOutsideBattle(influence, "strong"); j.PlayOutsideBattle(second, "base");
            Assert.That(j.Context.InfluencePool, Is.EqualTo(6)); Assert.That(p.Mana.GetTotal(), Is.Zero);
            return (j, offered);
        }

        [Test] public void OriginalGladeRecruitsSameOfferedCardWithActualFeeAttunementAndAction()
        {
            var (j, offered) = Start(); j.Player.Mana.AddCrystal(ManaColor.Red);
            Assert.That(j.AtMagicalGlade, Is.True); j.RecruitGladeUnit(offered, ManaColor.Red);
            var unit = j.Player.Units.Single(); Assert.That(unit.Card, Is.SameAs(offered)); Assert.That(unit.AttunementColor, Is.EqualTo(ManaColor.Red));
            Assert.That(j.Game.Globals.Units.Offer, Is.Empty); Assert.That(j.Context.InfluencePool, Is.Zero); Assert.That(j.Player.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(j.Context.SpentCrystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(j.Player.Reputation, Is.EqualTo(-7));
            Assert.That(j.Context.TurnActionCommitted, Is.True); Assert.That(j.Context.InNegotiation, Is.False); Assert.That(j.Context.InteractionReputationModifier, Is.Null);
            Assert.That(unit.IsReady, Is.True); Assert.Throws<InvalidOperationException>(() => j.BeginVillageInteraction());
        }

        [TestCase("village")] [TestCase("foreign")] [TestCase("mana")] [TestCase("rest")] [TestCase("slots")] [TestCase("color")]
        public void InvalidGladeRecruitPreservesActualOfferResourcesAndOwnedUnits(string failure)
        {
            var (j, offered) = Start(failure == "village"); var p = j.Player;
            if (failure != "mana") p.Mana.AddCrystal(ManaColor.Red);
            if (failure == "rest") RestActions.Rest(p, j.Context, p.Deck.Hand.Single(), Array.Empty<DeedCard>());
            if (failure == "slots") p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var originalUnits = p.Units.ToArray(); int mana = p.Mana.GetTotal(); bool action = j.Context.TurnActionCommitted;
            Assert.Throws<InvalidOperationException>(() => j.RecruitGladeUnit(failure == "foreign" ? CardJsonLoader.LoadUnit("army_2_004") : offered,
                failure == "color" ? ManaColor.Gold : ManaColor.Red));
            Assert.That(j.Game.Globals.Units.Offer.Single(), Is.SameAs(offered)); Assert.That(p.Units, Is.EqualTo(originalUnits));
            Assert.That(j.Context.InfluencePool, Is.EqualTo(6)); Assert.That(p.Mana.GetTotal(), Is.EqualTo(mana));
            Assert.That(j.Context.TurnActionCommitted, Is.EqualTo(action)); Assert.That(j.Context.HasRecruited, Is.False); Assert.That(j.Context.InNegotiation, Is.False);
        }
    }
}
