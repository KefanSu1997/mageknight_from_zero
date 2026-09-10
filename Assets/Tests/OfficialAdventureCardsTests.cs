using System.Linq;
using MageKnight.Adventure.Content;
using MK.Logic.Core;
using MK.Logic.Runtime.Adventure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MK.Tests.Adventure
{
    public class OfficialAdventureCardsTests
    {
        [TestCase("basic_card_000", "行进", AdventurePhase.Travel, 2, 4, ManaColor.Green)]
        [TestCase("basic_card_014", "耐力", AdventurePhase.Travel, 2, 4, ManaColor.Blue)]
        [TestCase("basic_card_008", "承诺", AdventurePhase.Interaction, 2, 4, ManaColor.White)]
        [TestCase("basic_card_016", "决心", AdventurePhase.Block, 2, 5, ManaColor.Blue)]
        [TestCase("basic_card_021", "狂怒", AdventurePhase.Attack, 2, 4, ManaColor.Red)]
        public void OriginalCardIdentityArtAndPrintedNumbersAgree(string id, string name,
            AdventurePhase phase, int basic, int enhanced, ManaColor color)
        {
            var binding = Resources.Load<ActionCardDefinition>("Adventure/Cards/" + id);
            var original = AssetDatabase.LoadAssetAtPath<ActionCardSO>("Assets/GameData/CardsAssets/" + id + ".asset");
            Assert.That(binding.source, Is.SameAs(original));
            Assert.That(binding.artwork, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png")));
            var card = binding.Snapshot();
            Assert.That(card.Name, Is.EqualTo(name)); Assert.That(card.Id, Is.EqualTo(id));
            Assert.That(card.BaseText, Is.EqualTo(original.BaseEffect));
            Assert.That(card.EnhancedText, Is.EqualTo(original.EnhancedEffect));
            Assert.That(card.Colors, Is.EqualTo(new[] { color }));
            Assert.That(OfficialActionAdapter.Preview(card, phase, false), Is.EqualTo(basic));
            Assert.That(OfficialActionAdapter.Preview(card, phase, true), Is.EqualTo(enhanced));
        }

        [Test]
        public void DeterminationAttackAndFuryBlockAreValidButEnhancedBranchesAreRestricted()
        {
            var determination = Resources.Load<ActionCardDefinition>("Adventure/Cards/basic_card_016").Snapshot();
            var fury = Resources.Load<ActionCardDefinition>("Adventure/Cards/basic_card_021").Snapshot();
            Assert.That(OfficialActionAdapter.Preview(determination, AdventurePhase.Attack, false), Is.EqualTo(2));
            Assert.That(OfficialActionAdapter.Preview(determination, AdventurePhase.Attack, true), Is.Zero);
            Assert.That(OfficialActionAdapter.Preview(fury, AdventurePhase.Block, false), Is.EqualTo(2));
            Assert.That(OfficialActionAdapter.Preview(fury, AdventurePhase.Block, true), Is.Zero);
        }

        [Test]
        public void SidewaysUsesOnePhysicalPointAndCannotUseWounds()
        {
            var s = new AdventureSession(Resources.Load<AdventureDefinition>("Adventure/Scenarios/01_combat").Snapshot());
            Assert.That(s.Execute("card:4"), Is.True); Assert.That(s.Sideways, Is.True);
            Assert.That(s.Execute("target:enemy:0"), Is.True); Assert.That(s.Execute("confirm"), Is.True);
            Assert.That(s.Battle.EffectiveBlock(0), Is.EqualTo(1));
            Assert.That(s.Hand.Count, Is.EqualTo(4)); Assert.That(s.Played.Count, Is.EqualTo(1));
            Assert.That(s.ReadState()["greenCrystal"], Is.EqualTo("1"));
            Assert.That(OfficialActionAdapter.Preview(new CardSpec(null), AdventurePhase.Block, false, true), Is.Zero);
        }

        [Test]
        public void ManaTokenIsConsumedBeforeCrystalAndCannotDoublePlay()
        {
            var s = new AdventureSession(Resources.Load<AdventureDefinition>("Adventure/Scenarios/01_combat").Snapshot());
            s.Player.Mana.Tokens[ManaColor.Blue] = 1;
            foreach (string cmd in new[] { "card:0", "target:enemy:0", "enhance", "confirm" }) Assert.That(s.Execute(cmd), Is.True);
            Assert.That(s.ReadState()["blueCrystal"], Is.EqualTo("1"));
            Assert.That(s.Player.Mana.Tokens.ContainsKey(ManaColor.Blue), Is.False);
            Assert.That(s.Execute("card:0"), Is.False);
        }

        [Test]
        public void EveryLessonUsesOnlyOriginalCardsWithExplicitLimitedMultiplicity()
        {
            foreach (var scene in Resources.LoadAll<AdventureDefinition>("Adventure/Scenarios"))
            {
                Assert.That(scene.deck.Length, Is.EqualTo(10));
                Assert.That(scene.deck.Select(c => c.id).Distinct().Count(), Is.EqualTo(5));
                Assert.That(scene.deck.GroupBy(c => c.id).All(g => g.Count() == 2), Is.True);
                Assert.That(scene.deck.All(c => c.source != null && OfficialActionAdapter.Supports(c.Snapshot())), Is.True);
            }
        }
    }
}
