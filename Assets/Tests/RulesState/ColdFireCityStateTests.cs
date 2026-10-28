using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ColdFireCityStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static void Play(PlayerState p,ActionContext c,string id,bool enhanced=false,int option=0)
        {
            ActionCardData data;
            if(id.StartsWith("magic_"))
            {
                var card=CardJsonLoader.LoadSpells().Single(s=>s.Id==id);
                data=new ActionCardData(card.Id,card.Name,card.Set,card.ImagePath,card.EnImagePath,card.Top.Effect,card.Bottom.Effect,card.Top.ManaCost);
            }
            else data=CardJsonLoader.LoadBasicActions().Single(s=>s.Id==id);
            CardPlayActions.Play(p,c,p.Deck.Hand.First(s=>s.Id==id),data,enhanced,option);
        }
        private static (PlayerState p,ActionContext c,PlaceState city,TurnMachine turn) Start(CityColor color,int preexistingWounds=0)
        {
            var guard=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_008");
            var supply=new EnemyTokenSupply(new[]{guard});
            var city=new PlaceState(PlaceType.City){CityColor=color,Enemy=supply.Draw(EnemyTokenColor.White)};
            var p=new PlayerState{Wounds=preexistingWounds};var c=new ActionContext{DayPart=DayPart.Night,EnemySupply=supply,MovementMap=new MapState()};
            for(int i=0;i<=1;i++)c.MovementMap.Placed[new AxialCoord(i,0)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Forest});
            c.MovementMap.Places[new AxialCoord(1,0)]=city;c.TravelPath.Add(new AxialCoord(1,0));
            foreach(var id in new[]{"magic_004","magic_010","basic_card_021","basic_card_021","magic_009"})
                p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            foreach(var colorMana in new[]{ManaColor.Green,ManaColor.Red,ManaColor.Black})p.Mana.AddToken(colorMana,2);
            Play(p,c,"magic_004",true);var game=new GameState();game.Players.Add(p);var turn=new TurnMachine(game);turn.BeginBattle(c);
            return(p,c,city,turn);
        }

        [Test] public void OriginalWhiteTokenMatchesItsActualPrintedSymbols()
        {
            var token=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_008");
            Assert.That(token.TokenColor,Is.EqualTo(EnemyTokenColor.White));Assert.That(token.AttackElement,Is.EqualTo(Element.ColdFire));
            Assert.That(token.Attack,Is.EqualTo(4));Assert.That(token.Armor,Is.EqualTo(8));Assert.That(token.Fame,Is.EqualTo(8));
            Assert.That(token.Abilities,Is.EquivalentTo(new[]{Ability.PhysicalResist,Ability.Brutal,Ability.Poison}));
        }

        [TestCase(CityColor.None,false,4,0)]
        [TestCase(CityColor.Blue,false,5,5)]
        [TestCase(CityColor.Blue,true,5,0)]
        public void OriginalFlameSurgeAndFuryBlockActualColdFireCityGuard(CityColor color,bool addFury,int required,int wounds)
        {
            var(p,c,city,turn)=Start(color);var original=city.Enemy;
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);
            Play(p,c,"magic_010",true,1);if(addFury)Play(p,c,"basic_card_021",false,1);
            var blocks=c.CombatPower.AvailableBlocks(0).Select(b=>b with{TargetIndex=0}).ToArray();
            Assert.That(blocks.Sum(b=>b.Value),Is.EqualTo(addFury?11:9));
            Assert.That(CombatMath.EffectiveBlock(Element.ColdFire,blocks),Is.EqualTo(addFury?5:4));
            Assert.That(AbilityRules.RequiredBlock(CityCombatRules.DefenseProfile(original,c)),Is.EqualTo(required));
            turn.SetBlock(blocks.Sum(b=>b.Value),Element.Fire);turn.ResolveDefense(c.Enemies,blocks,c);
            Assert.That(p.Wounds,Is.EqualTo(wounds));Assert.That(p.Deck.DiscardPile.Count(d=>d.Type==CardType.Wound),Is.EqualTo(wounds));
            Assert.That(c.WasBlockedThisBattle(original),Is.EqualTo(wounds==0));
            Assert.That(c.BattleWoundsReceived,Is.EqualTo(wounds));Assert.That(c.IsKnockedOut,Is.EqualTo(wounds>=5));
            if(wounds>=5)Assert.That(p.Deck.Hand.All(d=>d.Type==CardType.Wound),Is.True,"昏迷必须立即弃掉非伤牌");
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
            if(addFury)
            {
                Play(p,c,"basic_card_021",true);var attack=turn.Attack(new[]{0},c);
                Assert.That(attack.EffectivePower,Is.EqualTo(2));Assert.That(attack.RequiredPower,Is.EqualTo(8));Assert.That(attack.Kills,Is.Zero);
            }
            turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,true);
            Assert.That(city.Enemy,Is.SameAs(original));Assert.That(c.EnemySupply.InUse.Single(),Is.SameAs(original));
            Assert.That(c.EnemySupply.DiscardPile,Is.Empty);Assert.That(p.Fame,Is.Zero);Assert.That(p.Position.Q,Is.Zero);
            Assert.That(original.Attack,Is.EqualTo(4));Assert.That(c.CombatSite,Is.Null);
        }

        [Test] public void OriginalFirestormBypassesPhysicalResistanceAndPreventsTheAttack()
        {
            var(p,c,city,turn)=Start(CityColor.Blue);var original=city.Enemy;
            Play(p,c,"magic_009",true);var hit=CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Ranged);
            Assert.That(hit.EffectivePower,Is.EqualTo(8));Assert.That(hit.Kills,Is.EqualTo(1));
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,false);
            Assert.That(p.Wounds,Is.EqualTo(1),"火焰风暴自伤，不额外触发敌人的残暴或剧毒");
            Assert.That(p.Deck.DiscardPile.Count(d=>d.Type==CardType.Wound),Is.Zero);
            Assert.That(p.Fame,Is.EqualTo(8));Assert.That(city.Conquered,Is.True);
            Assert.That(c.EnemySupply.DiscardPile.Single(),Is.SameAs(original));Assert.That(city.CityShields.Count,Is.EqualTo(1));
        }

        [Test] public void OriginalFirestormSelfWoundTriggersKnockoutAfterItIsPlayedAndStillSuppliesAttack()
        {
            var(p,c,city,turn)=Start(CityColor.None,3);p.ExtraHandBonus=8;p.TacticHandBonus=4;
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);
            Assert.That(c.BattleWoundsReceived,Is.EqualTo(4));Assert.That(c.IsKnockedOut,Is.False);
            var fire=p.Deck.Hand.Single(d=>d.Id=="magic_009");Play(p,c,"magic_009",true);
            Assert.That(c.BattleWoundsReceived,Is.EqualTo(5));Assert.That(c.IsKnockedOut,Is.True);
            Assert.That(p.Deck.PlayedCards.Any(d=>ReferenceEquals(d,fire)),Is.True);
            Assert.That(p.Deck.Hand.All(d=>d.Type==CardType.Wound),Is.True);Assert.That(p.Wounds,Is.EqualTo(8));
            Assert.That(turn.Attack(new[]{0},c).Kills,Is.EqualTo(1),"昏迷仍能使用已产生的攻击");
            turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,false);Assert.That(city.Conquered,Is.True);
            turn.BeginBattle(c);Assert.That(c.IsKnockedOut,Is.False);Assert.That(c.BattleWoundsReceived,Is.Zero);
        }

        [Test] public void LaterEnemiesStillDealDamageAfterKnockoutAndPoisonDiscardDoesNotCountTwice()
        {
            var(p,c,city,turn)=Start(CityColor.Blue);
            var extra=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");city.AdditionalGuards.Add(extra);c.Enemies.Add(extra);
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);
            Assert.That(p.Wounds,Is.EqualTo(8));Assert.That(c.BattleWoundsReceived,Is.EqualTo(8));Assert.That(c.IsKnockedOut,Is.True);
            Assert.That(p.Deck.DiscardPile.Count(d=>d.Type==CardType.Wound),Is.EqualTo(5));
            Assert.That(p.Deck.Hand.All(d=>d.Type==CardType.Wound),Is.True);
        }

        [Test] public void EnteringBattleWithExistingWoundsDoesNotCausePrematureKnockout()
        {
            var(p,c,city,turn)=Start(CityColor.Blue,7);
            Assert.That(c.IsKnockedOut,Is.False);Assert.That(c.BattleWoundsReceived,Is.Zero);
            Assert.That(p.Wounds,Is.EqualTo(7));Assert.That(p.Deck.Hand.Count(d=>d.Type!=CardType.Wound),Is.EqualTo(4));
        }

        [TestCase(0,7,0)]
        [TestCase(1,8,1)]
        public void KnockedOutHeroCanUseOriginalUnitAndOwnedSkillWithoutBypassingTheirLimits(int option,int effective,int kills)
        {
            var(p,c,city,turn)=Start(CityColor.Blue,3);
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_003"));p.Units.Add(unit);
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[2];p.Skills.Add(skill);
            turn.ApplyRanged(c.Enemies,Array.Empty<AttackAllocation>(),c);turn.SetBlock(0,Element.Physical);
            turn.ResolveDefense(c.Enemies,Array.Empty<BlockAllocation>(),c);
            Assert.That(c.IsKnockedOut,Is.True);Assert.That(c.BattleWoundsReceived,Is.EqualTo(5));
            Assert.That(p.Deck.Hand.All(d=>d.Type==CardType.Wound),Is.True);Assert.That(p.Wounds,Is.EqualTo(8));
            CardUnitActions.Use(p,c,unit,1);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));
            Assert.That(unit.IsReady,Is.False);Assert.That(unit.Wounds,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(p,c,unit,1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));
            SkillActions.Use(p,c,skill,option);Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,option));
            Assert.That(c.RangedPool,Is.EqualTo(6));Assert.That(c.MeleePool,Is.EqualTo(2));
            var hit=turn.Attack(new[]{0},c);Assert.That(hit.EffectivePower,Is.EqualTo(effective));Assert.That(hit.Kills,Is.EqualTo(kills));
            turn.FinishBattle(c);CardTravelActions.FinishTunnelAssault(p,c,kills==0);
            Assert.That(city.Conquered,Is.EqualTo(kills==1));Assert.That(p.Fame,Is.EqualTo(8*kills));
            Assert.That(c.EnemySupply.InUse.Count,Is.EqualTo(1-kills));Assert.That(c.EnemySupply.DiscardPile.Count,Is.EqualTo(kills));
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(unit.IsReady,Is.False,"新回合不重整已用部队");
        }
    }
}
