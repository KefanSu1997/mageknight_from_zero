using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CardWoundConservationTests
    {
        [Test]
        public void HealingRemovesWoundsAndPreservesUnrelatedCards()
        {
            var player = new PlayerState { Wounds = 3 };
            var action = new DeedCard("basic_card_000", CardType.Action);
            player.Deck.Hand.Add(action);
            new HealOrDrawEffect(2, 2).Execute(player, new ActionContext());
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Deck.Hand, Has.Count.EqualTo(2));
            Assert.That(player.Deck.Hand, Does.Contain(action));
            Assert.That(player.Deck.DiscardPile, Is.Empty, "Healing removes wounds from the deck, not to discard.");
        }

        [Test]
        public void DiscardAndRedrawKeepWoundCountsInSync()
        {
            var player = new PlayerState { Wounds = 1 };
            var wound = player.Deck.Hand.Single();
            player.Deck.Discard(wound);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.DiscardWounds, Is.EqualTo(1));
            player.Deck.ShuffleFromDiscard();
            Assert.That(player.DiscardWounds, Is.Zero);
            player.Deck.DrawExact(1);
            Assert.That(player.Wounds, Is.EqualTo(1));
        }

        [Test]
        public void PoisonAddsEachHandAndDiscardWoundExactlyOnce()
        {
            var player = new PlayerState();
            var enemy = new Monster("poison_fixture", 4, 4, Element.Physical, 3, new[] { Ability.Poison });
            int wounds = 2;
            AbilityRules.AssignWounds(enemy, player, ref wounds);
            Assert.That(wounds, Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(player.DiscardWounds, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.Count + player.Deck.DiscardPile.Count, Is.EqualTo(4));
        }

        [Test]
        public void HealTriggersOnlyForActuallyRemovedWounds()
        {
            var player = new PlayerState { Wounds = 1 };
            player.Deck.PutOnTop(new DeedCard("basic_card_000", CardType.Action));
            player.Deck.PutOnTop(new DeedCard("basic_card_014", CardType.Action));
            Assert.That(CardHealing.Heal(player, new ActionContext { DrawPerHeal = 1 }, 5), Is.EqualTo(1));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand, Has.Count.EqualTo(1));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(1));
        }
    }
}
