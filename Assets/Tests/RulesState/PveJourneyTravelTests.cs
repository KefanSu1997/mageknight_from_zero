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
    public sealed class PveJourneyTravelTests
    {
        private static readonly AxialCoord Forest=new(1,0),Plains=new(0,1),Lake=new(-1,0);
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Create(params string[] ids)
        {
            var map=new MapState();
            foreach(var (coord,terrain) in new[]{(new AxialCoord(0,0),TerrainType.Plains),(Forest,TerrainType.Forest),(Plains,TerrainType.Plains),(Lake,TerrainType.Lake)})
                map.Placed.Add(coord,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(terrain,6).ToArray()));
            var p=new PlayerState();foreach(string id in ids)p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(color);
            var s=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),map:map);
            s.SelectTactic("day_1");s.BeginTurn();return s;
        }
        [TestCase("basic_card_000","base",2)]
        [TestCase("basic_card_014","strong",4)]
        [TestCase("basic_card_007","base",2)]
        [TestCase("basic_card_011","move",2)]
        [TestCase("basic_card_004","move",2)]
        [TestCase("basic_card_027","strong_move",4)]
        [TestCase("advanced_card_009","strong",4)]
        [TestCase("advanced_card_011","strong",4)]
        [TestCase("advanced_card_039","base",3)]
        public void OriginalMovementIsConsumedByActualPositionChange(string id,string choice,int points)
        {
            var s=Create(id,"basic_card_001");var card=s.Player.Deck.Hand[0];s.PlayOutsideBattle(card,choice);
            Assert.That(s.Context.MovementPool,Is.EqualTo(points));s.Move(Plains);
            Assert.That(s.Player.Position,Is.EqualTo(Plains));Assert.That(s.Context.MovementPool,Is.EqualTo(points-2));
            Assert.That(s.Player.Deck.PlayedCards.Single(),Is.SameAs(card));
        }
        [Test] public void DruidSelectedHexAndTerrainUsePrintedCostAndRejectMissingChoiceBeforeFee()
        {
            var s=Create("basic_card_018","basic_card_000");var card=s.Player.Deck.Hand[0];
            Assert.Throws<InvalidOperationException>(()=>s.PlayTravelCard(card,"strong"));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(s.Player.Deck.PlayedCards.Count,Is.Zero);
            s.PlayTravelCard(card,"base",Forest);Assert.That(s.MovementCost(Forest),Is.EqualTo(2));s.Move(Forest);
            Assert.That(s.Context.MovementPool,Is.Zero);Assert.That(s.Context.TargetHex,Is.Null);
            s.EndTurn();s.BeginTurn();Assert.That(s.Context.CurrentTerrain,Is.EqualTo(TerrainType.Forest));Assert.That(s.MovementCost(Forest),Is.EqualTo(3));
        }
        [Test] public void KeepMovingBonusAppliesToLaterActualSidewaysThenClearsAfterTurn()
        {
            var s=Create("basic_card_017","basic_card_021","basic_card_001");s.PlayOutsideBattle(s.Player.Deck.Hand[0],"base");
            s.SidewaysMovement(s.Player.Deck.Hand[0]);Assert.That(s.Context.MovementPool,Is.EqualTo(4));s.Move(Forest);
            Assert.That(s.Context.MovementPool,Is.EqualTo(1));s.EndTurn();Assert.That(s.Context.MovementPool,Is.Zero);Assert.That(s.Context.MoveBonusNext,Is.Zero);
        }
        [Test] public void WindSongRequiresSeparateRealBlueForLakeAndClearsPermission()
        {
            var s=Create("advanced_card_010","basic_card_001");s.Player.Mana.Crystals[ManaColor.Blue]=0;
            s.PlayOutsideBattle(s.Player.Deck.Hand[0],"strong");Assert.That(s.MovementCost(Lake),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>s.Move(Lake));Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(0,0)));
            Assert.That(s.Context.MovementPool,Is.EqualTo(2));s.Player.Mana.AddCrystal(ManaColor.Blue);s.Move(Lake);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);Assert.That(s.Context.MovementPool,Is.EqualTo(2));
            s.EndTurn();Assert.That(s.Context.RequireBlueForLake,Is.False);s.BeginTurn();Assert.That(s.MovementCost(Lake),Is.EqualTo(int.MaxValue));
        }
        [Test] public void DiscountCannotOpenLakeOrAllowNonAdjacentOrPostBattleMovement()
        {
            var s=Create("advanced_card_011","basic_card_001");s.PlayOutsideBattle(s.Player.Deck.Hand[0],"strong");
            Assert.Throws<InvalidOperationException>(()=>s.Move(Lake));Assert.Throws<InvalidOperationException>(()=>s.Move(new AxialCoord(4,4)));
            Assert.That(s.Context.MovementPool,Is.EqualTo(4));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var b=s.BeginBattle(new[]{enemy});
            // Committing the combat action discards unused movement, before either rejected move.
            Assert.That(s.Context.MovementPool,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>s.Move(Forest));b.Advance();b.Advance();b.Advance();b.Advance();
            Assert.Throws<InvalidOperationException>(()=>s.Move(Forest));Assert.That(s.Context.MovementPool,Is.Zero);
            Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(0,0)));
        }
        [Test] public void SteadyReturnUsesSamePlayedInstanceBeforeFormalRefill()
        {
            var s=Create("advanced_card_017","basic_card_001");var card=s.Player.Deck.Hand[0];s.PlayOutsideBattle(card,"strong");
            s.SelectSteadyReturn(card,true);s.Move(Forest);s.EndTurn();
            Assert.That(s.Player.Deck.Hand.Any(c=>ReferenceEquals(c,card)),Is.True);
            Assert.That(s.Player.Deck.DiscardPile.Any(c=>ReferenceEquals(c,card)),Is.False);Assert.That(s.Context.MovementPool,Is.Zero);
        }
    }
}
