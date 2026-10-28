using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class MagicFamiliarsStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static UnitCard Original => CardJsonLoader.LoadUnit("army_2_004");
        private static TacticCard[] Order(DayPart part = DayPart.Day) => new[] { new TacticCard("initiative_fixture", part, 1, 0) };
        private static UnitState Recruit(PlayerState p, ManaColor color)
        {
            var c = new ActionContext { InfluencePool = 6 };
            p.Mana.AddToken(color);
            Assert.That(CardUnitActions.TryRecruit(p, c, new List<UnitCard> { Original }, 0, RecruitLocation.MagicalGlade, color), Is.True);
            Assert.That(c.InfluencePool, Is.Zero);
            return p.Units.Last();
        }

        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)] [TestCase(ManaColor.White)]
        public void OriginalRecruitPaysManaOnceAndKeepsMarkerSeparateFromPlayerResources(ManaColor color)
        {
            var p = new PlayerState(); var original = Original;
            var c = new ActionContext { InfluencePool = 6 };
            var offer = new List<UnitCard> { original };
            p.Mana.AddCrystal(color); p.Mana.AddToken(color);
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Monastery, color), Is.True);
            var unit = p.Units.Single();
            Assert.That(unit.Card, Is.SameAs(original)); Assert.That(unit.Card.Level, Is.EqualTo(2));
            Assert.That(unit.Card.Armor, Is.EqualTo(5)); Assert.That(unit.Card.InfluenceCost, Is.EqualTo(6));
            Assert.That(unit.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.MagicalGlade | RecruitLocation.Monastery | RecruitLocation.MageTower));
            Assert.That(unit.Card.Actions.Length, Is.EqualTo(4)); Assert.That(unit.Card.Actions.SelectMany(a => a.Cost), Is.Empty);
            Assert.That(unit.AttunementColor, Is.EqualTo(color)); Assert.That(unit.CanActivate, Is.True);
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(p.Mana.Crystals[color], Is.EqualTo(1));
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(offer, Is.Empty);
            Assert.That(c.InNegotiation, Is.True);
            Assert.Throws<InvalidOperationException>(() => new GameEngine(new[] { p }, 3).ResolveFamiliarUpkeep(p, unit, color));
        }

        [TestCase(-7)] [TestCase(7)]
        public void GladeRecruitIsAnActionWithoutInteractionOrReputationAdjustment(int reputation)
        {
            var p = new PlayerState { Reputation = reputation, ExtraCommandSlots = 1 };
            var hero = new UnitState(CardJsonLoader.LoadUnit("army_008")); p.Units.Add(hero);
            var c = new ActionContext { InfluencePool = 1 };
            CardUnitActions.Use(p, c, hero, 2); // Printed +1 reputation only if used during interaction.
            Assert.That(p.Reputation, Is.EqualTo(reputation)); Assert.That(c.InfluencePool, Is.EqualTo(6));
            var offer = new List<UnitCard> { Original };
            p.Mana.AddCrystal(ManaColor.White);
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.MagicalGlade, ManaColor.White), Is.True);
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(p.Mana.Crystals[ManaColor.White], Is.Zero);
            Assert.That(c.SpentCrystals[ManaColor.White], Is.EqualTo(1));
            Assert.That(c.InNegotiation, Is.False); Assert.That(c.InteractionReputationModifier, Is.Null);
            Assert.That(c.TurnActionCommitted, Is.True);
            Assert.Throws<InvalidOperationException>(() => RecruitmentService.BeginInteraction(p, c, RecruitLocation.Monastery));
            c.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.FamiliarRecruitmentAtGlade, Is.False); Assert.That(c.TurnActionCommitted, Is.False);
        }

        [TestCase("mana")] [TestCase("gold")] [TestCase("slots")] [TestCase("influence")]
        [TestCase("location")] [TestCase("committed")] [TestCase("battle")]
        public void FailedRecruitPreservesOfferResourcesAndActionState(string failure)
        {
            var p = new PlayerState { ExtraCommandSlots = failure == "slots" ? -1 : 0 };
            if (failure != "mana") p.Mana.AddToken(ManaColor.Red);
            p.Mana.AddToken(ManaColor.Gold);
            var c = new ActionContext { InfluencePool = failure == "influence" ? 5 : 6,
                TurnActionCommitted = failure == "committed", InBattle = failure == "battle" };
            var original = Original; var offer = new List<UnitCard> { original };
            int funds = c.InfluencePool, tokens = p.Mana.Tokens.Values.Sum(); bool committed = c.TurnActionCommitted;
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0,
                failure == "location" ? RecruitLocation.Village : RecruitLocation.MagicalGlade,
                failure == "gold" ? ManaColor.Gold : ManaColor.Red), Is.False);
            Assert.That(offer.Single(), Is.SameAs(original)); Assert.That(p.Units, Is.Empty);
            Assert.That(c.InfluencePool, Is.EqualTo(funds)); Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(tokens));
            Assert.That(c.TurnActionCommitted, Is.EqualTo(committed)); Assert.That(c.HasRecruited, Is.False);
        }

        [Test] public void FamiliarCannotBeAcquiredForFreeOrBorrowedFromOffer()
        {
            var p = new PlayerState(); var original = Original;
            var c = new ActionContext { UnitOffer = new List<UnitCard> { original } };
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.RecruitFree(p, c, 0));
            new BattleSummonsEffect(false).Execute(p, c);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.UseFromOffer(p, c, 0, 0));
            Assert.That(c.OfferedUnitUses, Is.EqualTo(1)); Assert.That(c.UnitOffer.Single(), Is.SameAs(original));
            Assert.That(p.Units, Is.Empty);
        }

        [TestCase(ManaColor.Red, 5)] [TestCase(ManaColor.White, 3)]
        public void AttackUsesPaidMarkerAndActualEnemyConsumerWithoutAnotherManaCost(ManaColor color, int power)
        {
            var p = new PlayerState(); var unit = Recruit(p, color);
            var s = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            s.Advance(); s.Advance(); s.Advance();
            s.UseUnit(unit, 0);
            Assert.That(s.AttackTotal(AttackType.Melee), Is.EqualTo(power)); Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            Assert.That(unit.IsReady, Is.False); Assert.That(unit.AttunementColor, Is.EqualTo(color));
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 0));
        }

        [TestCase(ManaColor.Blue, 7)] [TestCase(ManaColor.Red, 4)]
        public void BlockMarkerFeedsFormalBlockAndPreventsActualWounds(ManaColor color, int power)
        {
            var p = new PlayerState(); var unit = Recruit(p, color);
            var s = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            s.Advance(); s.UseUnit(unit, 1);
            Assert.That(s.Context.BlockPool, Is.EqualTo(power)); s.Block(0); s.Advance(); s.Advance();
            Assert.That(p.Wounds, Is.Zero); Assert.That(unit.Wounds, Is.Zero); Assert.That(unit.IsReady, Is.False);
        }

        [TestCase(0)] [TestCase(1)]
        public void WhiteAbilityChoosesMovementOrInfluenceAndSpendsItOnRealConsumer(int option)
        {
            var p = new PlayerState { ExtraCommandSlots = 1 }; var unit = Recruit(p, ManaColor.White);
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState() };
            c.MovementMap.Placed[new AxialCoord(0, 0)] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            c.MovementMap.Placed[new AxialCoord(1, 0)] = new MapTile(TileSet.Countryside, 1, new[] { TerrainType.Forest });
            CardUnitActions.Use(p, c, unit, 2, option);
            Assert.That(c.MovementPool, Is.EqualTo(option == 0 ? 5 : 0));
            Assert.That(c.InfluencePool, Is.EqualTo(option == 1 ? 5 : 0));
            if (option == 0)
            {
                Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.True);
                Assert.That(c.MovementPool, Is.EqualTo(2)); Assert.That(p.Position, Is.EqualTo(new AxialCoord(1, 0)));
            }
            else
            {
                var offer = new List<UnitCard> { CardJsonLoader.LoadUnit("army_2_000") };
                Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
                Assert.That(offer, Is.Empty); Assert.That(c.InfluencePool, Is.Zero); Assert.That(p.Units.Count, Is.EqualTo(2));
            }
            Assert.That(unit.IsReady, Is.False);
        }

        [TestCase(ManaColor.Green, 3)] [TestCase(ManaColor.Blue, 2)]
        public void HealingPaysForRealWoundCardsAndDoesNotHealTheUnitByReadying(ManaColor color, int healing)
        {
            var p = new PlayerState(); var unit = Recruit(p, color); var c = new ActionContext();
            p.Deck.GainWound(); p.Deck.GainWound(); p.Deck.GainWound();
            CardUnitActions.Use(p, c, unit, 3); Assert.That(c.Healing.Total, Is.EqualTo(healing));
            for (int i = 0; i < healing; i++) CardHealing.Spend(p, c);
            Assert.That(p.Deck.Hand.Count(x => x.Type == CardType.Wound), Is.EqualTo(3 - healing));
            Assert.That(c.Healing.Total, Is.Zero); Assert.That(unit.IsReady, Is.False);
        }

        [Test] public void NextRoundRequiresInventoryCrystalBeforeTacticsAndPreservesSameWoundedUnit()
        {
            var p = new PlayerState(); p.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            var game = new GameEngine(new[] { p }, 3); game.StartNewRound(Order());
            var c = new ActionContext(); game.BeginNextTurn(c);
            p.Mana.AddToken(ManaColor.Red); c.InfluencePool = 6;
            Assert.That(CardUnitActions.TryRecruit(p, c, new List<UnitCard> { Original }, 0, RecruitLocation.MagicalGlade, ManaColor.Red), Is.True);
            var unit = p.Units.Single(); unit.Exhaust(); unit.AddWounds(1); var originalHand = p.Deck.Hand.Single();
            game.EndCurrentTurn(c,new[]{originalHand}); game.AnnounceRoundEnd(); game.EndRound();
            p.Mana.AddToken(ManaColor.Blue); // Tokens cannot replace the explicitly required inventory crystal.
            game.PrepareNewRound();
            Assert.That(unit.IsReady, Is.True); Assert.That(unit.Wounds, Is.EqualTo(1));
            Assert.That(unit.NeedsAttunementUpkeep, Is.True); Assert.That(unit.CanActivate, Is.False);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(originalHand));
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(Order(DayPart.Night)));
            Assert.Throws<InvalidOperationException>(() => game.BeginNextTurn(new ActionContext()));
            Assert.Throws<InvalidOperationException>(() => game.ResolveFamiliarUpkeep(p, unit, ManaColor.Blue));
            Assert.That(unit.AttunementColor, Is.EqualTo(ManaColor.Red)); Assert.That(unit.NeedsAttunementUpkeep, Is.True);
            p.Mana.AddCrystal(ManaColor.Blue);
            game.ResolveFamiliarUpkeep(p, unit, ManaColor.Blue);
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.Zero); Assert.That(p.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(p.Units.Single(), Is.SameAs(unit)); Assert.That(unit.AttunementColor, Is.EqualTo(ManaColor.Blue));
            Assert.That(unit.Wounds, Is.EqualTo(1)); Assert.That(unit.NeedsAttunementUpkeep, Is.False);
            Assert.Throws<InvalidOperationException>(() => game.ResolveFamiliarUpkeep(p, unit, null));
            Assert.Throws<InvalidOperationException>(() => game.PrepareNewRound());
            game.StartNewRound(Order(DayPart.Night)); game.BeginNextTurn(new ActionContext());
            Assert.That(game.CurrentPlayer, Is.SameAs(p)); Assert.That(unit.CanActivate, Is.False, "维持和新轮重整均不移除创伤");
        }

        [Test] public void DecliningUpkeepRemovesOnlySelectedUnitAndDoesNotCreateMana()
        {
            var p = new PlayerState { ExtraCommandSlots = 1 };
            var first = Recruit(p, ManaColor.Red); var second = Recruit(p, ManaColor.Green);
            var game = new GameEngine(new[] { p }, 3); game.PrepareNewRound();
            p.Mana.AddCrystal(ManaColor.White);
            game.ResolveFamiliarUpkeep(p, first, null);
            Assert.That(first.IsDestroyed, Is.True); Assert.That(p.Units.Single(), Is.SameAs(second));
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(p.Mana.Crystals[ManaColor.White], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(Order()));
            game.ResolveFamiliarUpkeep(p, second, ManaColor.White); game.StartNewRound(Order());
            Assert.That(second.AttunementColor, Is.EqualTo(ManaColor.White)); Assert.That(p.Mana.Crystals[ManaColor.White], Is.Zero);
            Assert.Throws<InvalidOperationException>(() => game.ResolveFamiliarUpkeep(p, second, null));
        }
    }
}
