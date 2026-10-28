using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class FortitudeBannerStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard b) Fixture(string unitId="army_2_006")
        {
            var p=new PlayerState{ExtraCommandSlots=1};
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit(unitId)));
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var c=new ActionContext{InBattle=true,TargetUnit=p.Units[0],SkipDraw=true};
            var b=new DeedCard("items_018",CardType.Artifact);p.Deck.Hand.Add(b);return(p,c,b);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard b,bool burn=false)
            =>CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i=>i.Id==b.Id),burn);
        private static Monster Enemy(string ability)
            => ability=="none"?CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027")
                :CardJsonLoader.LoadMonsters().First(m=>m.Abilities.Contains(Enum.Parse<Ability>(ability)));

        [TestCase("none")] [TestCase("Poison")] [TestCase("Paralyze")]
        public void PreventsWoundAndItsAdditionalEffectButCannotReceiveDamageTwice(string ability)
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            BannerActions.SelectFortitudeProtection(p,unit,true);
            Assert.That(UnitDamageResolver.Assign(p,c,unit,Enemy(ability),3,Element.Physical),Is.Zero);
            Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsDestroyed,Is.False);
            Assert.That(unit.FortitudeBannerSpent,Is.True);Assert.That(unit.Banner,Is.SameAs(b));
            Assert.That(c.DamageAssignedUnits.Contains(unit),Is.True);
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,unit,Enemy(ability),3,Element.Physical));
            Assert.Throws<InvalidOperationException>(()=>BannerActions.SelectFortitudeProtection(p,unit,true));
            UnitDamageResolver.EndBattle(p,c);c.InBattle=true;
            UnitDamageResolver.Assign(p,c,unit,Enemy(ability),3,Element.Physical);
            Assert.That(unit.IsDestroyed,Is.EqualTo(ability=="Paralyze"));
            Assert.That(unit.Wounds,Is.EqualTo(ability=="Paralyze"?0:ability=="Poison"?2:1));
            if(ability=="Paralyze")Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnselectedOrCancelledProtectionDoesNotPreventWound(bool cancelled)
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            if(cancelled){BannerActions.SelectFortitudeProtection(p,unit,true);BannerActions.SelectFortitudeProtection(p,unit,false);}
            UnitDamageResolver.Assign(p,c,unit,Enemy("none"),3,Element.Physical);
            Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(unit.FortitudeBannerSpent,Is.False);
        }

        [Test] public void ProtectionDoesNotApplyToAnotherUnitOrHero()
        {
            var(p,c,b)=Fixture();Play(p,c,b);BannerActions.SelectFortitudeProtection(p,p.Units[0],true);
            UnitDamageResolver.Assign(p,c,p.Units[1],Enemy("Poison"),3,Element.Physical);
            int wounds=1;AbilityRules.AssignWounds(Enemy("Poison"),p,ref wounds);
            Assert.That(p.Units[1].Wounds,Is.EqualTo(2));Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.DiscardWounds,Is.EqualTo(1));Assert.That(p.Units[0].FortitudeBannerSpent,Is.False);
        }

        [TestCase(0)] [TestCase(3)]
        public void ZeroOrFullyResistedDamageDoesNotSpendTheBanner(int damage)
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];unit.PhysicalResistTemp=true;
            BannerActions.SelectFortitudeProtection(p,unit,true);
            UnitDamageResolver.Assign(p,c,unit,Enemy("none"),damage,Element.Physical);
            Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.FortitudeBannerSpent,Is.False);
            Assert.That(c.DamageAssignedUnits.Contains(unit),Is.EqualTo(damage>0));
            UnitDamageResolver.EndBattle(p,c);Assert.That(unit.FortitudeProtectionSelected,Is.False);
        }

        [Test] public void OriginalGuardianGolemResistsBeforeFortitudeIsNeeded()
        {
            var(p,c,b)=Fixture("army_2_001");Play(p,c,b);var unit=p.Units[0];
            BannerActions.SelectFortitudeProtection(p,unit,true);
            UnitDamageResolver.Assign(p,c,unit,Enemy("none"),3,Element.Physical);
            Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.FortitudeBannerSpent,Is.False);
            Assert.That(c.DamageAssignedUnits.Contains(unit),Is.True);
        }

        [Test] public void ActualUtemSwordsmenSelfWoundCanBePreventedAndDoesNotUseDamageAllocation()
        {
            var(p,c,b)=Fixture("army_2_014");Play(p,c,b);var unit=p.Units[0];
            BannerActions.SelectFortitudeProtection(p,unit,true);CardUnitActions.Use(p,c,unit,1);
            Assert.That(c.MeleePool,Is.EqualTo(6));Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsReady,Is.False);
            Assert.That(unit.FortitudeBannerSpent,Is.True);Assert.That(c.DamageAssignedUnits.Contains(unit),Is.False);
            UnitDamageResolver.Assign(p,c,unit,Enemy("none"),3,Element.Physical);
            Assert.That(unit.Wounds,Is.EqualTo(1));
        }

        [Test] public void RoundUsageSurvivesBattleAndTurnButResetsAtActualNewRound()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            BannerActions.SelectFortitudeProtection(p,unit,true);unit.AddWounds(1);
            UnitDamageResolver.EndBattle(p,c);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(unit.FortitudeBannerSpent,Is.True);
            new GameEngine(new[]{p},3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(unit.FortitudeBannerSpent,Is.False);BannerActions.SelectFortitudeProtection(p,unit,true);
            Assert.That(unit.AddWounds(1),Is.Zero);Assert.That(unit.FortitudeBannerSpent,Is.True);
        }

        [Test] public void TwoActualCopiesHaveIndependentRoundUsageAndReplacementDiscardsOldOne()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            BannerActions.SelectFortitudeProtection(p,unit,true);unit.AddWounds(1);
            var next=new DeedCard(b.Id,b.Type);p.Deck.Hand.Add(next);Play(p,c,next);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(unit.FortitudeBannerSpent,Is.False);
            BannerActions.SelectFortitudeProtection(p,unit,true);Assert.That(unit.AddWounds(1),Is.Zero);
        }

        [Test] public void InvalidProtectionTargetAndExpiredChoiceDoNotAffectUnits()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            Assert.Throws<InvalidOperationException>(()=>BannerActions.SelectFortitudeProtection(p,p.Units[1],true));
            Assert.Throws<InvalidOperationException>(()=>BannerActions.SelectFortitudeProtection(p,new UnitState(unit.Card),true));
            BannerActions.SelectFortitudeProtection(p,unit,true);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null);
            Assert.That(unit.FortitudeProtectionSelected,Is.False);Assert.That(unit.FortitudeBannerSpent,Is.False);
        }

        [Test] public void BurnHealsOnlyOwnedUnitsWithoutReadyingThemOrHealingHero()
        {
            var(p,c,b)=Fixture();c.InBattle=false;p.Wounds=2;
            p.Units[0].AddWounds(2);p.Units[0].Exhaust();p.Units[1].AddWounds(1);
            Play(p,c,b,true);
            Assert.That(p.Units.All(u=>u.Wounds==0),Is.True);Assert.That(p.Units[0].IsReady,Is.False);
            Assert.That(p.Units[1].IsReady,Is.True);Assert.That(p.Wounds,Is.EqualTo(2));
            Assert.That(c.HealedUnitsThisTurn.Count,Is.EqualTo(2));Assert.That(c.HealedHandWoundsThisTurn,Is.Zero);
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));
        }

        [TestCase(false)] [TestCase(true)]
        public void CureBeforeOrAfterBurnReadiesOnlyUnitsActuallyHealed(bool before)
        {
            var(p,c,b)=Fixture();c.InBattle=false;p.Units[0].AddWounds(1);
            foreach(var unit in p.Units)unit.Exhaust();p.Mana.AddToken(ManaColor.Green);
            var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_001");
            void Cure()
            {
                var card=new DeedCard(spell.Id,CardType.Spell);p.Deck.Hand.Add(card);
                CardPlayActions.Play(p,c,card,new ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,
                    spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost),false);
            }
            if(before)Cure();Play(p,c,b,true);if(!before)Cure();
            Assert.That(p.Units[0].IsReady,Is.True);Assert.That(p.Units[1].IsReady,Is.False);
            Assert.That(c.HealedUnitsThisTurn.Count,Is.EqualTo(1));Assert.That(c.Healing.Total,Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void InvalidAttachmentPreservesActualHand(int invalid)
        {
            var(p,c,b)=Fixture();
            if(invalid==0)c.TargetUnit=null;
            if(invalid==1)c.TargetUnit=new UnitState(p.Units[0].Card);
            if(invalid==2)p.Units[0].Destroy();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Units.All(u=>u.Banner==null),Is.True);
        }

        [Test] public void BattleBurnRejectsBeforeRemovingArtifactOrHealing()
        {
            var(p,c,b)=Fixture();p.Units[0].AddWounds(1);
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(p.Units[0].Wounds,Is.EqualTo(1));Assert.That(c.HealedUnitsThisTurn,Is.Empty);
        }
    }
}
