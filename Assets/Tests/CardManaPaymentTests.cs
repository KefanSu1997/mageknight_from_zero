using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MK.Tests.Cards
{
    public sealed class CardManaPaymentTests
    {
        [Test]
        public void MissingSecondColorPreservesFirstColorAndCurse()
        {
            var player = new PlayerState { ManaCurseColor = ManaColor.Green };
            player.Mana.AddCrystal(ManaColor.Green);
            var context = new ActionContext();
            Assert.That(player.Mana.TryPayExactColors(new[] { ManaColor.Green, ManaColor.Black }, context, player), Is.False);
            Assert.That(player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1));
            Assert.That(context.SpentCrystals, Is.Empty);
            Assert.That(player.ManaCurseTriggered, Is.False);
            Assert.That(player.Wounds, Is.Zero);
        }

        [Test]
        public void RepeatedColorIsValidatedTogetherThenTokensPrecedeCrystals()
        {
            var player = new PlayerState();
            var context = new ActionContext();
            player.Mana.AddToken(ManaColor.Green);
            var cost = new[] { ManaColor.Green, ManaColor.Green };
            Assert.That(player.Mana.TryPayExactColors(cost, context), Is.False);
            Assert.That(player.Mana.Tokens[ManaColor.Green], Is.EqualTo(1));
            player.Mana.AddCrystal(ManaColor.Green, 2);
            Assert.That(player.Mana.TryPayExactColors(cost, context), Is.True);
            Assert.That(player.Mana.Tokens.ContainsKey(ManaColor.Green), Is.False);
            Assert.That(player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1));
            Assert.That(context.SpentCrystals[ManaColor.Green], Is.EqualTo(1));
        }

        [Test]
        public void EverySpellAssetAndJsonMatchPrintedCostGroups()
        {
            CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(Application.dataPath, "../resources/text_json"));
            var spells = CardJsonLoader.LoadSpells();
            Assert.That(spells, Has.Count.EqualTo(24));
            var printed = new[] { ManaColor.Green, ManaColor.Red, ManaColor.Blue, ManaColor.White };
            for (int i = 0; i < 24; i++)
            {
                string id = "magic_" + i.ToString("D3");
                var source = spells.Single(s => s.Id == id);
                var asset = AssetDatabase.LoadAssetAtPath<SpellCardSO>("Assets/GameData/CardsAssets/" + id + ".asset");
                Assert.That(source.ManaColor, Is.EqualTo(printed[i / 6]), id);
                Assert.That(source.Top.ManaCost, Is.EqualTo(new[] { printed[i / 6] }), id);
                Assert.That(source.Bottom.ManaCost, Is.EqualTo(new[] { printed[i / 6], ManaColor.Black }), id);
                Assert.That(asset.ManaColor, Is.EqualTo(source.ManaColor), id);
                Assert.That(asset.Top.ManaCost, Is.EqualTo(source.Top.ManaCost), id);
                Assert.That(asset.Bottom.ManaCost, Is.EqualTo(source.Bottom.ManaCost), id);
            }
        }
    }
}
