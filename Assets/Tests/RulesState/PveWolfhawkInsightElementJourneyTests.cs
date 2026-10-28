using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveWolfhawkInsightElementJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test] public void OwnInsightRemovesActualIceAttackElementThenOriginalPhysicalBlockPreventsWound()
        {
            var p = new PlayerState { HeroId = "Wolfhawk", Fame = 35, Armor = 3 };
            p.Mana.Crystals[ManaColor.Red] = 1;
            p.Mana.Crystals[ManaColor.Blue] = 1;
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[5];
            HeroSkillAcquisition.Acquire(p, skill);
            foreach (var id in new[] { "basic_card_000", "basic_card_016", "basic_card_021", "advanced_card_000" })
                p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            var originals = p.Deck.Hand.ToArray();
            DeedCard Card(string id) => p.Deck.Hand.Single(c => c.Id == id);
            var origin = new AxialCoord(0, 0); var village = new AxialCoord(0, 1);
            var map = new MapState();
            foreach (var h in new[] { origin, village })
                map.Placed.Add(h, new MapTile(TileSet.Countryside, 0,
                    Enumerable.Repeat(TerrainType.Plains, 6).ToArray()));
            map.Places.Add(village, new PlaceState(PlaceType.Village));
            var j = new PveJourneySession(p,
                CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
                map: map, manaRandom: new Random(0), supplyRandom: new Random(0));
            j.PrepareRound(); j.SelectTactic("day_1"); j.BeginTurn();
            var lich = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_004");
            var b = j.BeginBattle(new[] { lich }); b.Advance();
            b.Play(Card("basic_card_016"), "block");
            Assert.That(lich.GetAttack(0).Element, Is.EqualTo(Element.Ice));
            Assert.That(b.BlockPreview(0).Effective, Is.EqualTo(1));
            Assert.That(b.BlockPreview(0).Required, Is.EqualTo(2));
            b.UseSkill(skill, 200 + (int)Element.Physical, target: 0, attackIndex: 0);
            Assert.That(CardCombatActions.DefenseProfile(lich, 0, b.Context, 0).AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(CardCombatActions.DefenseProfile(lich, 0, b.Context, 0).Attack, Is.EqualTo(2));
            Assert.That(lich.GetAttack(0).Element, Is.EqualTo(Element.Ice), "Original enemy definition is preserved");
            Assert.That(CombatMath.HasResistance(lich, 0, Element.Physical, b.Context), Is.True);
            Assert.That(CombatMath.HasResistance(lich, 0, Element.Ice, b.Context), Is.True);
            Assert.That(b.BlockPreview(0).Effective, Is.EqualTo(2));
            b.Block(0, 0); b.Advance(); b.Advance();
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(b.Context.SuccessfullyBlockedAttacks.Contains((0, 0)), Is.True);
            b.Play(Card("advanced_card_000"), "strong");
            b.Play(Card("basic_card_021"), "attack");
            Assert.That(CombatMath.EffectiveAttack(b.Enemies, new[] { 0 },
                b.Context.CombatPower.Attacks.ToArray(), Phase.Melee, b.Context), Is.EqualTo(4));
            b.Attack(0); b.Advance(); j.EndTurn();
            Assert.That(p.Fame, Is.EqualTo(40));
            Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("basic_card_000"));
            j.BeginTurn();
            Assert.That(SkillActions.IsUsed(p, skill), Is.True, "Once per round survives next personal turn");
            j.PlayOutsideBattle(Card("basic_card_000"), "base"); j.Move(village); j.EndTurn();
            Assert.That(j.Player, Is.SameAs(p));
            Assert.That(j.CompletedTurns, Is.EqualTo(2));
            Assert.That(j.CompletedBattles, Is.EqualTo(1));
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.DrawPileCount, Is.Zero);
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(4));
            Assert.That(originals.All(c => p.Deck.DiscardPile.Any(d => ReferenceEquals(c, d))), Is.True);
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(p.Position, Is.EqualTo(village));
        }
    }
}
