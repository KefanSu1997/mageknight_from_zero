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
    public sealed class CardContributionSourceTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        static DeedCard Hand(PlayerState p, string id, CardType type) { var card=new DeedCard(id,type); p.Deck.Hand.Add(card); return card; }
        static void NoLeak(ActionContext c)
        {
            Assert.That(c.CurrentSourceCard,Is.Null);
            c.CombatPower.AddAttack(1);
            Assert.That(c.CombatPower.Attacks.Last().SourceCard,Is.Null);
        }
        [Test] public void SameIdCardsKeepDifferentPhysicalSources()
        {
            var p=new PlayerState();var c=new ActionContext();var a=Hand(p,"basic_card_021",CardType.Action);var b=Hand(p,a.Id,CardType.Action);
            var original=CardJsonLoader.LoadBasicActions().Single(x=>x.Id==a.Id);
            CardPlayActions.Play(p,c,a,original);CardPlayActions.Play(p,c,b,original);
            Assert.That(c.CombatPower.Attacks[0].SourceCard,Is.SameAs(a));Assert.That(c.CombatPower.Attacks[1].SourceCard,Is.SameAs(b));NoLeak(c);
        }
        [Test] public void PhysicalSpellRetainsFireTypeAndExactManaPayment()
        {
            var p=new PlayerState();var c=new ActionContext {InBattle=true};var a=Hand(p,"magic_009",CardType.Spell);p.Mana.AddToken(ManaColor.Red);
            var s=CardJsonLoader.LoadSpells().Single(x=>x.Id==a.Id);
            var data=new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost);
            CardPlayActions.Play(p,c,a,data,actionSystem:new ActionSystem(false,new[]{s}));
            var power=c.CombatPower.Attacks.Single();Assert.That(power.SourceCard,Is.SameAs(a));Assert.That(power.Element,Is.EqualTo(Element.Fire));Assert.That(power.Value,Is.EqualTo(5));NoLeak(c);
        }
        [TestCase(false)] [TestCase(true)] public void ArtifactNormalAndBurnKeepPhysicalSource(bool burn)
        {
            var p=new PlayerState();var c=new ActionContext();var a=Hand(p,"items_019",CardType.Artifact);
            CardPlayActions.PlayArtifact(p,c,a,CardJsonLoader.LoadItems().Single(x=>x.Id==a.Id),burn);
            Assert.That(c.CombatPower.Attacks.All(x=>ReferenceEquals(x.SourceCard,a)),Is.True);Assert.That(c.CombatPower.Attacks.Count,Is.GreaterThan(0));NoLeak(c);
        }
        [Test] public void SidewaysIsTheActualCardAndLaterPowerIsNot()
        {
            var p=new PlayerState();var c=new ActionContext();var a=Hand(p,"basic_card_000",CardType.Action);
            Assert.That(new ActionSystem().PlaySideways(a,p,c,SidewaysType.Attack),Is.True);Assert.That(c.CombatPower.Attacks.Single().SourceCard,Is.SameAs(a));NoLeak(c);
        }
        [Test] public void OverloadContinuationRetainsHostNotRemovedMaterial()
        {
            var p=new PlayerState();var c=new ActionContext {OverloadStepwise=true};var host=Hand(p,"advanced_card_024",CardType.Action);var material=Hand(p,"basic_card_021",CardType.Action);c.CardToRemove=material;
            CardPlayActions.Play(p,c,host,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==host.Id));
            Assert.That(c.PendingRepeatedEffect,Is.Not.Null);Assert.That(c.CurrentSourceCard,Is.Null);
            CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection());CardPlayActions.ContinueRepeatedEffect(p,c,new RepeatedCardSelection());
            Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(3));Assert.That(c.CombatPower.Attacks.All(x=>ReferenceEquals(x.SourceCard,host)),Is.True);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(material));NoLeak(c);
        }
        [Test] public void FailedPaymentRestoresSourceAndKeepsCard()
        {
            var p=new PlayerState();var c=new ActionContext();var a=Hand(p,"basic_card_021",CardType.Action);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,a,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==a.Id),true));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(a));Assert.That(c.CombatPower.Attacks,Is.Empty);NoLeak(c);
        }
        [Test] public void OfferedSpellKeepsActualSupplyEntityAndConsumesPermission()
        {
            var p=new PlayerState();var c=new ActionContext {InBattle=true,SpellSupply=new SpellSupply()};var offered=new SpellCard("magic_009");c.SpellSupply.Offer.Add(offered);c.SpellColors.Add(ManaColor.Red);p.Mana.AddToken(ManaColor.Red);
            CardSpellActions.PlayFromOffer(p,c,0);
            Assert.That(c.CombatPower.Attacks.Single().SourceCard,Is.SameAs(offered));Assert.That(c.SpellSupply.Offer.Single(),Is.SameAs(offered));Assert.That(c.SpellColors,Is.Empty);NoLeak(c);
        }
        [Test] public void MysticBoxUsesPhysicalBoxRatherThanBorrowedSupplyIdentity()
        {
            var p=new PlayerState();var c=new ActionContext {ArtifactSupply=new ArtifactSupply()};var box=Hand(p,"items_024",CardType.Artifact);
            c.ArtifactSupply.SetDeck(new[]{new ArtifactCard("items_019")});
            CardPlayActions.PlayArtifact(p,c,box,CardJsonLoader.LoadItems().Single(x=>x.Id==box.Id));CardPlayActions.UseMysticBox(p,c,box);
            Assert.That(c.CombatPower.Attacks.Single().SourceCard,Is.SameAs(box));NoLeak(c);
        }
    }
}
