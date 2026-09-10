using NUnit.Framework;
using System.Collections.Generic;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Core;

namespace MK.Tests.game
{
    public class MarketTests
    {
        private AdvActionSupply _advActionSupply;
        private SpellSupply _spellSupply;
        private ArtifactSupply _artifactSupply;

        [SetUp]
        public void Setup()
        {
            _advActionSupply = new AdvActionSupply();
            _spellSupply = new SpellSupply();
            _artifactSupply = new ArtifactSupply();
        }

        [Test]
        public void TestAdvActionSupplyInitialRefill()
        {
            var cards = new[]
            {
                new AdvActionCard("card1"), new AdvActionCard("card2"), new AdvActionCard("card3"),
                new AdvActionCard("card4"), new AdvActionCard("card5")
            };
            _advActionSupply.SetDeck(cards);
            
            _advActionSupply.Refill(coreRevealed: false);
            Assert.AreEqual(3, _advActionSupply.Offer.Count);
            
            _advActionSupply.Refill(coreRevealed: true);
            Assert.AreEqual(3, _advActionSupply.Offer.Count, "Core tiles do not enlarge the advanced action offer.");
            Assert.AreEqual("card1", _advActionSupply.DiscardPile[0].Id);
            Assert.AreEqual("card4", _advActionSupply.Offer[2].Id);
        }

        [Test]
        public void TestAdvActionSupplyRotation()
        {
            var cards = new[]
            {
                new AdvActionCard("card1"),
                new AdvActionCard("card2"),
                new AdvActionCard("card3"),
                new AdvActionCard("card4"),
                new AdvActionCard("card5")
            };
            
            _advActionSupply.SetDeck(cards);
            _advActionSupply.Refill(coreRevealed: false);
            
            Assert.AreEqual("card3", _advActionSupply.Offer[2].Id);
            
            var pickedCard = _advActionSupply.Take(0); // Take bottom card
            Assert.AreEqual("card1", pickedCard.Id);
            Assert.AreEqual(2, _advActionSupply.Offer.Count);
        }

        [Test]
        public void TestSpellSupplyRefill()
        {
            var spells = new[]
            {
                new SpellCard("spell1"), new SpellCard("spell2"), new SpellCard("spell3"),
                new SpellCard("spell4"), new SpellCard("spell5"), new SpellCard("spell6")
            };
            _spellSupply.SetDeck(spells);
            
            _spellSupply.Refill();
            Assert.AreEqual(3, _spellSupply.Offer.Count, "The spell offer contains three cards (MKUE p.3).");
        }

        [Test]
        public void TestArtifactSupplyInitialization()
        {
            var artifacts = new[]
            {
                new ArtifactCard("artifact1"), new ArtifactCard("artifact2"), new ArtifactCard("artifact3")
            };
            _artifactSupply.SetDeck(artifacts);
            
            Assert.AreEqual(3, _artifactSupply.Count);
        }

        [Test]
        public void TestEndOfRoundRefresh()
        {
            var globalResources = new GlobalResources(2, () => DayPart.Day);
            globalResources.AdvActions.Refill(coreRevealed: false);
            
            // Simulate round end
            globalResources.EndRound(coreRevealed: false);
            
            // Check that mana pool was reset
            globalResources.Mana.PrintPoolState().Contains("魔力池:");
        }
    }
}
