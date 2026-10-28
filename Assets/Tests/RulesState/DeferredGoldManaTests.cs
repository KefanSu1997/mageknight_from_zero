using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Data;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class DeferredGoldManaTests
    {
        [TestCase("basic_card_002",6)] [TestCase("basic_card_006",7)]
        public void OriginalFocusStrongEntryFeedsRealNextAction(string id,int attack)
        {
            CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),"resources/text_json"));
            var p=new PlayerState();p.Mana.AddToken(ManaColor.Gold);
            var focus=new DeedCard(id,CardType.Action);var fury=new DeedCard("basic_card_021",CardType.Action);
            p.Deck.Hand.Add(focus);p.Deck.Hand.Add(fury);
            var enemy=System.Linq.Enumerable.First(CardJsonLoader.LoadMonsters());
            var battle=new PveBattleSession(p,new[]{enemy},CardJsonLoader.LoadBasicActions());
            battle.Advance();battle.Advance();battle.Advance();
            Assert.That(System.Linq.Enumerable.Any(battle.Choices(focus),c=>c.Enhanced),Is.True);
            battle.Play(focus,"strong");
            Assert.That(battle.Context.NextCardEnhanced,Is.True);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.Zero);
            battle.Play(fury,"strong");
            Assert.That(battle.Context.CombatPower.AvailableAttacks(Phase.Melee,false)[0].Value,Is.EqualTo(attack));
            Assert.That(battle.Context.NextCardEnhanced,Is.False);
        }
        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)]
        [TestCase(ManaColor.Green)] [TestCase(ManaColor.White)]
        public void TakenGoldStaysGoldUntilActualPayment(ManaColor required)
        {
            var p=new PlayerState();
            var source=new ManaSource(1,()=>DayPart.Day,new Random(0));
            var die=source.Dice[0];die.TurnTo(ManaColor.Gold);
            var ctx=new ActionContext {DayPart=DayPart.Day,ManaSource=source};
            CardManaActions.UseSourceDie(p,ctx,die,ManaColor.Gold);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(required),Is.Zero);
            Assert.That(p.HeldManaDie,Is.SameAs(die));
            p.Mana.AddCrystal(required);
            Assert.That(p.Mana.TryPayExactColors(new[]{required},ctx,p),Is.True);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold),Is.Zero);
            Assert.That(p.Mana.Crystals[required],Is.EqualTo(1));
        }

        [Test]
        public void FailedMultiCostDoesNotSpendGoldAndCannotReplaceBlack()
        {
            var p=new PlayerState();p.Mana.AddToken(ManaColor.Gold);
            var ctx=new ActionContext {DayPart=DayPart.Day};
            Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Blue,ManaColor.Black},ctx,p),Is.False);
            Assert.That(p.Mana.Tokens[ ManaColor.Gold],Is.EqualTo(1));
            ctx.DayPart=DayPart.Night;
            Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Red},ctx,p),Is.False);
            Assert.That(p.Mana.Tokens[ ManaColor.Gold],Is.EqualTo(1));
            ctx.AllowGoldAtNight=true;
            Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Red},ctx,p),Is.True);
        }
    }
}
