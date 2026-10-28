using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveWolfhawkPrimaryConsumersJourneyTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void FameInspirationDrawThenInsightPhysicalResistanceAndTauntActualDamage()
        {
            var originals = CardJsonLoader.LoadBasicActions();
            var player = OriginalHeroes.Get("Wolfhawk").CreatePlayer(originals, new Random(0));
            player.Fame = 35;
            player.Armor = 2 + (player.Level + 1) / 4;
            player.Mana.Crystals[ManaColor.Red] = 1;
            player.Mana.Crystals[ManaColor.Blue] = 1;
            var pool = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004");
            var insight = pool[5]; var taunt = pool[6]; var inspiration = pool[8];
            foreach (var skill in new[] { insight, taunt, inspiration })
                HeroSkillAcquisition.Acquire(player, skill);
            Assert.That(player.Level, Is.EqualTo(6));
            Assert.That(player.Armor, Is.EqualTo(3));
            Assert.That(player.Skills.Count, Is.EqualTo(player.Level / 2));
            var originalInstances = player.Deck.DrawPile.ToArray();
            var journey = new PveJourneySession(player, originals,
                manaRandom: new Random(0), supplyRandom: new Random(0));
            journey.PrepareRound(); journey.SelectTactic("day_1"); journey.BeginTurn();
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            var initialFury = player.Deck.Hand.Single(c => c.Id == "basic_card_021");
            var march = player.Deck.Hand.Single(c => c.Id == "basic_card_000");
            var drawn = player.Deck.DrawPile.Take(2).ToArray();
            Assert.That(drawn.Select(c => c.Id), Is.EqualTo(new[] {
                "basic_card_021", "basic_card_002" }));
            journey.UseOutsideSkill(inspiration);
            Assert.That(drawn.All(c => player.Deck.Hand.Any(h => ReferenceEquals(h, c))), Is.True);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(8));
            Assert.That(player.Fame, Is.EqualTo(36));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(SkillActions.IsUsed(player, inspiration), Is.True);

            var lich = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_004");
            var battle = journey.BeginBattle(new[] { lich });
            var physicalFour = new[] { new AttackProfile(AttackType.Melee, 4, Element.Physical) };
            Assert.That(CombatMath.HasResistance(lich, 0, Element.Physical, battle.Context), Is.True);
            Assert.That(CombatMath.EffectiveAttack(battle.Enemies, new[] { 0 }, physicalFour,
                Phase.Melee, battle.Context), Is.EqualTo(2));
            battle.UseSkill(insight, 100 + (int)Ability.PhysicalResist, target: 0);
            Assert.That(CombatMath.HasResistance(lich, 0, Element.Physical, battle.Context), Is.False);
            Assert.That(CombatMath.HasResistance(lich, 0, Element.Ice, battle.Context), Is.True);
            Assert.That(lich.Abilities.Contains(Ability.PhysicalResist), Is.True,
                "The original definition remains unchanged; only this battle target is ignored");
            Assert.That(CombatMath.EffectiveAttack(battle.Enemies, new[] { 0 }, physicalFour,
                Phase.Melee, battle.Context), Is.EqualTo(4));
            battle.Advance(); battle.Advance(); battle.Advance();
            var oldWound = player.Deck.Hand.Single(c => c.Type == CardType.Wound);
            battle.Play(initialFury, "strong");
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            battle.Attack(0); battle.Advance(); journey.EndTurn();
            Assert.That(player.Fame, Is.EqualTo(41));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(8));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(8));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, drawn[0])), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, oldWound)), Is.True);

            journey.BeginTurn();
            Assert.That(insight.Frequency, Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.That(SkillActions.IsUsed(player, insight), Is.True,
                "Insight is once per round and remains spent across the next personal turn");
            Assert.That(SkillActions.IsUsed(player, inspiration), Is.True);
            var skeleton = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var second = journey.BeginBattle(new[] { skeleton });
            second.Advance();
            Assert.That(CardCombatActions.DefenseProfile(skeleton, 0, second.Context, 0).Attack, Is.EqualTo(4));
            second.UseSkill(taunt, 0, target: 0, attackIndex: 0);
            Assert.That(CardCombatActions.DefenseProfile(skeleton, 0, second.Context, 0).Attack, Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveArmor(skeleton, 0, second.Context), Is.EqualTo(3));
            second.Advance(); second.Advance();
            Assert.That(player.Wounds, Is.EqualTo(2), "Attack 3 versus armor 3 adds one actual wound");
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, oldWound)), Is.True);
            second.Play(drawn[0], "attack");
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, drawn[0])), Is.True,
                "Consume the exact original Fury drawn by Inspiration in the previous turn");
            second.PlaySideways(march);
            second.Attack(0); second.Advance(); journey.EndTurn();
            Assert.That(journey.Player, Is.SameAs(player));
            Assert.That(journey.CompletedTurns, Is.EqualTo(2));
            Assert.That(journey.CompletedBattles, Is.EqualTo(2));
            Assert.That(player.Fame, Is.EqualTo(43));
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(7));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(8));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(3));
            var finalCards = player.Deck.Hand.Concat(player.Deck.DrawPile)
                .Concat(player.Deck.DiscardPile).ToArray();
            Assert.That(finalCards.Length, Is.EqualTo(18));
            Assert.That(originalInstances.All(c => finalCards.Any(x => ReferenceEquals(x, c))), Is.True);
            Assert.That(player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
        }
    }
}
