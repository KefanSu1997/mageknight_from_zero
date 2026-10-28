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
    public sealed class PveOriginalDeedEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static ActionCardData AsAction(SpellCardData s)=>new(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost);
        private static PveBattleSession Spell(string id,DayPart time=DayPart.Day,int enemies=1,bool metadata=true)
        {
            var p=new PlayerState();var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id==id);
            p.Deck.Hand.Add(new DeedCard(id,CardType.Spell));
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            return new PveBattleSession(p,Enumerable.Range(0,enemies).Select(_=>e with{}),new[]{AsAction(spell)},new ActionContext {DayPart=time},
                spells:metadata ? new[]{spell}:null);
        }
        [TestCase("magic_009",ManaColor.Red,Element.Fire)]
        [TestCase("magic_015",ManaColor.Blue,Element.Ice)]
        public void PackagedOriginalSpellPaysItsTopCostAndAttacksWithOriginalElement(string id,ManaColor color,Element element)
        {
            var b=Spell(id);var p=b.Player;var card=p.Deck.Hand.Single();p.Mana.AddCrystal(color);
            b.Play(card,"base");Assert.That(p.Mana.Crystals[color],Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            var power=b.Context.CombatPower.Attacks.Single();Assert.That(power.Element,Is.EqualTo(element));Assert.That(power.Value,Is.EqualTo(5));
            var hit=b.Attack(0);Assert.That(hit.Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(2));
        }
        [TestCase("magic_009",ManaColor.Red)]
        [TestCase("magic_015",ManaColor.Blue)]
        public void StrongSpellRejectsDayBeforePaymentThenUsesNightBlackAndRealWound(string id,ManaColor color)
        {
            var b=Spell(id);var p=b.Player;var card=p.Deck.Hand.Single();p.Mana.AddCrystal(color);p.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,"strong"));
            Assert.That(p.Mana.Crystals[color],Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
            var night=Spell(id,DayPart.Night);p=night.Player;card=p.Deck.Hand.Single();p.Mana.AddCrystal(color);p.Mana.AddToken(ManaColor.Black);
            night.Play(card,"strong");Assert.That(p.Mana.Crystals[color],Is.Zero);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
            Assert.That(night.Context.SiegePool,Is.EqualTo(8));Assert.That(p.Deck.Hand.Single().Type,Is.EqualTo(CardType.Wound));
            Assert.That(night.Attack(0).Kills,Is.EqualTo(1));
        }
        [Test] public void MissingPackagedSpellCostRejectsInsteadOfGuessingFromActionColor()
        {
            var b=Spell("magic_009",metadata:false);var card=b.Player.Deck.Hand.Single();b.Player.Mana.AddCrystal(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,"base"));
            Assert.That(b.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(b.Player.Deck.Hand.Single(),Is.SameAs(card));
        }
        [Test] public void StrongFlameWallUsesActualEngagedEnemyCountAndConsumesBlock()
        {
            var b=Spell("magic_010",DayPart.Night,2);var p=b.Player;var card=p.Deck.Hand.Single();p.Mana.AddCrystal(ManaColor.Red);p.Mana.AddToken(ManaColor.Black);
            b.Advance();b.Play(card,"strong_block");
            Assert.That(b.Context.EngagedEnemies,Is.EqualTo(2));Assert.That(b.Context.BlockPool,Is.EqualTo(11));
            Assert.That(b.BlockPreview(0),Is.EqualTo((11,4)));b.Block(0);
            Assert.That(b.Context.BlockPool,Is.Zero);Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
        }
        [TestCase(ManaColor.Red,7,Element.ColdFire,AttackType.Melee)]
        [TestCase(ManaColor.Blue,8,Element.Ice,AttackType.Melee)]
        [TestCase(ManaColor.Green,5,Element.Ice,AttackType.Siege)]
        [TestCase(ManaColor.White,6,Element.Ice,AttackType.Ranged)]
        public void ManaBoltUsesPlayerColorChoiceAndBothPrintedAndAdditionalCosts(ManaColor color,int value,Element element,AttackType type)
        {
            var b=Spell("magic_017");var p=b.Player;var card=p.Deck.Hand.Single();p.Mana.AddCrystal(ManaColor.Blue);p.Mana.AddCrystal(color);
            if(type==AttackType.Melee){b.Advance();b.Advance();b.Advance();}
            b.Play(card,"base_"+color);var power=b.Context.CombatPower.Attacks.Single();
            Assert.That(power.Value,Is.EqualTo(value));Assert.That(power.Element,Is.EqualTo(element));Assert.That(power.Type,Is.EqualTo(type));
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(p.Mana.Crystals[color],Is.Zero);Assert.That(b.Attack(0).Kills,Is.EqualTo(1));
        }
        [Test] public void ManaBoltCannotPartiallyPayItsBluePlusBlueCost()
        {
            var b=Spell("magic_017");var p=b.Player;var card=p.Deck.Hand.Single();p.Mana.AddCrystal(ManaColor.Blue);
            b.Advance();b.Advance();b.Advance();Assert.Throws<InvalidOperationException>(()=>b.Play(card,"base_Blue"));
            Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));Assert.That(p.Deck.Hand.Contains(card),Is.True);Assert.That(b.Context.MeleePool,Is.Zero);
        }
        [Test] public void VersatileCombatFireBlockChoicePaysRedAndActuallyBlocksWithSidewaysSupport()
        {
            var p=new PlayerState();var card=new DeedCard("basic_card_024",CardType.Action);var support=new DeedCard("basic_card_000",CardType.Action);
            p.Deck.Hand.Add(card);p.Deck.Hand.Add(support);p.Mana.AddCrystal(ManaColor.Red);
            var b=new PveBattleSession(p,new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")},CardJsonLoader.LoadBasicActions());
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,"strong_fire_block"));
            Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));b.Advance();b.Play(card,"strong_fire_block");b.PlaySideways(support);
            Assert.That(b.BlockPreview(0),Is.EqualTo((4,4)));b.Block(0);b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);
        }
    }
}
