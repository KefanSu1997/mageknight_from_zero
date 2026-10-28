using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class UnusedCombatTurnEndTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        [TestCase("basic_card_021",false,0,false,2)] [TestCase("basic_card_021",false,0,true,2)]
        [TestCase("basic_card_024",false,2,false,1)] [TestCase("basic_card_024",false,2,true,1)]
        [TestCase("advanced_card_003",true,0,false,3)] [TestCase("advanced_card_003",true,0,true,3)]
        [TestCase("basic_card_016",true,0,false,5)] [TestCase("basic_card_016",true,0,true,5)]
        public void OriginalUnusedPowerExpiresOnEitherRefillPath(string id,bool enhanced,int option,bool skipDraw,int amount)
        {
            var p=new PlayerState();var c=new ActionContext{SkipDraw=skipDraw};
            var card=new DeedCard(id,CardType.Action);p.Deck.Hand.Add(card);
            p.Deck.PutUnder(new DeedCard("basic_card_000",CardType.Action));
            var original=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Single(x=>x.Id==id);
            foreach(var color in original.RequiredCrystals)p.Mana.AddToken(color);
            CardPlayActions.Play(p,c,card,original,enhanced,option);
            Assert.That(c.MeleePool+c.RangedPool+c.SiegePool+c.BlockPool,Is.EqualTo(amount));
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(c.CombatPower.Blocks,Is.Empty);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(skipDraw?0:1));
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);
            var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_002");
            Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee));
            Assert.That(p.Fame,Is.Zero);
        }
    }
}
