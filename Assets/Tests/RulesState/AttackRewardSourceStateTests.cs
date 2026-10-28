using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class AttackRewardSourceStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor=1) => new("target",armor,0,Element.Physical,3,Array.Empty<Ability>());
        private static DeedCard Play(PlayerState p,ActionContext c,string id,bool strong,int option=0)
        {
            var data=(id.StartsWith("basic")?CardJsonLoader.LoadBasicActions():CardJsonLoader.LoadAdvancedActions()).Single(x=>x.Id==id);
            var card=new DeedCard(id,CardType.Action);p.Deck.Hand.Add(card);CardPlayActions.Play(p,c,card,data,strong,option);return card;
        }
        private static PlayerState Player()
        {
            var p=new PlayerState();foreach(ManaColor color in Enum.GetValues(typeof(ManaColor)))p.Mana.AddToken(color,3);return p;
        }

        [TestCase(false,0,3,0,6)] [TestCase(false,1,2,2,6)] [TestCase(true,0,6,0,6)] [TestCase(true,1,4,2,8)]
        public void ChivalryPrintedChoiceAndRewardApplyPerDefeatedEnemy(bool strong,int option,int power,int reputation,int fame)
        {
            var p=Player();var c=new ActionContext{InBattle=true};Play(p,c,"advanced_card_034",strong,option);
            Assert.That(c.MeleePool,Is.EqualTo(power));
            var r=CardCombatActions.Attack(p,c,new[]{Enemy(),Enemy()},new[]{0,1},Phase.Melee);
            Assert.That(r.Kills,Is.EqualTo(2));Assert.That(p.Reputation,Is.EqualTo(reputation));Assert.That(p.Fame,Is.EqualTo(fame));
            Assert.That(c.MeleePool,Is.Zero);
        }

        [TestCase(false,6)] [TestCase(true,10)]
        public void TwoOriginalChivalryCardsBothContributeTheirPrintedRewards(bool strong,int fame)
        {
            var p=Player();var c=new ActionContext{InBattle=true};Play(p,c,"advanced_card_034",strong,1);Play(p,c,"advanced_card_034",strong,1);
            CardCombatActions.Attack(p,c,new[]{Enemy(),Enemy()},new[]{0,1},Phase.Melee);
            Assert.That(p.Reputation,Is.EqualTo(4));Assert.That(p.Fame,Is.EqualTo(fame));
        }

        [TestCase(Phase.Ranged)] [TestCase(Phase.Melee)]
        public void AxeRewardIsOnceForItsAttackEvenWhenSeveralEnemiesDie(Phase phase)
        {
            var p=Player();var c=new ActionContext{InBattle=true};Play(p,c,"basic_card_011",true);
            CardCombatActions.Attack(p,c,new[]{Enemy(),Enemy()},new[]{0,1},phase);
            Assert.That(p.Fame,Is.EqualTo(7));Assert.That(c.RangedPool,Is.Zero);
        }

        [Test]
        public void TwoAxesRewardBothCardsAndFailedAxeDoesNotRewardLaterPlainAttack()
        {
            var p=Player();var c=new ActionContext{InBattle=true};Play(p,c,"basic_card_011",true);Play(p,c,"basic_card_011",true);
            CardCombatActions.Attack(p,c,new[]{Enemy(),Enemy()},new[]{0,1},Phase.Ranged);Assert.That(p.Fame,Is.EqualTo(8));
            UnitDamageResolver.EndBattle(p,c);c=new ActionContext{InBattle=true};
            Play(p,c,"basic_card_011",true);
            var reserved=c.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,new[]{Enemy(4)},new[]{0},Phase.Ranged));
            Assert.That(c.CombatPower.Attacks.Single(),Is.SameAs(reserved));
            Play(p,c,"basic_card_011",false,1);CardCombatActions.Attack(p,c,new[]{Enemy()},new[]{0},Phase.Ranged,new[]{c.CombatPower.Attacks.Last()});
            Assert.That(c.CombatPower.Attacks.Single(),Is.SameAs(reserved));
            Assert.That(p.Fame,Is.EqualTo(11));
        }

        [Test]
        public void FailedAxeRewardCannotAttachToFollowingPlainAxe()
        {
            var p=Player();var c=new ActionContext{InBattle=true};Play(p,c,"basic_card_011",true);
            var reserved=c.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,new[]{Enemy(4)},new[]{0},Phase.Ranged));Assert.That(p.Fame,Is.Zero);
            Assert.That(c.CombatPower.Attacks.Single(),Is.SameAs(reserved));
            Play(p,c,"basic_card_011",false,1);CardCombatActions.Attack(p,c,new[]{Enemy()},new[]{0},Phase.Ranged,new[]{c.CombatPower.Attacks.Last()});
            Assert.That(c.CombatPower.Attacks.Single(),Is.SameAs(reserved));
            Assert.That(p.Fame,Is.EqualTo(3));
        }

        [TestCase("basic_card_011")] [TestCase("advanced_card_034")]
        public void BattleEndExpiresUnspentRewardAndRetainsActualPlayedCard(string id)
        {
            var p=Player();var c=new ActionContext{InBattle=true};var card=Play(p,c,id,true,id.StartsWith("advanced")?1:0);
            UnitDamageResolver.EndBattle(p,c);c.InBattle=true;Play(p,c,"basic_card_011",false,1);
            CardCombatActions.Attack(p,c,new[]{Enemy()},new[]{0},Phase.Ranged);
            Assert.That(p.Fame,Is.EqualTo(3));Assert.That(p.Reputation,Is.Zero);Assert.That(p.Deck.PlayedCards.Contains(card),Is.True);
        }

        [TestCase("advanced_card_034",false,-1)] [TestCase("advanced_card_034",true,2)]
        [TestCase("basic_card_011",false,2)] [TestCase("basic_card_011",true,1)]
        public void InvalidChoiceCannotPayManaOrMoveActualCard(string id,bool strong,int option)
        {
            var p=Player();var c=new ActionContext{InBattle=true};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,id,strong,option));
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.Hand.Count,Is.EqualTo(1));
            Assert.That(c.MeleePool+c.RangedPool+c.MovementPool,Is.Zero);
            Assert.That(p.Mana.Tokens.Values.All(v=>v==3),Is.True);
        }
    }
}
