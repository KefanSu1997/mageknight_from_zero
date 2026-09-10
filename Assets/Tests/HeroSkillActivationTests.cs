using System;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeroSkillActivationTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.GetFullPath("resources/text_json"));

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void RepeatedAndReloadedDefinitionsShareUsageMarker(int index)
        {
            var player = new PlayerState(); var context = new ActionContext();
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[index]; player.Skills.Add(skill);
            SkillActions.Use(player, context, skill);
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, context, skill));
            player.Skills.Clear(); skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[index]; player.Skills.Add(skill);
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, context, skill));
            new PlayerTurnEngine().StartTurn(player, DayPart.Day);
            Assert.That(SkillActions.IsUsed(player, skill), Is.EqualTo(index == 4));
            new GameEngine(new[] { player }, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
            Assert.DoesNotThrow(() => SkillActions.Use(player, new ActionContext(), skill));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidOptionsPreserveResourcesAndAvailableActivation(int index)
        {
            var player = new PlayerState(); var context = new ActionContext();
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[index]; player.Skills.Add(skill);
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, context, skill, -1));
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
            Assert.That(context.MovementPool + context.InfluencePool + context.SiegePool + context.MeleePool, Is.Zero);
            Assert.That(player.Mana.Crystals.Count + player.Mana.Tokens.Count, Is.Zero);
            Assert.DoesNotThrow(() => SkillActions.Use(player, context, skill));
        }

        [Test]
        public void ForeignEquivalentSkillCannotExecuteAnUnownedEffect()
        {
            var player = new PlayerState(); var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[1];
            player.Skills.Add(skill);
            var clone = skill with { };
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, new ActionContext(), clone));
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
        }

        [Test]
        public void OtherPlayersTurnDoesNotRefreshOwnersSkill()
        {
            var player = new PlayerState(); var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[0];
            player.Skills.Add(skill); SkillActions.Use(player, new ActionContext(), skill);
            new PlayerTurnEngine().StartTurn(new PlayerState(), DayPart.Day);
            Assert.That(SkillActions.IsUsed(player, skill), Is.True);
        }

        [Test]
        public void ComplexTimingWithoutMetadataIsRejected()
        {
            var player = new PlayerState(); var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[9];
            player.Skills.Add(skill);
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, new ActionContext(), skill));
        }
    }
}
