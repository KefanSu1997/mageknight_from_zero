using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class QuickReflexesBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);
        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action); player.Deck.Hand.Add(card); return card;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong = false, int option = 0)
            => CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), strong, option);

        [TestCase(false)] [TestCase(true)]
        public void SelectedOriginalSwiftAttackIsReducedBeforeBlockAndActualWounds(bool strong)
        {
            var p = new PlayerState();
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block,
                Enemies = new List<Monster> { Enemy("monster_013"), Enemy("monster_007") } };
            var quick = Add(p, "basic_card_010"); var determination = Add(p, "basic_card_016");
            if (strong) p.Mana.AddToken(ManaColor.Blue);
            Play(p, c, quick, strong, 2);
            Assert.That(c.AttackReduction, Is.EqualTo(strong ? 2 : 1));
            Assert.That(c.AttackReductionTimes, Is.EqualTo(1));
            Assert.That(c.MovementPool, Is.Zero); Assert.That(c.RangedPool, Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.ReduceEnemyAttack(c, 2));
            Assert.That(c.AttackReductionTimes, Is.EqualTo(1), "Invalid target must not spend the permission");
            CardCombatActions.ReduceEnemyAttack(c, 1);
            Assert.That(c.PendingAttackReductions[(1, 0)], Is.EqualTo(strong ? 2 : 1));
            Assert.That(c.AttackReductionTimes, Is.Zero);
            Assert.That(c.AttackReduction, Is.Zero);
            Play(p, c, determination, option: 1);
            var reduced = CardCombatActions.Block(p, c, c.Enemies, 1);
            Assert.That(reduced.PrintedPower, Is.EqualTo(2));
            Assert.That(reduced.RequiredPower, Is.EqualTo(strong ? 2 : 4));
            Assert.That(reduced.Wounds, Is.EqualTo(strong ? 0 : 1));
            Assert.That(c.PendingAttackReductions, Is.Empty);
            Assert.That(c.Enemies[1].Attack, Is.EqualTo(3), "Original enemy definition must remain intact");
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.ReduceEnemyAttack(c, 0));
            var untouched = CardCombatActions.Block(p, c, c.Enemies, 0);
            Assert.That(untouched.RequiredPower, Is.EqualTo(4));
            Assert.That(untouched.Wounds, Is.EqualTo(2));
            Assert.That(p.Wounds, Is.EqualTo(strong ? 2 : 3));
            Assert.That(c.BattleWoundsReceived, Is.EqualTo(p.Wounds));
            Assert.That(c.BlockPool, Is.Zero);
            Assert.That(p.Deck.PlayedCards[0], Is.SameAs(quick));
            Assert.That(p.Deck.PlayedCards[1], Is.SameAs(determination));
            Assert.That(p.Deck.Hand.All(x => x.Type == CardType.Wound), Is.True);
        }

        [Test]
        public void InvalidEnhancedChoicePreservesActualCardAndBlueMana()
        {
            var p = new PlayerState(); var c = new ActionContext();
            var card = Add(p, "basic_card_010"); p.Mana.AddToken(ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, true, 3));
            Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card));
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(c.AttackReductionTimes, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void ArcaneImmuneOriginalEnemyStillHasItsSelectedAttackReduced(bool strong)
        {
            var p = new PlayerState();
            var enemy = Enemy("monster_014");
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block, Enemies = new List<Monster> { enemy } };
            var quick = Add(p, "basic_card_010");
            if (strong) p.Mana.AddToken(ManaColor.Blue);
            Play(p, c, quick, strong, 2); CardCombatActions.ReduceEnemyAttack(c, 0);
            var result = CardCombatActions.Block(p, c, c.Enemies, 0);
            Assert.That(result.RequiredPower, Is.EqualTo(strong ? 5 : 6));
            Assert.That(result.Wounds, Is.EqualTo(3)); Assert.That(p.Wounds, Is.EqualTo(3));
            Assert.That(enemy.Attack, Is.EqualTo(7)); Assert.That(enemy.Abilities.Contains(Ability.MagicResist), Is.True);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(quick)); Assert.That(c.PendingAttackReductions, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void AssignedAndUnassignedReductionsExpireAtBattleOrTurnEnd(bool endTurn)
        {
            var p = new PlayerState();
            var c = new ActionContext { InBattle = true, Enemies = new List<Monster> { Enemy("monster_013") } };
            Play(p, c, Add(p, "basic_card_010"), option: 2);
            CardCombatActions.ReduceEnemyAttack(c, 0);
            Play(p, c, Add(p, "basic_card_010"), option: 2);
            if (endTurn) new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            else UnitDamageResolver.EndBattle(p, c);
            Assert.That(c.PendingAttackReductions, Is.Empty);
            Assert.That(c.AttackReduction, Is.Zero); Assert.That(c.AttackReductionTimes, Is.Zero);
            c.InBattle = true;
            var next = CardCombatActions.Block(p, c, c.Enemies, 0);
            Assert.That(next.RequiredPower, Is.EqualTo(4));
            Assert.That(next.Wounds, Is.EqualTo(2));
        }
    }
}
