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
    public sealed class WeaponBurnStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static void Burn(PlayerState p, ActionContext c, string id)
        {
            var w = new DeedCard(id,CardType.Artifact); p.Deck.Hand.Add(w);
            CardPlayActions.PlayWeapon(p,c,w,CardJsonLoader.LoadItems().Single(x=>x.Id==id),true);
            Assert.That(p.Deck.RemovedCards.Last(),Is.SameAs(w));
        }
        private static Monster Enemy(int armor, params Ability[] abilities) => new Monster("fixture",armor,0,Element.Physical,3,abilities);

        [TestCase(Phase.Melee,8)] [TestCase(Phase.Ranged,5)]
        public void SwordOnlyDoublesPhysicalInAttackPhaseAndKeepsElementalResistance(Phase phase,int effective)
        {
            var p=new PlayerState();var c=new ActionContext();Burn(p,c,"items_008");
            c.CombatPower.AddAttack(3,type:AttackType.Ranged);c.CombatPower.AddAttack(4,Element.Fire,AttackType.Ranged);
            var r=CardCombatActions.Attack(p,c,new[]{Enemy(8,Ability.PhysicalResist,Ability.FireResist)},new[]{0},phase);
            Assert.That(r.EffectivePower,Is.EqualTo(effective)); Assert.That(r.Kills,Is.EqualTo(effective>=8?1:0));
            Assert.That(p.Fame,Is.EqualTo(effective>=8?4:0)); Assert.That(c.IgnoreResist,Is.False);
        }

        [TestCase(AttackType.Ranged,0,6,false)] [TestCase(AttackType.Ranged,1,6,false)]
        [TestCase(AttackType.Ranged,2,3,true)] [TestCase(AttackType.Siege,0,3,true)] [TestCase(AttackType.Siege,1,6,false)]
        public void BowChoiceIsExclusivePreservesElementAndFortification(AttackType type,int choice,int expected,bool siege)
        {
            var p=new PlayerState();var c=new ActionContext();Burn(p,c,"items_016");
            c.CombatPower.AddAttack(3,Element.Fire,type);CardCombatActions.ChooseStarBow(c,0,choice);
            Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Fire));
            Assert.That(CombatMath.IsSiege(c.CombatPower.Attacks.Single(),c),Is.EqualTo(siege));
            Assert.That(CombatMath.CanAttack(new[]{Enemy(1,Ability.Fortified)},new[]{0},c.CombatPower.Attacks,Phase.Ranged,c),Is.EqualTo(siege));
            var r=CardCombatActions.Attack(p,c,new[]{Enemy(1,Ability.FireResist)},new[]{0},Phase.Ranged);
            Assert.That(r.EffectivePower,Is.EqualTo(expected/2)); Assert.That(r.Fame,Is.EqualTo(3));
        }

        [Test]
        public void BowChoicesArePerContributionAndNewAttackDefaultsToDouble()
        {
            var p=new PlayerState();var c=new ActionContext();Burn(p,c,"items_016");
            c.CombatPower.AddAttack(3,Element.Fire,AttackType.Ranged);c.CombatPower.AddAttack(4,Element.Ice,AttackType.Ranged);
            CardCombatActions.ChooseStarBow(c,0,2);
            Assert.That(c.CombatPower.Attacks[1].StarBowChoice,Is.Zero);
            var r=CardCombatActions.Attack(p,c,new[]{Enemy(11)},new[]{0},Phase.Ranged);
            Assert.That(r.EffectivePower,Is.EqualTo(11));Assert.That(r.Kills,Is.EqualTo(1));
            c.CombatPower.AddAttack(3,Element.Ice,AttackType.Ranged);
            Assert.That(CardCombatActions.Attack(p,c,new[]{Enemy(6)},new[]{0},Phase.Melee).EffectivePower,Is.EqualTo(6));
        }

        [TestCase(-1,1)] [TestCase(0,3)] [TestCase(1,2)] [TestCase(2,1)]
        public void InvalidBowChoiceDoesNotChangeAttackContributions(int index,int choice)
        {
            var p=new PlayerState();var c=new ActionContext();Burn(p,c,"items_016");
            c.CombatPower.AddAttack(3,type:AttackType.Ranged);c.CombatPower.AddAttack(4,type:AttackType.Siege);c.CombatPower.AddAttack(2);
            var before=c.CombatPower.Attacks.ToArray();
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.ChooseStarBow(c,index,choice));
            Assert.That(c.CombatPower.Attacks,Is.EqualTo(before));
        }

        [TestCase("items_008")] [TestCase("items_016")]
        public void BurnUsesExactInstanceCannotRepeatAndExpiresAtTurnEnd(string id)
        {
            var p=new PlayerState();var c=new ActionContext();var first=new DeedCard(id,CardType.Artifact);p.Deck.Hand.Add(first);
            Burn(p,c,id);var burned=p.Deck.RemovedCards.Single();Assert.That(p.Deck.Hand.Single(),Is.SameAs(first));
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayWeapon(p,c,burned,CardJsonLoader.LoadItems().Single(x=>x.Id==id),true));
            UnitDamageResolver.EndBattle(p,c);Assert.That(id=="items_008"?c.DoublePhysicalAttack:c.StarBowActive,Is.True);
            c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.DoublePhysicalAttack||c.IgnorePhysicalResist||c.StarBowActive,Is.False);
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(burned));Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.ChooseStarBow(c,0,1));
        }
    }
}
