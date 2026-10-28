using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class HeadbandBasicStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard headband, SkillCard[] originals) Fixture()
        {
            var p = new PlayerState();
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState(),
                SkillSupply = new SkillSupply(), SkipDraw = true };
            for (int q = 0; q <= 1; q++)
                c.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            var originals = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000").ToArray();
            var headband = new DeedCard("items_022", CardType.Artifact);
            p.Deck.Hand.Add(headband);
            return (p, c, headband, originals);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard card, int option, bool burn = false, int skillOption = 0)
        {
            c.HeadbandSkillOption = skillOption;
            CardPlayActions.PlayArtifact(p, c, card,
                CardJsonLoader.LoadItems().Single(x => x.Id == "items_022"), burn, option);
        }

        private static void EndTurn(PlayerState p, ActionContext c, DeedCard card, bool burned)
        {
            if (c.InBattle) UnitDamageResolver.EndBattle(p, c);
            new PlayerTurnEngine().EndTurn(p, c.DayPart, new ManaSource(1, () => c.DayPart), c);
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x, card)), Is.EqualTo(!burned));
            Assert.That(p.Deck.RemovedCards.Any(x => ReferenceEquals(x, card)), Is.EqualTo(burned));
        }

        [Test]
        public void NormalPerTurnActualOfferSkillDoublesAndPaysMovementAfterAtomicValidation()
        {
            var (p, c, card, skills) = Fixture();
            // 原互动技能和同英雄原黑暗道路都在公共供应；不是从玩家个人技能区借用。
            c.SkillSupply.Offer.AddRange(new[] { skills[9], skills[0] });
            Assert.That(skills[0].SourceId, Is.EqualTo("hero_skill_pool_000:00"));
            Assert.That(skills[0].Frequency, Is.EqualTo(SkillFrequency.OncePerTurn));
            Assert.That(skills[9].IsInteractive, Is.True);
            Assert.That(skills[4].IsInteractive, Is.False);
            Assert.That(CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[9].IsInteractive, Is.True);
            Assert.That(CardJsonLoader.LoadHeroSkills("hero_skill_pool_006")[11].IsInteractive, Is.False);
            var interactive = Assert.Throws<InvalidOperationException>(() => Play(p, c, card, 0));
            Assert.That(interactive.Message, Is.EqualTo("启迪头环的普通效果不能使用互动技能"));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, -1));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, 9)); // 不允许Clamp改选
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, 1, skillOption: 1)); // 黑暗道路只有0
            Assert.Throws<InvalidOperationException>(() => Play(p, c,
                new DeedCard("items_022", CardType.Artifact), 1)); // 同ID不等于持有实体
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card));
            Assert.That(p.Deck.PlayedCards.Concat(p.Deck.DiscardPile).Concat(p.Deck.RemovedCards), Is.Empty);
            Assert.That(c.MovementPool, Is.Zero);
            Assert.That(p.Skills, Is.Empty);
            Assert.That(c.SkillSupply.Offer[0], Is.SameAs(skills[9]));
            Assert.That(c.SkillSupply.Offer[1], Is.SameAs(skills[0]));

            Play(p, c, card, 1);
            Assert.That(c.MovementPool, Is.EqualTo(2)); // 白昼1，头环双用=2
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.That(c.SkillSupply.Offer[1], Is.SameAs(skills[0]));
            Assert.That(p.Skills, Is.Empty);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(c.MovementPool, Is.Zero); // 真实平原费用2
            EndTurn(p, c, card, false);
        }

        [Test]
        public void NormalPerRoundDarkFireIsNotDoubledAndItsTokenPaysOriginalFury()
        {
            var (p, c, card, skills) = Fixture();
            c.SkillSupply.Offer.AddRange(new[] { skills[0], skills[4] });
            Assert.That(skills[4].SourceId, Is.EqualTo("hero_skill_pool_000:04"));
            Assert.That(skills[4].Frequency, Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, 1, skillOption: 2));
            Assert.That(p.Mana.Crystals.Count + p.Mana.Tokens.Count, Is.Zero);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card));
            Play(p, c, card, 1, skillOption: 0); // 红标记分支，非默认供给下标
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
            Assert.That(c.SkillSupply.Offer[1], Is.SameAs(skills[4]));
            Assert.That(p.Skills, Is.Empty);
            var fury = new DeedCard("basic_card_021", CardType.Action);
            p.Deck.Hand.Add(fury);
            var game = new GameState(); game.Players.Add(p);
            new TurnMachine(game).BeginBattle(c);
            c.Enemies = new List<Monster>
                { CardJsonLoader.LoadMonsters().Single(x => x.Id == "monster_032") };
            Assert.That(c.Enemies[0].Armor, Is.EqualTo(3)); // 原兽人骷髅战士：攻击4、护甲3。
            CardCombatActions.EnterPhase(c, Phase.Melee);
            CardPlayActions.Play(p, c, fury, CardJsonLoader.LoadBasicActions().Single(x => x.Id == fury.Id), true);
            Assert.That(c.MeleePool, Is.EqualTo(4));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards.Any(x => ReferenceEquals(x, fury)), Is.True);
            var attack = CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(4));
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(3));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(c.DefeatedEnemyIndices, Does.Contain(0));
            Assert.That(p.Fame, Is.EqualTo(2));
            Assert.That(c.MeleePool, Is.Zero);
            EndTurn(p, c, card, false);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
        }

        [Test]
        public void BurnTakesExactOriginalSkillAndOwnedUsePaysMovementAndPersists()
        {
            var (p, c, card, skills) = Fixture();
            c.DayPart = DayPart.Night;
            c.SkillSupply.Offer.AddRange(new[] { skills[4], skills[0] });
            Play(p, c, card, 1, true);
            Assert.That(p.Skills.Single(), Is.SameAs(skills[0]));
            Assert.That(c.SkillSupply.Offer.Single(), Is.SameAs(skills[4]));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(card));
            Assert.That(c.MovementPool, Is.Zero); // 取得不等于自动使用
            SkillActions.Use(p, c, p.Skills[0]);
            Assert.That(c.MovementPool, Is.EqualTo(2)); // 夜间原技能一次=2，不能再头环翻倍
            Assert.That(SkillActions.IsUsed(p, skills[0]), Is.True);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.True);
            Assert.That(c.MovementPool, Is.Zero);
            EndTurn(p, c, card, true);
            new PlayerTurnEngine().StartTurn(p, DayPart.Night);
            Assert.That(p.Skills.Single(), Is.SameAs(skills[0]));
            Assert.That(SkillActions.IsUsed(p, skills[0]), Is.False);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(card));

            // 互动技能可以取得；不把尚未接入的复杂技能发动混入本次基本验收。
            var (other, otherContext, otherCard, originals) = Fixture();
            otherContext.SkillSupply.Offer.Add(originals[9]);
            Assert.That(originals[9].IsInteractive, Is.True);
            Play(other, otherContext, otherCard, 0, true);
            Assert.That(other.Skills.Single(), Is.SameAs(originals[9]));
            Assert.That(otherContext.SkillSupply.Offer, Is.Empty);
            Assert.That(other.Deck.RemovedCards.Single(), Is.SameAs(otherCard));
            Assert.That(other.Wounds, Is.Zero);
            Assert.That(otherContext.MovementPool + otherContext.InfluencePool, Is.Zero);
        }
    }
}
