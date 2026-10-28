using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    /// <summary>
    /// Original advanced_card_024: throw away another Action from hand, repeat its
    /// basic effect three times or its strong effect twice without its printed cost.
    /// The option belongs to that original effect, never to ActionEffectId.
    /// </summary>
    public sealed class OverloadStateTests
    {
        [SetUp]
        public void LoadOriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard source, DeedCard material) Fixture(
            string materialId = "basic_card_021")
        {
            var p = new PlayerState();
            var source = new DeedCard("advanced_card_024", CardType.Action);
            var material = new DeedCard(materialId, CardType.Action);
            p.Deck.Hand.AddRange(new[] { source, material });
            p.Mana.AddToken(ManaColor.Red);
            return (p, new ActionContext { CardToRemove = material, SkipDraw = true }, source, material);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard source, bool strong, int option = 0)
            => CardPlayActions.Play(p, c, source,
                CardJsonLoader.LoadAdvancedActions().Single(card => card.Id == source.Id), strong, option);

        private static int[] Pools(ActionContext c)
            => new[] { c.MovementPool, c.InfluencePool, c.MeleePool, c.BlockPool, c.RangedPool, c.SiegePool };

        [TestCase("basic_card_000", false, 0, 6)]
        [TestCase("basic_card_000", true, 0, 8)]
        [TestCase("basic_card_008", false, 1, 6)]
        [TestCase("basic_card_008", true, 1, 8)]
        [TestCase("basic_card_021", false, 2, 6)]
        [TestCase("basic_card_021", true, 2, 8)]
        [TestCase("basic_card_016", true, 3, 10)]
        [TestCase("advanced_card_003", true, 5, 6)]
        public void PrintedMaterialDeterminesEffectAndOnlyHostPrintedManaIsPaid(
            string materialId, bool strong, int pool, int printedTotal)
        {
            var (p, c, source, material) = Fixture(materialId);
            Play(p, c, source, strong);
            var expected = new int[6];
            expected[pool] = printedTotal;
            {
                Assert.That(Pools(c), Is.EqualTo(expected));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(strong ? 0 : 1));
                Assert.That(p.Mana.Crystals, Is.Empty);
                Assert.That(p.Deck.RemovedCards.SingleOrDefault(), Is.SameAs(material));
                Assert.That(p.Deck.PlayedCards.SingleOrDefault(), Is.SameAs(source));
                Assert.That(p.Deck.Hand, Is.Empty);
                Assert.That(c.CardToRemove, Is.Null);
            }
        }

        [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)]
        [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)]
        public void OriginalInstinctChoiceIsForwardedWithoutInventingAnotherEffect(bool strong, int choice)
        {
            var (p, c, source, _) = Fixture("basic_card_027");
            Play(p, c, source, strong, choice);
            var expected = new int[6];
            expected[choice] = strong ? 8 : 6;
            Assert.That(Pools(c), Is.EqualTo(expected));
        }

        [Test]
        public void LegacyEffectEnumCannotTurnMarchMaterialIntoFury()
        {
            var (p, c, source, _) = Fixture("basic_card_000");
            Play(p, c, source, false, (int)ActionEffectId.FuryEnhanced);
            {
                Assert.That(c.MovementPool, Is.EqualTo(6));
                Assert.That(c.MeleePool, Is.Zero);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void OnlyTheSelectedPhysicalCopyIsRemovedAndStaysRemovedAcrossRound(bool strong)
        {
            var (p, c, source, material) = Fixture();
            var otherCopy = new DeedCard(material.Id, material.Type);
            p.Deck.Hand.Insert(1, otherCopy);
            Play(p, c, source, strong);
            var game = new GameEngine(new[] { p }, 5);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, game.Globals.Mana, c);
            game.EndRound();
            {
                Assert.That(p.Deck.Hand, Is.Empty);
                Assert.That(p.Deck.RemovedCards.SingleOrDefault(), Is.SameAs(material));
                Assert.That(p.Deck.DrawPile.Count, Is.EqualTo(2));
                Assert.That(p.Deck.DrawPile.Count(x => ReferenceEquals(x, source)), Is.EqualTo(1));
                Assert.That(p.Deck.DrawPile.Count(x => ReferenceEquals(x, otherCopy)), Is.EqualTo(1));
                Assert.That(p.Deck.PlayedCards, Is.Empty);
                Assert.That(p.Deck.DiscardPile, Is.Empty);
                Assert.That(p.Mana.Tokens, Is.Empty);
                Assert.That(Pools(c), Is.EqualTo(new int[6]));
            }
        }

        [TestCase("missing")] [TestCase("foreign")] [TestCase("wound")]
        [TestCase("spell")] [TestCase("artifact")] [TestCase("forged-spell")]
        [TestCase("unknown")] [TestCase("placeholder")] [TestCase("source")]
        public void InvalidMaterialIsRejectedBeforeHostPaymentOrAnyZoneMutation(string failure)
        {
            var (p, c, source, material) = Fixture();
            switch (failure)
            {
                case "missing": c.CardToRemove = null; break;
                case "foreign": c.CardToRemove = new DeedCard(material.Id, material.Type); break;
                case "source": c.CardToRemove = source; break;
                default:
                    var bad = failure switch
                    {
                        "wound" => new DeedCard("w", CardType.Wound),
                        "spell" => new DeedCard("magic_009", CardType.Spell),
                        "artifact" => new DeedCard("items_000", CardType.Artifact),
                        "forged-spell" => new DeedCard("magic_009", CardType.Action),
                        "placeholder" => new DeedCard("advanced_card_044", CardType.Action),
                        _ => new DeedCard("not-an-original-card", CardType.Action)
                    };
                    p.Deck.Hand.Add(bad);
                    c.CardToRemove = bad;
                    break;
            }
            AssertRejectedUnchanged(p, c, () => Play(p, c, source, true));
        }

        [TestCase(-1)] [TestCase(4)]
        public void InvalidOriginalInnerChoiceIsRejectedBeforeHostPayment(int choice)
        {
            var (p, c, source, _) = Fixture("basic_card_027");
            AssertRejectedUnchanged(p, c, () => Play(p, c, source, true, choice));
        }

        [Test]
        public void MissingHostRedDoesNotBecomeFreeBecauseTheRepeatedStrongEffectIsFree()
        {
            var (p, c, source, _) = Fixture("basic_card_016");
            p.Mana.Tokens.Clear();
            AssertRejectedUnchanged(p, c, () => Play(p, c, source, true));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RepeatedMagicTalentPaysEachAdditionalTextCostInAdditionToHostRed(int availableRed)
        {
            var (p, c, source, material) = Fixture("advanced_card_025");
            p.Mana.AddToken(ManaColor.Red, availableRed - 1);
            c.SpellSupply = new SpellSupply();
            c.SpellSupply.Offer.AddRange(new[]
            {
                new SpellCard("magic_006"), new SpellCard("magic_007"), new SpellCard("magic_008")
            });
            if (availableRed < 3)
            {
                AssertRejectedUnchanged(p, c, () => Play(p, c, source, true));
                Assert.That(c.SpellSupply.Offer.Select(card => card.Id),
                    Is.EqualTo(new[] { "magic_006", "magic_007", "magic_008" }));
                return;
            }
            Play(p, c, source, true);
            Assert.That(p.Mana.GetTotal(), Is.Zero);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(material));
            Assert.That(p.Deck.DiscardPile.Select(card => card.Id), Is.EqualTo(new[] { "magic_007", "magic_006" }));
            Assert.That(c.SpellSupply.Offer.Single().Id, Is.EqualTo("magic_008"));
        }

        [Test]
        public void CopiedMaterialCannotAlsoPayTheBorrowedEffectsDiscard()
        {
            var (p, c, source, material) = Fixture("advanced_card_025");
            c.CardToDiscard = material;
            AssertRejectedUnchanged(p, c, () => Play(p, c, source, false));
        }

        [TestCase(false)] [TestCase(true)]
        public void BorrowedTranquilityStillCannotDrawDuringCombat(bool strong)
        {
            var (p, c, source, _) = Fixture("basic_card_001");
            c.InBattle = true;
            AssertRejectedUnchanged(p, c, () => Play(p, c, source, strong, 1));
        }

        [Test]
        public void RepeatedDeterminationIsConsumedTogetherAgainstOneEnemyAttack()
        {
            var (p, c, source, _) = Fixture("basic_card_016");
            var enemies = MonsterJsonLoader.LoadMonsters(
                Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
            var enemy = enemies.Single(x => x.Id == "monster_002");
            c.InBattle = true;
            Play(p, c, source, true);
            var result = CardCombatActions.Block(p, c, enemy);
            {
                Assert.That(result.PrintedPower, Is.EqualTo(10));
                Assert.That(result.Wounds, Is.Zero);
                Assert.That(c.BlockPool, Is.Zero);
                Assert.That(c.CombatPower.AvailableBlocks(1), Is.Empty);
            }
        }

        private static void AssertRejectedUnchanged(PlayerState p, ActionContext c, TestDelegate play)
        {
            var hand = p.Deck.Hand.ToArray();
            var selected = c.CardToRemove;
            var pools = Pools(c);
            int mana = p.Mana.GetTotal();
            {
                Assert.Throws<InvalidOperationException>(play);
                Assert.That(p.Mana.GetTotal(), Is.EqualTo(mana));
                Assert.That(p.Deck.Hand, Is.EqualTo(hand));
                Assert.That(p.Deck.PlayedCards, Is.Empty);
                Assert.That(p.Deck.RemovedCards, Is.Empty);
                Assert.That(p.Deck.DiscardPile, Is.Empty);
                Assert.That(c.CardToRemove, Is.SameAs(selected));
                Assert.That(Pools(c), Is.EqualTo(pools));
            }
        }
    }
}
