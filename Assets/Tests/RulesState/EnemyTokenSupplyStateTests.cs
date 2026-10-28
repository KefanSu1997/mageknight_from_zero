using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class EnemyTokenSupplyStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static Monster Token(string id) => CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);
        private static EnemyTokenSupply Supply() => new(new[]{"monster_025","monster_027","monster_028","monster_002"}.Select(Token));
        private static AxialCoord At(int q) => new(q,0);
        private static void Play(PlayerState p,ActionContext c,string id,bool enhanced)
        {
            var original=CardJsonLoader.LoadSpells().Single(s=>s.Id==id);
            CardPlayActions.Play(p,c,p.Deck.Hand.First(s=>s.Id==id),
                new ActionCardData(original.Id,original.Name,original.Set,original.ImagePath,original.EnImagePath,
                    original.Top.Effect,original.Bottom.Effect,original.Top.ManaCost),enhanced);
        }
        private static void EndBattle(PlayerState p,ActionContext c,bool attack)
        {
            var game=new GameState();game.Players.Add(p);var turn=new TurnMachine(game);turn.BeginBattle(c);
            if(attack){Play(p,c,"magic_009",false);CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged);}
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);turn.FinishBattle(c);
            CardTravelActions.FinishTunnelAssault(p,c,!attack);
        }

        [TestCase("monster_025",EnemyTokenColor.Gray,7,3,3)]
        [TestCase("monster_027",EnemyTokenColor.Gray,5,6,4)]
        [TestCase("monster_028",EnemyTokenColor.Purple,6,5,5)]
        [TestCase("monster_002",EnemyTokenColor.Purple,5,5,4)]
        public void AuditedOriginalTokenValuesMatchTheirPrintedImages(string id,EnemyTokenColor color,int armor,int attack,int fame)
        {
            var token=Token(id);Assert.That(token.TokenColor,Is.EqualTo(color));Assert.That(token.Armor,Is.EqualTo(armor));
            Assert.That(token.Attack,Is.EqualTo(attack));Assert.That(token.Fame,Is.EqualTo(fame));
        }

        [TestCase(PlaceType.Keep,"monster_025","monster_027",2)]
        [TestCase(PlaceType.MageTower,"monster_028","monster_002",3)]
        public void RetreatThenRealNextTurnDrawsAnotherGuardAndConservesBothTokens(PlaceType type,string firstId,string nextId,int wounds)
        {
            var supply=Supply();var globals=new GlobalResources(1,()=>DayPart.Night){Enemies=supply};
            var site=new PlaceManager(CardJsonLoader.LoadMonsters(),globals).Flip(type);
            var first=site.Enemy;Assert.That(first.Id,Is.EqualTo(firstId));
            globals.Spells.SetDeck(new[]{new SpellCard("magic_015")});globals.Spells.Refill();
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Night,EnemySupply=supply,MovementMap=new MapState(),ManaSource=globals.Mana,SpellSupply=globals.Spells};
            for(int i=0;i<=3;i++)c.MovementMap.Placed[At(i)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Forest});
            c.MovementMap.Places[At(3)]=site;c.TravelPath.AddRange(new[]{At(1),At(2),At(3)});
            p.Deck.Hand.Add(new DeedCard("magic_004",CardType.Spell));
            p.Mana.AddToken(ManaColor.Green);p.Mana.AddToken(ManaColor.Black);
            p.Mana.AddCrystal(ManaColor.Green);p.Mana.AddCrystal(ManaColor.Red);
            // A pre-existing Mana Claim ongoing benefit provides the next turn's black token.
            p.TokensPerTurn[ManaColor.Black]=1;
            foreach(var id in new[]{"basic_card_000","magic_009","magic_004"})
                p.Deck.PutOnTop(new DeedCard(id,id.StartsWith("magic")?CardType.Spell:CardType.Action));
            Play(p,c,"magic_004",true);EndBattle(p,c,false);
            Assert.That(p.Wounds,Is.EqualTo(wounds));Assert.That(site.GuardNeedsReplacement,Is.True);
            Assert.That(supply.DiscardPile.Single(),Is.SameAs(first));Assert.That(supply.InUse,Is.Empty);
            var turns=new PlayerTurnEngine();turns.EndTurn(p,DayPart.Night,globals.Mana,c);turns.StartTurn(p,DayPart.Night);
            c.TravelPath.AddRange(new[]{At(1),At(2),At(3)});Play(p,c,"magic_004",true);
            var next=site.Enemy;Assert.That(next.Id,Is.EqualTo(nextId));Assert.That(next,Is.Not.SameAs(first));
            Assert.That(c.Enemies.Single(),Is.SameAs(next));Assert.That(site.GuardNeedsReplacement,Is.False);
            Assert.That(p.Reputation,Is.EqualTo(-2));Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);
            EndBattle(p,c,true);Assert.That(site.Conquered,Is.True);Assert.That(p.Fame,Is.EqualTo(4));
            Assert.That(supply.DiscardPile.Select(t=>t.Id),Is.EqualTo(new[]{firstId,nextId}));
            Assert.That(supply.DrawPile.Count,Is.EqualTo(2));Assert.That(supply.InUse,Is.Empty);Assert.That(p.Wounds,Is.EqualTo(wounds));
            if(type==PlaceType.MageTower)FortifiedSiteActions.SelectSpellReward(c,0);
            turns.EndTurn(p,DayPart.Night,globals.Mana,c);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(type==PlaceType.MageTower?5:3));
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.PendingSpellReward,Is.False);
        }

        [Test] public void EmptyDrawPileRecyclesOnlyMatchingColorAndPreservesPhysicalIdentity()
        {
            var gray=Token("monster_025");var purple=Token("monster_002");var supply=new EnemyTokenSupply(new[]{gray,purple},new Random(1));
            supply.Discard(supply.Draw(EnemyTokenColor.Gray));supply.Discard(supply.Draw(EnemyTokenColor.Purple));
            Assert.That(supply.Draw(EnemyTokenColor.Gray),Is.SameAs(gray));Assert.That(supply.DiscardPile.Single(),Is.SameAs(purple));
            Assert.That(supply.InUse.Count,Is.EqualTo(1));Assert.That(supply.CanDraw(EnemyTokenColor.Gray),Is.False);
        }

        [Test] public void DuplicateOrForeignTokenCannotBeDiscardedOrSeededTwice()
        {
            var original=Token("monster_025");var supply=new EnemyTokenSupply(new[]{original});
            supply.Draw(EnemyTokenColor.Gray);
            Assert.Throws<InvalidOperationException>(()=>supply.Discard(original with {}));
            supply.Discard(original);Assert.Throws<InvalidOperationException>(()=>supply.Discard(original));
            Assert.Throws<ArgumentException>(()=>new EnemyTokenSupply(new[]{original,original}));
            Assert.That(supply.DiscardPile.Count,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void MissingGuardOrMissingManaRejectsBeforeDrawingOrMoving(bool missingMana)
        {
            var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Night,EnemySupply=missingMana?Supply():new EnemyTokenSupply(),MovementMap=new MapState()};
            c.MovementMap.Placed[At(1)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Forest});
            c.MovementMap.Places[At(1)]=new PlaceState(PlaceType.Keep){GuardNeedsReplacement=true};c.TravelPath.Add(At(1));
            p.Deck.Hand.Add(new DeedCard("magic_004",CardType.Spell));p.Mana.AddToken(ManaColor.Black);if(!missingMana)p.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,"magic_004",true));
            Assert.That(p.Position.Q,Is.Zero);Assert.That(p.Deck.Hand.Count,Is.EqualTo(1));Assert.That(p.Reputation,Is.Zero);
            Assert.That(c.EnemySupply.InUse,Is.Empty);Assert.That(c.EnemySupply.DrawPile.Count,Is.EqualTo(missingMana?4:0));
            Assert.That(c.MovementMap.Places[At(1)].GuardNeedsReplacement,Is.True);Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
        }
    }
}
