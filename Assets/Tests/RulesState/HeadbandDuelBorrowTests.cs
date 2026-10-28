using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandDuelBorrowTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private (PlayerState p, ActionContext c, DeedCard h, SkillCard s) Setup()
        {
            var p = new PlayerState { HeroId = "Tovak", Fame = 3 };
            var enemy = CardJsonLoader.LoadMonsters().Single(x => x.Id == "monster_032");
            var c = new ActionContext { InBattle = true, CombatPhase = Phase.Block,
                Enemies = new[] { enemy, enemy with { } }, SkillSupply = new SkillSupply() };
            var h = new DeedCard("items_022", CardType.Artifact); p.Deck.Hand.Add(h);
            var s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[7]; c.SkillSupply.Offer.Add(s);
            return (p,c,h,s);
        }
        private void Play(PlayerState p, ActionContext c, string id)
        {
            var card = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x => x.Id == id), option: id == "basic_card_016" ? 1 : 0);
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualSingleEnemyBlockAndMeleeConsumeDoubleBorrowAndUnitCancelsReward(bool unit)
        {
            var (p,c,h,s) = Setup();
            HeadbandBorrowResolution.UseDuel(p,c,h,s,c.Enemies[0]);
            Assert.That(c.PendingHeadbandBorrow, Is.Null);
            Assert.That(c.CombatPower.Blocks.Single().Value, Is.EqualTo(2));
            Assert.That(c.CombatPower.AvailableBlocks(1,0,c.Enemies[1]), Is.Empty);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(h));
            if (unit)
            {
                var u = new UnitState(UnitJsonLoader.Load(Folder,"army_001")); p.Units.Add(u);
                CardUnitActions.Use(p,c,u,1,1);
                Assert.That(u.IsReady, Is.False);
            }
            else Play(p,c,"basic_card_016");
            CardCombatActions.Block(p,c,c.Enemies,0);
            Assert.That(c.SuccessfullyBlockedAttacks.Contains((0,0)), Is.True);
            CardCombatActions.EnterPhase(c,Phase.Melee);
            Assert.That(c.CombatPower.Attacks.Single().Value, Is.EqualTo(2));
            Assert.That(c.CombatPower.Attacks.Single().RequiredEnemy, Is.SameAs(c.Enemies[0]));
            CardCombatActions.EnterPhase(c,Phase.Melee);
            Assert.That(c.CombatPower.Attacks.Count, Is.EqualTo(1));
            Play(p,c,"basic_card_021");
            var result = CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Melee);
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(3 + c.Enemies[0].Fame + (unit ? 0 : 2)));
            Assert.That(DuelActions.TakeDefeatBonus(c,c.Enemies[0]), Is.Zero);
            Assert.That(p.Skills, Is.Empty); Assert.That(SkillActions.IsUsed(p,s), Is.False);
            Assert.That(c.SkillSupply.Offer.Single(), Is.SameAs(s));
            Assert.Throws<InvalidOperationException>(() => HeadbandBorrowResolution.UseDuel(p,c,h,s,c.Enemies[1]));
        }
        [Test] public void JourneyRejectsReservedBlockBeforeDeferredDefenseWithoutSpendingHeadband()
        {
            var p = new PlayerState(); var h = new DeedCard("items_022", CardType.Artifact);
            p.Deck.Hand.Add(h);
            var j = new PveJourneySession(p, CardJsonLoader.LoadBasicActions());
            j.SelectTactic("day_1"); j.BeginTurn();
            var enemy = CardJsonLoader.LoadMonsters().Single(x => x.Id == "monster_032");
            j.BeginBattle(new[]{enemy}); j.Battle.Advance();
            var s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[7]; j.Context.SkillSupply.Offer.Add(s);
            var block = new DeedCard("basic_card_016", CardType.Action); p.Deck.Hand.Add(block);
            p.Mana.AddToken(ManaColor.Blue);
            CardPlayActions.Play(p,j.Context,block,CardJsonLoader.LoadBasicActions().Single(x => x.Id == block.Id),true);
            j.Battle.Block(0);
            Assert.That(j.Battle.IsBlocked(0), Is.True);
            Assert.That(j.Context.SuccessfullyBlockedAttacks.Contains((0,0)), Is.False);
            Assert.Throws<InvalidOperationException>(() => j.UseHeadbandDuel(h,s,enemy));
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x,h)), Is.True);
            Assert.That(j.Context.PendingHeadbandBorrow, Is.Null); Assert.That(j.Context.Duel.EnemyTarget, Is.Null);
            Assert.That(SkillActions.IsUsed(p,s), Is.False); Assert.That(j.Battle.IsBlocked(0), Is.True);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void RejectedBorrowPreservesPhysicalHeadbandSupplyAndContribution(int invalid)
        {
            var(p,c,h,s) = Setup(); var target = c.Enemies[0]; int slot = 0;
            if(invalid == 0) c.CombatPhase = Phase.Melee;
            if(invalid == 1) target = target with { };
            if(invalid == 2) slot = target.AttackCount;
            if(invalid == 3) c.DefeatedEnemyIndices.Add(0);
            if(invalid == 4) c.SuccessfullyBlockedAttacks.Add((0,0));
            if(invalid == 5) s = CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[7];
            if(invalid == 6) h = new DeedCard("items_022", CardType.Artifact);
            Assert.Throws<InvalidOperationException>(() => HeadbandBorrowResolution.UseDuel(p,c,h,s,target,slot));
            Assert.That(c.PendingHeadbandBorrow, Is.Null); Assert.That(c.CombatPower.Blocks, Is.Empty);
            Assert.That(c.Duel.EnemyTarget, Is.Null); Assert.That(p.Deck.Hand.Single().Id, Is.EqualTo("items_022"));
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(SkillActions.IsUsed(p,s), Is.False);
            Assert.That(p.Skills, Is.Empty); Assert.That(c.SkillSupply.Offer.Count, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(3));
        }
    }
}
