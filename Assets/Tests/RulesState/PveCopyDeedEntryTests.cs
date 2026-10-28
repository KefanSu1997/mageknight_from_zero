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
    public sealed class PveCopyDeedEntryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static PveJourneySession Start(params string[] ids)
        {
            var player = new PlayerState();
            player.Deck.Hand.AddRange(ids.Select(id => new DeedCard(id, CardType.Action)));
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var journey = new PveJourneySession(player, cards, spells: spells, manaRandom: new Random(0), tacticRandom: new Random(0), supplyRandom: new Random(0));
            journey.SelectTactic("day_1"); journey.BeginTurn(); return journey;
        }
        private static DeedCard Card(PveJourneySession j, string id) => j.Player.Deck.Hand.Single(c => c.Id == id);
        private static int Tokens(PveJourneySession j, ManaColor color) => j.Player.Mana.Tokens.TryGetValue(color, out int n) ? n : 0;
        private static MK.Logic.Data.Monster Enemy() => CardJsonLoader.LoadMonsters().Single(c => c.Id == "monster_032") with { };
        private static AdvActionCard Advanced(PveJourneySession j, string id)
        {
            var actual = new AdvActionCard(id); j.Game.Globals.AdvActions.Offer.Clear();
            j.Game.Globals.AdvActions.SetDeck(Array.Empty<AdvActionCard>()); j.Game.Globals.AdvActions.Offer.Add(actual); return actual;
        }
        private static SpellCard Spell(PveJourneySession j, string id, params string[] refill)
        {
            var actual = new SpellCard(id); j.Game.Globals.Spells.Offer.Clear(); j.Game.Globals.Spells.Offer.Add(actual);
            j.Game.Globals.Spells.SetDeck(refill.Select(x => new SpellCard(x))); return actual;
        }

        [Test] public void OverloadDrawsTwoActualCardsThenChoosesHealingAndCannotStartBattleMidEffect()
        {
            var j = Start("advanced_card_024", "basic_card_001"); var host = Card(j, "advanced_card_024"); var donor = Card(j, "basic_card_001");
            var first = new DeedCard("basic_card_021", CardType.Action); var second = new DeedCard("basic_card_016", CardType.Action);
            j.Player.Deck.PutOnTop(second); j.Player.Deck.PutOnTop(first); j.Player.AddWoundCards(1);
            j.PlayOutsideWithInputs(host, "base", new PveDeedSelection { Cards = new[] { donor }, BorrowedChoice = "draw" });
            Assert.That(j.Player.Deck.Hand.Any(c => ReferenceEquals(c, first)), Is.True); Assert.That(j.Context.PendingRepeatedEffect.Remaining, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => j.BeginBattle(new[] { Enemy() }));
            Assert.That(j.Context.InBattle, Is.False); Assert.That(j.Context.PendingRepeatedEffect.Remaining, Is.EqualTo(2));
            var pending = j.Context.PendingRepeatedEffect;
            Assert.Throws<InvalidOperationException>(() => j.Game.ValidateEndCurrentTurn(j.Context));
            Assert.Throws<InvalidOperationException>(() => j.EndTurn());
            Assert.That(j.Game.IsEndingTurn, Is.False);
            Assert.That(j.Context.PendingRepeatedEffect, Is.SameAs(pending));
            Assert.That(pending.Remaining, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => j.ContinueCopiedEffect("not_an_original_choice"));
            Assert.That(j.Player.Deck.DrawPile.Single(), Is.SameAs(second));
            j.ContinueCopiedEffect("draw"); Assert.That(j.Player.Deck.Hand.Any(c => ReferenceEquals(c, second)), Is.True);
            j.ContinueCopiedEffect("heal"); Assert.That(j.Context.PendingRepeatedEffect, Is.Null); Assert.That(j.Context.Healing.Total, Is.EqualTo(1));
            j.HealOutsideBattle(); Assert.That(j.Player.Wounds, Is.Zero); Assert.That(j.Context.Healing.Total, Is.Zero);
            Assert.DoesNotThrow(() => j.Game.ValidateEndCurrentTurn(j.Context));
            Assert.That(j.Player.Deck.RemovedCards.Single(), Is.SameAs(donor)); Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(host));
            Assert.That(j.Player.Deck.Hand, Is.EquivalentTo(new[] { first, second }));
        }

        [Test] public void StrongOverloadClaimsFirstSpellThenChoosesDifferentActuallyReplenishedSpell()
        {
            var j = Start("advanced_card_024", "advanced_card_025"); var donor = Card(j, "advanced_card_025");
            var first = Spell(j, "magic_006", "magic_000"); j.Player.Mana.AddToken(ManaColor.Red, 2); j.Player.Mana.AddToken(ManaColor.Green);
            j.PlayOutsideWithInputs(Card(j, "advanced_card_024"), "strong", new PveDeedSelection {
                Cards = new[] { donor }, BorrowedChoice = "strong", BorrowedInputs = new PveDeedSelection { Spells = new[] { first } } });
            Assert.That(j.Player.Deck.DiscardPile.Single().Id, Is.EqualTo("magic_006")); Assert.That(Tokens(j, ManaColor.Red), Is.Zero);
            var actualRefill = j.Game.Globals.Spells.Offer.Single(); Assert.That(actualRefill.Id, Is.EqualTo("magic_000"));
            Assert.That(j.Context.PendingRepeatedEffect.Remaining, Is.EqualTo(1));
            j.ContinueCopiedEffect("strong", new PveDeedSelection { Spells = new[] { actualRefill } });
            Assert.That(j.Context.PendingRepeatedEffect, Is.Null); Assert.That(j.Game.Globals.Spells.Offer, Is.Empty);
            Assert.That(j.Player.Deck.DiscardPile.Select(c => c.Id), Is.EqualTo(new[] { "magic_000", "magic_006" }));
            Assert.That(Tokens(j, ManaColor.Green), Is.Zero); Assert.That(j.Player.Deck.RemovedCards.Single(), Is.SameAs(donor));
        }

        [Test] public void LaterCopyMissingFeeKeepsFirstClaimAndCanChooseAnotherLiveSupplyCard()
        {
            var j = Start("advanced_card_024", "advanced_card_025"); var first = Spell(j, "magic_006", "magic_000", "magic_007");
            j.Player.Mana.AddToken(ManaColor.Red, 3);
            j.PlayOutsideWithInputs(Card(j, "advanced_card_024"), "strong", new PveDeedSelection {
                Cards = new[] { Card(j, "advanced_card_025") }, BorrowedChoice = "strong", BorrowedInputs = new PveDeedSelection { Spells = new[] { first } } });
            var pending = j.Context.PendingRepeatedEffect; var offer = j.Game.Globals.Spells.Offer.ToArray();
            Assert.Throws<InvalidOperationException>(() => j.ContinueCopiedEffect("strong", new PveDeedSelection { Spells = new[] { offer[0] } }));
            Assert.That(j.Context.PendingRepeatedEffect, Is.SameAs(pending)); Assert.That(pending.Remaining, Is.EqualTo(1));
            Assert.That(j.Player.Deck.DiscardPile.Single().Id, Is.EqualTo("magic_006")); Assert.That(Tokens(j, ManaColor.Red), Is.EqualTo(1));
            Assert.That(j.Game.Globals.Spells.Offer[0], Is.SameAs(offer[0])); Assert.That(j.Game.Globals.Spells.Offer[1], Is.SameAs(offer[1]));
            j.ContinueCopiedEffect("strong", new PveDeedSelection { Spells = new[] { offer[1] } });
            Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offer[0])); Assert.That(j.Context.PendingRepeatedEffect, Is.Null);
            Assert.That(j.Player.Deck.DiscardPile.Select(c => c.Id), Is.EqualTo(new[] { "magic_007", "magic_006" })); Assert.That(Tokens(j, ManaColor.Red), Is.Zero);
        }

        [Test] public void PendingCombatCopyCannotAdvanceOrAttackUntilAllThreeOriginalAttacksComplete()
        {
            var j = Start("advanced_card_024", "basic_card_021"); var donor = Card(j, "basic_card_021"); var battle = j.BeginBattle(new[] { Enemy() });
            battle.Advance(); battle.Advance(); battle.Advance();
            battle.Play(Card(j, "advanced_card_024"), "base", inputs: new PveDeedSelection { Cards = new[] { donor }, BorrowedChoice = "attack" });
            Assert.That(j.Context.MeleePool, Is.EqualTo(2)); Assert.Throws<InvalidOperationException>(() => battle.Advance());
            Assert.Throws<InvalidOperationException>(() => battle.Attack(0)); Assert.That(battle.Phase, Is.EqualTo(Phase.Melee)); Assert.That(j.Context.PendingRepeatedEffect.Remaining, Is.EqualTo(2));
            battle.ContinueCopiedEffect("attack"); battle.ContinueCopiedEffect("attack"); Assert.That(j.Context.MeleePool, Is.EqualTo(6));
            Assert.That(battle.Attack(0).Kills, Is.EqualTo(1)); Assert.That(j.Player.Fame, Is.EqualTo(2)); Assert.That(j.Context.MeleePool, Is.Zero);
            Assert.That(j.Player.Deck.RemovedCards.Single(), Is.SameAs(donor));
        }

        [Test] public void MagicTalentDiscardsActualColoredCardAndPermissionPaysOriginalOfferedSpell()
        {
            var j = Start("advanced_card_025", "basic_card_021"); var material = Card(j, "basic_card_021"); var offered = Spell(j, "magic_009");
            j.PlayOutsideWithInputs(Card(j, "advanced_card_025"), "base", new PveDeedSelection { Cards = new[] { material } });
            Assert.That(j.Player.Deck.DiscardPile.Single(), Is.SameAs(material)); Assert.That(j.Context.SpellColors, Is.EqualTo(new[] { ManaColor.Red }));
            j.Player.Mana.AddToken(ManaColor.Red); var battle = j.BeginBattle(new[] { Enemy() }); j.PlayOfferedSpell(offered, "base");
            Assert.That(battle.Attack(0).Kills, Is.EqualTo(1)); Assert.That(j.Context.SpellColors, Is.Empty); Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
        }

        [Test] public void StrongMagicTalentRequiresTwoBlueForBlueSpellBeforeTakingActualSupply()
        {
            var j = Start("advanced_card_025"); var host = Card(j, "advanced_card_025"); var offered = Spell(j, "magic_015"); j.Player.Mana.AddToken(ManaColor.Blue);
            var input = new PveDeedSelection { Spells = new[] { offered } };
            Assert.Throws<InvalidOperationException>(() => j.PlayOutsideWithInputs(host, "strong", input));
            Assert.That(j.Player.Deck.Hand.Single(), Is.SameAs(host)); Assert.That(Tokens(j, ManaColor.Blue), Is.EqualTo(1)); Assert.That(j.Game.Globals.Spells.Offer.Single(), Is.SameAs(offered));
            j.Player.Mana.AddToken(ManaColor.Blue); j.PlayOutsideWithInputs(host, "strong", input);
            Assert.That(Tokens(j, ManaColor.Blue), Is.Zero); Assert.That(j.Game.Globals.Spells.Offer, Is.Empty);
            Assert.That(j.Player.Deck.DiscardPile.Single().Id, Is.EqualTo("magic_015")); Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(host));
        }

        [Test] public void BaseBloodlinePaysMatchingColorGainsOriginalCardAndImmediatelyPlaysIt()
        {
            var j = Start("advanced_card_030"); var offered = Advanced(j, "advanced_card_000"); j.Player.Mana.AddToken(ManaColor.Red);
            j.PlayOutsideWithInputs(Card(j, "advanced_card_030"), "base", new PveDeedSelection { Advanced = offered });
            Assert.That(j.Player.Wounds, Is.EqualTo(1)); Assert.That(j.Player.DiscardWounds, Is.Zero); Assert.That(Tokens(j, ManaColor.Red), Is.Zero);
            Assert.That(j.Game.Globals.AdvActions.Offer, Is.Empty); var gained = Card(j, "advanced_card_000");
            j.PlayOutsideBattle(gained, "base"); Assert.That(j.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, gained)), Is.True);
            Assert.That(j.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(j.Player.Deck.Hand.Single().Type, Is.EqualTo(CardType.Wound));
        }

        [TestCase(false)] [TestCase(true)] public void StrongBloodlineBorrowsActualRangedEffectAndPreservesChosenWoundDestination(bool discard)
        {
            var j = Start("advanced_card_030"); var offered = Advanced(j, "advanced_card_000"); j.Player.Mana.AddToken(ManaColor.Red);
            var battle = j.BeginBattle(new[] { Enemy() }); battle.Play(Card(j, "advanced_card_030"), "strong", inputs: new PveDeedSelection {
                Advanced = offered, BorrowedChoice = "strong", WoundToDiscard = discard });
            Assert.That(j.Context.RangedPool, Is.EqualTo(3)); Assert.That(j.Context.CombatPower.Attacks.Single().Element, Is.EqualTo(Element.Fire));
            Assert.That(j.Player.Wounds, Is.EqualTo(discard ? 0 : 1)); Assert.That(j.Player.DiscardWounds, Is.EqualTo(discard ? 1 : 0));
            Assert.That(j.Game.Globals.AdvActions.Offer.Single(), Is.SameAs(offered)); Assert.That(j.Player.Deck.Hand.Any(c => c.Id == offered.Id), Is.False);
            Assert.That(battle.Attack(0).Kills, Is.EqualTo(1)); Assert.That(j.Player.Fame, Is.EqualTo(2)); Assert.That(Tokens(j, ManaColor.Red), Is.Zero);
        }

        [Test] public void IllegalBorrowedPhaseRejectsHostFeePermanentRemovalAndBloodlineWound()
        {
            var overload = Start("advanced_card_024", "basic_card_021"); overload.Player.Mana.AddToken(ManaColor.Red);
            var donor = Card(overload, "basic_card_021"); var b = overload.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => b.Play(Card(overload, "advanced_card_024"), "strong", inputs: new PveDeedSelection { Cards = new[] { donor }, BorrowedChoice = "strong" }));
            Assert.That(overload.Player.Deck.Hand.Count, Is.EqualTo(2)); Assert.That(overload.Player.Deck.RemovedCards, Is.Empty); Assert.That(Tokens(overload, ManaColor.Red), Is.EqualTo(1));
            var bloodline = Start("advanced_card_030"); var offered = Advanced(bloodline, "advanced_card_004"); bloodline.Player.Mana.AddToken(ManaColor.Red);
            var battle = bloodline.BeginBattle(new[] { Enemy() });
            Assert.Throws<InvalidOperationException>(() => battle.Play(Card(bloodline, "advanced_card_030"), "strong", inputs: new PveDeedSelection { Advanced = offered, BorrowedChoice = "strong" }));
            Assert.That(bloodline.Player.Wounds + bloodline.Player.DiscardWounds, Is.Zero); Assert.That(bloodline.Player.Deck.Hand.Single().Id, Is.EqualTo("advanced_card_030"));
            Assert.That(Tokens(bloodline, ManaColor.Red), Is.EqualTo(1)); Assert.That(bloodline.Game.Globals.AdvActions.Offer.Single(), Is.SameAs(offered));
        }
    }
}
