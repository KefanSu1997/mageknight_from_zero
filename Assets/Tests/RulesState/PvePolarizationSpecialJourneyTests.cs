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
    public sealed class PvePolarizationSpecialJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        [Test]
        public void OriginalAmuletDayBlackAndNightGoldPayActualOppositeCostsAcrossFormalRound()
        {
            var player=new PlayerState{HeroId="Arythea",Fame=15};
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[7];HeroSkillAcquisition.Acquire(player,skill);
            player.Mana.AddCrystal(ManaColor.Red);player.Mana.AddCrystal(ManaColor.Blue);
            player.Deck.SetDeck(new[]{"basic_card_000","basic_card_007","basic_card_008","magic_009","items_015"}
                .Select(id=>new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:id.StartsWith("magic_")?CardType.Spell:CardType.Action)));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var deeds=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var j=new PveJourneySession(player,deeds,CardJsonLoader.LoadItems(),spells:spells);
            j.SelectTactic("day_1");j.BeginTurn();
            DeedCard Card(string id)=>player.Deck.Hand.Single(c=>c.Id==id);
            var amulet=Card("items_015");var swift=Card("basic_card_007");var fire=Card("magic_009");
            j.PlayOutsideArtifact(amulet,false,null,ManaColor.Black);
            Assert.That(player.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            var first=j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"));
            ManaSkillPaymentActions.Execute(player,j.Context,new ManaSkillPaymentSelection(skill,ManaColor.White,SourceColor:ManaColor.Black),()=>first.Play(swift,"strong"));
            Assert.That(j.Context.RangedPool,Is.EqualTo(3));Assert.That(SkillActions.IsUsed(player,skill),Is.True);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
            Assert.That(first.Attack(0).Kills,Is.EqualTo(1));
            while(first.Phase!=Phase.End)first.Advance();j.EndTurn(player.Deck.Hand.ToArray());
            Assert.That(player.Deck.Hand,Is.Empty);Assert.That(player.Deck.DiscardPile.Count,Is.EqualTo(5));
            j.AnnounceRoundEnd();j.EndRound();j.PrepareRound();j.SelectTactic("night_1");j.BeginTurn();
            Assert.That(j.Player,Is.SameAs(player));Assert.That(Card("items_015"),Is.SameAs(amulet));Assert.That(Card("magic_009"),Is.SameAs(fire));
            Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Night));Assert.That(SkillActions.IsUsed(player,skill),Is.False);
            j.PlayOutsideArtifact(amulet,false,null,ManaColor.Gold);
            Assert.That(player.Mana.Tokens[ManaColor.Gold],Is.EqualTo(1));Assert.That(j.Context.GoldManaWild,Is.False);
            var second=j.BeginBattle(CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_007"));
            ManaSkillPaymentActions.Execute(player,j.Context,new ManaSkillPaymentSelection(skill,ManaColor.Black,SourceColor:ManaColor.Gold),()=>second.Play(fire,"strong"));
            Assert.That(j.Context.SiegePool,Is.EqualTo(8));Assert.That(player.Mana.Crystals[ManaColor.Red],Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.Zero);Assert.That(SkillActions.IsUsed(player,skill),Is.True);
            Assert.That(player.Wounds,Is.EqualTo(1));Assert.That(second.Attack(0).Kills,Is.EqualTo(1));
            while(second.Phase!=Phase.End)second.Advance();j.EndTurn(player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray());
            Assert.That(j.CompletedTurns,Is.EqualTo(2));Assert.That(j.CompletedBattles,Is.EqualTo(2));
            Assert.That(player.Fame,Is.EqualTo(20));Assert.That(player.Wounds,Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count,Is.EqualTo(1));Assert.That(player.Deck.DiscardPile.Count,Is.EqualTo(5));
        }
    }
}
