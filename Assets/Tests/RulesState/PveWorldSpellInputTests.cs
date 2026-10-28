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
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Battles
{
    public sealed class PveWorldSpellInputTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PveJourneySession Start(string spell, bool night = false, bool movement = false, bool fireball = false)
        {
            var player = new PlayerState();
            player.Deck.Hand.Add(new DeedCard(spell, CardType.Spell));
            if (fireball) player.Deck.Hand.Add(new DeedCard("magic_009", CardType.Spell));
            if (movement)
                for (int i = 0; i < 3; i++) player.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            var map = new MapState();
            for (int q = 0; q <= 5; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, Enumerable.Repeat(TerrainType.Forest, 6).ToArray());
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var journey = new PveJourneySession(player, cards, map: map, spells: spells,
                manaRandom: new Random(0), tacticRandom: new Random(0), supplyRandom: new Random(0));
            if (night) journey.Game.Clock.NextRound();
            journey.SelectTactic(night ? "night_1" : "day_1"); journey.BeginTurn();
            if (movement)
                foreach (var card in player.Deck.Hand.Where(c => c.Id == "basic_card_000").ToArray()) journey.PlayOutsideBattle(card, "base");
            return journey;
        }

        private static DeedCard Card(PveJourneySession j, string id) => j.Player.Deck.Hand.Single(c => c.Id == id);
        private static int Tokens(PveJourneySession j, ManaColor color) => j.Player.Mana.Tokens.TryGetValue(color, out int n) ? n : 0;
        private static AxialCoord[] Path(int count) => Enumerable.Range(1, count).Select(q => new AxialCoord(q, 0)).ToArray();
        private static Monster Enemy() => CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") with { };
        private static void AssertUnspent(PveJourneySession j, DeedCard card, ManaColor color, int movement)
        {
            Assert.That(j.Player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.True);
            Assert.That(j.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card)), Is.False);
            Assert.That(Tokens(j, color), Is.EqualTo(1));
            Assert.That(j.Context.MovementPool, Is.EqualTo(movement));
        }

        [TestCase(1)] [TestCase(3)]
        public void TunnelBasePaysGreenAndMovesActualPathWithoutSpendingMovement(int distance)
        {
            var j = Start("magic_004"); var spell = Card(j, "magic_004"); j.Player.Mana.AddToken(ManaColor.Green);
            j.Player.Deck.Hand.Add(new DeedCard("basic_card_021",CardType.Action));
            j.PlayOutsideWithInputs(spell, "base", new PveDeedSelection { TravelPath = Path(distance) });
            Assert.That(j.Player.Position, Is.EqualTo(new AxialCoord(distance, 0)));
            Assert.That(j.Context.MovementPool, Is.Zero); Assert.That(Tokens(j, ManaColor.Green), Is.Zero);
            Assert.That(j.Player.Deck.PlayedCards.Single(), Is.SameAs(spell));
            Assert.That(j.Context.TravelPath, Is.Empty); Assert.That(j.Context.InBattle, Is.False);
            j.EndTurn(); j.BeginTurn();
            Assert.That(j.Player.Position, Is.EqualTo(new AxialCoord(distance, 0)));
            Assert.That(j.Player.Deck.DiscardPile.Single(), Is.SameAs(spell));
        }

        [TestCase(TerrainType.Lake)] [TestCase(TerrainType.Swamp)]
        public void TunnelRejectsForbiddenIntermediateTerrainBeforeManaOrCardConsumption(TerrainType terrain)
        {
            var j = Start("magic_004"); var spell = Card(j, "magic_004"); j.Player.Mana.AddToken(ManaColor.Green);
            j.Game.Map.Placed[new AxialCoord(1, 0)] = new MapTile(TileSet.Countryside, 1, Enumerable.Repeat(terrain, 6).ToArray());
            Assert.Throws<InvalidOperationException>(() => j.PlayOutsideWithInputs(spell, "base", new PveDeedSelection { TravelPath = Path(2) }));
            AssertUnspent(j, spell, ManaColor.Green, 0); Assert.That(j.Player.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(j.Context.TravelPath, Is.Empty);
        }

        [TestCase(1)] [TestCase(5)]
        public void WindWingsSpendsPrintedWhiteAndOneActualMovePerPathStep(int distance)
        {
            var j = Start("magic_023", movement: true); var spell = Card(j, "magic_023"); j.Player.Mana.AddToken(ManaColor.White);
            if (distance > 1) j.Game.Map.Places[new AxialCoord(1, 0)] = new PlaceState(PlaceType.OrcRampaging) { Enemy = Enemy() };
            j.PlayOutsideWithInputs(spell, "base", new PveDeedSelection { TravelPath = Path(distance) });
            Assert.That(j.Player.Position, Is.EqualTo(new AxialCoord(distance, 0))); Assert.That(j.Context.MovementPool, Is.EqualTo(6 - distance));
            Assert.That(Tokens(j, ManaColor.White), Is.Zero); Assert.That(j.Player.Wounds, Is.Zero);
            Assert.That(j.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, spell)), Is.True); Assert.That(j.Context.TravelPath, Is.Empty);
            if (distance > 1) Assert.That(j.Game.Map.Places[new AxialCoord(1, 0)].Enemy, Is.Not.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void WindWingsRejectsDisconnectedPathOrUnsafeActualEndpointAtomically(bool unsafeEndpoint)
        {
            var j = Start("magic_023", movement: true); var spell = Card(j, "magic_023"); j.Player.Mana.AddToken(ManaColor.White);
            var path = unsafeEndpoint ? Path(2) : new[] { new AxialCoord(2, 0) };
            if (unsafeEndpoint) j.Game.Map.Places[new AxialCoord(2, 0)] = new PlaceState(PlaceType.Keep) { Enemy = Enemy() };
            Assert.Throws<InvalidOperationException>(() => j.PlayOutsideWithInputs(spell, "base", new PveDeedSelection { TravelPath = path }));
            AssertUnspent(j, spell, ManaColor.White, 6); Assert.That(j.Player.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(j.Context.TravelPath, Is.Empty);
        }

        [TestCase(2, 1)] [TestCase(3, 3)]
        public void NightWingsSelectsNonPrefixLiveEnemiesAndOnlyOthersActuallyDamageHero(int count, int cost)
        {
            var j = Start("magic_023", night: true, movement: true); var spell = Card(j, "magic_023");
            j.Player.Mana.AddToken(ManaColor.White); j.Player.Mana.AddToken(ManaColor.Black);
            var enemies = Enumerable.Range(0, 4).Select(_ => Enemy()).ToArray(); var battle = j.BeginBattle(enemies);
            var targets = new[] { 3, 1, 2 }.Take(count).ToArray();
            if (count == 3) battle.Advance(); // Both legal pre-attack phases use the same original effect.
            battle.Play(spell, "strong", inputs: new PveDeedSelection { EnemyIndices = targets });
            Assert.That(j.Context.SkipAttackIndices, Is.EquivalentTo(targets)); Assert.That(j.Context.MovementPool, Is.EqualTo(6 - cost));
            Assert.That(Tokens(j, ManaColor.White), Is.Zero); Assert.That(Tokens(j, ManaColor.Black), Is.Zero);
            Assert.That(j.Context.SpellTargetIndices, Is.Empty); Assert.That(j.Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, spell)), Is.True);
            if (battle.Phase == Phase.Ranged) battle.Advance();
            battle.Advance(); battle.Advance();
            Assert.That(battle.Phase, Is.EqualTo(Phase.Melee));
            Assert.That(j.Player.Wounds, Is.EqualTo((4 - count) * 2), "原兽人攻击4对英雄护甲2：仅未被选择者各造成两伤");
            Assert.That(j.Context.DefeatedEnemyIndices, Is.Empty); Assert.That(battle.Enemies[3], Is.SameAs(enemies[3]));
        }

        [TestCase(false)] [TestCase(true)]
        public void NightWingsRejectsActuallyDefeatedOrOriginalArcaneImmuneTargetBeforePayment(bool immune)
        {
            var j = Start("magic_023", night: true, movement: true, fireball: !immune); var spell = Card(j, "magic_023");
            j.Player.Mana.AddToken(ManaColor.White); j.Player.Mana.AddToken(ManaColor.Black);
            var target = immune ? CardJsonLoader.LoadMonsters().First(m => m.Abilities.Contains(Ability.MagicResist)) with { } : Enemy();
            var battle = j.BeginBattle(new[] { Enemy(), target });
            if (!immune)
            {
                j.Player.Mana.AddToken(ManaColor.Red); battle.Play(Card(j, "magic_009"), "base");
                Assert.That(battle.Attack(1).Kills, Is.EqualTo(1)); Assert.That(j.Context.DefeatedEnemyIndices.Contains(1), Is.True);
            }
            Assert.Throws<InvalidOperationException>(() => battle.Play(spell, "strong", inputs: new PveDeedSelection { EnemyIndices = new[] { 1 } }));
            AssertUnspent(j, spell, ManaColor.White, 6); Assert.That(Tokens(j, ManaColor.Black), Is.EqualTo(1));
            Assert.That(j.Context.SkipAttackIndices, Is.Empty); Assert.That(j.Context.SpellTargetIndices, Is.Empty);
            Assert.That(j.Context.DefeatedEnemyIndices.Count, Is.EqualTo(immune ? 0 : 1));
        }
    }
}
