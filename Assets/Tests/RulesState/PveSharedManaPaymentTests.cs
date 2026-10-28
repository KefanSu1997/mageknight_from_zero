using System;
using System.Collections.Generic;
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
    public sealed class PveSharedManaPaymentTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PveJourneySession j, SkillCard skill, PlayerState other) Start(string pool)
        {
            var player = new PlayerState(1, "owner"); var other = new PlayerState(2, "other");
            player.Deck.SetDeck(Enumerable.Range(0, 8).Select(_ => new DeedCard("basic_card_000", CardType.Action)));
            other.Deck.SetDeck(Enumerable.Range(0, 8).Select(_ => new DeedCard("basic_card_000", CardType.Action)));
            var skill = CardJsonLoader.LoadHeroSkills(pool)[9]; HeroSkillAcquisition.Acquire(player, skill);
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s =>
                new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var j = new PveJourneySession(player, cards, spells: spells, companions: new[] { other }, manaRandom: new Random(1));
            j.SelectTactics("day_1", "day_4"); j.BeginTurn(); return (j, skill, other);
        }
        private static DeedCard Play(PveJourneySession j, string id, bool strong, int option = 0)
        {
            var spell = id.StartsWith("magic_");
            var card = new DeedCard(id, spell ? CardType.Spell : CardType.Action); j.Player.Deck.Hand.Add(card);
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var data = spell ? spells.Where(s => s.Id == id).Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)).Single()
                : CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Single(c => c.Id == id);
            CardPlayActions.Play(j.Player, j.Context, card, data, strong, option, new ActionSystem(false, spells)); return card;
        }

        [Test]
        public void EnhancementWaitsForActualSuccessfulCostAndReturnsCrystalAfterCommit()
        {
            var (j, skill, _) = Start("hero_skill_pool_003"); var p = j.Player; var area = j.Game.SharedSkills;
            area.ArmEnhancement(p, j.Context, skill, ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => Play(j, "basic_card_021", true));
            Assert.That(area.Tokens, Is.Empty); Assert.That(SkillActions.IsUsed(p, skill), Is.False);
            p.Mana.AddCrystal(ManaColor.Red);
            Play(j, "basic_card_021", true);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(j.Context.SpentCrystals[ManaColor.Red], Is.EqualTo(1));
            Assert.That(area.Tokens.Single().Color, Is.EqualTo(ManaColor.Red));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(SkillActions.IsUsed(p, skill), Is.True);
        }

        [Test]
        public void SharedEnhancementTokenIsConsumedByOtherPlayersActualOriginalCard()
        {
            var (j, skill, other) = Start("hero_skill_pool_003"); var owner = j.Player; var area = j.Game.SharedSkills;
            owner.Mana.AddToken(ManaColor.Red); area.ArmEnhancement(owner, j.Context, skill, ManaColor.Red);
            Play(j, "basic_card_021", true); var token = area.Tokens.Single(); j.EndTurn(); j.BeginTurn();
            Assert.That(j.Player, Is.SameAs(other)); area.ClaimEnhancement(other, j.Context, token);
            Play(j, "basic_card_021", true);
            Assert.That(other.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(owner.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(area.Tokens, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => area.ClaimEnhancement(other, j.Context, token));
        }

        [Test]
        public void EnhancementDoesNotTriggerForFreeOrInfiniteManaAndCanBeCancelled()
        {
            var (j, skill, _) = Start("hero_skill_pool_003"); var area = j.Game.SharedSkills;
            area.ArmEnhancement(j.Player, j.Context, skill, ManaColor.Red);
            j.Context.NextCardEnhanced = true; Play(j, "basic_card_021", false);
            j.Context.InfiniteMana.Add(ManaColor.Red); Play(j, "basic_card_021", true);
            Assert.That(area.Tokens, Is.Empty); Assert.That(SkillActions.IsUsed(j.Player, skill), Is.False);
            area.CancelEnhancement(j.Player, j.Context); j.Context.InfiniteMana.Clear();
            j.Player.Mana.AddToken(ManaColor.Red); Play(j, "basic_card_021", true);
            Assert.That(area.Tokens, Is.Empty);
        }

        [Test]
        public void UnclaimedEnhancementExpiresOnlyWhenOwnersActualNextTurnStarts()
        {
            var (j, skill, other) = Start("hero_skill_pool_003"); var area = j.Game.SharedSkills; var owner = j.Player;
            area.ArmEnhancement(owner, j.Context, skill, ManaColor.Red); owner.Mana.AddToken(ManaColor.Red);
            Play(j, "basic_card_021", true); var token = area.Tokens.Single();
            j.EndTurn(); j.BeginTurn();
            Assert.That(j.Player, Is.SameAs(other)); Assert.That(area.Tokens.Single(), Is.SameAs(token));
            j.EndTurn(new[] { other.Deck.Hand.First() }); j.BeginTurn();
            Assert.That(j.Player, Is.SameAs(owner)); Assert.That(area.Tokens, Is.Empty);
            Assert.That(SkillActions.IsUsed(owner, skill), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void OverloadUsesActualNormalOrBlackStrongSpellPaymentAndKeepsElement(bool strong)
        {
            var (j, skill, _) = Start("hero_skill_pool_007"); var area = j.Game.SharedSkills;
            var color = strong ? ManaColor.Black : ManaColor.Red;
            area.UseOverload(j.Player, j.Context, skill, color);
            j.Context.DayPart = DayPart.Night;
            if (strong) j.Player.Mana.AddToken(ManaColor.Red);
            var card = Play(j, "magic_009", strong);
            var attack = j.Context.CombatPower.Attacks.Single();
            Assert.That(attack.SourceCard, Is.SameAs(card)); Assert.That(attack.Element, Is.EqualTo(Element.Fire));
            Assert.That(attack.Value, Is.EqualTo(strong ? 12 : 9));
            Assert.That(area.Tokens, Is.Empty);
        }

        [Test]
        public void OverloadIgnoresFreeEnhancementAndNoOutputPaymentWithoutLosingCentralSkill()
        {
            var (j, skill, _) = Start("hero_skill_pool_007"); var area = j.Game.SharedSkills;
            var token = area.UseOverload(j.Player, j.Context, skill, ManaColor.Red);
            j.Context.NextCardEnhanced = true; Play(j, "basic_card_021", false);
            Assert.That(j.Context.CombatPower.Attacks.Single().Value, Is.EqualTo(4));
            Assert.That(area.Tokens.Single(), Is.SameAs(token));
            Assert.That(j.Player.Mana.TryPayExactColors(new[] { ManaColor.Red }, j.Context, j.Player), Is.True);
            Assert.That(area.Tokens.Single(), Is.SameAs(token));
        }

        [Test]
        public void ActualPaidCrystallizeDoesNotConsumeOverloadWithoutSupportedOutput()
        {
            var (j, skill, _) = Start("hero_skill_pool_007"); var area = j.Game.SharedSkills;
            var token = area.UseOverload(j.Player, j.Context, skill, ManaColor.Blue);
            Play(j, "basic_card_015", true, (int)ManaColor.White);
            Assert.That(j.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(j.Player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(area.Tokens.Single(), Is.SameAs(token));
        }

        [Test]
        public void OriginalBlockRetainsPhysicalSourceAndReceivesOnlyOneBonus()
        {
            var (j, skill, _) = Start("hero_skill_pool_007"); var area = j.Game.SharedSkills;
            area.UseOverload(j.Player, j.Context, skill, ManaColor.Blue);
            var card = Play(j, "basic_card_016", true);
            var block = j.Context.CombatPower.Blocks.Single();
            Assert.That(block.SourceCard, Is.SameAs(card)); Assert.That(block.Value, Is.EqualTo(9));
            Assert.That(block.Element, Is.EqualTo(Element.Physical));
            Assert.That(area.Tokens, Is.Empty);
        }

        [Test]
        public void RepeatedOriginalAttackRequiresActualContributionChoiceAndBlocksNextCommand()
        {
            var (j, skill, _) = Start("hero_skill_pool_007"); var area = j.Game.SharedSkills;
            area.UseOverload(j.Player, j.Context, skill, ManaColor.Red);
            var material = new DeedCard("basic_card_021", CardType.Action); j.Player.Deck.Hand.Add(material);
            j.Context.CardToRemove = material; j.Context.OverloadStepwise = true;
            var card = Play(j, "advanced_card_024", true);
            Assert.That(j.Context.PendingRepeatedEffect, Is.Not.Null);
            CardPlayActions.ContinueRepeatedEffect(j.Player, j.Context, new RepeatedCardSelection());
            var choices = area.PendingOverloadChoices(j.Context);
            Assert.That(choices.Count, Is.EqualTo(2));
            Assert.That(choices.All(c => ReferenceEquals(c.Attack.SourceCard, card)), Is.True);
            Assert.Throws<InvalidOperationException>(() => Play(j, "basic_card_021", false));
            Assert.Throws<InvalidOperationException>(() => j.EndTurn());
            Assert.Throws<InvalidOperationException>(() => area.ResolveOverload(j.Player, j.Context, 2));
            area.ResolveOverload(j.Player, j.Context, 1);
            Assert.That(j.Context.CombatPower.Attacks.Select(a => a.Value), Is.EqualTo(new[] { 4, 8 }));
            Assert.That(area.Tokens, Is.Empty); Assert.That(area.PendingOverloadChoices(j.Context), Is.Empty);
        }
    }
}
