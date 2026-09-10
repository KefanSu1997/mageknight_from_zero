using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MK.Tests.Cards
{
    public sealed class OriginalBasicCardRulesTests
    {
        [SetUp]
        public void SetSource() => CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(Application.dataPath, "../resources/text_json"));

        [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)]
        [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)]
        public void InstinctUsesTheOriginalCostAndExactlyOnePool(bool enhanced, int option)
        {
            var player = new PlayerState(); player.Mana.AddToken(ManaColor.Red, 2);
            var context = new ActionContext();
            var card = CardJsonLoader.LoadBasicActions().Single(c => c.Id == "basic_card_027");
            new ActionSystem().Play(card, player, context, enhanced, option);
            var values = new[] { context.MovementPool, context.InfluencePool, context.MeleePool, context.BlockPool };
            Assert.That(values[option], Is.EqualTo(enhanced ? 4 : 2));
            Assert.That(values.Sum(), Is.EqualTo(values[option]));
            Assert.That(player.Mana.Tokens[ManaColor.Red], Is.EqualTo(enhanced ? 1 : 2));
        }

        [Test]
        public void InstinctAssetContainsTheOriginalPrintedFaceAndCost()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ActionCardSO>("Assets/GameData/CardsAssets/basic_card_027.asset");
            Assert.That(asset.NameCn, Is.EqualTo("本能"));
            Assert.That(asset.BaseEffect, Is.EqualTo("移动力2，影响力2，攻击2或格挡2。"));
            Assert.That(asset.EnhancedEffect, Is.EqualTo("移动力4，影响力4，攻击4或格挡4。"));
            Assert.That(asset.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Red }));
        }

        [TestCase("basic_card_027", -1, ManaColor.Red)]
        [TestCase("basic_card_027", 4, ManaColor.Red)]
        [TestCase("basic_card_004", 3, ManaColor.Green)]
        [TestCase("basic_card_020", 1, ManaColor.Blue)]
        public void InvalidOptionsRejectBeforePaying(string id, int option, ManaColor color)
        {
            var player = new PlayerState(); player.Mana.AddToken(color, 2);
            var context = new ActionContext();
            var card = CardJsonLoader.LoadBasicActions().Single(c => c.Id == id);
            Assert.Throws<InvalidOperationException>(() => new ActionSystem().Play(card, player, context, true, option));
            Assert.That(player.Mana.Tokens[color], Is.EqualTo(2));
            Assert.That(context.MovementPool + context.BlockPool + context.MeleePool + context.InfluencePool, Is.Zero);
        }

        [TestCase(TerrainType.Forest, DayPart.Day, 3, Element.Fire)]
        [TestCase(TerrainType.Forest, DayPart.Night, 5, Element.Ice)]
        [TestCase(TerrainType.Desert, DayPart.Day, 5, Element.Fire)]
        [TestCase(TerrainType.Desert, DayPart.Night, 3, Element.Ice)]
        [TestCase(TerrainType.Mountain, DayPart.Night, 5, Element.Ice)]
        [TestCase(TerrainType.Lake, DayPart.Day, 2, Element.Fire)]
        public void EarthBlockUsesUnmodifiedCostForTheCurrentTime(TerrainType terrain, DayPart time, int value, Element element)
        {
            var context = new ActionContext { CurrentTerrain = terrain, DayPart = time };
            context.TerrainCostOverride[terrain] = 2;
            new EarthStrengthEffect(4, 2, true).Execute(new PlayerState(), context, 1);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(value));
            Assert.That(context.CombatPower.Blocks.Single().Element, Is.EqualTo(element));
        }

        [Test]
        public void CrystalGainFillsInventoryThenConvertsExcessIntoMana()
        {
            var pool = new ManaPool();
            pool.AddCrystal(ManaColor.Green, 2);
            pool.AddCrystal(ManaColor.Green, 3);
            Assert.That(pool.Crystals[ManaColor.Green], Is.EqualTo(3));
            Assert.That(pool.Tokens[ManaColor.Green], Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => pool.AddCrystal(ManaColor.Black));
            Assert.That(pool.Crystals.ContainsKey(ManaColor.Black), Is.False);
        }

        [Test]
        public void ColdToughnessFollowsTheEnemyActuallyBlockedWithoutDoubleCounting()
        {
            var player = new PlayerState();
            var context = new ActionContext();
            new IcyShellEffect(true).Execute(player, context);
            Assert.That(context.BlockPool, Is.EqualTo(5));
            var enemy = new Monster("actual", 5, 3, Element.Fire, 2, new[] { Ability.Swift });
            var result = CardCombatActions.Block(player, context, enemy);
            Assert.That(result.PrintedPower, Is.EqualTo(7));
            Assert.That(result.EffectivePower, Is.EqualTo(7));
            Assert.That(result.RequiredPower, Is.EqualTo(6));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
        }
    }
}
