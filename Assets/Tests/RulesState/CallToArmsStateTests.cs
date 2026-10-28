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
    public sealed class CallToArmsStateTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard card) Fixture(string unit = "army_2_006")
        {
            var p = new PlayerState();
            p.Mana.AddToken(ManaColor.White, 1);
            var c = new ActionContext { DayPart = DayPart.Day, UnitOffer = new List<UnitCard> { CardJsonLoader.LoadUnit(unit) } };
            var card = new DeedCard("magic_020", CardType.Spell);
            p.Deck.Hand.Add(card);
            var own = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            own.Exhaust(); p.Units.Add(own);
            return (p, c, card);
        }

        private static void Cast(PlayerState p, ActionContext c, DeedCard card)
        {
            var original = CardJsonLoader.LoadSpells().Single(s => s.Id == card.Id);
            CardPlayActions.Play(p,c,card,new ActionCardData(original.Id,original.Name,original.Set,original.ImagePath,
                original.EnImagePath,original.Top.Effect,original.Bottom.Effect,original.Top.ManaCost),false);
        }

        [TestCase(1,2,0)] [TestCase(2,0,2)]
        public void DelayedAbilityUsesActualOfferOnceWithoutRecruitment(int ability, int influence, int move)
        {
            var (p,c,card) = Fixture(); var offered = c.UnitOffer.Single(); var own = p.Units.Single();
            c.FamePerRecruit=2; c.ReputationPerRecruit=1;
            Cast(p,c,card);
            Assert.That(c.OfferedUnitUses,Is.EqualTo(1)); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White),Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card)); Assert.That(p.FreeSlots,Is.Zero);
            Assert.That(p.TempCommandSlots,Is.Zero); Assert.That(c.NoUnitDamage,Is.False);
            Assert.That(c.MovementPool+c.InfluencePool,Is.Zero);
            CardUnitActions.UseFromOffer(p,c,0,ability);
            Assert.That(c.InfluencePool,Is.EqualTo(influence)); Assert.That(c.MovementPool,Is.EqualTo(move));
            Assert.That(c.OfferedUnitUses,Is.Zero); Assert.That(c.UnitOffer.Single(),Is.SameAs(offered));
            Assert.That(p.Units.Single(),Is.SameAs(own)); Assert.That(own.IsReady,Is.False);
            Assert.That(c.HasRecruited,Is.False); Assert.That(p.Fame+p.Reputation,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.UseFromOffer(p,c,0,ability));
            c.SkipDraw=true; new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card)); Assert.That(c.UnitOffer.Single(),Is.SameAs(offered));
            Assert.That(c.MovementPool+c.InfluencePool,Is.Zero);
        }

        [TestCase(0)] [TestCase(1)]
        public void OriginalIceChoicePaysAbilityManaAndKeepsElement(int choice)
        {
            var(p,c,card)=Fixture("army_2_005"); c.InBattle=true; p.Mana.AddToken(ManaColor.Blue,1);
            Cast(p,c,card); CardUnitActions.UseFromOffer(p,c,0,1,choice);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue),Is.Zero);
            Assert.That(c.MeleePool,Is.EqualTo(choice==0?4:0)); Assert.That(c.BlockPool,Is.EqualTo(choice==1?4:0));
            if(choice==0) Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Ice));
            else Assert.That(c.CombatPower.Blocks.Single().Element,Is.EqualTo(Element.Ice));
            Assert.That(c.OfferedUnitUses,Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidAbilityDoesNotConsumePermissionOrMana(int failure)
        {
            var(p,c,card)=Fixture("army_2_005"); p.Mana.AddToken(ManaColor.Blue,1); c.InBattle=true; Cast(p,c,card);
            int unit=0, ability=1, option=0;
            if(failure==0) unit=9;
            if(failure==1) ability=9;
            if(failure==2) option=2;
            if(failure==3) c.InBattle=false;
            if(failure==4) p.Mana.Tokens[ManaColor.Blue]=0;
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.UseFromOffer(p,c,unit,ability,option));
            Assert.That(c.OfferedUnitUses,Is.EqualTo(1)); Assert.That(c.MeleePool+c.BlockPool,Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Blue],Is.EqualTo(failure==4?0:1));
            Assert.That(c.UnitOffer.Count,Is.EqualTo(1)); Assert.That(p.Units.Count,Is.EqualTo(1));
            if(failure==4) p.Mana.AddToken(ManaColor.Blue,1);
            c.InBattle=true; CardUnitActions.UseFromOffer(p,c,0,1,1);
            Assert.That(c.BlockPool,Is.EqualTo(4)); Assert.That(c.OfferedUnitUses,Is.Zero);
        }

        [Test] public void SummonedThugsChangeReputationWithoutReversingRecruitment()
        {
            var(p,c,card)=Fixture("army_2_011"); Cast(p,c,card); CardUnitActions.UseFromOffer(p,c,0,2);
            Assert.That(c.InfluencePool,Is.EqualTo(4)); Assert.That(p.Reputation,Is.EqualTo(-1));
            Assert.That(c.InteractionReversed,Is.False); Assert.That(c.HasRecruited,Is.False);
        }

        [Test] public void OwnedUnitStillAbsorbsDamageButOfferedUnitCannot()
        {
            var(p,c,card)=Fixture(); c.InBattle=true; Cast(p,c,card);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,new UnitState(c.UnitOffer.Single()),enemy,3,Element.Physical));
            Assert.That(UnitDamageResolver.Assign(p,c,p.Units.Single(),enemy,3,Element.Physical),Is.Zero);
            Assert.That(p.Units.Single().Wounds,Is.EqualTo(1)); Assert.That(c.OfferedUnitUses,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnusedPermissionSurvivesCombatButExpiresAtTurnEnd(bool skipDraw)
        {
            var(p,c,card)=Fixture(); Cast(p,c,card);
            UnitDamageResolver.EndBattle(p,c); Assert.That(c.OfferedUnitUses,Is.EqualTo(1));
            c.SkipDraw=skipDraw; new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(c.OfferedUnitUses,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.UseFromOffer(p,c,0,2));
        }

        [Test] public void SelectedOriginalFireRangedAbilityReachesRealCombatResolver()
        {
            var(p,c,card)=Fixture(); c.UnitOffer.Add(CardJsonLoader.LoadUnit("army_003"));
            c.InBattle=true; p.Mana.AddToken(ManaColor.Red,1); Cast(p,c,card);
            CardUnitActions.UseFromOffer(p,c,1,1);
            Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Fire));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            var result=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Ranged);
            Assert.That(result.PrintedPower,Is.EqualTo(6)); Assert.That(result.Kills,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(enemy.Fame)); Assert.That(c.RangedPool,Is.Zero);
            Assert.That(c.UnitOffer.Count,Is.EqualTo(2)); Assert.That(p.Units.Single().Card.Id,Is.EqualTo("army_2_006"));
        }

        [Test] public void TwoActualSpellInstancesGrantTwoIndependentUses()
        {
            var(p,c,card)=Fixture(); var second=new DeedCard("magic_020",CardType.Spell); p.Deck.Hand.Add(second);
            p.Mana.AddToken(ManaColor.White,1); Cast(p,c,card); Cast(p,c,second);
            Assert.That(c.OfferedUnitUses,Is.EqualTo(2));
            CardUnitActions.UseFromOffer(p,c,0,1); CardUnitActions.UseFromOffer(p,c,0,2);
            Assert.That(c.InfluencePool,Is.EqualTo(2)); Assert.That(c.MovementPool,Is.EqualTo(2));
            Assert.That(c.OfferedUnitUses,Is.Zero); Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.UseFromOffer(p,c,0,2));
        }

        [Test] public void EmptyOfferOrMissingSpellManaCreatesNoFreeEffect()
        {
            var(p,c,card)=Fixture(); p.Mana.Tokens[ManaColor.White]=0;
            Assert.Throws<InvalidOperationException>(()=>Cast(p,c,card)); Assert.That(c.OfferedUnitUses,Is.Zero);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
            p.Mana.AddToken(ManaColor.White,1); Cast(p,c,card); c.UnitOffer.Clear();
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.UseFromOffer(p,c,0,2));
            Assert.That(c.OfferedUnitUses,Is.EqualTo(1)); Assert.That(c.MovementPool,Is.Zero);
        }
    }
}
