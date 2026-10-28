using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BattleRageBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);
        private static DeedCard Add(PlayerState p, string id)
        {
            var card = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(card); return card;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong = false)
            => CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), strong);

        [TestCase(false)] [TestCase(true)]
        public void AlreadyReceivedBattleWoundsIncreaseOriginalCardThenActualAttack(bool strong)
        {
            var p = new PlayerState();
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block,
                Enemies = new List<Monster> { Enemy(strong ? "monster_014" : "monster_013") } };
            var card = Add(p, "basic_card_025");
            var fury = strong ? null : Add(p, "basic_card_021");
            if (strong) p.Mana.AddToken(ManaColor.Red);
            var defense = CardCombatActions.Block(p, c, c.Enemies, 0);
            Assert.That(defense.Wounds, Is.EqualTo(strong ? 4 : 2));
            Assert.That(c.BattleWoundsReceived, Is.EqualTo(strong ? 4 : 2));
            CardCombatActions.EnterPhase(c, Phase.Melee);
            Play(p, c, card, strong);
            Assert.That(c.MeleePool, Is.EqualTo(strong ? 8 : 3));
            if (!strong) Play(p, c, fury);
            var attack = CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(strong ? 8 : 5));
            Assert.That(attack.EffectivePower, Is.EqualTo(strong ? 8 : 5));
            Assert.That(attack.RequiredPower, Is.EqualTo(strong ? 6 : 4));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(strong ? 4 : 2));
            Assert.That(p.Wounds, Is.EqualTo(strong ? 4 : 2));
            Assert.That(c.MeleePool, Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(p.Deck.PlayedCards[0], Is.SameAs(card));
            Assert.That(p.Deck.Hand.All(x => x.Type == CardType.Wound), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void SeparateEnemyDamageSharesOnePrintedBonusCapAndEndsWithBattle(bool strong)
        {
            var p = new PlayerState();
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block,
                Enemies = new List<Monster> { Enemy("monster_013"), Enemy("monster_013"), Enemy("monster_013") } };
            var rage = Add(p, "basic_card_025");
            if (strong) p.Mana.AddToken(ManaColor.Red);
            Play(p, c, rage, strong);
            int count = strong ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                Assert.That(CardCombatActions.Block(p, c, c.Enemies, i).Wounds, Is.EqualTo(2));
                Assert.That(c.MeleePool, Is.EqualTo((strong ? 4 : 2) + System.Math.Min((i + 1) * 2, strong ? 4 : 1)));
            }
            Assert.That(p.Wounds, Is.EqualTo(count * 2));
            Assert.That(c.BattleWoundsReceived, Is.EqualTo(count * 2));
            CardCombatActions.EnterPhase(c, Phase.Melee);
            if (!strong) Play(p, c, Add(p, "basic_card_021"));
            var attack = CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(strong ? 8 : 5));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(2));
            Assert.That(c.MeleePool, Is.Zero);
            UnitDamageResolver.EndBattle(p, c);
            c.InBattle = true; c.Enemies = new List<Monster> { Enemy("monster_013") };
            c.DefeatedEnemyIndices.Clear();
            CardCombatActions.Block(p, c, c.Enemies, 0);
            Assert.That(c.MeleePool, Is.Zero, "Previous battle's Rage must not reward new damage");
        }
    }
}
