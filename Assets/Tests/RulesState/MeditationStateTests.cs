using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class MeditationStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private sealed class LastRandom : Random { public override int Next(int maxValue) => maxValue - 1; }
        private static DeedCard Card(string id) => new(id, id == "w" ? CardType.Wound : CardType.Action);
        private static (PlayerState p, ActionContext c, DeedCard spell) Start(int discards = 3)
        {
            var p = new PlayerState(); var c = new ActionContext { DayPart = DayPart.Night, EffectRandom = new LastRandom() };
            p.Deck.SetDeck(new[] { Card("basic_card_000"), Card("basic_card_014"), Card("basic_card_021") });
            foreach (var id in new[] { "basic_card_008", "w", "basic_card_016" }.Take(discards)) p.Deck.GainToDiscard(Card(id));
            var spell = new DeedCard("magic_002", CardType.Spell); p.Deck.Hand.Add(spell);
            p.Mana.AddToken(ManaColor.Green, 2); p.Mana.AddToken(ManaColor.Black, 2);
            return (p, c, spell);
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard spell, bool strong, int option = 0)
        {
            var s = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_002");
            CardPlayActions.Play(p, c, spell, new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost), strong, option);
        }
        private static void Exact(DeedCard[] expected, System.Collections.Generic.IReadOnlyList<DeedCard> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(actual[i], Is.SameAs(expected[i]));
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(true,0)] [TestCase(true,1)]
        public void ActualDiscardInstancesPreserveOriginalDeckAndNextRefill(bool strong, int option)
        {
            var (p,c,spell)=Start(); var old=p.Deck.DrawPile.ToArray(); var disc=p.Deck.DiscardPile.ToArray();
            var picked=new[]{disc[2],disc[1]}; c.CardsFromDiscard.AddRange(picked);
            Play(p,c,spell,strong,option);
            Exact(option==0?picked.Concat(old).ToArray():old.Concat(picked).ToArray(),p.Deck.DrawPile);
            Exact(new[]{disc[0]},p.Deck.DiscardPile); Assert.That(c.CardsFromDiscard,Is.Empty);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(spell)); Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(strong?1:2)); Assert.That(c.NextDrawBonus,Is.EqualTo(2));
            // Add enough supply to distinguish a +2 refill from the normal five.
            for(int i=0;i<5;i++) p.Deck.PutUnder(Card("basic_card_000"));
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(7)); Assert.That(c.NextDrawBonus,Is.Zero);
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,spell)),Is.True);
            foreach(var card in p.Deck.Hand.ToArray()) p.Deck.TryDiscardExact(card);
            for(int i=0;i<5;i++) p.Deck.PutUnder(Card("basic_card_014"));
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(5)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(3));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidChosenMaterialIsRejectedBeforeManaAndDeckMutation(int failure)
        {
            var(p,c,spell)=Start(); var d=p.Deck.DiscardPile.ToArray(); var old=p.Deck.DrawPile.ToArray();
            c.CardsFromDiscard.AddRange(failure switch {
                0 => new[]{new DeedCard(d[0].Id,d[0].Type),d[1]},
                1 => new[]{d[0],d[0]}, 2 => new[]{d[0]}, 3 => d, _ => new[]{spell,d[1]} });
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,spell,true));
            Exact(d,p.Deck.DiscardPile); Exact(old,p.Deck.DrawPile); Assert.That(p.Deck.Hand.Single(),Is.SameAs(spell));
            Assert.That(p.Deck.PlayedCards,Is.Empty); Assert.That(c.NextDrawBonus,Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));
        }

        [TestCase(false,-1)] [TestCase(false,2)] [TestCase(true,-1)] [TestCase(true,2)]
        public void InvalidDestinationPreservesResources(bool strong,int option)
        {
            var(p,c,s)=Start(); c.CardsFromDiscard.AddRange(p.Deck.DiscardPile.Take(2));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,s,strong,option));
            Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(3)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));
            Assert.That(c.NextDrawBonus,Is.Zero); Assert.That(p.Deck.Hand.Single(),Is.SameAs(s));
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(true,0)] [TestCase(true,1)]
        public void ShortDiscardPileStillGrantsOneNextRefillBonus(bool strong,int count)
        {
            var(p,c,s)=Start(count); c.CardsFromDiscard.AddRange(p.Deck.DiscardPile);
            Play(p,c,s,strong,1); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(3+count)); Assert.That(p.Deck.DiscardPile,Is.Empty);
            c.SkipDraw=true; new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(c.NextDrawBonus,Is.EqualTo(2)); Assert.That(p.Deck.Hand,Is.Empty);
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(3+count)); Assert.That(c.NextDrawBonus,Is.Zero);
        }

        [Test]
        public void EqualIdDiscardInstancesDoNotSubstituteOneAnother()
        {
            var(p,c,s)=Start(0); var first=Card("basic_card_008");var second=Card("basic_card_008");var third=Card("basic_card_016");
            p.Deck.GainToDiscard(first);p.Deck.GainToDiscard(second);p.Deck.GainToDiscard(third);
            c.CardsFromDiscard.AddRange(new[]{first,third});Play(p,c,s,true);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(second)); Exact(new[]{first,third},p.Deck.DrawPile.Take(2).ToArray());
        }

        [TestCase(false)] [TestCase(true)]
        public void BottomInsertionKeepsExistingOrderForSharedConsumers(bool necklace)
        {
            var(p,c,s)=Start(1);var old=p.Deck.DrawPile.ToArray();var recycled=p.Deck.DiscardPile.Single();
            if(necklace)new MK.Logic.Runtime.CardEffects.RevivalNecklaceEffect().Execute(p,c);
            else { c.CardToRecycle=s;c.RecycleToBottom=true;c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Night,null,c);recycled=s; }
            Exact(old.Append(recycled).ToArray(),p.Deck.DrawPile);
        }

        [TestCase(false,0)] [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        public void PrintedManaAndCrystalCostsAreAtomic(bool strong,int mode)
        {
            var(p,c,s)=Start();c.CardsFromDiscard.AddRange(p.Deck.DiscardPile.Take(2));
            p.Mana.Tokens[ManaColor.Green]=0;
            if(mode==1 || mode==3)p.Mana.AddCrystal(ManaColor.Green);
            if(mode==1)p.Mana.Tokens[ManaColor.Black]=0;
            if(mode==2){p.Mana.Tokens[ManaColor.Green]=2;c.DayPart=DayPart.Day;}
            if(mode==3){Play(p,c,s,strong);Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(c.NextDrawBonus,Is.EqualTo(2));}
            else
            {
                Assert.Throws<InvalidOperationException>(()=>Play(p,c,s,strong));
                Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(3));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(3));
                Assert.That(p.Deck.Hand.Single(),Is.SameAs(s));Assert.That(c.NextDrawBonus,Is.Zero);
                Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(mode==2?2:0));
                Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(mode==1?1:0));
            }
        }
    }
}
