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
    // Proposed input contract, not a parallel rule implementation:
    // ActionContext.SelectedEnemy is an actual current Monster reference;
    // SelectedEnemyAttackIndex selects the base effect's one printed attack slot.
    public sealed class ColdStareBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action); player.Deck.Hand.Add(card); return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("advanced_card_") ? CardJsonLoader.LoadAdvancedActions()
                : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_043")
            {
                Assert.That(original.Name, Is.EqualTo("冷峻目光"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Blue, ManaColor.White }));
            }
            CardPlayActions.Play(player, context, card, original, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static MK.Logic.Data.Monster Enemy(string id)
            => MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == id);

        private static TurnMachine BlockPhase(PlayerState player, ActionContext context, params MK.Logic.Data.Monster[] enemies)
        {
            context.Enemies = new List<MK.Logic.Data.Monster>(enemies);
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Block)); return turn;
        }

        private static void End(PlayerState player, ActionContext context, params DeedCard[] played)
        {
            context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(played.Length));
            foreach (var card in played)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChosenInfluencePaysForActualOriginalFarmerWithoutCombatEffect(bool strong)
        {
            var player = new PlayerState { TempCommandSlots = 1 };
            var existing = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); existing.Exhaust();
            player.Units.Add(existing);
            var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_043");
            var promise = strong ? null : Add(player, "basic_card_008");
            if (strong)
            {
                player.Mana.AddToken(ManaColor.White);
                context.SelectedActionManaColor = ManaColor.White;
            }
            Play(player, context, source, strong, 0);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 5 : 3));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SelectedActionManaColor, Is.Null);
            Assert.That(context.InBattle, Is.False);
            if (!strong) Play(player, context, promise);
            Assert.That(context.InfluencePool, Is.EqualTo(5));
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            Assert.That(candidate.InfluenceCost, Is.EqualTo(4));
            Assert.That(candidate.Level, Is.EqualTo(1));
            Assert.That(candidate.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village));
            var offer = new List<UnitCard> { candidate };
            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Village);
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Village, context), Is.EqualTo(4));
            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(1)); Assert.That(offer, Is.Empty);
            Assert.That(player.Units.Count, Is.EqualTo(2)); Assert.That(player.Units[0], Is.SameAs(existing));
            Assert.That(player.Units[1].Card, Is.SameAs(candidate)); Assert.That(player.Units[1].IsReady, Is.True);
            Assert.That(player.Wounds, Is.Zero); Assert.That(player.Reputation, Is.Zero); Assert.That(player.Fame, Is.Zero);
            Assert.That(context.AttackDisabled, Is.False); Assert.That(context.EnemySkipAttack, Is.False);
            End(player, context, strong ? new[] { source } : new[] { source, promise });
            Assert.That(context.InfluencePool, Is.Zero);
        }

        [Test]
        public void BaseRemovesChosenBrutalAttackAbilityButRetainsItsFireColorAndDefense()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_043"); var determination = Add(player, "basic_card_016");
            var furyA = Add(player, "basic_card_021"); var furyB = Add(player, "basic_card_021");
            Assert.That(furyA, Is.Not.SameAs(furyB));
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Red, 2);
            var enemy = Enemy("monster_001");
            Assert.That(enemy.Attack, Is.EqualTo(6)); Assert.That(enemy.AttackElement, Is.EqualTo(Element.Fire));
            Assert.That(enemy.Armor, Is.EqualTo(8)); Assert.That(enemy.Fame, Is.EqualTo(8));
            Assert.That(enemy.Abilities, Is.EquivalentTo(new[] { Ability.FireResist, Ability.Fortified, Ability.Brutal }));
            var turn = BlockPhase(player, context, enemy);
            context.SelectedEnemy = enemy; context.SelectedEnemyAttackIndex = 0;
            Play(player, context, source, false, 1);
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            var profile = CardCombatActions.DefenseProfile(enemy, 0, context, 0);
            Assert.That(profile.Attack, Is.EqualTo(6)); Assert.That(profile.AttackElement, Is.EqualTo(Element.Fire));
            Assert.That(profile.Abilities.Contains(Ability.Brutal), Is.False);
            Assert.That(profile.Abilities.Contains(Ability.FireResist), Is.True);
            Assert.That(profile.Abilities.Contains(Ability.Fortified), Is.True);
            Assert.That(enemy.Abilities.Contains(Ability.Brutal), Is.True, "Do not mutate the original enemy");
            Play(player, context, determination, true, 0);
            var blocks = context.CombatPower.AvailableBlocks(0).ToArray();
            Assert.That(blocks.Sum(b => b.Value), Is.EqualTo(5));
            Assert.That(CombatMath.EffectiveBlock(profile.AttackElement, blocks), Is.EqualTo(2));
            turn.SetBlock(5, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, blocks, context);
            Assert.That(damage.TotalWounds, Is.EqualTo(3)); Assert.That(player.Wounds, Is.EqualTo(3));
            Assert.That(player.Deck.DiscardPile, Is.Empty, "The removed Poison ability must not add three discard wounds");
            Assert.That(context.IsKnockedOut, Is.False);
            Play(player, context, furyA, true); Play(player, context, furyB, true);
            var hit = turn.Attack(new[] { 0 }, context);
            Assert.That(hit.PrintedPower, Is.EqualTo(8)); Assert.That(hit.EffectivePower, Is.EqualTo(8));
            Assert.That(hit.RequiredPower, Is.EqualTo(8)); Assert.That(hit.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(8));
            turn.FinishBattle(context); End(player, context, source, determination, furyA, furyB);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(3));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
        }

        [Test]
        public void StrongStopsBothAttacksOfOnlyChosenOriginalEnemyAndFuryActuallyKillsIt()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_043"); var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Red);
            var target = Enemy("monster_058"); var other = Enemy("monster_032");
            Assert.That(target.AttackCount, Is.EqualTo(2));
            for (int slot = 0; slot < 2; slot++)
            {
                Assert.That(target.GetAttack(slot).Value, Is.EqualTo(1));
                Assert.That(target.GetAttack(slot).Element, Is.EqualTo(Element.Physical));
            }
            Assert.That(target.Armor, Is.EqualTo(4)); Assert.That(target.Fame, Is.EqualTo(2));
            Assert.That(target.Abilities, Is.Empty);
            Assert.That(other.Attack, Is.EqualTo(4)); Assert.That(other.Armor, Is.EqualTo(3));
            var turn = BlockPhase(player, context, target, other);
            context.SelectedEnemy = target; context.SelectedEnemyAttackIndex = 0;
            context.SelectedActionManaColor = ManaColor.Blue;
            Play(player, context, source, true, 1);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.InfluencePool, Is.Zero);
            turn.SetBlock(0, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(damage.TotalWounds, Is.EqualTo(2), "Only original032 deals damage; both 058 attack slots stop");
            Assert.That(player.Wounds, Is.EqualTo(2));
            Play(player, context, fury, true);
            var hit = turn.Attack(new[] { 0 }, context);
            Assert.That(hit.PrintedPower, Is.EqualTo(4)); Assert.That(hit.RequiredPower, Is.EqualTo(4));
            Assert.That(hit.Kills, Is.EqualTo(1)); Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.DefeatedEnemyIndices.Contains(1), Is.False);
            turn.FinishBattle(context); End(player, context, source, fury);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(context.ColdStareSkips(target), Is.False, "Turn-end clears the exact selected enemy's permission");
        }

        [Test]
        public void BaseSuppressesOnlyTheChosenActualAttackAndConsumesItAfterDefense()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_043");
            var firstBlock = Add(player, "basic_card_016"); var secondBlock = Add(player, "basic_card_016");
            player.Mana.AddToken(ManaColor.Blue, 2);
            var target = Enemy("monster_007"); var other = Enemy("monster_007");
            Assert.That(target.Attack, Is.EqualTo(3)); Assert.That(target.Abilities, Does.Contain(Ability.Swift));
            Assert.That(target, Is.Not.SameAs(other));
            var turn = BlockPhase(player, context, target, other);
            context.SelectedEnemy = target; context.SelectedEnemyAttackIndex = 0;
            Play(player, context, source, false, 1);
            Assert.That(AbilityRules.RequiredBlock(CardCombatActions.DefenseProfile(target, 0, context)), Is.EqualTo(3));
            Assert.That(AbilityRules.RequiredBlock(CardCombatActions.DefenseProfile(other, 1, context)), Is.EqualTo(6));
            Play(player, context, firstBlock, true, 0); Play(player, context, secondBlock, true, 0);
            var blocks = context.CombatPower.Blocks.Select((b,i) => b with { TargetIndex = i }).ToArray();
            Assert.That(blocks.Select(b => b.Value), Is.EqualTo(new[] { 5, 5 }));
            turn.SetBlock(10, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, blocks, context);
            Assert.That(damage.TotalWounds, Is.EqualTo(2));
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(context.ColdStareSuppresses(target, 0), Is.False);
            Assert.That(AbilityRules.RequiredBlock(CardCombatActions.DefenseProfile(target, 0, context)), Is.EqualTo(6));
            turn.FinishBattle(context); End(player, context, source, firstBlock, secondBlock);
        }

        [Test]
        public void WhitePaymentCannotBypassChosenOriginalIceResistantEnemy()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var source = Add(player, "advanced_card_043"); player.Mana.AddToken(ManaColor.White);
            var enemy = Enemy("monster_028");
            Assert.That(enemy.Abilities.Contains(Ability.IceResist), Is.True);
            Assert.That(enemy.Attack, Is.EqualTo(5)); Assert.That(enemy.AttackElement, Is.EqualTo(Element.Ice));
            BlockPhase(player, context, enemy);
            context.SelectedEnemy = enemy; context.SelectedEnemyAttackIndex = 0;
            context.SelectedActionManaColor = ManaColor.White;
            var original = CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == source.Id);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.Play(player, context, source, original, true, 1));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(source));
            Assert.That(player.Deck.PlayedCards, Is.Empty); Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(context.InfluencePool, Is.Zero); Assert.That(player.Wounds, Is.Zero);
        }
    }
}
