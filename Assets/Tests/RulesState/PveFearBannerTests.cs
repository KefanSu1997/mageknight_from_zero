using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveFearBannerTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(bool three = false)
        {
            var p = new PlayerState(); p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_001")));
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_011" }) p.Deck.Hand.Add(new DeedCard(id,CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_001",CardType.Artifact));
            var ids = three ? new[] { "monster_015", "monster_032", "monster_032" } : new[] { "monster_015" };
            var source = CardJsonLoader.LoadMonsters().ToArray();
            return new PveBattleSession(p, ids.Select(id => source.Single(m => m.Id == id) with { }), CardJsonLoader.LoadBasicActions(), artifacts:CardJsonLoader.LoadItems());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")),choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));

        [TestCase(false)] [TestCase(true)]
        public void NormalCancelsOnlyOneAttackAndBlockedOtherAttackDoesNotPreventUse(bool blockFirst)
        {
            var s=Create(); var p=s.Player; var u=p.Units.Single(); var banner=p.Deck.Hand.Single(c=>c.Id=="items_001");
            s.PlayArtifact(banner,false,u); Assert.That(u.Banner,Is.SameAs(banner)); s.Advance();
            if(blockFirst)
            {
                Play(s,16,"strong"); Sideways(s); s.Block(0,0);
                Assert.Throws<InvalidOperationException>(()=>s.UseFearBanner(u,0,0)); Assert.That(u.IsReady,Is.True);
            }
            int slot=blockFirst?1:0; s.UseFearBanner(u,0,slot);
            Assert.That(s.Context.CancelledAttacks,Is.EquivalentTo(new[]{(0,slot)})); Assert.That(s.Context.SkipAttackIndices,Is.Empty);
            Assert.That(p.Fame,Is.EqualTo(1)); Assert.That(u.IsReady,Is.False);
            Assert.Throws<InvalidOperationException>(()=>s.UseFearBanner(u,0,slot)); Assert.That(p.Fame,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>s.Block(0,slot));
            s.Advance();
            if(!blockFirst)
            {
                Assert.Throws<InvalidOperationException>(()=>s.PlanDamage(0,0,u));
                s.PlanDamage(0,1,u);
            }
            s.Advance(); Assert.That(p.Wounds,Is.Zero); Assert.That(u.Wounds,Is.EqualTo(blockFirst?0:1),"未取消的第二次冰3仍造成真实部队创伤");
            Play(s,21,"strong"); Assert.That(s.Attack(0).Kills,Is.EqualTo(1)); s.Advance();
            Assert.That(s.Won,Is.True); Assert.That(p.Fame,Is.EqualTo(4)); Assert.That(s.Context.CancelledAttacks,Is.Empty);
            Assert.That(u.Banner,Is.SameAs(banner)); Assert.That(u.IsReady,Is.False);
        }

        [Test] public void BurnCancelsAllAttacksOfThreeChosenEnemiesAndOnlyBurnsRealBanner()
        {
            var s=Create(true); var p=s.Player; var banner=p.Deck.Hand.Single(c=>c.Id=="items_001");
            s.BurnFearBanner(banner,0,1,2); Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(banner));
            Assert.That(s.Context.SkipAttackIndices,Is.EquivalentTo(new[]{0,1,2})); Assert.That(p.Units.Single().IsReady,Is.True);
            Assert.That(p.Fame,Is.Zero); s.Advance(); s.Advance(); s.Advance(); Assert.That(p.Wounds,Is.Zero);
            Play(s,21,"strong"); Play(s,16,"attack"); Play(s,11,"strong"); Sideways(s);
            Assert.That(s.Attack(0,1,2).EffectivePower,Is.EqualTo(10)); s.Advance();
            Assert.That(s.Won,Is.True); Assert.That(p.Fame,Is.EqualTo(8)); Assert.That(s.Context.SkipAttackIndices,Is.Empty);
        }

        [Test] public void FormalBlockRecordsExactSlotAndCancellationRemapsWithoutLeaking()
        {
            var s=Create(true); var p=s.Player; var u=p.Units.Single(); s.PlayArtifact(p.Deck.Hand.Single(c=>c.Id=="items_001"),false,u);s.Advance();
            s.Context.CombatPower.AddBlock(3,Element.Ice); CardCombatActions.Block(p,s.Context,s.Enemies,0,0);
            Assert.That(s.Context.SuccessfullyBlockedAttacks.Contains((0,0)),Is.True);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseFear(p,s.Context,u,0,0));
            BannerActions.UseFear(p,s.Context,u,0,1);
            Assert.That(s.Context.CancelledAttacks.Contains((0,1)),Is.True);
            s.Context.RemapEnemyModifiers(new System.Collections.Generic.Dictionary<int,int>{{0,1},{2,0}});
            Assert.That(s.Context.CancelledAttacks,Is.EquivalentTo(new[]{(1,1)}));
            Assert.That(s.Context.SuccessfullyBlockedAttacks,Is.EquivalentTo(new[]{(1,0)}));
        }

        [Test] public void TurnEndAlsoClearsAttackSlotsWithoutReadyingAttachedUnit()
        {
            var s=Create();var p=s.Player;var u=p.Units.Single();
            s.PlayArtifact(p.Deck.Hand.Single(c=>c.Id=="items_001"),false,u);s.Advance();s.UseFearBanner(u,0,0);
            s.Context.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,s.Context);
            Assert.That(s.Context.CancelledAttacks,Is.Empty);Assert.That(s.Context.SuccessfullyBlockedAttacks,Is.Empty);
            Assert.That(u.IsReady,Is.False);Assert.That(u.Banner.Id,Is.EqualTo("items_001"));
        }
    }
}
