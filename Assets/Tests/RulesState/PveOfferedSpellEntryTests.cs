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
    public sealed class PveOfferedSpellEntryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PveJourneySession Start(string offered, string material = "basic_card_021", bool night = false)
        {
            var p = new PlayerState();
            p.Deck.Hand.AddRange(new[] { new DeedCard("advanced_card_025", CardType.Action), new DeedCard(material, CardType.Action) });
            var spells = CardJsonLoader.LoadSpells().Where(c => c.Id == offered).ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var j = new PveJourneySession(p, cards, spells: spells, manaRandom: new Random(0), tacticRandom: new Random(0));
            if (night) j.Game.Clock.NextRound();
            j.SelectTactic(night ? "night_1" : "day_1"); j.BeginTurn();
            return j;
        }

        private static void Permit(PveJourneySession j)
        {
            var source = j.Player.Deck.Hand.Single(c => c.Id == "advanced_card_025");
            var material = j.Player.Deck.Hand.Single(c => c.Type == CardType.Action && c.Id != source.Id);
            var old = j.Context.CardToDiscard;
            try
            {
                j.Context.CardToDiscard = material;
                CardPlayActions.Play(j.Player, j.Context, source, CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == source.Id));
            }
            finally { j.Context.CardToDiscard = old; }
        }

        private static MK.Logic.Data.Monster Enemy() => CardJsonLoader.LoadMonsters().Single(c => c.Id == "monster_032") with { };
        private static int Tokens(PveJourneySession j, ManaColor color) => j.Player.Mana.Tokens.TryGetValue(color, out var n) ? n : 0;

        [Test] public void ActualPermissionPaysOriginalFireballAndKillsWithoutMovingSupplyCard()
        {
            var j = Start("magic_009"); Permit(j); var offered = j.Game.Globals.Spells.Offer.Single();
            j.Player.Mana.AddToken(ManaColor.Red); var battle = j.BeginBattle(new[] { Enemy() });
            j.PlayOfferedSpell(offered, "base");
            Assert.That(j.Context.RangedPool, Is.EqualTo(5)); Assert.That(Tokens(j, ManaColor.Red), Is.Zero);
            Assert.That(j.Context.SpellColors, Is.Empty); Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
            Assert.That(j.Player.Deck.Hand, Is.Empty); Assert.That(j.Player.Deck.PlayedCards.Single().Id, Is.EqualTo("advanced_card_025"));
            Assert.That(j.Player.Deck.DiscardPile.Single().Id, Is.EqualTo("basic_card_021"));
            Assert.That(battle.Attack(0).Kills, Is.EqualTo(1)); Assert.That(j.Player.Fame, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base"));
        }

        [Test] public void ForgedSupplyReferenceAndMissingPermissionPreserveMoneyAndActualSupply()
        {
            var j = Start("magic_009"); var offered = j.Game.Globals.Spells.Offer.Single();
            j.Player.Mana.AddToken(ManaColor.Red); j.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base"));
            Permit(j);
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(new SpellCard(offered.Id), "base"));
            Assert.That(Tokens(j, ManaColor.Red), Is.EqualTo(1)); Assert.That(j.Context.SpellColors, Is.EqualTo(new[] { ManaColor.Red }));
            Assert.That(j.Context.RangedPool, Is.Zero); Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
        }

        [Test] public void WrongPhaseAndMissingOriginalFeeKeepGrantedPermission()
        {
            var j = Start("magic_009"); Permit(j); var offered = j.Game.Globals.Spells.Offer.Single();
            j.Player.Mana.AddToken(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base"));
            Assert.That(Tokens(j, ManaColor.Red), Is.EqualTo(1));
            var battle = j.BeginBattle(new[] { Enemy() }); battle.Advance();
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base"));
            Assert.That(j.Context.SpellColors.Count, Is.EqualTo(1)); Assert.That(j.Context.RangedPool, Is.Zero);
            var noMana = Start("magic_009"); Permit(noMana); noMana.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => noMana.PlayOfferedSpell(noMana.Game.Globals.Spells.Offer.Single(), "base"));
            Assert.That(noMana.Context.SpellColors.Count, Is.EqualTo(1)); Assert.That(noMana.Context.RangedPool, Is.Zero);
        }

        [Test] public void TargetedSpellRejectsInvalidEnemyBeforePaymentThenStopsActualEnemy()
        {
            var j = Start("magic_013", "basic_card_016"); Permit(j); var offered = j.Game.Globals.Spells.Offer.Single();
            j.Player.Mana.AddToken(ManaColor.Blue); var battle = j.BeginBattle(new[] { Enemy(), Enemy() });
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base", -1));
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "base", 2));
            Assert.That(Tokens(j, ManaColor.Blue), Is.EqualTo(1)); Assert.That(j.Context.SpellColors.Count, Is.EqualTo(1));
            j.PlayOfferedSpell(offered, "base", 1); Assert.That(Tokens(j, ManaColor.Blue), Is.Zero);
            battle.Advance(); battle.Advance(); battle.Advance();
            Assert.That(j.Player.Wounds, Is.EqualTo(2), "仅未被冰冻的第一只原怪物造成伤害");
            Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
        }

        [Test] public void StrongOfferCastNeedsNightAndBlackThenPaysBothColorsAndActualWound()
        {
            var day = Start("magic_009"); Permit(day); day.Player.Mana.AddToken(ManaColor.Red); day.Player.Mana.AddToken(ManaColor.Black);
            day.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => day.PlayOfferedSpell(day.Game.Globals.Spells.Offer.Single(), "strong"));
            Assert.That(day.Context.SpellColors.Count, Is.EqualTo(1)); Assert.That(Tokens(day, ManaColor.Red), Is.EqualTo(1));
            var j = Start("magic_009", night: true); Permit(j); j.Player.Mana.AddToken(ManaColor.Red);
            var offered = j.Game.Globals.Spells.Offer.Single(); var battle = j.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => j.PlayOfferedSpell(offered, "strong"));
            Assert.That(Tokens(j, ManaColor.Red), Is.EqualTo(1)); Assert.That(j.Context.SpellColors.Count, Is.EqualTo(1));
            j.Player.Mana.AddToken(ManaColor.Black); j.PlayOfferedSpell(offered, "strong");
            Assert.That(j.Context.SiegePool, Is.EqualTo(8)); Assert.That(Tokens(j, ManaColor.Red) + Tokens(j, ManaColor.Black), Is.Zero);
            Assert.That(j.Player.Wounds, Is.EqualTo(1)); Assert.That(j.Context.SpellColors, Is.Empty);
            Assert.That(battle.Attack(0).Kills, Is.EqualTo(1)); Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
        }

        [Test] public void OfferedSacrificeUsesRealMaterialInputAndRestoresCallerScope()
        {
            var j = Start("magic_011"); Permit(j); var material = new DeedCard("basic_card_000", CardType.Action);
            j.Player.Deck.Hand.Add(material); j.Player.Mana.AddToken(ManaColor.Red);
            var offered = j.Game.Globals.Spells.Offer.Single();
            j.PlayOfferedSpell(offered, "base", inputs: new PveDeedSelection { Cards = new[] { material } });
            Assert.That(j.Player.Deck.Hand, Is.Empty); Assert.That(j.Player.Deck.DiscardPile.Any(c => ReferenceEquals(c, material)), Is.True);
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1)); Assert.That(j.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            Assert.That(j.Context.CardsToDiscard, Is.Empty); Assert.That(j.Context.SpellColors, Is.Empty);
            Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
        }
    }
}
