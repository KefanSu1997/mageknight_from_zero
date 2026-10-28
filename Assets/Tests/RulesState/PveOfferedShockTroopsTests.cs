using System;
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
    public sealed class PveOfferedShockTroopsTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PveJourneySession j,UnitCard offered) Start()
        {
            var p=new PlayerState();var spell=new DeedCard("magic_020",CardType.Spell);p.Deck.Hand.Add(spell);
            p.Deck.Hand.Add(new DeedCard("basic_card_021",CardType.Action));p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var offered=CardJsonLoader.LoadUnit("army_2_010");var deck=new[]{CardJsonLoader.LoadUnit("army_2_006"),CardJsonLoader.LoadUnit("army_2_000"),CardJsonLoader.LoadUnit("army_2_002"),offered};
            var j=new PveJourneySession(p,cards,spells:spells,unitDeck:deck);j.SelectTactic("day_1");j.BeginTurn();
            Assert.That(j.Game.Globals.Units.AppendRegularToOffer(1),Is.EqualTo(1));Assert.That(j.Context.UnitOffer[3],Is.SameAs(offered));
            j.BeginBattle(new[]{MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Single(m=>m.Id=="monster_058")});
            j.Battle.Advance();p.Mana.AddToken(ManaColor.White);j.Battle.Play(spell,"base");Assert.That(j.Context.OfferedUnitUses,Is.EqualTo(1));return(j,offered);
        }
        [Test] public void FourthActualOfferWeakensOnlySecondAttackAndArmorFeedsRealKill()
        {
            var(j,offered)=Start();var p=j.Player;var c=j.Context;var supply=j.Context.UnitOffer.ToArray();
            j.UseOfferedUnit(offered,1,target:0,attackIndex:1);
            Assert.That(j.Battle.BlockPreview(0,0).Required,Is.EqualTo(1));Assert.That(j.Battle.BlockPreview(0,1).Required,Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(j.Battle.Enemies[0],0,c),Is.EqualTo(3));Assert.That(c.OfferedUnitUses,Is.Zero);
            Assert.That(p.Units,Is.Empty);Assert.That(c.UnitOffer,Is.EqualTo(supply));Assert.That(c.UnitOffer[3],Is.SameAs(offered));Assert.That(c.HasRecruited,Is.False);
            j.Battle.Block(0,1);j.Battle.Advance();j.Battle.Advance();Assert.That(p.Wounds,Is.EqualTo(1));
            j.Battle.Play(p.Deck.Hand.First(x=>x.Id=="basic_card_021"),"attack");j.Battle.PlaySideways(p.Deck.Hand.First(x=>x.Id=="basic_card_000"));
            Assert.That(j.Battle.Attack(0).Kills,Is.EqualTo(1));Assert.That(c.UnitOffer[3],Is.SameAs(offered));Assert.That(p.Units,Is.Empty);
        }
        [Test] public void PendingRealHeadbandChoiceBlocksSupplyAbilityWithoutSpendingPermission()
        {
            var(j,offered)=Start();var p=j.Player;var c=j.Context;
            var headband=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(headband);
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[6];c.SkillSupply.Offer.Add(skill);
            j.UseHeadbandShapeshift(headband,skill,p.Deck.Hand.First(x=>x.Id=="basic_card_000"),false,0,FixedOutputKind.Block);
            Assert.Throws<InvalidOperationException>(()=>j.UseOfferedUnit(offered,1,target:0,attackIndex:1));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.OfferedUnitUses,Is.EqualTo(1));
            Assert.That(j.Battle.BlockPreview(0,1).Required,Is.EqualTo(1));Assert.That(c.UnitOffer[3],Is.SameAs(offered));Assert.That(p.Units,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void IllegalAttackSlotOrUnownedInterceptionPreservesPermissionAndResources(bool intercept)
        {
            var(j,offered)=Start();var c=j.Context;var p=j.Player;p.Mana.AddToken(ManaColor.Blue);var before=c.UnitOffer.ToArray();
            Assert.Throws<InvalidOperationException>(()=>j.UseOfferedUnit(offered,intercept?2:1,target:0,attackIndex:intercept?1:2));
            Assert.That(c.OfferedUnitUses,Is.EqualTo(1));Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));Assert.That(c.UnitOffer,Is.EqualTo(before));Assert.That(p.Units,Is.Empty);
            Assert.That(j.Battle.BlockPreview(0,0).Required,Is.EqualTo(1));Assert.That(j.Battle.BlockPreview(0,1).Required,Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(j.Battle.Enemies[0],0,c),Is.EqualTo(4));
            j.UseOfferedUnit(offered,1,target:0,attackIndex:1);Assert.That(c.OfferedUnitUses,Is.Zero);
        }
    }
}
