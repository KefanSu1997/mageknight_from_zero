using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeadbandUnitPaymentSessionTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Hand(PlayerState p, string id, CardType type = CardType.Action)
        { var card = new DeedCard(id, type); p.Deck.Hand.Add(card); return card; }
        private static SkillCard Offer(ActionContext context)
        { context.SkillSupply ??= new SkillSupply(); var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[6]; context.SkillSupply.Offer.Add(skill); return skill; }

        [TestCase(false)] [TestCase(true)]
        public void OutsideCardAndUnitBothOrdersConsumePrintedFeeAndHealRealWound(bool unitFirst)
        {
            var p = new PlayerState(); var headband = Hand(p, "items_022", CardType.Artifact);
            var march = Hand(p, "basic_card_000"); var first = Hand(p, "basic_card_014"); var second = Hand(p, "basic_card_021");
            var herb = new UnitState(CardJsonLoader.LoadUnit("army_2_002")); p.Units.Add(herb);
            var journey = new PveJourneySession(p, CardJsonLoader.LoadBasicActions()); journey.SelectTactic("day_1"); journey.BeginTurn();
            var c = journey.Context; var skill = Offer(c); p.Deck.GainWound();
            if (unitFirst) journey.UseOutsideUnitWithBorrowedPayment(headband, new(skill, ManaColor.Green, first), herb, 0);
            else journey.PlayOutsideWithBorrowedPayment(headband, new(skill, ManaColor.Green, first), march, "strong");
            Assert.That(c.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
            if (unitFirst) journey.PlayOutsideWithBorrowedPayment(headband, new(skill, ManaColor.Green, second), march, "strong");
            else
            {
                Assert.Throws<InvalidOperationException>(() => journey.UseOutsideUnitWithBorrowedPayment(headband, new(skill, ManaColor.Green, first), herb, 0));
                Assert.That(herb.IsReady, Is.True); Assert.That(c.Healing.Total, Is.Zero);
                Assert.That(c.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
                journey.UseOutsideUnitWithBorrowedPayment(headband, new(skill, ManaColor.Green, second), herb, 0);
            }
            Assert.That(c.PendingHeadbandBorrow, Is.Null); Assert.That(c.MovementPool, Is.EqualTo(4));
            Assert.That(c.Healing.Total, Is.EqualTo(2)); Assert.That(herb.IsReady, Is.False);
            journey.HealOutsideBattle(); Assert.That(p.Wounds, Is.Zero); Assert.That(c.Healing.Total, Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Contains(first), Is.True); Assert.That(p.Deck.DiscardPile.Contains(second), Is.True);
            Assert.That(p.Deck.PlayedCards.Count(x => ReferenceEquals(x, headband)), Is.EqualTo(1));
            Assert.That(p.Mana.GetTotal(), Is.Zero); Assert.That(p.Skills, Is.Empty); Assert.That(SkillActions.IsUsed(p, skill), Is.False);
        }

        [Test] public void CombatUnitThenCardCompletesBorrowAndOriginalBlockConsumesActualContribution()
        {
            var p = new PlayerState(); var headband = Hand(p, "items_022", CardType.Artifact); var wound = Hand(p, "w", CardType.Wound);
            var influence = Hand(p, "basic_card_008"); var material = Hand(p, "basic_card_014");
            var mage = new UnitState(CardJsonLoader.LoadUnit("army_007")); p.Units.Add(mage);
            var battle = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(x => x.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            var c = battle.Context; var skill = Offer(c);
            Assert.Throws<InvalidOperationException>(() => battle.UseUnitWithBorrowedPayment(headband, new(skill, ManaColor.Red, wound), mage, 1, option: 1));
            Assert.That(mage.IsReady, Is.True); Assert.That(c.PendingHeadbandBorrow, Is.Null);
            battle.Advance();
            battle.UseUnitWithBorrowedPayment(headband, new(skill, ManaColor.Red, wound), mage, 1, option: 1);
            Assert.That(mage.IsReady, Is.False); Assert.That(c.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Contains(wound), Is.True);
            battle.PlayWithBorrowedPayment(headband, new(skill, ManaColor.White, material), influence, "strong");
            Assert.That(c.PendingHeadbandBorrow, Is.Null); Assert.That(c.InfluencePool, Is.EqualTo(4));
            Assert.That(battle.BlockPreview(0).Effective, Is.EqualTo(6)); battle.Block(0); Assert.That(battle.IsBlocked(0), Is.True);
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); Assert.That(p.Skills, Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OutsideRejectsFreeAbilityInvalidUnitTargetAndBattleBeforeMutation(int invalid)
        {
            var p = new PlayerState(); var headband = Hand(p, "items_022", CardType.Artifact); var material = Hand(p, "basic_card_014");
            var herb = new UnitState(CardJsonLoader.LoadUnit("army_2_002")); p.Units.Add(herb);
            var journey = new PveJourneySession(p, CardJsonLoader.LoadBasicActions()); journey.SelectTactic("day_1"); journey.BeginTurn();
            var c = journey.Context; var skill = Offer(c);
            if (invalid == 2) journey.BeginBattle(CardJsonLoader.LoadMonsters().Where(x => x.Id == "monster_032").ToArray());
            Assert.Throws<InvalidOperationException>(() => journey.UseOutsideUnitWithBorrowedPayment(headband, new(skill, ManaColor.Green, material), herb,
                invalid == 0 ? 2 : 0, unitTarget: invalid == 1 ? herb : null));
            Assert.That(herb.IsReady, Is.True); Assert.That(c.Healing.Total, Is.Zero); Assert.That(c.PendingHeadbandBorrow, Is.Null); Assert.That(p.Mana.GetTotal(), Is.Zero);
            Assert.That(p.Deck.Hand.Contains(headband), Is.True); Assert.That(p.Deck.Hand.Contains(material), Is.True);
        }

        [Test] public void MastersIndependentPaymentIsExplicitlyRejectedBeforeDispatch()
        {
            var p = new PlayerState(); var headband = Hand(p, "items_022", CardType.Artifact); var material = Hand(p, "basic_card_014");
            var masters = new UnitState(CardJsonLoader.LoadUnit("army_005")); p.Units.Add(masters);
            var battle = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(x => x.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            var skill = Offer(battle.Context);
            Assert.Throws<InvalidOperationException>(() => battle.UseUnitWithBorrowedPayment(headband, new(skill, ManaColor.White, material), masters, 0, target: 0));
            Assert.That(masters.IsReady, Is.True); Assert.That(battle.Context.CombatPower.Attacks, Is.Empty);
            Assert.That(battle.Context.PendingHeadbandBorrow, Is.Null); Assert.That(p.Deck.Hand.Contains(headband), Is.True); Assert.That(p.Deck.Hand.Contains(material), Is.True);
        }
    }
}
