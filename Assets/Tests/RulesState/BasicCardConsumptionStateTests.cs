using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BasicCardConsumptionStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PlayerState Player()
        {
            var player = new PlayerState();
            var farmer = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            farmer.Exhaust();
            player.Units.Add(farmer);
            return player;
        }

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, id == "w" ? CardType.Wound : CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool enhanced = false, int option = 0)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, enhanced, option);
        }

        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);

        private static CardCombatActions.Result Attack(PlayerState player, ActionContext context,
            string enemyId, Phase phase = Phase.Melee)
            => CardCombatActions.Attack(player, context, new[] { Enemy(enemyId) }, new[] { 0 }, phase);

        [Test]
        public void FocusFreeBloodRageStrongPowerIsConsumedByRealAttack()
        {
            var player = Player();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee };
            var focus = Add(player, "basic_card_006");
            var bloodRage = Add(player, "advanced_card_004");
            player.Mana.AddToken(ManaColor.Green);
            Play(player, context, focus, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.NextCardEnhanced, Is.True);
            Assert.That(context.BoostValue, Is.EqualTo(3));

            Play(player, context, bloodRage, false, 1);
            Assert.That(context.MeleePool, Is.EqualTo(12));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.NextCardEnhanced, Is.False);
            Assert.That(context.BoostValue, Is.Zero);
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(focus));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(bloodRage));
            var result = Attack(player, context, "monster_027");
            Assert.That(result.PrintedPower, Is.EqualTo(12));
            Assert.That(result.EffectivePower, Is.EqualTo(12));
            Assert.That(result.RequiredPower, Is.EqualTo(5));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(4));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Single().Type, Is.EqualTo(CardType.Wound));
        }

        [Test]
        public void ThreatenNobleMannersAndCallForHelpPayForOriginalUnit()
        {
            var player = Player();
            player.TempCommandSlots = 1;
            var context = new ActionContext();
            var threaten = Add(player, "basic_card_022");
            var noble = Add(player, "basic_card_012");
            var help = Add(player, "advanced_card_019");
            var wound = Add(player, "w");
            var candidate = CardJsonLoader.LoadUnit("army_003");
            var offer = new List<UnitCard> { candidate };

            Play(player, context, threaten);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Keep);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            Assert.That(context.InNegotiation, Is.True);
            Play(player, context, noble);
            Assert.That(context.InfluencePool, Is.EqualTo(4));
            Assert.That(player.Fame, Is.EqualTo(1));
            Assert.That(player.Reputation, Is.Zero);
            Play(player, context, help);
            Assert.That(context.InfluencePool, Is.EqualTo(8));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Keep, context), Is.EqualTo(8));

            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Keep), Is.True);
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(offer, Is.Empty);
            Assert.That(player.Units.Count, Is.EqualTo(2));
            Assert.That(player.Units[1].Card, Is.SameAs(candidate));
            Assert.That(player.Units[1].IsReady, Is.True);
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(threaten));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(noble));
            Assert.That(player.Deck.PlayedCards[2], Is.SameAs(help));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(wound));
            Assert.That(player.Fame, Is.EqualTo(1));
            Assert.That(player.Reputation, Is.Zero);
        }

        [Test]
        public void CrystallizedManaPaysImprovisationAndActualDiscardAttack()
        {
            var player = Player();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee };
            var crystallize = Add(player, "basic_card_015");
            var improvisation = Add(player, "basic_card_023");
            var material = Add(player, "basic_card_000");
            context.CardToDiscard = material;
            player.Mana.AddToken(ManaColor.Red);

            Play(player, context, crystallize, false, (int)ManaColor.Red);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Play(player, context, improvisation, true, 2);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(5));
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(material));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(crystallize));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(improvisation));
            Assert.That(player.Deck.Hand, Is.Empty);

            var result = Attack(player, context, "monster_027");
            Assert.That(result.PrintedPower, Is.EqualTo(5));
            Assert.That(result.EffectivePower, Is.EqualTo(5));
            Assert.That(result.RequiredPower, Is.EqualTo(5));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(4));
        }

        [Test]
        public void SwiftBoltPaysWhiteAndConsumesRangedPowerAgainstOriginalWolfRider()
        {
            var player = Player();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Ranged };
            var card = Add(player, "advanced_card_002");
            player.Mana.AddToken(ManaColor.White);
            Play(player, context, card, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(context.RangedPool, Is.EqualTo(4));
            var result = Attack(player, context, "monster_007", Phase.Ranged);
            Assert.That(result.PrintedPower, Is.EqualTo(4));
            Assert.That(result.EffectivePower, Is.EqualTo(4));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(3));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(card));
        }

        [Test]
        public void CoercionAttackPaysRedAndReputationThenDefeatsOriginalEnemy()
        {
            var player = Player();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee };
            var card = Add(player, "advanced_card_008");
            player.Mana.AddToken(ManaColor.Red);
            Play(player, context, card, true, 1);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(7));
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(player.Reputation, Is.EqualTo(-2));
            var result = Attack(player, context, "monster_027");
            Assert.That(result.PrintedPower, Is.EqualTo(7));
            Assert.That(result.EffectivePower, Is.EqualTo(7));
            Assert.That(result.RequiredPower, Is.EqualTo(5));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(4));
            Assert.That(player.Reputation, Is.EqualTo(-2));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(card));
        }

        [Test]
        public void SpellForgeUsesTwoDifferentActualRedSpellsAndSpendsOneCrystal()
        {
            var player = Player();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee, SpellSupply = new SpellSupply() };
            var first = new SpellCard("magic_009");
            var second = new SpellCard("magic_010");
            context.SpellSupply.Offer.AddRange(new[] { first, second });
            var forge = Add(player, "advanced_card_033");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, forge, true, 0 | (1 << 8));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(2));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.SpellSupply.Offer.Count, Is.EqualTo(2));
            Assert.That(context.SpellSupply.Offer[0], Is.SameAs(first));
            Assert.That(context.SpellSupply.Offer[1], Is.SameAs(second));

            Play(player, context, fury, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            var result = Attack(player, context, "monster_007");
            Assert.That(result.PrintedPower, Is.EqualTo(4));
            Assert.That(result.EffectivePower, Is.EqualTo(4));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(3));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(forge));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(fury));
            Assert.That(player.Deck.Hand, Is.Empty);
        }

        // Insert inside MK.Tests.Cards.BasicCardConsumptionStateTests.
        // Uses its existing Player/Add/Play/Attack helpers; no extra fixture setup.
        // One normal base selection from the same actual red offer used by the strong case.
        [Test]
        public void SpellForgeBaseSelectsOneActualRedSpellAndSpendsItsCrystal()
        {
            var player = Player();
            var context = new ActionContext
            {
                InBattle = true, CombatPhase = Phase.Melee, SpellSupply = new SpellSupply()
            };
            var first = new SpellCard("magic_009");
            var second = new SpellCard("magic_010");
            context.SpellSupply.Offer.AddRange(new[] { first, second });
            var forge = Add(player, "advanced_card_033");
            var fury = Add(player, "basic_card_021");

            // Select actual offer index 1; base has no printed mana cost.
            Play(player, context, forge, false, 1);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.SpellSupply.Offer.Count, Is.EqualTo(2));
            Assert.That(context.SpellSupply.Offer[0], Is.SameAs(first));
            Assert.That(context.SpellSupply.Offer[1], Is.SameAs(second));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(forge));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(fury));

            // The only red resource is the crystal earned above; Fury spends it.
            Play(player, context, fury, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            var result = Attack(player, context, "monster_007");
            Assert.That(result.PrintedPower, Is.EqualTo(4));
            Assert.That(result.EffectivePower, Is.EqualTo(4));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(3));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(2));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(forge));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(fury));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(context.SpellSupply.Offer.Count, Is.EqualTo(2));
            Assert.That(context.SpellSupply.Offer[0], Is.SameAs(first));
            Assert.That(context.SpellSupply.Offer[1], Is.SameAs(second));
        }
    }
}
