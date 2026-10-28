using System;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MageKnight.Tests
{
    public sealed class MistFormResolutionTests
    {
        [SetUp] public void SetUp() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static void Hit(PlayerState player, ActionContext context, int attack, Element element,
            UnitState[] units, params Ability[] traits)
            => BattleResolver.Resolve(player, new[] { new Monster("mist_damage_fixture", 5, attack, element, 0, traits) },
                Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: context,
                damageAssignments: new[] { new UnitDamageAllocation(0, units) });

        [TestCase(Element.Physical)] [TestCase(Element.Fire)] [TestCase(Element.Ice)] [TestCase(Element.ColdFire)]
        public void AllResistanceAbsorbsOneArmorWithoutConsumingHeroProtection(Element element)
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); player.Units.Add(unit);
            new MistFormEffect(true).Execute(player, ctx);
            Hit(player, ctx, 3, element, new[] { unit }, Ability.Poison, Ability.Paralyze);
            Assert.That(unit.Wounds, Is.Zero); Assert.That(unit.IsDestroyed, Is.False);
            Assert.That(player.Wounds, Is.Zero); Assert.That(ctx.IgnoreNextWound, Is.True);
            Hit(player, ctx, 2, element, Array.Empty<UnitState>(), Ability.Poison, Ability.Paralyze);
            Assert.That(player.Wounds, Is.Zero); Assert.That(player.DiscardWounds, Is.Zero);
            Assert.That(ctx.IgnoreNextWound, Is.False);
        }

        [TestCase(Ability.Poison)] [TestCase(Ability.Paralyze)]
        public void IgnoredFirstWoundDoesNotSuppressLaterWoundEffects(Ability trait)
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            player.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            new MistFormEffect(true).Execute(player, ctx);
            Hit(player, ctx, 2, Element.Physical, Array.Empty<UnitState>(), trait);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(1));
            Hit(player, ctx, 2, Element.Physical, Array.Empty<UnitState>(), trait);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.DiscardWounds, Is.EqualTo(trait == Ability.Poison ? 1 : 0));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(trait == Ability.Poison ? 2 : 1));
        }

        [Test]
        public void TurnCleanupExpiresUnspentProtectionEvenWhenDrawingIsSkipped()
        {
            var player = new PlayerState(); var ctx = new ActionContext { SkipDraw = true };
            new MistFormEffect(true).Execute(player, ctx);
            UnitDamageResolver.EndBattle(player, ctx);
            Assert.That(ctx.UnitsResistAll && ctx.IgnoreNextWound, Is.True);
            new PlayerTurnEngine().EndTurn(player, DayPart.Night, null, ctx);
            Assert.That(ctx.UnitsResistAll || ctx.IgnoreNextWound, Is.False);
            Hit(player, ctx, 2, Element.Fire, Array.Empty<UnitState>());
            Assert.That(player.Wounds, Is.EqualTo(1));
        }

        [TestCase(TerrainType.Lake, int.MaxValue)] [TestCase(TerrainType.Hills, 3)]
        [TestCase(TerrainType.Mountain, int.MaxValue)] [TestCase(TerrainType.Forest, 5)]
        public void MovementOverridesAndBansExpireAtTurnEnd(TerrainType terrain, int originalCost)
        {
            var player = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night, SkipDraw = true, MovementMap = new MapState() };
            var target = new AxialCoord(1, 0);
            ctx.MovementMap.Placed[target] = new MapTile(TileSet.Countryside, 1, new[] { terrain });
            new MistFormEffect(false).Execute(player, ctx);
            Assert.That(MovementService.GetCost(target, ctx.MovementMap, ctx.DayPart, ctx),
                Is.EqualTo(terrain == TerrainType.Hills || terrain == TerrainType.Mountain ? int.MaxValue : 2));
            new PlayerTurnEngine().EndTurn(player, ctx.DayPart, null, ctx);
            Assert.That(ctx.TerrainCostOverride, Is.Empty); Assert.That(ctx.MoveForbidden, Is.Empty);
            Assert.That(MovementService.GetCost(target, ctx.MovementMap, ctx.DayPart, ctx), Is.EqualTo(originalCost));
        }

        [Test]
        public void InvalidOptionDoesNotApplyAnyHalfOfEffect()
        {
            var ctx = new ActionContext();
            Assert.Throws<InvalidOperationException>(() => new MistFormEffect(true).Execute(new PlayerState(), ctx, 1));
            Assert.That(ctx.UnitsResistAll || ctx.IgnoreNextWound, Is.False);
        }
    }
}
