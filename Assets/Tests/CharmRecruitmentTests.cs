using System;
using System.Collections.Generic;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MageKnight.Tests
{
    public sealed class CharmRecruitmentTests
    {
        [SetUp] public void SetUp() => CardJsonLoader.SetBaseDirectory(Path.Combine(Application.dataPath, "../resources/text_json"));

        private static void Play(PlayerState p, ActionContext ctx, int option)
        {
            var card = AssetDatabase.LoadAssetAtPath<SpellCardSO>("Assets/GameData/CardsAssets/magic_021.asset");
            new ActionSystem().Play(new ActionCardData(card.Id, card.NameCn, card.Set, card.ImagePath, card.EnImagePath,
                card.Top.Effect, card.Bottom.Effect, card.Top.ManaCost), p, ctx, false, option);
        }

        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)] [TestCase(ManaColor.White)]
        public void NegotiationPaysWhiteAndAwardsChosenCrystal(ManaColor color)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White);
            var ctx = new ActionContext { InNegotiation = true }; Play(p, ctx, (int)color);
            Assert.That(ctx.InfluencePool, Is.EqualTo(4)); Assert.That(p.Mana.Crystals[color], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(ctx.CharmRecruitDiscount, Is.Zero);
        }

        [TestCase(-1)] [TestCase(4)] [TestCase(5)] [TestCase(257)] [TestCase(512)] [TestCase(65536)]
        public void InvalidChoiceRejectsBeforeCostInfluenceAndRewards(int option)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White);
            var ctx = new ActionContext { InNegotiation = true };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, option));
            Assert.That(ctx.InfluencePool, Is.Zero); Assert.That(ctx.CharmRecruitDiscount, Is.Zero);
            Assert.That(p.Mana.Crystals, Is.Empty); Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(1));
        }

        [Test]
        public void CannotPaySpellWithItsFutureRewardButExistingWhiteCrystalCanBeReplaced()
        {
            var p = new PlayerState(); var ctx = new ActionContext { InNegotiation = true };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, 3));
            Assert.That(ctx.InfluencePool, Is.Zero); Assert.That(p.Mana.Crystals, Is.Empty);
            p.Mana.AddCrystal(ManaColor.White); Play(p, ctx, 3);
            Assert.That(p.Mana.Crystals[ ManaColor.White ], Is.EqualTo(1)); Assert.That(ctx.SpentCrystals[ ManaColor.White ], Is.EqualTo(1));
        }

        [Test]
        public void DiscountAppliesToOnlyOneSuccessfulRecruitmentAndConsumesRealOffer()
        {
            var p = new PlayerState { ExtraCommandSlots = 1 }; p.Mana.AddToken(ManaColor.White);
            var ctx = new ActionContext { InNegotiation = true, InfluencePool = 2 }; Play(p, ctx, 256);
            var farmer = CardJsonLoader.LoadUnit("army_2_006"); var offer = new List<UnitCard> { farmer, farmer };
            Assert.That(RecruitmentService.GetCost(p, farmer, RecruitLocation.Village, ctx), Is.EqualTo(1));
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(ctx.InfluencePool, Is.EqualTo(5)); Assert.That(ctx.CharmRecruitDiscount, Is.Zero);
            Assert.That(RecruitmentService.GetCost(p, farmer, RecruitLocation.Village, ctx), Is.EqualTo(4));
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(ctx.InfluencePool, Is.EqualTo(1)); Assert.That(offer, Is.Empty); Assert.That(p.Units.Count, Is.EqualTo(2));
            Assert.That(p.Units[0].CanActivate && p.Units[1].CanActivate, Is.True);
        }

        [Test]
        public void FailedLocationPreservesDiscountAndResourcesForAnotherCandidate()
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White);
            var ctx = new ActionContext { InNegotiation = true }; Play(p, ctx, 256);
            var offer = new List<UnitCard> { CardJsonLoader.LoadUnit("army_2_005"), CardJsonLoader.LoadUnit("army_2_006") };
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 0, RecruitLocation.Village), Is.False);
            Assert.That(ctx.CharmRecruitDiscount, Is.EqualTo(3)); Assert.That(ctx.InfluencePool, Is.EqualTo(4));
            Assert.That(offer.Count, Is.EqualTo(2)); Assert.That(p.Units, Is.Empty);
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 1, RecruitLocation.Village), Is.True);
            Assert.That(ctx.InfluencePool, Is.EqualTo(3)); Assert.That(offer[0].Id, Is.EqualTo("army_2_005"));
        }

        [Test]
        public void SingleRecruitDiscountDoesNotEraseOtherDiscountOrCreateNegativeCost()
        {
            var p = new PlayerState { ExtraCommandSlots = 1 }; p.Mana.AddToken(ManaColor.White);
            var ctx = new ActionContext { InNegotiation = true, RecruitDiscount = 2 }; Play(p, ctx, 256);
            var farmer = CardJsonLoader.LoadUnit("army_2_006"); var offer = new List<UnitCard> { farmer, farmer };
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(ctx.InfluencePool, Is.EqualTo(4)); Assert.That(ctx.RecruitDiscount, Is.EqualTo(2));
            Assert.That(ctx.CharmRecruitDiscount, Is.Zero);
            Assert.That(CardUnitActions.TryRecruit(p, ctx, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(ctx.InfluencePool, Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(256)]
        public void OutsideNegotiationOnlyGrantsInfluence(int option)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White); var ctx = new ActionContext(); Play(p, ctx, option);
            Assert.That(ctx.InfluencePool, Is.EqualTo(4)); Assert.That(p.Mana.Crystals, Is.Empty); Assert.That(ctx.CharmRecruitDiscount, Is.Zero);
        }

        [Test]
        public void UnusedDiscountExpiresAtEndTurnWithoutRemovingCrystals()
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White); p.Mana.AddCrystal(ManaColor.Blue);
            var ctx = new ActionContext { InNegotiation = true, SkipDraw = true }; Play(p, ctx, 256);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, ctx);
            Assert.That(ctx.CharmRecruitDiscount, Is.Zero); Assert.That(p.Mana.Crystals[ ManaColor.Blue ], Is.EqualTo(1));
        }
    }
}
