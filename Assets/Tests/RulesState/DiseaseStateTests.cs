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
    public sealed class DiseaseStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Cast(PlayerState p, ActionContext c)
        {
            var s = CardJsonLoader.LoadSpells().Single(x => x.Id == "magic_001");
            var card = new DeedCard(s.Id, CardType.Spell); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath,
                s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost), true);
            return card;
        }

        private static (PlayerState p, ActionContext c) Setup()
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Green); p.Mana.AddToken(ManaColor.Black);
            return (p, new ActionContext { InBattle = true, DayPart = DayPart.Night });
        }

        private static Monster Enemy(params Ability[] abilities) => new("target", 5, 2, Element.Physical, 3, abilities);

        [TestCase(false)] [TestCase(true)]
        public void CastBeforeOrAfterSuccessfulBlockMakesActualMeleeNeedOne(bool castAfter)
        {
            var (p,c) = Setup(); var enemy = Enemy();
            if (!castAfter) Cast(p,c);
            c.CombatPower.AddBlock(2,Element.Physical); Assert.That(CardCombatActions.Block(p,c,enemy).Wounds,Is.Zero);
            if (castAfter) Cast(p,c);
            c.CombatPower.AddAttack(1,Element.Physical);
            var result = CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(result.RequiredPower,Is.EqualTo(1)); Assert.That(result.Kills,Is.EqualTo(1)); Assert.That(p.Fame,Is.EqualTo(3));
            Assert.That(c.MeleePool+c.BlockPool,Is.Zero); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.Zero);
        }

        [TestCase(Ability.MagicResist,5,0)] [TestCase(Ability.FireResist,1,1)] [TestCase(Ability.IceResist,1,1)]
        public void ArcaneImmunityRejectsDiseaseButElementResistanceDoesNot(Ability ability,int required,int kills)
        {
            var(p,c)=Setup();var enemy=Enemy(ability);Cast(p,c);c.CombatPower.AddBlock(2,Element.Physical);
            CardCombatActions.Block(p,c,enemy);c.CombatPower.AddAttack(1,Element.Physical);
            var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(r.RequiredPower,Is.EqualTo(required));Assert.That(r.Kills,Is.EqualTo(kills));Assert.That(c.MeleePool,Is.Zero);
        }

        [TestCase(2,1,1)] [TestCase(3,2,2)]
        public void PartialBlockDoesNotInfect(int attack,int block,int wounds)
        {
            var(p,c)=Setup();var enemy=Enemy() with { Attack=attack };Cast(p,c);c.CombatPower.AddBlock(block,Element.Physical);
            // Partial blocks prevent none of the attack: armor 2 takes ceil(attack / 2) wounds.
            Assert.That(CardCombatActions.Block(p,c,enemy).Wounds,Is.EqualTo(wounds));c.CombatPower.AddAttack(1,Element.Physical);
            var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(r.RequiredPower,Is.EqualTo(5));Assert.That(r.Kills,Is.Zero);
        }

        [Test]
        public void IdenticalUnblockedEnemyDoesNotBorrowAnotherInstancesDisease()
        {
            var(p,c)=Setup();var first=Enemy();var second=first with { };Cast(p,c);
            c.CombatPower.AddBlock(2,Element.Physical);CardCombatActions.Block(p,c,new[]{first,second},0);
            c.CombatPower.AddAttack(1,Element.Physical);
            var r=CardCombatActions.Attack(p,c,new[]{second,first},new[]{0},Phase.Melee);
            Assert.That(r.RequiredPower,Is.EqualTo(5));Assert.That(r.Kills,Is.Zero);
        }

        [Test]
        public void BattleEndRemovesDiseaseWithoutOverwritingOtherArmorReduction()
        {
            var(p,c)=Setup();var enemy=Enemy();c.ArmorReduction[0]=2;Cast(p,c);
            c.CombatPower.AddBlock(2,Element.Physical);CardCombatActions.Block(p,c,enemy);
            UnitDamageResolver.EndBattle(p,c);Assert.That(c.BlockedArmorOne,Is.False);Assert.That(c.ArmorReduction[0],Is.EqualTo(2));
            c.InBattle=true;c.CombatPower.AddAttack(1,Element.Physical);
            var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(r.RequiredPower,Is.EqualTo(3));Assert.That(r.Kills,Is.Zero);
        }

        [Test]
        public void EndTurnExpiresDiseaseAndDiscardsActualSource()
        {
            var(p,c)=Setup();c.SkipDraw=true;var card=Cast(p,c);var enemy=Enemy();
            c.CombatPower.AddBlock(2,Element.Physical);CardCombatActions.Block(p,c,enemy);
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,new ManaSource(1,()=>DayPart.Night),c);
            Assert.That(c.BlockedArmorOne,Is.False);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }

        [TestCase(ManaColor.Green)] [TestCase(ManaColor.Black)]
        public void MissingEitherPrintedColorRejectsWholeCost(ManaColor missing)
        {
            var p=new PlayerState();var kept=missing==ManaColor.Green?ManaColor.Black:ManaColor.Green;p.Mana.AddToken(kept);
            var c=new ActionContext{InBattle=true,DayPart=DayPart.Night};
            Assert.Throws<InvalidOperationException>(()=>Cast(p,c));Assert.That(p.Mana.Tokens[kept],Is.EqualTo(1));
            Assert.That(c.BlockedArmorOne,Is.False);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void DayOrNoCombatCannotSpendManaOrEnableDisease(bool outside)
        {
            var(p,c)=Setup();if(outside)c.InBattle=false;else c.DayPart=DayPart.Day;
            Assert.Throws<InvalidOperationException>(()=>Cast(p,c));Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(1));Assert.That(c.BlockedArmorOne,Is.False);
        }

        [TestCase(Phase.Ranged)] [TestCase(Phase.Melee)]
        public void OrdinaryArmorReductionStillWorksWithoutDisease(Phase phase)
        {
            var(p,c)=Setup();var enemy=Enemy();c.ArmorReduction[0]=2;
            c.CombatPower.AddAttack(3,Element.Physical,phase==Phase.Ranged?AttackType.Ranged:AttackType.Melee);
            var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},phase);
            Assert.That(r.RequiredPower,Is.EqualTo(3));Assert.That(r.Kills,Is.EqualTo(1));
        }
    }
}
