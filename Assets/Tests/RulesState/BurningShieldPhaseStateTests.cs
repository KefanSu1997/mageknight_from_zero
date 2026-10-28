using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BurningShieldPhaseStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor,int attack,params Ability[] traits) => new("target",armor,attack,Element.Physical,3,traits);
        private static (PlayerState p,ActionContext c,GameState g,TurnMachine t,List<Monster> enemies,DeedCard card) Setup(bool strong,int attack, params Ability[] traits)
        {
            var p=new PlayerState();p.Mana.AddToken(ManaColor.Red,2);p.Mana.AddToken(ManaColor.Black,2);
            var c=new ActionContext{DayPart=DayPart.Night};var g=new GameState();g.Players.Add(p);
            var t=new TurnMachine(g);t.BeginBattle(c);var enemies=new List<Monster>{Enemy(9,attack,traits),Enemy(4,0)};c.Enemies=enemies;
            t.ApplyRanged(enemies,Array.Empty<AttackAllocation>(),c);
            var s=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_008");var card=new DeedCard(s.Id,CardType.Spell);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost),strong);
            t.SetBlock(4,Element.Fire);return(p,c,g,t,enemies,card);
        }
        private static BattleResult Defend(TurnMachine t,ActionContext c,List<Monster> enemies)
            => t.ResolveDefense(enemies,c.CombatPower.AvailableBlocks(0),c);

        [TestCase(Ability.PhysicalResist,4,1)] [TestCase(Ability.FireResist,2,0)] [TestCase(Ability.MagicResist,4,1)]
        public void SuccessfulBlockRewardKeepsFireAndCanSelectAnotherEnemy(Ability targetResistance,int effective,int kills)
        {
            var(p,c,g,t,enemies,card)=Setup(false,4,Ability.MagicResist);enemies[1]=Enemy(4,0,targetResistance);
            Assert.That(Defend(t,c,enemies).TotalWounds,Is.Zero);Assert.That(g.CurrentPhase,Is.EqualTo(Phase.Melee));
            Assert.That(c.InBattle,Is.True);Assert.That(c.MeleePool,Is.EqualTo(4));Assert.That(c.BlockPool,Is.Zero);
            Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Fire));
            var r=t.Attack(new[]{1},c);Assert.That(r.EffectivePower,Is.EqualTo(effective));Assert.That(r.Kills,Is.EqualTo(kills));
            Assert.That(p.Fame,Is.EqualTo(3*kills));Assert.That(p.Wounds,Is.Zero);Assert.That(c.MeleePool,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>t.Attack(new[]{1},c));Assert.That(p.Fame,Is.EqualTo(3*kills));
            t.FinishBattle(c);Assert.That(g.CurrentPhase,Is.EqualTo(Phase.End));Assert.That(c.InBattle,Is.False);
            c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Night,new ManaSource(1,()=>DayPart.Night),c);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }

        [Test]
        public void FailedBlockGivesNoRewardAndEnemyDamageIsNotRepeatedByAttackAttempt()
        {
            var(p,c,g,t,enemies,card)=Setup(false,5);
            Assert.That(Defend(t,c,enemies).TotalWounds,Is.EqualTo(3));Assert.That(c.MeleePool,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>t.Attack(new[]{1},c));Assert.That(p.Wounds,Is.EqualTo(3));
            t.FinishBattle(c);Assert.That(p.Fame,Is.Zero);
        }

        [TestCase(Ability.PhysicalResist,true)] [TestCase(Ability.FireResist,false)] [TestCase(Ability.MagicResist,false)]
        public void ExplodingShieldKillTracksDefeatedTargetAndDoesNotGenerateAttack(Ability trait,bool killed)
        {
            var(p,c,g,t,enemies,card)=Setup(true,4,trait);var r=Defend(t,c,enemies);
            Assert.That(r.KilledIndices.Count,Is.EqualTo(killed?1:0));Assert.That(p.Fame,Is.EqualTo(killed?3:0));Assert.That(c.MeleePool,Is.Zero);
            if(killed)
            {
                enemies[1]=Enemy(2,0);
                var fury=new DeedCard("basic_card_021",CardType.Action);p.Deck.Hand.Add(fury);
                CardPlayActions.Play(p,c,fury,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==fury.Id));
                Assert.Throws<InvalidOperationException>(()=>t.Attack(new[]{0},c));Assert.That(c.MeleePool,Is.EqualTo(2));
                Assert.That(t.Attack(new[]{1},c).Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(6));
            }
            t.FinishBattle(c);Assert.That(c.MeleePool,Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));
        }

        [Test]
        public void RewardCanBeForgoneAndCannotEscapeToNextBattleOrAnotherContext()
        {
            var(p,c,g,t,enemies,card)=Setup(false,4);Defend(t,c,enemies);
            Assert.Throws<InvalidOperationException>(()=>t.ResolveDefense(enemies,Array.Empty<BlockAllocation>(),c));
            Assert.Throws<InvalidOperationException>(()=>t.Attack(new[]{1},new ActionContext()));
            Assert.That(c.MeleePool,Is.EqualTo(4));Assert.That(p.Wounds,Is.Zero);
            t.FinishBattle(c);Assert.That(c.MeleePool,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>t.FinishBattle(c));
            t.BeginBattle(c);Assert.Throws<InvalidOperationException>(()=>t.Attack(new[]{1},c));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }
    }
}
