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
    public sealed class ChargeUnitStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard card) Fixture(string unit="army_2_005")
        {
            var p=new PlayerState();
            foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddToken(color,2);
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit(unit)));
            var c=new ActionContext{InBattle=true,CombatPhase=Phase.Ranged,UnitOffer=new List<UnitCard>{p.Units[0].Card}};
            var card=new DeedCard("advanced_card_016",CardType.Action);p.Deck.Hand.Add(card);
            return(p,c,card);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard card,bool strong)
            =>CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(a=>a.Id==card.Id),strong);

        [TestCase(false,false,0)] [TestCase(false,false,1)] [TestCase(false,true,0)] [TestCase(false,true,1)]
        [TestCase(true,false,0)] [TestCase(true,false,1)] [TestCase(true,true,0)] [TestCase(true,true,1)]
        public void OriginalIceUnitReceivesBattleBonusForExactlyChosenAbility(bool strong,bool offer,int choice)
        {
            var(p,c,card)=Fixture();Play(p,c,card,strong);
            if(offer)
            {
                var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_020");
                new ActionSystem().Play(new MK.Logic.Data.Cards.ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,
                    spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost),p,c,false);
                CardUnitActions.UseFromOffer(p,c,0,1,choice);
            }
            else CardUnitActions.Use(p,c,p.Units[0],1,choice);
            Assert.That(c.MeleePool,Is.EqualTo(choice==0?(strong?7:6):0));
            Assert.That(c.BlockPool,Is.EqualTo(choice==1?(strong?7:6):0));
            Assert.That(p.Mana.Tokens[ ManaColor.Blue ],Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ ManaColor.Red ],Is.EqualTo(strong?1:2));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            Assert.That(p.Units[0].IsReady,Is.EqualTo(offer));Assert.That(c.UnitOffer.Count,Is.EqualTo(1));
            Assert.That(c.NoUnitDamage,Is.True);Assert.That(c.CombatActionStarted,Is.True);
            if(choice==0)Assert.That(c.CombatPower.Attacks.Single().Element,Is.EqualTo(Element.Ice));
            else Assert.That(c.CombatPower.Blocks.Single().Element,Is.EqualTo(Element.Ice));
        }

        [TestCase("army_003",AttackType.Ranged,Element.Fire,8)]
        [TestCase("army_2_008",AttackType.Siege,Element.Physical,6)]
        public void OriginalRangedAndSiegeTypesSurviveBonusAndRealKill(string unit,AttackType type,Element element,int power)
        {
            var(p,c,card)=Fixture(unit);Play(p,c,card,false);CardUnitActions.Use(p,c,p.Units[0],1);
            var part=c.CombatPower.Attacks.Single();Assert.That(part.Type,Is.EqualTo(type));Assert.That(part.Element,Is.EqualTo(element));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            var result=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Ranged);
            Assert.That(result.PrintedPower,Is.EqualTo(power));Assert.That(result.Kills,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(4));Assert.That(c.RangedPool+c.SiegePool,Is.Zero);
        }

        [TestCase(1)] [TestCase(2)]
        public void NonCombatAbilityAndHeroCardDoNotGetUnitBonus(int ability)
        {
            var(p,c,card)=Fixture("army_2_006");Play(p,c,card,true);CardUnitActions.Use(p,c,p.Units[0],ability);
            Assert.That(c.InfluencePool,Is.EqualTo(ability==1?2:0));Assert.That(c.MovementPool,Is.EqualTo(ability==2?2:0));
            var rage=CardJsonLoader.LoadBasicActions().Single(a=>a.Id=="basic_card_021");new ActionSystem().Play(rage,p,c,false);
            Assert.That(c.MeleePool,Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void LateOrOutsidePlayRejectsBeforeManaAndCardConsumption(int late)
        {
            var(p,c,card)=Fixture("army_2_006");
            if(late==0)c.InBattle=false;
            if(late==1)c.CombatPhase=Phase.Melee;
            if(late==2)c.CombatPower.AddAttack(2,Element.Physical);
            if(late==3)CardUnitActions.Use(p,c,p.Units[0],1);
            if(late==4)
            {
                c.CombatPower.AddAttack(1,Element.Physical,AttackType.Ranged);
                var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
                CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Ranged);
                Assert.That(c.RangedPool,Is.Zero);
            }
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,true));
            Assert.That(p.Mana.Tokens[ ManaColor.Red ],Is.EqualTo(2));Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
            Assert.That(c.ChargeUnitBonus,Is.Zero);Assert.That(c.NoUnitDamage,Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void BonusExpiresWithBattleButDamageBanLastsThroughTurn(bool skipDraw)
        {
            var(p,c,card)=Fixture("army_2_006");Play(p,c,card,true);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            UnitDamageResolver.EndBattle(p,c);
            Assert.That(c.ChargeUnitBonus,Is.Zero);Assert.That(c.NoUnitDamage,Is.True);
            c.InBattle=true;CardUnitActions.Use(p,c,p.Units[0],0);
            Assert.That(c.MeleePool,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,p.Units[0],enemy,3,Element.Physical));
            Assert.That(p.Units[0].Wounds,Is.Zero);
            c.SkipDraw=skipDraw;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(c.NoUnitDamage,Is.False);Assert.That(c.ChargeUnitBonus,Is.Zero);
            Assert.That(UnitDamageResolver.Assign(p,c,p.Units[0],enemy,3,Element.Physical),Is.Zero);
            Assert.That(p.Units[0].Wounds,Is.EqualTo(1));
        }

        [Test] public void AbilitySelfWoundIsNotDamageAssignment()
        {
            var(p,c,card)=Fixture("army_2_014");Play(p,c,card,true);CardUnitActions.Use(p,c,p.Units[0],1);
            Assert.That(c.MeleePool,Is.EqualTo(9));Assert.That(p.Units[0].Wounds,Is.EqualTo(1));Assert.That(c.NoUnitDamage,Is.True);
        }

        [Test] public void MissingEnhancedManaGrantsNoBonusOrBan()
        {
            var(p,c,card)=Fixture();p.Mana.Tokens[ ManaColor.Red ]=0;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,card,true));
            Assert.That(c.ChargeUnitBonus,Is.Zero);Assert.That(c.NoUnitDamage,Is.False);Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));
        }
    }
}
