using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveMultiplayerMindInputTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PveJourneySession j,PlayerState other,DeedCard spell,DeedCard offered) Start(bool strong=false,bool noGreen=false)
        {
            var p=new PlayerState(1,"Caster");var other=new PlayerState(2,"Responder");
            var spell=new DeedCard("magic_018",CardType.Spell);p.Deck.Hand.Add(spell);p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));
            var offered=new DeedCard(noGreen?"basic_card_021":"basic_card_000",CardType.Action);other.Deck.Hand.Add(offered);
            other.Deck.Hand.Add(new DeedCard("basic_card_014",CardType.Action));p.Mana.AddCrystal(ManaColor.White);
            var originals=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(originals.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();for(int q=0;q<3;q++)map.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,new[]{TerrainType.Plains});
            var j=new PveJourneySession(p,cards,map:map,spells:originals,companions:new[]{other},supplyRandom:new Random(0));
            if(strong)j.Game.Clock.NextRound();j.SelectTactics(strong?"night_1":"day_1",strong?"night_2":"day_4");j.BeginTurn();
            if(strong)p.Mana.AddToken(ManaColor.Black);
            return(j,other,spell,offered);
        }
        private static PveDeedSelection Input(PlayerState other,DeedCard response,DeedCard steal=null)=>new(){Colors=new[]{ManaColor.Green},MindResponses=new[]{new PveMindReadResponse(other,response)},MindStealTarget=steal};
        [Test]public void BaseRespondsWithoutChangingTurnThenCrystalPaysMarchAndOtherTakesOwnTurn()
        {
            var(j,other,spell,offered)=Start();var caster=j.Player;var context=j.Context;
            j.PlayOutsideWithInputs(spell,"base",Input(other,offered));
            Assert.That(j.Game.CurrentPlayer,Is.SameAs(caster));Assert.That(j.Context,Is.SameAs(context));Assert.That(j.CompletedTurns,Is.Zero);
            Assert.That(other.Deck.DiscardPile.Single(),Is.SameAs(offered));Assert.That(caster.Mana.Crystals[ManaColor.White],Is.Zero);
            var march=caster.Deck.Hand.Single();j.PlayOutsideBattle(march,"strong");j.Move(new AxialCoord(1,0));j.Move(new AxialCoord(2,0));
            Assert.That(caster.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(j.Context.MovementPool,Is.Zero);
            j.EndTurn();j.BeginTurn();Assert.That(j.Player,Is.SameAs(other));Assert.That(j.CompletedTurns,Is.EqualTo(1));
            Assert.That(other.Deck.DiscardPile.Single(),Is.SameAs(offered));
        }
        [TestCase(false)][TestCase(true)]public void StrongOptionalTheftUsesSameCardAndRestoresTemporaryInputs(bool steal)
        {
            var(j,other,spell,offered)=Start(true);var caster=j.Player;
            var prior=other.Deck.Hand.Last();j.Context.MindReadChoices[other]=prior;j.Context.MindStealTarget=prior;
            j.PlayOutsideWithInputs(spell,"strong",Input(other,offered,steal?offered:null));
            Assert.That(j.Context.MindReadChoices[other],Is.SameAs(prior));Assert.That(j.Context.MindStealTarget,Is.SameAs(prior));
            Assert.That(caster.Mana.Tokens.Values.Sum(),Is.Zero);Assert.That(caster.Mana.Crystals[ManaColor.White],Is.Zero);
            Assert.That(other.Deck.Hand.Any(c=>ReferenceEquals(c,offered)),Is.False);
            Assert.That(other.Deck.DiscardPile.Any(c=>ReferenceEquals(c,offered)),Is.EqualTo(!steal));
            var actual=steal?offered:caster.Deck.Hand.Single();j.PlayOutsideBattle(actual,"strong");j.Move(new AxialCoord(1,0));
            Assert.That(caster.Deck.PlayedCards.Any(c=>ReferenceEquals(c,actual)),Is.True);Assert.That(caster.Mana.Crystals[ManaColor.Green],Is.Zero);
            j.EndTurn();Assert.That(other.Deck.Hand.Concat(other.Deck.DiscardPile).Any(c=>ReferenceEquals(c,offered)),Is.EqualTo(!steal));
        }
        [Test]public void ExplicitNoMatchingCardKeepsHandAndPublishesOriginalSnapshotUntilTurnEnd()
        {
            var(j,other,spell,offered)=Start(noGreen:true);var hand=other.Deck.Hand.ToArray();
            j.PlayOutsideWithInputs(spell,"base",Input(other,null));
            Assert.That(j.Context.MindReadRevealedHands[other],Is.EqualTo(hand));Assert.That(other.Deck.Hand,Is.EqualTo(hand));
            Assert.That(j.Context.MindReadChoices,Is.Empty);j.EndTurn();Assert.That(j.Context.MindReadRevealedHands,Is.Empty);
        }
        [TestCase("missing")][TestCase("duplicate")][TestCase("foreign")][TestCase("same_id")][TestCase("wrong_color")][TestCase("false_reveal")][TestCase("no_fee")]
        public void InvalidResponseOrFeePreservesBothActualHandsAndRestoresPriorInput(string failure)
        {
            var(j,other,spell,offered)=Start();var p=j.Player;var old=other.Deck.Hand.Last();
            j.Context.MindReadChoices[other]=old;j.Context.MindStealTarget=old;
            var responses=failure=="missing"?Array.Empty<PveMindReadResponse>():failure=="duplicate"?new[]{new PveMindReadResponse(other,offered),new PveMindReadResponse(other,offered)}:
                new[]{new PveMindReadResponse(failure=="foreign"?new PlayerState(2,"Imposter"):other,
                    failure=="same_id"?new DeedCard(offered.Id,offered.Type):failure=="wrong_color"?old:failure=="false_reveal"?null:offered)};
            if(failure=="no_fee")p.Mana.Crystals.Clear();var own=p.Deck.Hand.ToArray();var theirs=other.Deck.Hand.ToArray();int mana=p.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(spell,"base",new PveDeedSelection{Colors=new[]{ManaColor.Green},MindResponses=responses}));
            Assert.That(p.Deck.Hand,Is.EqualTo(own));Assert.That(other.Deck.Hand,Is.EqualTo(theirs));Assert.That(p.Mana.GetTotal(),Is.EqualTo(mana));
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(other.Deck.DiscardPile,Is.Empty);Assert.That(j.Context.MindReadRevealedHands,Is.Empty);
            Assert.That(j.Context.MindReadChoices[other],Is.SameAs(old));Assert.That(j.Context.MindStealTarget,Is.SameAs(old));Assert.That(j.Game.CurrentPlayer,Is.SameAs(p));
        }
    }
}
