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
    public sealed class RitualAttackLifecycleTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static void Play(PlayerState p,ActionContext c,DeedCard ritual,bool strong)
            => CardPlayActions.Play(p,c,ritual,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==ritual.Id),strong,999);
        [TestCase("basic_card_021",false,5,Element.Physical,AttackType.Melee)]
        [TestCase("basic_card_021",true,6,Element.Fire,AttackType.Melee)]
        [TestCase("basic_card_016",false,3,Element.Ice,AttackType.Melee)]
        [TestCase("basic_card_016",true,4,Element.ColdFire,AttackType.Melee)]
        [TestCase("basic_card_008",false,3,Element.Physical,AttackType.Ranged)]
        [TestCase("basic_card_008",true,4,Element.Fire,AttackType.Ranged)]
        [TestCase("basic_card_000",false,2,Element.Physical,AttackType.Siege)]
        [TestCase("basic_card_000",true,3,Element.Fire,AttackType.Siege)]
        public void MaterialStaysRemovedThroughActualAttackTurnAndRound(string id,bool strong,int amount,Element element,AttackType type)
        {
            var p=new PlayerState();var game=new GameEngine(new[]{p},5);
            var ritual=new DeedCard("advanced_card_029",CardType.Action);var m=new DeedCard(id,CardType.Action);
            p.Deck.Hand.AddRange(new[]{ritual,m});p.Mana.AddToken(ManaColor.Red);
            var c=new ActionContext{CardToRemove=m,SkipDraw=true};Play(p,c,ritual,strong);
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(ritual));
            var contribution=c.CombatPower.Attacks.Single();Assert.That(contribution.Value,Is.EqualTo(amount));
            Assert.That(contribution.Element,Is.EqualTo(element));Assert.That(contribution.Type,Is.EqualTo(type));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(strong?0:1));
            var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_002");
            var result=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},type==AttackType.Melee?Phase.Melee:Phase.Ranged);
            Assert.That(result.EffectivePower,Is.EqualTo(amount));Assert.That(result.Kills,Is.EqualTo(amount>=5?1:0));
            Assert.That(p.Fame,Is.EqualTo(amount>=5?4:0));
            UnitDamageResolver.EndBattle(p,c);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,game.Globals.Mana,c);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(ritual));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));
            game.EndRound();Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(ritual));
            Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(m));Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void DuplicateOriginalAdvancedMaterialsKeepUnselectedInstance(bool strong)
        {
            var p=new PlayerState();var ritual=new DeedCard("advanced_card_029",CardType.Action);
            var first=new DeedCard("advanced_card_004",CardType.Action);var selected=new DeedCard(first.Id,first.Type);
            p.Deck.Hand.AddRange(new[]{ritual,first,selected});p.Mana.AddToken(ManaColor.Red);
            var c=new ActionContext{CardToRemove=selected,SkipDraw=true};Play(p,c,ritual,strong);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(first));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(selected));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(ritual));
            var game=new GameEngine(new[]{p},5);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,game.Globals.Mana,c);game.EndRound();
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(selected));
            Assert.That(p.Deck.DrawPile.Count,Is.EqualTo(2));
            Assert.That(p.Deck.DrawPile.Count(x=>ReferenceEquals(x,first)),Is.EqualTo(1));
            Assert.That(p.Deck.DrawPile.Count(x=>ReferenceEquals(x,ritual)),Is.EqualTo(1));
            Assert.That(c.CombatPower.Attacks,Is.Empty);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ForgedCategoryOrSelfCannotConsumeManaOrMaterial(int mode)
        {
            var p=new PlayerState();var ritual=new DeedCard("advanced_card_029",CardType.Action);
            var material=new DeedCard("magic_009",mode==2?CardType.Spell:CardType.Action);p.Deck.Hand.AddRange(new[]{ritual,material});p.Mana.AddToken(ManaColor.Red);
            var c=new ActionContext{CardToRemove=mode==1?ritual:material};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,ritual,true));
            Assert.That(p.Deck.Hand,Is.EqualTo(new[]{ritual,material}));Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(1));
            Assert.That(c.CombatPower.Attacks,Is.Empty);
        }
    }
}
