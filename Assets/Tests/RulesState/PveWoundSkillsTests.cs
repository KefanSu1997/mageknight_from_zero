using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveWoundSkillsTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static SkillCard Skill(PlayerState player, string pool, int index)
        {
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_" + pool)[index];
            player.Skills.Add(skill); return skill;
        }
        private static PlayerState Player()
        {
            var player = new PlayerState { Fame = 5, Reputation = -5 };
            player.Deck.SetDeck(new[] { new DeedCard("basic_card_000", CardType.Action) });
            player.Deck.GainWound(); player.Deck.GainWound(); return player;
        }
        [TestCase("001", 8, ManaColor.Red, false)]
        [TestCase("001", 8, ManaColor.Blue, false)]
        [TestCase("001", 8, ManaColor.Green, true)]
        [TestCase("001", 8, ManaColor.White, false)]
        [TestCase("001", 8, ManaColor.Gold, false)]
        [TestCase("001", 8, ManaColor.Black, false)]
        [TestCase("003", 4, ManaColor.Red, true)]
        [TestCase("003", 4, ManaColor.Green, false)]
        public void RegenerationPaysExactSelectedColorRemovesExactWoundAndDrawsOriginal(string pool, int slot, ManaColor color, bool draw)
        {
            var player = Player(); var context = new ActionContext { InBattle = true };
            context.OtherPlayers.Add(new PlayerState { Fame = 4, Reputation = 5 });
            var kept = player.Deck.Hand[0]; var removed = player.Deck.Hand[1]; context.SelectedSkillWound = removed;
            player.Mana.AddToken(color); player.Mana.AddToken(color == ManaColor.White ? ManaColor.Green : ManaColor.White);
            var skill = Skill(player, pool, slot); SkillActions.Use(player, context, skill, (int)color);
            Assert.That(player.Mana.Tokens.ContainsKey(color), Is.False);
            Assert.That(player.Mana.GetTotal(), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, kept)), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, removed)), Is.False);
            Assert.That(player.Deck.DiscardPile.Count, Is.Zero); Assert.That(player.Deck.RemovedCards.Count, Is.Zero);
            Assert.That(player.Deck.Hand.Any(c => c.Id == "basic_card_000"), Is.EqualTo(draw));
            Assert.That(context.HealedHandWoundsThisTurn, Is.Zero);
            Assert.That(SkillActions.IsUsed(player, skill), Is.True);
        }
        [TestCase(4, false)] [TestCase(5, false)] [TestCase(6, true)]
        public void RegenerationLowestUsesFameAndExcludesTie(int otherFame, bool draws)
        {
            var player = Player(); var context = new ActionContext { SelectedSkillWound = player.Deck.Hand[1] };
            context.OtherPlayers.Add(new PlayerState { Fame = otherFame, Reputation = 99 }); player.Mana.AddCrystal(ManaColor.Blue);
            SkillActions.Use(player, context, Skill(player, "003", 4), (int)ManaColor.Blue);
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(draws ? 0 : 1)); Assert.That(player.Mana.GetTotal(), Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidRegenerationPreservesManaWoundsAndUsage(int invalid)
        {
            var player = Player(); var context = new ActionContext { SelectedSkillWound = player.Deck.Hand[1] };
            if (invalid != 0) player.Mana.AddToken(ManaColor.Green);
            if (invalid == 1) context.SelectedSkillWound = new DeedCard("w", CardType.Wound);
            if (invalid == 2) context.SelectedSkillWound = null;
            var skill = Skill(player, "001", 8); int mana = player.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(player, context, skill, invalid == 3 ? 99 : (int)ManaColor.Green));
            Assert.That(player.Wounds, Is.EqualTo(2)); Assert.That(player.Mana.GetTotal(), Is.EqualTo(mana));
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
        }
        [Test] public void PainPermissionSurvivesOrdinaryCardThenConsumesOneExactWoundAndDiscardsAtTurnEnd()
        {
            var player = Player(); var context = new ActionContext(); var engine = new PlayerTurnEngine(); engine.StartTurn(player, DayPart.Day);
            var skill = Skill(player, "000", 5); SkillActions.Use(player, context, skill);
            var action = new ActionSystem(false); var ordinary = new DeedCard("basic_card_008", CardType.Action); player.Deck.Hand.Add(ordinary);
            var wound = player.Deck.Hand[1]; var kept = player.Deck.Hand[0];
            Assert.That(action.PlaySideways(ordinary, player, context, SidewaysType.Influence), Is.True);
            Assert.That(context.SidewaysWoundValue, Is.EqualTo(2));
            Assert.That(action.PlaySideways(new DeedCard("w", CardType.Wound), player, context, SidewaysType.Move), Is.False);
            Assert.That(context.SidewaysWoundValue, Is.EqualTo(2));
            Assert.That(action.PlaySideways(wound, player, context, SidewaysType.Move), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(2)); Assert.That(context.InfluencePool, Is.EqualTo(1));
            Assert.That(context.SidewaysWoundValue, Is.Zero);
            Assert.That(action.PlaySideways(kept, player, context, SidewaysType.Move), Is.False);
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, wound)), Is.True);
            engine.EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(player.Deck.DiscardPile.Any(c => ReferenceEquals(c, wound)), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, kept)), Is.True);
        }
        [TestCase("001", 8, ManaColor.Green)]
        [TestCase("003", 4, ManaColor.Red)]
        public void JourneyEntryUsesSelectedWoundAndRestoresTemporaryInput(string pool, int slot, ManaColor color)
        {
            var player = Player(); player.Deck.Hand.Add(new DeedCard("basic_card_008", CardType.Action));
            var skill = Skill(player, pool, slot);
            var journey = new MK.Logic.Runtime.Battles.PveJourneySession(player, CardJsonLoader.LoadBasicActions());
            journey.SelectTactic("day_1"); journey.BeginTurn();
            var wound = player.Deck.Hand.Last(c => c.Type == CardType.Wound); player.Mana.AddToken(color);
            journey.UseOutsideSkill(skill, (int)color, wound: wound);
            Assert.That(journey.Context.SelectedSkillWound, Is.Null);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, wound)), Is.False);
            Assert.That(player.Deck.Hand.Any(c => c.Id == "basic_card_000"), Is.True);
        }

        [Test] public void UnusedPainPermissionExpiresAtFormalTurnEnd()
        {
            var player=Player();var context=new ActionContext();var engine=new PlayerTurnEngine();
            engine.StartTurn(player,DayPart.Day);SkillActions.Use(player,context,Skill(player,"000",5));
            Assert.That(context.SidewaysWoundValue,Is.EqualTo(2));
            engine.EndTurn(player,DayPart.Day,new ManaSource(1,()=>DayPart.Day),context);
            Assert.That(context.SidewaysWoundValue,Is.Zero);
            Assert.That(new ActionSystem(false).PlaySideways(player.Deck.Hand.First(c=>c.Type==CardType.Wound),player,context,SidewaysType.Move),Is.False);
        }
    }
}
