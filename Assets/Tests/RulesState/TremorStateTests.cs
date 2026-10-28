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
    public sealed class TremorStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static Monster Enemy(int armor, params Ability[] traits) => new("target", armor, 0, Element.Physical, 3, traits);
        private static void Play(PlayerState p, ActionContext c, string id = "magic_003", bool strong = false, int option = 0)
        {
            ActionCardData data;
            if (id.StartsWith("magic"))
            {
                var s = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
                data = new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost);
            }
            else data = CardJsonLoader.LoadBasicActions().Single(a => a.Id == id);
            var card = new DeedCard(id, id.StartsWith("magic") ? CardType.Spell : CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, data, strong, option);
        }
        private static (PlayerState p, ActionContext c, TurnMachine t, List<Monster> enemies) Start(params Monster[] enemies)
        {
            var p = new PlayerState(); foreach (ManaColor color in Enum.GetValues(typeof(ManaColor))) p.Mana.AddToken(color, 3);
            var list = enemies.ToList(); var c = new ActionContext { DayPart = DayPart.Night, Enemies = list };
            var g = new GameState(); g.Players.Add(p); var t = new TurnMachine(g); t.BeginBattle(c);
            return (p, c, t, list);
        }
        private static void Defend(TurnMachine t, ActionContext c, List<Monster> enemies)
        {
            t.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c); t.SetBlock(0, Element.Physical);
            t.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void SingleAndAllBranchesUseCurrentSiteOrEnemyFortification(bool strong, bool all)
        {
            var (p,c,t,enemies)=Start(Enemy(strong ? (all ? 6 : 8) : (all ? 4 : 5)), Enemy(8,Ability.Fortified), Enemy(8,Ability.MagicResist));
            c.FortifiedSite=true; Play(p,c,strong:strong,option:all?-1:0);
            Assert.That(CombatMath.EffectiveArmor(enemies[0],0,c),Is.EqualTo(2));
            Assert.That(CombatMath.EffectiveArmor(enemies[1],1,c),Is.EqualTo(all ? (strong?4:6):8));
            Assert.That(CombatMath.EffectiveArmor(enemies[2],2,c),Is.EqualTo(8));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(strong?2:3));
            Defend(t,c,enemies); Play(p,c,"basic_card_021"); Assert.That(t.Attack(new[]{0},c).Kills,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(3)); t.FinishBattle(c); c.SkipDraw=true;
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,new ManaSource(1,()=>DayPart.Night),c);
            Assert.That(c.ArmorReduction,Is.Empty); Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void AllModeDistinguishesIntrinsicFortifiedAndOrdinaryEnemies(bool strong)
        {
            var (p,c,t,enemies)=Start(Enemy(8),Enemy(8,Ability.Fortified),Enemy(8,Ability.FireResist,Ability.IceResist),Enemy(8,Ability.MagicResist));
            Play(p,c,strong:strong,option:-1);
            Assert.That(enemies.Select((e,i)=>CombatMath.EffectiveArmor(e,i,c)),Is.EqualTo(new[]{6,strong?4:6,6,8}));
            Defend(t,c,enemies); Play(p,c,"basic_card_021",true);
            Assert.That(t.Attack(new[]{1},c).Kills,Is.EqualTo(strong?1:0));
        }

        [TestCase(false)] [TestCase(true)]
        public void RepeatedTremorsAddInsteadOfReplacingAndStopAtOne(bool strong)
        {
            var (p,c,t,enemies)=Start(Enemy(5)); Play(p,c,strong:strong); Play(p,c,strong:strong);
            Assert.That(c.ArmorReduction[0],Is.EqualTo(6)); Assert.That(CombatMath.EffectiveArmor(enemies[0],0,c),Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(strong?1:3));
            Defend(t,c,enemies); Play(p,c,"basic_card_021"); Assert.That(t.Attack(new[]{0},c).Kills,Is.EqualTo(1));
        }

        [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
        public void DemolishOrderAndIntrinsicFortificationDetermineEarthquakeBonus(bool demolishFirst,bool intrinsic)
        {
            var (p,c,t,enemies)=Start(Enemy(10,intrinsic?new[]{Ability.Fortified}:Array.Empty<Ability>())); c.FortifiedSite=true;
            Play(p,c,demolishFirst?"magic_007":"magic_003",!demolishFirst);
            Play(p,c,demolishFirst?"magic_003":"magic_007",demolishFirst);
            Assert.That(CombatMath.EffectiveArmor(enemies[0],0,c),Is.EqualTo(demolishFirst&&!intrinsic?6:3));
            Assert.That(p.Mana.Tokens[ManaColor.Red],Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));
            Defend(t,c,enemies); Play(p,c,"basic_card_021",true);
            Assert.That(t.Attack(new[]{0},c).Kills,Is.EqualTo(demolishFirst&&!intrinsic?0:1));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExposeBeforeEarthquakeRemovesFortificationBonus(bool all)
        {
            var (p,c,t,enemies)=Start(Enemy(8,Ability.Fortified),Enemy(8,Ability.Fortified)); c.FortifiedSite=true;
            Play(p,c,"magic_019"); Play(p,c,strong:true,option:all?-1:0);
            Assert.That(CombatMath.EffectiveArmor(enemies[0],0,c),Is.EqualTo(all?6:5));
            Assert.That(CombatMath.EffectiveArmor(enemies[1],1,c),Is.EqualTo(all?4:8));
            Defend(t,c,enemies); Play(p,c,"magic_009");
            var attack=t.Attack(new[]{0},c); Assert.That(attack.EffectivePower,Is.EqualTo(5));
            Assert.That(attack.Kills,Is.EqualTo(all?0:1));
        }

        [TestCase(false)] [TestCase(true)]
        public void DefeatedEnemyIsSkippedByAllModeAndRejectedAsSingleTarget(bool strong)
        {
            var (p,c,t,enemies)=Start(Enemy(2),Enemy(6)); Defend(t,c,enemies); Play(p,c,strong:strong,option:1); Play(p,c,"basic_card_021");
            Assert.That(t.Attack(new[]{0},c).Kills,Is.EqualTo(1)); Play(p,c,strong:strong,option:-1);
            Assert.That(c.ArmorReduction.ContainsKey(0),Is.False); Assert.That(c.ArmorReduction[1],Is.EqualTo(5));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong:strong)).Message,Is.EqualTo("目标已经被击败"));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(strong?1:3));
            Assert.That(p.Fame,Is.EqualTo(3)); Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(3));
        }

        [TestCase(false,-2)] [TestCase(true,-2)] [TestCase(false,1)] [TestCase(true,1)]
        [TestCase(false,2)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        public void InvalidSingleTargetOrModeDoesNotConsumeResources(bool strong,int failure)
        {
            var (p,c,t,enemies)=Start(Enemy(8,failure==2?new[]{Ability.MagicResist}:Array.Empty<Ability>()));
            if(failure==3)c.InBattle=false;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong:strong,option:failure<2?failure:0));
            Assert.That(p.Mana.Tokens.Values.All(v=>v==3),Is.True); Assert.That(c.ArmorReduction,Is.Empty);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase(false,0)] [TestCase(true,0)] [TestCase(true,1)] [TestCase(true,2)] [TestCase(false,3)] [TestCase(true,3)]
        public void PrintedGreenBlackAndCrystalCostsAreAtomic(bool strong,int mode)
        {
            var (p,c,t,enemies)=Start(Enemy(5));
            if(mode!=2)p.Mana.Tokens[ManaColor.Green]=0;
            if(mode==1||mode==3)p.Mana.Crystals[ManaColor.Green]=1;
            if(mode==1)p.Mana.Tokens[ManaColor.Black]=0;
            if(mode==2)c.DayPart=DayPart.Day;
            if(mode==3)Play(p,c,strong:strong);
            else Assert.That(Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong:strong)).Message,
                Is.EqualTo(mode==2?"白昼不能施放强效法术":"法力不足，不能支付所选效果"));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(mode==2?3:0));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.EqualTo(mode==1?1:0));
            Assert.That(p.Mana.Tokens[ManaColor.Black],Is.EqualTo(mode==1?0:mode==3&&strong?2:3));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(mode==3?1:0));
            Assert.That(CombatMath.EffectiveArmor(enemies[0],0,c),Is.EqualTo(mode==3?2:5));
        }
    }
}
