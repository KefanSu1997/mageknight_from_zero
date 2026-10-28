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
    public sealed class PveManaSpellEntryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PveJourneySession Start(bool night, params string[] ids)
        {
            var p = new PlayerState();
            p.Deck.Hand.AddRange(ids.Select(id => new DeedCard(id, id.StartsWith("magic_") ? CardType.Spell : CardType.Action)));
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var j = new PveJourneySession(p, cards, spells: spells, manaRandom: new Random(0), tacticRandom: new Random(0), supplyRandom: new Random(0));
            if (night) j.Game.Clock.NextRound();
            j.SelectTactic(night ? "night_1" : "day_1"); j.BeginTurn(); return j;
        }
        private static DeedCard Card(PveJourneySession j, string id) => j.Player.Deck.Hand.Single(c => c.Id == id);
        private static int Tokens(PveJourneySession j, ManaColor color) => j.Player.Mana.Tokens.TryGetValue(color, out int n) ? n : 0;
        private static int Crystals(PveJourneySession j, ManaColor color) => j.Player.Mana.Crystals.TryGetValue(color, out int n) ? n : 0;
        private static PveDeedSelection Color(ManaColor color) => new PveDeedSelection { Colors = new[] { color } };
        private static string Consumer(ManaColor color) => color switch
        {
            ManaColor.Red => "magic_009", ManaColor.Blue => "magic_015", ManaColor.Green => "magic_005", ManaColor.White => "magic_021",
            _ => throw new InvalidOperationException("仅使用原基本色费用")
        };
        private static void Consume(PveJourneySession j, ManaColor color)
        {
            var card = Card(j, Consumer(color));
            if (color == ManaColor.Red || color == ManaColor.Blue)
            {
                var battle = j.BeginBattle(new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") with { } });
                battle.Play(card, "base");
                Assert.That(j.Context.RangedPool, Is.GreaterThan(0));
            }
            else
            {
                j.PlayOutsideBattle(card, color == ManaColor.White ? "base_Red" : "base");
                Assert.That(color == ManaColor.Green ? j.Context.Healing.Total : j.Context.InfluencePool, Is.GreaterThan(0));
            }
            Assert.That(j.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card)), Is.True);
        }

        [Test] public void ImmediateClaimTakesActualDieAndThreeTokensPayAnOriginalSpell()
        {
            var j = Start(false, "magic_012", "magic_009", "magic_015", "magic_005", "magic_021");
            var source = j.Game.Globals.Mana; var die = source.Dice.First(d => d.Face <= ManaColor.White); var color = die.Face;
            var host = Card(j, "magic_012"); j.Player.Mana.AddToken(ManaColor.Blue);
            j.PlayOutsideWithInputs(host, "base", Color(color));
            Assert.That(Tokens(j, color), Is.EqualTo(3)); Assert.That(source.RoundClaims.Single().Key, Is.SameAs(die));
            Assert.That(source.RoundClaims[die], Is.SameAs(j.Player)); Assert.That(source.Dice.Contains(die), Is.False);
            Assert.That(j.Player.HeldManaDie, Is.Null); Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(host));
            Consume(j, color); Assert.That(Tokens(j, color), Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)] public void DeferredClaimStartsNextFormalTurnAndReturnsTheSameDieAtRoundEnd(bool strong)
        {
            var j = Start(strong, "magic_012", "basic_card_000"); var source = j.Game.Globals.Mana;
            var all = source.Dice.ToArray(); var die = source.Dice.First(d => d.Face <= ManaColor.White); var color = die.Face;
            j.Player.Mana.AddToken(ManaColor.Blue); if (strong) j.Player.Mana.AddToken(ManaColor.Black);
            j.PlayOutsideWithInputs(Card(j, "magic_012"), strong ? "strong_deferred" : "base_deferred", Color(color));
            Assert.That(Tokens(j, color), Is.Zero); Assert.That(j.Player.TokensPerTurn[color], Is.EqualTo(1));
            Assert.That(j.Context.OtherPlayers, Is.Empty); Assert.That(j.Player.ManaCurseColor, Is.Null);
            var ordinary = source.Dice.First(d => d.Face <= ManaColor.White);
            CardManaActions.UseSourceDie(j.Player, j.Context, ordinary, ordinary.Face);
            Assert.That(j.Player.HeldManaDie, Is.SameAs(ordinary)); Assert.That(source.RoundClaims.Single().Key, Is.SameAs(die));
            j.EndTurn(); Assert.That(source.Dice.Contains(ordinary), Is.True); Assert.That(source.Dice.Contains(die), Is.False);
            j.BeginTurn(); Assert.That(Tokens(j, color), Is.EqualTo(1));
            j.PlayOutsideBattle(Card(j, "basic_card_000"), "base"); j.EndTurn(); j.AnnounceRoundEnd(); j.EndRound();
            Assert.That(source.RoundClaims, Is.Empty); Assert.That(source.Dice, Is.EquivalentTo(all));
            Assert.That(source.Dice.Count(d => ReferenceEquals(d, die)), Is.EqualTo(1)); Assert.That(j.Player.TokensPerTurn, Is.Empty);
        }

        [Test] public void AbsentClaimColorRejectsBeforeBlueFeeOrActualCardMovement()
        {
            var j = Start(false, "magic_012"); var host = Card(j, "magic_012"); var source = j.Game.Globals.Mana;
            var before = source.Dice.ToArray(); var color = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White }.First(c => source.GetAvailableCount(c) == 0);
            j.Player.Mana.AddToken(ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(() => j.PlayOutsideWithInputs(host, "base", Color(color)));
            Assert.That(Tokens(j, ManaColor.Blue), Is.EqualTo(1)); Assert.That(j.Player.Deck.Hand.Single(), Is.SameAs(host));
            Assert.That(j.Player.Deck.PlayedCards, Is.Empty); Assert.That(source.Dice, Is.EqualTo(before)); Assert.That(source.RoundClaims, Is.Empty);
        }

        [Test] public void BaseMeltdownPaysLastRedCrystalThenReceivesARealWoundWithoutOtherPlayers()
        {
            var j = Start(false, "magic_006", "basic_card_000"); var host = Card(j, "magic_006"); j.Player.Mana.AddCrystal(ManaColor.Red);
            j.PlayOutsideWithInputs(host, "base", new PveDeedSelection());
            Assert.That(Crystals(j, ManaColor.Red), Is.Zero); Assert.That(j.Player.Wounds, Is.EqualTo(1));
            Assert.That(j.Player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(1)); Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(host));
            Assert.That(j.Context.OtherPlayers, Is.Empty); Assert.That(j.Context.ManaMeltdownOwner, Is.Null); Assert.That(j.Context.ManaMeltdownChoices, Is.Empty);
        }

        [Test] public void StrongMeltdownPaysRedBlackThenWoundsOldBlueStockBeforeGivingTwoSpendableCrystals()
        {
            var j = Start(true, "magic_006", "magic_015", "basic_card_000");
            j.Player.Mana.AddToken(ManaColor.Red); j.Player.Mana.AddToken(ManaColor.Black); j.Player.Mana.AddCrystal(ManaColor.Blue);
            j.PlayOutsideWithInputs(Card(j, "magic_006"), "strong", Color(ManaColor.Blue));
            Assert.That(j.Player.Mana.Tokens.Values.Sum(), Is.Zero); Assert.That(j.Player.Wounds, Is.EqualTo(1)); Assert.That(Crystals(j, ManaColor.Blue), Is.EqualTo(3));
            Consume(j, ManaColor.Blue); Assert.That(Crystals(j, ManaColor.Blue), Is.EqualTo(2)); Assert.That(j.Player.Wounds, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)] public void StrongMeltdownRejectsDayOrMissingBlackBeforeAnyCostOrWound(bool night)
        {
            var j = Start(night, "magic_006"); var host = Card(j, "magic_006"); j.Player.Mana.AddToken(ManaColor.Red);
            if (!night) j.Player.Mana.AddToken(ManaColor.Black);
            j.Player.Mana.AddCrystal(ManaColor.Blue); int before = j.Player.Mana.Tokens.Values.Sum();
            Assert.Throws<InvalidOperationException>(() => j.PlayOutsideWithInputs(host, "strong", Color(ManaColor.Blue)));
            Assert.That(j.Player.Mana.Tokens.Values.Sum(), Is.EqualTo(before)); Assert.That(Crystals(j, ManaColor.Blue), Is.EqualTo(1));
            Assert.That(j.Player.Wounds, Is.Zero); Assert.That(j.Player.Deck.Hand.Single(), Is.SameAs(host)); Assert.That(j.Player.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(ManaColor.Red, false)] [TestCase(ManaColor.Blue, true)]
        [TestCase(ManaColor.Green, false)] [TestCase(ManaColor.White, true)]
        public void SoloMindReadChoosesOriginalBasicColorAndItsCrystalPaysAnOriginalCard(ManaColor color, bool strong)
        {
            var j = Start(strong, "magic_018", Consumer(color)); var host = Card(j, "magic_018");
            j.Player.Mana.AddToken(ManaColor.White); if (strong) j.Player.Mana.AddToken(ManaColor.Black);
            j.PlayOutsideWithInputs(host, strong ? "strong" : "base", Color(color));
            Assert.That(j.Player.Mana.Tokens.Values.Sum(), Is.Zero); Assert.That(Crystals(j, color), Is.EqualTo(1));
            Assert.That(j.Context.OtherPlayers, Is.Empty); Assert.That(j.Context.MindReadRevealedHands, Is.Empty);
            Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(host)); Assert.That(j.Player.Deck.Hand.Count, Is.EqualTo(1));
            Consume(j, color); Assert.That(Crystals(j, color), Is.Zero);
        }
    }
}
