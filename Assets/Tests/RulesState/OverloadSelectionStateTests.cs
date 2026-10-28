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
    public sealed class OverloadSelectionStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard source, DeedCard material) Fixture(string id)
        {
            var p = new PlayerState();
            var source = new DeedCard("advanced_card_024", CardType.Action);
            var material = new DeedCard(id, CardType.Action);
            p.Deck.Hand.AddRange(new[] { source, material });
            p.Mana.AddToken(ManaColor.Red);
            return (p, new ActionContext { CardToRemove=material, OverloadSelections=new List<RepeatedCardSelection>(), SkipDraw=true }, source, material);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard source, bool strong)
            => CardPlayActions.Play(p, c, source, CardJsonLoader.LoadAdvancedActions().Single(card=>card.Id==source.Id), strong);

        [TestCase(false)] [TestCase(true)]
        public void EachUseOfOriginalInstinctHasItsOwnChoice(bool strong)
        {
            var (p,c,source,material)=Fixture("basic_card_027");
            c.OverloadSelections.AddRange((strong?new[]{2,3}:new[]{0,1,2}).Select(option=>new RepeatedCardSelection { Option=option }));
            Play(p,c,source,strong);
            Assert.That(c.MovementPool,Is.EqualTo(strong?0:2));
            Assert.That(c.InfluencePool,Is.EqualTo(strong?0:2));
            Assert.That(c.MeleePool,Is.EqualTo(strong?4:2));
            Assert.That(c.BlockPool,Is.EqualTo(strong?4:0));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(material));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(source));
            Assert.That(c.OverloadSelections,Is.Null);
        }

        [Test]
        public void TwoStrongCrystallizeUsesChooseDifferentColorsWithoutPayingMaterialBlue()
        {
            var(p,c,source,_)=Fixture("basic_card_015");
            c.OverloadSelections.Add(new RepeatedCardSelection { Option=(int)ManaColor.Blue });
            c.OverloadSelections.Add(new RepeatedCardSelection { Option=(int)ManaColor.Green });
            Play(p,c,source,true);
            Assert.That(p.Mana.Tokens,Is.Empty);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue),Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red),Is.Zero);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White),Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void EachDecomposeUsesItsOwnPhysicalMaterialAndAllRemainRemovedNextRound(bool strong)
        {
            var(p,c,source,material)=Fixture("advanced_card_020");
            var materials=(strong?new[]{"basic_card_021","basic_card_016"}:new[]{"basic_card_021","basic_card_016","basic_card_008"})
                .Select(id=>new DeedCard(id,CardType.Action)).ToArray();
            p.Deck.Hand.AddRange(materials);
            c.OverloadSelections.AddRange(materials.Select(card=>new RepeatedCardSelection { CardToRemove=card }));
            Play(p,c,source,strong);
            Assert.That(p.Deck.Hand,Is.Empty);
            var expected = strong?new[]{1,1,2,2}:new[]{2,2,0,2};
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
                Assert.That(p.Mana.Crystals.GetValueOrDefault(color),Is.EqualTo(expected[(int)color]));
            Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(materials.Length+1));
            Assert.That(p.Deck.RemovedCards[0],Is.SameAs(material));
            for(int i=0;i<materials.Length;i++)Assert.That(p.Deck.RemovedCards[i+1],Is.SameAs(materials[i]));
            var game=new GameEngine(new[]{p},5);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,game.Globals.Mana,c);game.EndRound();
            Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(source));
            Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(materials.Length+1));
            Assert.That(c.CardToRemove,Is.Null);Assert.That(c.OverloadSelections,Is.Null);
        }

        [Test]
        public void RepeatedMagicTalentDiscardsThreeRealCardsAndGrantsThreeOriginalColors()
        {
            var(p,c,source,material)=Fixture("advanced_card_025");
            var cards=new[]{"basic_card_021","basic_card_016","basic_card_000"}.Select(id=>new DeedCard(id,CardType.Action)).ToArray();
            p.Deck.Hand.AddRange(cards);
            c.OverloadSelections.AddRange(cards.Select(card=>new RepeatedCardSelection { CardToDiscard=card }));
            Play(p,c,source,false);
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(material));
            Assert.That(p.Deck.DiscardPile.Select(card=>card.Id),Is.EqualTo(cards.Reverse().Select(card=>card.Id)));
            Assert.That(c.SpellColors,Is.EquivalentTo(new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green}));
            Assert.That(c.CardToDiscard,Is.Null);Assert.That(c.CardToRemove,Is.Null);
            Assert.That(c.OverloadSelections,Is.Null);
        }

        [TestCase("repeat")] [TestCase("foreign")] [TestCase("source")] [TestCase("copied-material")]
        public void InvalidSecondMaterialRejectsAllBeforeHostManaOrAnyCardIsConsumed(string failure)
        {
            var(p,c,source,material)=Fixture("advanced_card_020");
            var first=new DeedCard("basic_card_021",CardType.Action);
            var second=new DeedCard("basic_card_016",CardType.Action);
            p.Deck.Hand.AddRange(new[]{first,second});
            var selected = failure switch { "repeat"=>first, "source"=>source, "copied-material"=>material, _=>new DeedCard(second.Id,second.Type) };
            c.OverloadSelections.AddRange(new[]{new RepeatedCardSelection { CardToRemove=first },new RepeatedCardSelection { CardToRemove=selected }});
            AssertUnchanged(p,c,()=>Play(p,c,source,true));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(3)]
        public void StrongRequiresExactlyTwoSelectionsBeforePayment(int count)
        {
            var(p,c,source,_)=Fixture("basic_card_027");
            c.OverloadSelections.AddRange(Enumerable.Range(0,count).Select(_=>new RepeatedCardSelection()));
            AssertUnchanged(p,c,()=>Play(p,c,source,true));
        }

        [Test]
        public void InvalidLastOriginalOptionDoesNotApplyEarlierValidChoice()
        {
            var(p,c,source,_)=Fixture("basic_card_027");
            c.OverloadSelections.AddRange(new[]{new RepeatedCardSelection { Option=0 },new RepeatedCardSelection { Option=4 }});
            AssertUnchanged(p,c,()=>Play(p,c,source,true));
            Assert.That(c.MovementPool,Is.Zero);Assert.That(c.InfluencePool,Is.Zero);
            Assert.That(c.MeleePool,Is.Zero);Assert.That(c.BlockPool,Is.Zero);
        }

        private static void AssertUnchanged(PlayerState p, ActionContext c, TestDelegate command)
        {
            var hand=p.Deck.Hand.ToArray();var material=c.CardToRemove;var choices=c.OverloadSelections;
            Assert.Throws<InvalidOperationException>(command);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(p.Mana.Crystals,Is.Empty);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(hand.Length));
            for(int i=0;i<hand.Length;i++)Assert.That(p.Deck.Hand[i],Is.SameAs(hand[i]));
            Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(c.CardToRemove,Is.SameAs(material));Assert.That(c.OverloadSelections,Is.SameAs(choices));
        }
    }
}
