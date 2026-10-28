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

namespace MK.Tests.Battles
{
    // Draft for the new public PVE travel commands; no fabricated rules or enemy values.
    public sealed class PveTravelChoiceEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static DeedCard Card(PveJourneySession s,string id)=>s.Player.Deck.Hand.First(c=>c.Id==id);
        private static int Crystal(PlayerState p,ManaColor color)=>p.Mana.Crystals.TryGetValue(color,out int n)?n:0;
        private static PveJourneySession Create(bool night=false,int reserve=0,params string[] ids)
        {
            var p=new PlayerState();
            p.Deck.SetDeck(Enumerable.Range(0,reserve).Select(_=>new DeedCard("basic_card_008",CardType.Action)));
            foreach(string id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:id.StartsWith("items_")?CardType.Artifact:CardType.Action));
            var map=new MapState();
            for(int q=0;q<7;q++)map.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,Enumerable.Repeat(TerrainType.Plains,6).ToArray());
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var originals=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(x=>new ActionCardData(x.Id,x.Name,x.Set,x.ImagePath,x.EnImagePath,x.Top.Effect,x.Bottom.Effect,x.Top.ManaCost)));
            var s=new PveJourneySession(p,originals,map:map,spells:spells);
            if(night)s.Game.Clock.NextRound();
            s.SelectTactic(night?"night_1":"day_1");s.BeginTurn();return s;
        }
        private static void Terrain(PveJourneySession s,int q,TerrainType terrain)
            =>s.Game.Map.Placed[new AxialCoord(q,0)]=new MapTile(TileSet.Countryside,q,Enumerable.Repeat(terrain,6).ToArray());

        [Test] public void BaseHarvestDiscardsActualGreenThenCrystalPaysMarchAndPersistsThroughEndTurn()
        {
            var s=Create(ids:new[]{"basic_card_003","basic_card_001","basic_card_000","basic_card_008"});var p=s.Player;
            var harvest=Card(s,"basic_card_003");var material=Card(s,"basic_card_001");var march=Card(s,"basic_card_000");
            s.PlayOutsideBattle(harvest,"base");Assert.That(s.Context.MovementPool,Is.EqualTo(2));
            s.Move(new AxialCoord(1,0),material);Assert.That(s.Context.MovementPool,Is.Zero);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));Assert.That(Crystal(p,ManaColor.Green),Is.EqualTo(1));
            s.PlayOutsideBattle(march,"strong");Assert.That(Crystal(p,ManaColor.Green),Is.Zero);Assert.That(s.Context.MovementPool,Is.EqualTo(4));
            s.Move(new AxialCoord(2,0));s.Move(new AxialCoord(3,0));s.EndTurn();s.BeginTurn();
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(3,0)));Assert.That(s.Context.HarvestTriggers,Is.Zero);
            foreach(var c in new[]{harvest,material,march})Assert.That(p.Deck.DiscardPile.Count(x=>ReferenceEquals(x,c)),Is.EqualTo(1));
        }

        [Test] public void StrongHarvestMaySkipOneStepThenDiscardActualArtifactForChosenRed()
        {
            var s=Create(ids:new[]{"basic_card_003","items_008","basic_card_021"});var p=s.Player;
            var sword=Card(s,"items_008");var fury=Card(s,"basic_card_021");p.Mana.AddCrystal(ManaColor.Green);
            s.PlayOutsideBattle(Card(s,"basic_card_003"),"strong");Assert.That(Crystal(p,ManaColor.Green),Is.Zero);
            s.Move(new AxialCoord(1,0));Assert.That(s.Context.HarvestTriggers,Is.EqualTo(-1));
            s.Move(new AxialCoord(2,0),sword,ManaColor.Red);Assert.That(Crystal(p,ManaColor.Red),Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(sword));Assert.That(s.Context.MeleePool,Is.Zero);
            var b=s.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032")});
            b.Advance();b.Advance();b.Advance();b.Play(fury,"strong");Assert.That(Crystal(p,ManaColor.Red),Is.Zero);
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();s.EndTurn();
            Assert.That(p.Deck.DiscardPile.Any(c=>ReferenceEquals(c,sword)),Is.True);Assert.That(p.Deck.RemovedCards,Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void InvalidHarvestMaterialOrDestinationPreservesActualHandMovementAndCrystal(bool invalidDestination)
        {
            var s=Create(ids:new[]{"basic_card_003","basic_card_001"});var material=Card(s,"basic_card_001");
            s.PlayOutsideBattle(Card(s,"basic_card_003"),"base");
            var chosen=invalidDestination?material:new DeedCard(material.Id,material.Type);
            Assert.Throws<InvalidOperationException>(()=>s.Move(new AxialCoord(invalidDestination?3:1,0),chosen));
            Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(s.Context.MovementPool,Is.EqualTo(2));
            Assert.That(s.Context.HarvestTriggers,Is.EqualTo(1));Assert.That(s.Player.Deck.Hand.Single(),Is.SameAs(material));
            Assert.That(s.Player.Deck.DiscardPile,Is.Empty);Assert.That(s.Player.Mana.Crystals.Values.Sum(),Is.Zero);
        }

        [TestCase("base",1,6)] [TestCase("strong_range",2,6)] [TestCase("strong_draw",1,7)]
        public void PortalChoicesConsumeBlueAndUseActualNextRefill(string choice,int range,int refill)
        {
            var s=Create(reserve:10,ids:new[]{"advanced_card_032","basic_card_000","basic_card_008","basic_card_021","basic_card_016"});var p=s.Player;
            var portal=Card(s,"advanced_card_032");var reserve=p.Deck.DrawPile.ToArray();p.Mana.AddCrystal(ManaColor.Blue);
            s.PlayOutsideBattle(portal,choice);Assert.That(Crystal(p,ManaColor.Blue),Is.EqualTo(choice=="base"?1:0));
            Assert.That(s.Context.TeleportRange,Is.EqualTo(range));s.TemporalPortal(new AxialCoord(range,0));
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(range,0)));Assert.That(s.Context.MovementPool,Is.Zero);
            Assert.That(s.Context.TemporalPortalPending,Is.False);Assert.That(s.Context.TurnActionCommitted,Is.True);
            Assert.Throws<InvalidOperationException>(()=>s.BeginBattle(new[]{CardJsonLoader.LoadMonsters().First()}));
            s.EndTurn();Assert.That(p.Deck.Hand.Count,Is.EqualTo(refill));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(14-refill));
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(portal));
            Assert.That(p.Deck.Hand.Count(c=>reserve.Any(r=>ReferenceEquals(c,r))),Is.EqualTo(refill-4));
            s.BeginTurn();Assert.That(s.Context.NextDrawBonus,Is.Zero);Assert.That(s.Context.TurnActionCommitted,Is.False);
        }

        [Test] public void PortalRejectsUnsafeLakeWithoutLosingPendingThenMayDeclineAndStillRefill()
        {
            var s=Create(reserve:10,ids:new[]{"advanced_card_032","basic_card_000","basic_card_008","basic_card_021","basic_card_016"});Terrain(s,1,TerrainType.Lake);
            s.PlayOutsideBattle(Card(s,"advanced_card_032"),"base");
            Assert.Throws<InvalidOperationException>(()=>s.TemporalPortal(new AxialCoord(1,0)));
            Assert.That(s.Context.TemporalPortalPending,Is.True);Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(0,0)));
            s.TemporalPortal(null);Assert.That(s.Context.TemporalPortalPending,Is.False);Assert.That(s.Context.TurnActionCommitted,Is.True);
            Assert.Throws<InvalidOperationException>(()=>s.Move(new AxialCoord(1,0)));
            s.EndTurn();Assert.That(s.Player.Deck.Hand.Count,Is.EqualTo(6));Assert.That(s.Context.NextDrawBonus,Is.Zero);
        }

        [Test] public void PortalCannotReplaceRestActionOrPayBlueAfterRest()
        {
            var s=Create(ids:new[]{"advanced_card_032","basic_card_000","basic_card_008"});var portal=Card(s,"advanced_card_032");
            s.Player.Mana.AddCrystal(ManaColor.Blue);s.Rest(Card(s,"basic_card_008"));
            Assert.Throws<InvalidOperationException>(()=>s.PlayOutsideBattle(portal,"strong_range"));
            Assert.That(Crystal(s.Player,ManaColor.Blue),Is.EqualTo(1));Assert.That(s.Player.Deck.Hand.Any(c=>ReferenceEquals(c,portal)),Is.True);
            Assert.That(s.Context.TemporalPortalPending,Is.False);
        }

        [Test] public void SpaceBendingPaysEachRealDestinationAndDoesNotSurviveTurn()
        {
            var s=Create(ids:new[]{"magic_016","basic_card_000","basic_card_014"});var p=s.Player;
            Terrain(s,1,TerrainType.Mountain);Terrain(s,3,TerrainType.Lake);Terrain(s,5,TerrainType.Mountain);
            p.Mana.AddCrystal(ManaColor.Blue);p.Mana.AddCrystal(ManaColor.Green);
            s.PlayOutsideBattle(Card(s,"magic_016"),"base");Assert.That(Crystal(p,ManaColor.Blue),Is.Zero);Assert.That(s.Context.MovementPool,Is.Zero);
            s.PlayOutsideBattle(Card(s,"basic_card_000"),"strong");s.Move(new AxialCoord(2,0));Assert.That(s.Context.MovementPool,Is.EqualTo(2));
            s.Move(new AxialCoord(4,0));Assert.That(s.Context.MovementPool,Is.Zero);s.EndTurn();s.BeginTurn();
            s.PlayOutsideBattle(Card(s,"basic_card_014"),"base");Assert.That(s.Context.SpaceBendingActive,Is.False);
            Assert.Throws<InvalidOperationException>(()=>s.Move(new AxialCoord(6,0)));
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(4,0)));Assert.That(s.Context.MovementPool,Is.EqualTo(2));
        }

        [Test] public void TimeBendingReturnsOnlyPlayedInstancesAndActualExtraTurnKeepsPosition()
        {
            var s=Create(night:true,reserve:2,ids:new[]{"magic_016","basic_card_000","basic_card_008","basic_card_001","basic_card_021"});var p=s.Player;
            var warp=Card(s,"magic_016");var march=Card(s,"basic_card_000");var material=Card(s,"basic_card_001");var reserve=p.Deck.DrawPile.ToArray();
            p.Mana.AddCrystal(ManaColor.Blue);p.Mana.AddToken(ManaColor.Black);
            s.PlayOutsideBattle(march,"base");s.Move(new AxialCoord(1,0));s.PlayOutsideBattle(warp,"strong");
            Assert.That(Crystal(p,ManaColor.Blue),Is.Zero);Assert.That(p.Mana.Tokens.Values.Sum(),Is.Zero);
            s.EndTurn(new[]{material});Assert.That(s.CompletedTurns,Is.EqualTo(1));
            Assert.That(p.Deck.RoundAsideCards.Single(),Is.SameAs(warp));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));
            Assert.That(p.Deck.Hand.Any(c=>ReferenceEquals(c,march)),Is.True);Assert.That(p.Deck.DrawPileCount,Is.EqualTo(2));
            for(int i=0;i<2;i++)Assert.That(p.Deck.DrawPile.ElementAt(i),Is.SameAs(reserve[i]));
            s.BeginTurn();Assert.That(s.Game.CurrentPlayer,Is.SameAs(p));Assert.That(p.Position,Is.EqualTo(new AxialCoord(1,0)));
            s.PlayOutsideBattle(march,"base");s.Move(new AxialCoord(2,0));s.EndTurn();
            Assert.That(s.CompletedTurns,Is.EqualTo(2));Assert.That(p.Deck.RoundAsideCards.Single(),Is.SameAs(warp));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(4));Assert.That(p.Deck.DrawPileCount,Is.Zero);
        }

        [Test] public void MistFormPaysBlueAndActualLakeCostsTwoWhileHillsRemainForbidden()
        {
            var s=Create(ids:new[]{"magic_014","basic_card_008"});var p=s.Player;p.Mana.AddCrystal(ManaColor.Blue);
            Terrain(s,1,TerrainType.Lake);Terrain(s,2,TerrainType.Hills);
            s.PlayOutsideBattle(Card(s,"magic_014"),"base");Assert.That(Crystal(p,ManaColor.Blue),Is.Zero);
            Assert.That(s.MovementCost(new AxialCoord(1,0)),Is.EqualTo(2));s.Move(new AxialCoord(1,0));
            Assert.That(s.Context.MovementPool,Is.EqualTo(2));Assert.Throws<InvalidOperationException>(()=>s.Move(new AxialCoord(2,0)));
            Assert.That(p.Position,Is.EqualTo(new AxialCoord(1,0)));Assert.That(s.Context.MovementPool,Is.EqualTo(2));
            s.EndTurn();s.BeginTurn();Assert.That(s.MovementCost(new AxialCoord(1,0)),Is.EqualTo(int.MaxValue));
        }
    }
}
