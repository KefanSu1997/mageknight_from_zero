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
using Monster=MK.Logic.Data.Monster;

namespace MK.Tests.Battles
{
    public sealed class PveWorldJourneyEntryTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static AxialCoord At(int q)=>new(q,0);
        private static Monster Token(string id)=>CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);
        private static PveDeedSelection Path()=>new(){TravelPath=new[]{At(1),At(2),At(3)}};
        private static DeedCard Card(PveJourneySession j,string id)=>j.Player.Deck.Hand.First(c=>c.Id==id);
        private static PveJourneySession Start(PlaceType type,string first,string second=null,params string[] ids)
        {
            var tokens=second==null?new[]{Token(first)}:new[]{Token(first),Token(second)};
            var supply=new EnemyTokenSupply(tokens,new Random(0));var map=new MapState();
            for(int i=0;i<=3;i++)map.Placed[At(i)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Plains});
            map.Places[At(3)]=new PlaceState(type){Enemy=supply.Draw(EnemyTokenSupply.GuardColor(type))};
            var player=new PlayerState();player.TokensPerTurn[ManaColor.Black]=1;
            player.Mana.AddCrystal(ManaColor.Green,2);player.Mana.AddCrystal(ManaColor.Red);
            player.Deck.Hand.AddRange((ids.Length==0?new[]{"magic_004","magic_009"}:ids)
                .Select(id=>new DeedCard(id,id.StartsWith("magic")?CardType.Spell:CardType.Action)));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var journey=new PveJourneySession(player,cards,map:map,spells:spells,enemySupply:supply,
                manaRandom:new Random(0),tacticRandom:new Random(0),supplyRandom:new Random(0));
            journey.Game.Clock.NextRound();journey.SelectTactic("night_1");journey.BeginTurn();return journey;
        }
        private static void EndBattle(PveJourneySession j)
        {while(j.Battle.Phase!=Phase.End)j.Battle.Advance();}

        [TestCase(PlaceType.Keep,"monster_027")]
        [TestCase(PlaceType.MageTower,"monster_002")]
        public void OriginalTunnelAttachesSameGuardAndSettlesActualSiteBeforeFormalTurnReward(PlaceType type,string id)
        {
            var j=Start(type,id);var player=j.Player;var context=j.Context;var site=j.Game.Map.Places[At(3)];var guard=site.Enemy;
            if(type==PlaceType.Keep)player.Deck.Hand.Add(new DeedCard("basic_card_021",CardType.Action));
            var tunnel=Card(j,"magic_004");j.PlayOutsideWithInputs(tunnel,"strong",Path());
            Assert.That(j.Battle,Is.Not.Null);Assert.That(j.Battle.Context,Is.SameAs(context));Assert.That(j.Battle.Player,Is.SameAs(player));
            Assert.That(j.Battle.Enemies.Single(),Is.SameAs(guard));Assert.That(j.Context.TunnelDefenders.Single(),Is.SameAs(guard));
            Assert.That(j.Player.Position,Is.EqualTo(At(3)));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
            Assert.That(j.Player.Mana.Tokens.ContainsKey(ManaColor.Black),Is.False);Assert.That(j.Player.Reputation,Is.EqualTo(-1));
            Assert.That(j.Player.Deck.PlayedCards.Single(),Is.SameAs(tunnel));Assert.That(context.IgnoreSiteFortifications,Is.True);Assert.That(context.IgnoreFortified,Is.False);
            Assert.Throws<InvalidOperationException>(()=>j.FinishTunnelAssault(false));
            j.Battle.Play(Card(j,"magic_009"),"base");Assert.That(j.Battle.Attack(0).Kills,Is.EqualTo(1));EndBattle(j);
            Assert.Throws<InvalidOperationException>(()=>j.EndTurn());Assert.That(site.Conquered,Is.False);
            j.FinishTunnelAssault(false);Assert.That(site.Conquered,Is.True);Assert.That(site.OwnerId,Is.EqualTo(player.Id));
            Assert.That(j.EnemySupply.DiscardPile.Single(),Is.SameAs(guard));Assert.That(j.EnemySupply.InUse,Is.Empty);
            Assert.That(context.IgnoreSiteFortifications,Is.False);Assert.That(context.TunnelAssaultActive,Is.False);
            Assert.That(player.Fame,Is.EqualTo(guard.Fame));Assert.Throws<InvalidOperationException>(()=>j.FinishTunnelAssault(false));
            SpellCard reward=null;
            if(type==PlaceType.MageTower)
            {
                Assert.That(context.PendingSpellReward,Is.True);Assert.Throws<InvalidOperationException>(()=>j.EndTurn());
                reward=j.Game.Globals.Spells.Offer[0];Assert.Throws<InvalidOperationException>(()=>j.SelectSiteSpellReward(new SpellCard(reward.Id)));
                j.SelectSiteSpellReward(reward);Assert.That(player.Deck.Hand.Any(c=>c.Id==reward.Id),Is.False);
                Assert.That(j.Game.Globals.Spells.Offer.Any(s=>ReferenceEquals(s,reward)),Is.True);
            }
            j.EndTurn();Assert.That(j.CompletedBattles,Is.EqualTo(1));Assert.That(j.CompletedTurns,Is.EqualTo(1));
            Assert.That(player.Deck.DiscardPile.Any(c=>ReferenceEquals(c,tunnel)),Is.True);
            if(reward!=null)
            {
                Assert.That(player.Deck.Hand.Any(c=>c.Id==reward.Id),Is.True);Assert.That(context.PendingSpellReward,Is.False);
                Assert.That(j.Game.Globals.Spells.Offer.Any(s=>ReferenceEquals(s,reward)),Is.False);
            }
            j.BeginTurn();Assert.That(j.Player,Is.SameAs(player));Assert.That(j.Game.Map.Places[At(3)],Is.SameAs(site));
            Assert.That(j.Context.EnemySupply,Is.SameAs(j.EnemySupply));Assert.That(j.Game.Globals.Enemies,Is.SameAs(j.EnemySupply));
            Assert.That(j.Player.Position,Is.EqualTo(At(3)));Assert.That(j.IsDefeated(guard),Is.True);
        }

        [Test] public void InnateFortificationSurvivesTunnelAndRetreatDrawsNextActualGuardNextTurn()
        {
            var j=Start(PlaceType.Keep,"monster_025","monster_027","magic_004","magic_004","magic_009","advanced_card_000");
            j.Player.Mana.AddCrystal(ManaColor.Red);
            var site=j.Game.Map.Places[At(3)];var first=site.Enemy;var survivorCard=j.Player.Deck.Hand.Where(c=>c.Id=="magic_004").Last();
            j.PlayOutsideWithInputs(Card(j,"magic_004"),"strong",Path());j.Battle.Play(Card(j,"magic_009"),"base");
            j.Battle.Play(Card(j,"advanced_card_000"),"strong");Assert.That(j.Context.RangedPool,Is.EqualTo(8));
            Assert.Throws<InvalidOperationException>(()=>j.Battle.Attack(0));Assert.That(j.Context.DefeatedEnemyIndices,Is.Empty);
            EndBattle(j);Assert.Throws<InvalidOperationException>(()=>j.FinishTunnelAssault(false));j.FinishTunnelAssault(true);
            Assert.That(j.Player.Position,Is.EqualTo(At(0)));Assert.That(j.Player.Wounds,Is.EqualTo(2));Assert.That(site.GuardNeedsReplacement,Is.True);
            Assert.That(j.EnemySupply.DiscardPile.Single(),Is.SameAs(first));Assert.That(j.Player.Fame,Is.Zero);
            j.EndTurn();j.BeginTurn();Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,survivorCard)),Is.True);
            j.PlayOutsideWithInputs(survivorCard,"strong",Path());
            Assert.That(j.Battle.Enemies.Single(),Is.SameAs(site.Enemy));Assert.That(site.Enemy.Id,Is.EqualTo("monster_027"));
            Assert.That(site.Enemy,Is.Not.SameAs(first));Assert.That(site.GuardNeedsReplacement,Is.False);
            Assert.That(j.EnemySupply.InUse.Single(),Is.SameAs(site.Enemy));Assert.That(j.Player.Wounds,Is.EqualTo(2));
            Assert.That(j.Player.Reputation,Is.EqualTo(-2));Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.Zero);
        }

        [TestCase("empty")][TestCase("foreign")][TestCase("replacement_without_supply")]
        public void MissingPhysicalDefenderRejectsBeforeFeesPositionOrCardsChange(string defect)
        {
            var j=Start(PlaceType.Keep,"monster_027");var site=j.Game.Map.Places[At(3)];var actual=site.Enemy;
            if(defect=="foreign")site.Enemy=actual with{};
            else {site.Enemy=null;site.GuardNeedsReplacement=defect=="replacement_without_supply";}
            var host=Card(j,"magic_004");Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(host,"strong",Path()));
            Assert.That(j.Battle,Is.Null);Assert.That(j.Player.Position,Is.EqualTo(At(0)));Assert.That(j.Player.Reputation,Is.Zero);
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));Assert.That(j.Player.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
            Assert.That(j.Player.Deck.Hand.Any(c=>ReferenceEquals(c,host)),Is.True);Assert.That(j.EnemySupply.InUse.Single(),Is.SameAs(actual));
        }

        [Test] public void MissingGreenFeeDoesNotDrawReplacementOrMovePlayer()
        {
            var j=Start(PlaceType.Keep,"monster_025","monster_027");var site=j.Game.Map.Places[At(3)];
            j.EnemySupply.Discard(site.Enemy);site.Enemy=null;site.GuardNeedsReplacement=true;j.Player.Mana.Crystals.Clear();
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(Card(j,"magic_004"),"strong",Path()));
            Assert.That(site.GuardNeedsReplacement,Is.True);Assert.That(j.EnemySupply.DrawPile.Single().Id,Is.EqualTo("monster_027"));
            Assert.That(j.EnemySupply.InUse,Is.Empty);Assert.That(j.Player.Position,Is.EqualTo(At(0)));Assert.That(j.Player.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
        }

        [Test] public void OfferedTunnelConsumesOriginalGreenPermissionAndAttachesSameWorldBattle()
        {
            var j=Start(PlaceType.MageTower,"monster_002",null,"advanced_card_025","basic_card_001");
            var offered=new SpellCard("magic_004");j.Game.Globals.Spells.Offer.Clear();j.Game.Globals.Spells.Offer.Add(offered);
            j.PlayOutsideWithInputs(Card(j,"advanced_card_025"),"base",new PveDeedSelection{Cards=new[]{Card(j,"basic_card_001")}});
            Assert.That(j.Context.SpellColors,Is.EqualTo(new[]{ManaColor.Green}));j.PlayOfferedSpell(offered,"strong",inputs:Path());
            Assert.That(j.Battle.Enemies.Single(),Is.SameAs(j.Game.Map.Places[At(3)].Enemy));Assert.That(j.Context.SpellColors,Is.Empty);
            Assert.That(j.Game.Globals.Spells.Offer.Single(),Is.SameAs(offered));Assert.That(j.Player.Deck.PlayedCards.Single().Id,Is.EqualTo("advanced_card_025"));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(j.Player.Mana.Tokens.ContainsKey(ManaColor.Black),Is.False);
        }

        [Test] public void PlayerOccupiedDestinationRejectsUnsupportedPvpBeforeOriginalPayment()
        {
            var j=Start(PlaceType.Keep,"monster_027");j.Context.OtherPlayers.Add(new PlayerState{Position=At(3)});
            Assert.Throws<InvalidOperationException>(()=>j.PlayOutsideWithInputs(Card(j,"magic_004"),"strong",Path()));
            Assert.That(j.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(2));Assert.That(j.Player.Position,Is.EqualTo(At(0)));
            Assert.That(j.Context.TunnelAssaultActive,Is.False);Assert.That(j.Battle,Is.Null);
        }
    }
}
