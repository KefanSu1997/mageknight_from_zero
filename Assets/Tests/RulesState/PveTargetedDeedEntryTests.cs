using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveTargetedDeedEntryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static PveBattleSession Start(string[] cards, params string[] enemies)
        {
            var player = new PlayerState();
            foreach (string id in cards) player.Deck.Hand.Add(new DeedCard(id, id.StartsWith("magic_") ? CardType.Spell : CardType.Action));
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var originals = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var catalog = CardJsonLoader.LoadMonsters();
            return new PveBattleSession(player, enemies.Select(id=>catalog.Single(e=>e.Id==id) with {}), originals,
                new ActionContext { DayPart = DayPart.Night }, spells:spells);
        }
        private static DeedCard Card(PveBattleSession b, string id) => b.Player.Deck.Hand.First(c=>c.Id==id);
        private static void Mana(PveBattleSession b, ManaColor color, int count=1) => b.Player.Mana.AddToken(color,count);
        private static void Melee(PveBattleSession b) { b.Advance(); b.Advance(); b.Advance(); }

        [TestCase("magic_003",ManaColor.Green)]
        [TestCase("magic_013",ManaColor.Blue)]
        [TestCase("magic_019",ManaColor.White)]
        [TestCase("magic_022",ManaColor.White)]
        public void MissingTargetRejectsBeforeOriginalPaymentOrCardMovement(string id,ManaColor color)
        {
            var b=Start(new[]{id},"monster_032","monster_032"); var card=Card(b,id); Mana(b,color);
            Assert.Throws<InvalidOperationException>(()=>b.Play(card,"base"));
            Assert.That(b.Player.Deck.Hand.Single(),Is.SameAs(card)); Assert.That(b.Player.Mana.Tokens[color],Is.EqualTo(1));
            Assert.That(b.Context.DefeatedEnemyIndices,Is.Empty); Assert.That(b.Context.CombatPower.Attacks,Is.Empty);
        }
        [Test] public void TargetedSpellsKeepSecondEnemyIdentityAndActualRangedConsumer()
        {
            var b=Start(new[]{"magic_003","magic_019","magic_013","basic_card_021"},"monster_032","monster_032");
            Mana(b,ManaColor.Green);Mana(b,ManaColor.White);Mana(b,ManaColor.Blue);
            b.Play(Card(b,"magic_003"),"base",1);
            Assert.That(CombatMath.EffectiveArmor(b.Enemies[0],0,b.Context),Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveArmor(b.Enemies[1],1,b.Context),Is.EqualTo(1));
            b.Play(Card(b,"magic_019"),"base",1);Assert.That(b.Context.ResistancesRemoved,Is.EquivalentTo(new[]{1}));
            Assert.That(b.Attack(1).Kills,Is.EqualTo(1));Assert.That(b.Player.Fame,Is.EqualTo(2));
            b.Play(Card(b,"magic_013"),"base",0);Melee(b);Assert.That(b.Player.Wounds,Is.Zero);
            b.Play(Card(b,"basic_card_021"),"attack");Assert.That(b.Attack(0).Kills,Is.Zero,"Original attack2 still cannot kill armor3");
            Assert.That(b.Context.MeleePool,Is.Zero);Assert.That(b.Context.DefeatedEnemyIndices,Is.EquivalentTo(new[]{1}));
            b.Advance();Assert.That(b.Context.ResistancesRemoved,Is.Empty);Assert.That(b.Context.SkipAttackIndices,Is.Empty);
        }
        [TestCase("strong_fortification",true)]
        [TestCase("strong_resistance",false)]
        public void MassExposureKeepsPrintedMutuallyExclusiveChoice(string choice,bool fortification)
        {
            var b=Start(new[]{"magic_019"},"monster_001","monster_032");Mana(b,ManaColor.White);Mana(b,ManaColor.Black);
            b.Play(Card(b,"magic_019"),choice);
            Assert.That(b.Context.FortificationsRemoved.Contains(0),Is.EqualTo(fortification));
            Assert.That(b.Context.ResistancesRemoved.Contains(0),Is.EqualTo(!fortification));
            Assert.That(b.Context.RangedPool,Is.EqualTo(3));Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.Zero);
        }
        [Test] public void QuakeSingleAndAllAreDistinctAndPrintedGreenBlackArePaid()
        {
            var single=Start(new[]{"magic_003"},"monster_001","monster_032");Mana(single,ManaColor.Green);Mana(single,ManaColor.Black);
            single.Play(Card(single,"magic_003"),"strong",0);
            Assert.That(CombatMath.EffectiveArmor(single.Enemies[0],0,single.Context),Is.EqualTo(2));
            Assert.That(CombatMath.EffectiveArmor(single.Enemies[1],1,single.Context),Is.EqualTo(3));
            var all=Start(new[]{"magic_003"},"monster_001","monster_032");Mana(all,ManaColor.Green);Mana(all,ManaColor.Black);
            all.Play(Card(all,"magic_003"),"strong_all");
            Assert.That(CombatMath.EffectiveArmor(all.Enemies[0],0,all.Context),Is.EqualTo(4));
            Assert.That(CombatMath.EffectiveArmor(all.Enemies[1],1,all.Context),Is.EqualTo(1));
            Assert.That(all.Player.Mana.Tokens.Values.Sum(),Is.Zero);
        }
        [TestCase("magic_007",ManaColor.Red)]
        [TestCase("magic_022",ManaColor.White)]
        public void DestroyIsMeleeOnlyAndAwardsActualEnemyOnce(string id,ManaColor color)
        {
            var b=Start(new[]{id,id},"monster_032","monster_032");Mana(b,color,2);Mana(b,ManaColor.Black,2);
            var first=Card(b,id);Assert.Throws<InvalidOperationException>(()=>b.Play(first,"strong",1));
            Assert.That(b.Player.Mana.Tokens[color],Is.EqualTo(2));Melee(b);b.Play(first,"strong",1);
            Assert.That(b.Context.DefeatedEnemyIndices,Is.EquivalentTo(new[]{1}));Assert.That(b.Player.Fame,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>b.Play(Card(b,id),"strong",1));Assert.That(b.Player.Fame,Is.EqualTo(2));
            Assert.That(b.Player.Mana.Tokens[color],Is.EqualTo(1));Assert.That(b.Player.Deck.PlayedCards.Single(),Is.SameAs(first));
        }
        [TestCase("strong_enemy_blue",ManaColor.Blue)]
        [TestCase("strong_enemy_white",ManaColor.White)]
        public void ColdStareUsesChosenColorAndSameEnemyAllAttackSlots(string choice,ManaColor color)
        {
            var b=Start(new[]{"advanced_card_043"},"monster_058","monster_032");Mana(b,color);
            b.Play(Card(b,"advanced_card_043"),choice,0,1);
            Assert.That(b.Context.ColdStareSkips(b.Enemies[0]),Is.True);Assert.That(b.Context.ColdStareSkips(b.Enemies[1]),Is.False);
            Assert.That(b.Context.SelectedEnemy,Is.Null);Assert.That(b.Context.SelectedActionManaColor,Is.Null);
            Melee(b);Assert.That(b.Player.Wounds,Is.EqualTo(2));Assert.That(b.Player.Mana.Tokens.Values.Sum(),Is.Zero);
        }
        [Test] public void ColdStareBaseChangesOnlySelectedAttackAndPreservesOriginalEnemy()
        {
            var b=Start(new[]{"advanced_card_043"},"monster_007","monster_007");b.Play(Card(b,"advanced_card_043"),"enemy",1);
            Assert.That(CardCombatActions.DefenseProfile(b.Enemies[0],0,b.Context).Abilities,Does.Contain(Ability.Swift));
            Assert.That(CardCombatActions.DefenseProfile(b.Enemies[1],1,b.Context).Abilities.Contains(Ability.Swift),Is.False);
            Assert.That(b.Enemies[1].Abilities,Does.Contain(Ability.Swift));
        }
        [Test] public void QuickReflexesReductionIsConsumedBeforeSwiftBlockCalculation()
        {
            var b=Start(new[]{"basic_card_010","basic_card_016"},"monster_007");Mana(b,ManaColor.Blue);b.Advance();
            b.Play(Card(b,"basic_card_010"),"strong_reduce");Assert.That(b.Context.AttackReductionTimes,Is.EqualTo(1));
            b.ReduceAttack(0);Assert.That(b.Context.AttackReductionTimes,Is.Zero);
            b.Play(Card(b,"basic_card_016"),"block");Assert.That(b.BlockPreview(0),Is.EqualTo((2,2)));
            b.Block(0);b.Advance();b.Advance();Assert.That(b.Player.Wounds,Is.Zero);
        }
        [Test] public void OutmaneuverSplitUsesDistinctActualSlotsAndAwardsAfterRealDefense()
        {
            var b=Start(new[]{"advanced_card_036"},"monster_058");Mana(b,ManaColor.White);b.Advance();
            b.Play(Card(b,"advanced_card_036"),"strong_split");b.ReduceAttack(0,0);
            Assert.Throws<InvalidOperationException>(()=>b.ReduceAttack(0,0));Assert.That(b.Context.AttackReductionTimes,Is.EqualTo(1));
            b.ReduceAttack(0,1);Assert.That(b.Context.AttackReductionTimes,Is.Zero);
            Assert.That(b.Context.MeleePool,Is.Zero);b.Advance();b.Advance();
            Assert.That(b.Player.Wounds,Is.Zero);Assert.That(b.Context.MeleePool,Is.EqualTo(2));
            Assert.That(b.Attack(0).Kills,Is.Zero);Assert.That(b.Context.MeleePool,Is.Zero);
        }
        [Test] public void StoppedEnemyCannotConsumePendingReduction()
        {
            var b=Start(new[]{"advanced_card_043","basic_card_010"},"monster_007");Mana(b,ManaColor.Blue);
            b.Play(Card(b,"advanced_card_043"),"strong_enemy_blue",0);b.Advance();
            b.Play(Card(b,"basic_card_010"),"reduce");
            Assert.Throws<InvalidOperationException>(()=>b.ReduceAttack(0));
            Assert.That(b.Context.AttackReductionTimes,Is.EqualTo(1));Assert.That(b.Context.PendingAttackReductions,Is.Empty);
        }
    }
}
