using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class WeaponBaseStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static void Play(PlayerState p, ActionContext c, DeedCard weapon)
            => CardPlayActions.PlayWeaponBase(p, c, weapon, CardJsonLoader.LoadItems().Single(x => x.Id == weapon.Id));
        private static (PlayerState p, ActionContext c, DeedCard weapon) Fixture(bool bow)
        {
            var p = new PlayerState(); var c = new ActionContext { InBattle = true, CombatPhase = bow ? Phase.Ranged : Phase.Melee };
            var weapon = new DeedCard(bow ? "items_016" : "items_008", CardType.Artifact);
            p.Deck.Hand.Add(weapon);
            p.Deck.Hand.Add(new DeedCard("basic_card_021", CardType.Action));
            p.Deck.Hand.Add(new DeedCard("basic_card_021", CardType.Action));
            p.Deck.Hand.Add(new DeedCard("wound", CardType.Wound));
            return (p,c,weapon);
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(false,2)]
        [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)]
        public void DiscardExactInstancesCreatesOneContributionAndConservesCards(bool bow, int count)
        {
            var (p,c,w) = Fixture(bow); var selected = p.Deck.Hand.Skip(1).Take(count).ToArray();
            c.CardsToDiscard.AddRange(selected); Play(p,c,w);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(w));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(3-count)); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(count));
            foreach (var card in selected) Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x,card)), Is.True);
            Assert.That(c.CombatPower.Attacks.Count, Is.EqualTo(count == 0 ? 0 : 1));
            Assert.That(bow ? c.RangedPool : c.MeleePool, Is.EqualTo(count*(bow?2:3)));
            Assert.That(c.CardsToDiscard, Is.Empty); Assert.That(p.Wounds, Is.EqualTo(1));
            c.SkipDraw=true; new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(count+1));
            Assert.That(c.SwordFamePerKill+c.BowFamePerKill, Is.Zero);
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(false,2)] [TestCase(false,3)]
        [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)] [TestCase(true,3)]
        public void InvalidSecondTargetNeverPartiallyDiscardsOrCreatesAttack(bool bow, int failure)
        {
            var(p,c,w)=Fixture(bow); var real=p.Deck.Hand[1]; c.CardsToDiscard.Add(real);
            c.CardsToDiscard.Add(failure switch { 0=>real, 1=>new DeedCard(real.Id,real.Type), 2=>p.Deck.Hand[3], _=>w });
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,w));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(4)); Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(p.Deck.PlayedCards,Is.Empty); Assert.That(c.CombatPower.Attacks,Is.Empty);
            Assert.That(c.SwordFamePerKill+c.BowFamePerKill,Is.Zero); Assert.That(c.CardsToDiscard.Count,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void SameIdForeignWeaponAndAlreadyPlayedWeaponAreRejected(bool bow)
        {
            var(p,c,w)=Fixture(bow);
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,new DeedCard(w.Id,w.Type)));
            Play(p,c,w); Assert.Throws<InvalidOperationException>(()=>Play(p,c,w));
            Assert.That(c.SwordFamePerKill+c.BowFamePerKill,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void MultipleKillsAndLaterAttackInSamePhaseEachGrantOneBonusPerEnemy(bool bow)
        {
            var(p,c,w)=Fixture(bow); c.CardsToDiscard.AddRange(p.Deck.Hand.Skip(1).Take(2)); Play(p,c,w);
            // 同场第二次攻击继续使用原敌人列表，避免把新敌人放到已击败的索引0。
            var enemies = new[] { Enemy(2), Enemy(2), Enemy(2) };
            var result=CardCombatActions.Attack(p,c,enemies,new[]{0,1},bow?Phase.Ranged:Phase.Melee);
            Assert.That(result.Kills,Is.EqualTo(2)); Assert.That(result.Fame,Is.EqualTo(8));
            c.CombatPower.AddAttack(2,type:bow?AttackType.Ranged:AttackType.Melee);
            Assert.That(CardCombatActions.Attack(p,c,enemies,new[]{2},bow?Phase.Ranged:Phase.Melee).Fame,Is.EqualTo(4));
            Assert.That(p.Fame,Is.EqualTo(12));
        }

        [TestCase(false)] [TestCase(true)]
        public void PhaseChangeAndBattleEndRespectDifferentRewardLifetimes(bool bow)
        {
            var(p,c,w)=Fixture(bow); Play(p,c,w);
            CardCombatActions.EnterPhase(c,Phase.Block);
            c.CombatPower.AddAttack(2);
            Assert.That(CardCombatActions.Attack(p,c,new[]{Enemy(2)},new[]{0},Phase.Melee).Fame,Is.EqualTo(bow?3:4));
            UnitDamageResolver.EndBattle(p,c);
            var nextBattle=new GameState();nextBattle.Players.Add(p);new TurnMachine(nextBattle).BeginBattle(c);
            c.CombatPower.AddAttack(2,type:AttackType.Ranged);
            Assert.That(CardCombatActions.Attack(p,c,new[]{Enemy(2)},new[]{0},Phase.Ranged).Fame,Is.EqualTo(bow?3:4));
            UnitDamageResolver.EndBattle(p,c);
            c.SkipDraw=true; new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            var nextTurnBattle=new GameState();nextTurnBattle.Players.Add(p);new TurnMachine(nextTurnBattle).BeginBattle(c);
            c.CombatPower.AddAttack(2);
            Assert.That(CardCombatActions.Attack(p,c,new[]{Enemy(2)},new[]{0},Phase.Melee).Fame,Is.EqualTo(3));
        }
        private static Monster Enemy(int armor) => new Monster("fixture",armor,0,Element.Physical,3,Array.Empty<Ability>());
    }
}
