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
    public sealed class CityColorStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static void Play(PlayerState p, ActionContext c, string id, bool enhanced = false)
        {
            var card = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
            CardPlayActions.Play(p,c,p.Deck.Hand.First(s=>s.Id==id),
                new ActionCardData(card.Id,card.Name,card.Set,card.ImagePath,card.EnImagePath,card.Top.Effect,card.Bottom.Effect,card.Top.ManaCost),enhanced);
        }
        private static (PlayerState p,ActionContext c,PlaceState city,TurnMachine turn) Start(CityColor color,string id="monster_027")
        {
            var guard=CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);
            var p=new PlayerState(); var city=new PlaceState(PlaceType.City){CityColor=color,Enemy=guard};
            var c=new ActionContext{MovementMap=new MapState(),DayPart=DayPart.Night};
            for(int i=0;i<2;i++)c.MovementMap.Placed[new AxialCoord(i,0)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Forest});
            c.MovementMap.Places[new AxialCoord(1,0)]=city;c.TravelPath.Add(new AxialCoord(1,0));
            foreach(var name in new[]{"magic_004","magic_009","magic_009"})p.Deck.Hand.Add(new DeedCard(name,CardType.Spell));
            foreach(var mana in new[]{ManaColor.Green,ManaColor.Black,ManaColor.Red})p.Mana.AddToken(mana,2);
            Play(p,c,"magic_004",true);var game=new GameState();game.Players.Add(p);var turn=new TurnMachine(game);turn.BeginBattle(c);
            return(p,c,city,turn);
        }

        [TestCase(CityColor.Red,"monster_027",6,0)]
        [TestCase(CityColor.Green,"monster_027",3,3)]
        [TestCase(CityColor.Blue,"monster_028",4,0)]
        [TestCase(CityColor.Blue,"monster_030",4,0)]
        [TestCase(CityColor.Blue,"monster_027",3,0)]
        [TestCase(CityColor.Red,"monster_028",3,0)]
        [TestCase(CityColor.Green,"monster_028",3,0)]
        [TestCase(CityColor.Green,"monster_002",3,3)]
        public void OriginalGuardsReceiveOnlyTheirCityBonusAndKeepTheirPrintedToken(CityColor color,string id,int hand,int discard)
        {
            var(p,c,city,turn)=Start(color,id);var original=city.Enemy;var originalAttack=original.Attack;var originalAbilities=original.Abilities.ToArray();
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);
            Assert.That(p.Wounds,Is.EqualTo(hand));Assert.That(p.Deck.DiscardPile.Count(d=>d.Type==CardType.Wound),Is.EqualTo(discard));
            Assert.That(city.Enemy,Is.SameAs(original));Assert.That(original.Attack,Is.EqualTo(originalAttack));Assert.That(original.Abilities,Is.EqualTo(originalAbilities));
            turn.FinishBattle(c);Assert.That(c.CombatSite,Is.Null);c.InBattle=true;
            Assert.That(CityCombatRules.DefenseProfile(original,c),Is.SameAs(original));c.InBattle=false;
            CardTravelActions.FinishTunnelAssault(p,c,true);Assert.That(city.Conquered,Is.False);Assert.That(city.Enemy,Is.SameAs(original));
        }

        [Test] public void WhiteCityRequiresSixActualFireAttackAndStrongerOriginalSpellCanConquer()
        {
            var(p,c,city,turn)=Start(CityColor.White);var original=city.Enemy;
            Play(p,c,"magic_009");var first=CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged);
            Assert.That(first.RequiredPower,Is.EqualTo(6));Assert.That(first.EffectivePower,Is.EqualTo(5));Assert.That(first.Kills,Is.Zero);
            Assert.That(c.RangedPool,Is.Zero);Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            Play(p,c,"magic_009",true);var second=CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged);
            Assert.That(second.Kills,Is.EqualTo(1));Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(4));
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,false);
            Assert.That(city.Conquered,Is.True);Assert.That(original.Armor,Is.EqualTo(5));Assert.That(c.CombatSite,Is.Null);
        }

        [TestCase(Element.Physical,6)] [TestCase(Element.Fire,8)] [TestCase(Element.Ice,8)] [TestCase(Element.ColdFire,7)]
        public void BlueCityBlockAndDamageUseSameElementSpecificBonus(Element element,int required)
        {
            // Explicit boundary fixture, not an original-token claim; all four element branches.
            var guard=new Monster("city_element_boundary",5,6,element,0,Array.Empty<Ability>());
            var site=new PlaceState(PlaceType.City){CityColor=CityColor.Blue,Enemy=guard};
            var c=new ActionContext{CombatSite=site,InBattle=true};var p=new PlayerState();
            c.CombatPower.AddBlock(required-1,Element.ColdFire);
            var failed=CardCombatActions.Block(p,c,new[]{guard},0);
            Assert.That(failed.RequiredPower,Is.EqualTo(required));Assert.That(p.Wounds,Is.EqualTo((required+1)/2));
            p=new PlayerState();c.CombatPower.AddBlock(required,Element.ColdFire);
            var blocked=CardCombatActions.Block(p,c,new[]{guard},0);
            Assert.That(blocked.RequiredPower,Is.EqualTo(required));Assert.That(p.Wounds,Is.Zero);
            Assert.That(c.WasBlockedThisBattle(guard),Is.True,"格挡记录须使用原实体，而非加成临时副本");
        }

        [Test] public void CityBonusDoesNotApplyToForeignSameIdEnemyOrOtherSite()
        {
            foreach(var color in new[]{CityColor.White,CityColor.Blue,CityColor.Red,CityColor.Green})
            {
                var(p,c,city,turn)=Start(color);var other=city.Enemy with {};
                Assert.That(CombatMath.EffectiveArmor(other,1,c),Is.EqualTo(5));
                Assert.That(CityCombatRules.DefenseProfile(other,c),Is.SameAs(other));
                c.CombatSite=new PlaceState(PlaceType.Keep){CityColor=color,Enemy=city.Enemy};
                Assert.That(CityCombatRules.DefenseProfile(city.Enemy,c),Is.SameAs(city.Enemy));
                Assert.That(CombatMath.EffectiveArmor(city.Enemy,0,c),Is.EqualTo(5));
            }
        }

        [Test] public void GreenCityPoisonReachesOriginalUnitAndRemainingHeroDamage()
        {
            var(p,c,city,turn)=Start(CityColor.Green);
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(unit);
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c,new[]{new UnitDamageAllocation(0,new[]{unit})});
            Assert.That(unit.Wounds,Is.EqualTo(2));Assert.That(p.Wounds,Is.EqualTo(2));
            Assert.That(p.Deck.DiscardPile.Count(d=>d.Type==CardType.Wound),Is.EqualTo(2));
        }
    }
}
