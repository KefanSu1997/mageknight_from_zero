using System;
using System.Linq;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveMaterialArtifactEntryTests
    {
        [SetUp]public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static PveJourneySession Start(params string[] ids)=>StartSeed(0,ids);
        private static PveJourneySession StartSeed(int seed,params string[] ids)
        {
            var p=new PlayerState();foreach(var id in ids)p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var map=new MapState();for(int i=0;i<3;i++)map.Placed[new AxialCoord(i,0)]=new MapTile(TileSet.Countryside,i,new[]{TerrainType.Plains});
            var s=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,supplyRandom:new Random(seed));
            s.PrepareRound();s.SelectTactic("day_1");s.BeginTurn();return s;
        }
        private static DeedCard Card(PveJourneySession s,string id)=>s.Player.Deck.Hand.First(c=>c.Id==id);
        private static SpellCard Offer(PveJourneySession s,string id)
        {
            var offer=new SpellCard(id);s.Game.Globals.Spells.Offer.Clear();s.Game.Globals.Spells.Offer.Add(offer);return offer;
        }
        private static PveBattleSession Fight(PveJourneySession s)=>s.BeginBattle(new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")});
        [Test]public void SuppliesExistWithoutHeroAndExcludeActualOwnedUniqueCards()
        {
            var s=Start("advanced_card_009","magic_009","items_013");
            Assert.That(s.Context.AdvActionSupply,Is.SameAs(s.Game.Globals.AdvActions));Assert.That(s.Context.SpellSupply,Is.SameAs(s.Game.Globals.Spells));
            Assert.That(s.Context.AdvActionSupply.Offer.Count,Is.EqualTo(3));Assert.That(s.Context.SpellSupply.Offer.Count,Is.EqualTo(3));
            Assert.That(s.Context.ArtifactSupply.Count,Is.EqualTo(24));
            while(s.Context.AdvActionSupply.Offer.Count>0)Assert.That(s.Context.AdvActionSupply.Take(0).Id,Is.Not.EqualTo("advanced_card_009"));
            while(s.Context.SpellSupply.Offer.Count>0)Assert.That(s.Context.SpellSupply.Claim(0).Id,Is.Not.EqualTo("magic_009"));
            while(s.Context.ArtifactSupply.Count>0)Assert.That(s.Context.ArtifactSupply.Draw().Id,Is.Not.EqualTo("items_013"));
        }
        [Test]public void WisdomNormalRemovesExactMaterialAndAcquiredMarchActuallyMoves()
        {
            var s=Start("items_013","basic_card_000");var book=Card(s,"items_013");var material=Card(s,"basic_card_000");
            var offer=new AdvActionCard("advanced_card_011");s.Context.AdvActionSupply.Offer.Clear();s.Context.AdvActionSupply.Offer.Add(offer);
            s.PlayMaterialArtifact(book,false,material,advanced:offer);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(material));Assert.That(s.Context.AdvActionSupply.Offer.Any(c=>ReferenceEquals(c,offer)),Is.False);
            Assert.That(s.Context.AdvActionSupply.Offer.Count,Is.EqualTo(3));
            s.PlayOutsideBattle(Card(s,offer.Id),"base");s.Move(new AxialCoord(1,0));
            Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(1,0)));s.EndTurn();Assert.That(s.Player.Deck.DiscardPile.Count,Is.EqualTo(2));
        }
        [Test]public void WisdomBurnObtainsActualOfferedSpellThenPaysItsPrintedCost()
        {
            var s=Start("items_013","basic_card_021");var spell=Offer(s,"magic_009");
            s.PlayMaterialArtifact(Card(s,"items_013"),true,Card(s,"basic_card_021"),spell:spell);
            Assert.That(s.Player.Deck.RemovedCards.Count,Is.EqualTo(2));Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            var b=Fight(s);b.Play(Card(s,"magic_009"),"base");b.Attack(0);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.Zero);Assert.That(s.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));
        }
        [TestCase(false)][TestCase(true)]public void MaterialAndSupplyMustBeActualObjectsBeforeAnyPayment(bool fakeOffer)
        {
            var s=Start("items_013","basic_card_021");var spell=Offer(s,"magic_009");var m=Card(s,"basic_card_021");
            Assert.Throws<InvalidOperationException>(()=>s.PlayMaterialArtifact(Card(s,"items_013"),true,fakeOffer?m:new DeedCard(m.Id,m.Type),spell:fakeOffer?new SpellCard(spell.Id):spell));
            Assert.That(s.Player.Deck.Hand.Count,Is.EqualTo(2));Assert.That(s.Player.Deck.RemovedCards,Is.Empty);Assert.That(s.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));
        }
        [TestCase(false)][TestCase(true)]public void TomeUsesOriginalSupplyWithoutAcquiringAndActualAttackConsumes(bool burn)
        {
            var s=Start("items_023","basic_card_021","basic_card_000");var spell=Offer(s,"magic_009");var b=Fight(s);
            s.PlayMaterialArtifact(Card(s,"items_023"),burn,Card(s,"basic_card_021"),spell:spell,spellChoice:burn?"strong":"base");
            Assert.That(s.Context.SpellSupply.Offer.Single(),Is.SameAs(spell));Assert.That(s.Player.Mana.GetTotal(),Is.Zero);
            Assert.That(s.Player.Wounds,Is.EqualTo(burn?1:0));Assert.That(s.Player.Deck.RemovedCards.Count,Is.EqualTo(burn?1:0));
            b.Attack(0);Assert.That(s.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));Assert.That(s.Player.Fame,Is.EqualTo(2));
        }
        [Test]public void TomeTargetedLowerEffectUsesExplicitEnemyAtFormalMelee()
        {
            var s=Start("items_023","basic_card_021","basic_card_000");var spell=Offer(s,"magic_007");var b=Fight(s);
            Assert.Throws<InvalidOperationException>(()=>s.PlayMaterialArtifact(Card(s,"items_023"),true,Card(s,"basic_card_021"),spell:spell,spellChoice:"strong",target:0));
            b.Advance();b.Advance();b.Advance();
            s.PlayMaterialArtifact(Card(s,"items_023"),true,Card(s,"basic_card_021"),spell:spell,spellChoice:"strong",target:0);
            Assert.That(s.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));Assert.That(s.Player.Fame,Is.EqualTo(2));
        }
        [Test]public void StaffBlueConsumesActualMaterialAndCrystalsPayOriginalRage()
        {
            var s=Start("items_021","basic_card_014","basic_card_021");var m=Card(s,"basic_card_014");
            s.PlayMaterialArtifact(Card(s,"items_021"),false,m,first:1,crystal:ManaColor.Red);
            Assert.That(s.Player.Deck.DiscardPile.Single(),Is.SameAs(m));Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(2));
            var b=Fight(s);b.Advance();b.Advance();b.Advance();b.Play(Card(s,"basic_card_021"),"strong");b.Attack(0);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(s.Player.Fame,Is.EqualTo(2));
        }
        [Test]public void StaffWhiteMovesActualPathAndGreenBurnHealsRealWounds()
        {
            var s=Start("items_021","basic_card_007");var card=Card(s,"items_021");
            s.Player.Wounds=2;
            s.PlayMaterialArtifact(card,true,first:0,second:3,path:new[]{new AxialCoord(1,0),new AxialCoord(2,0)});
            Assert.That(s.Player.Position,Is.EqualTo(new AxialCoord(2,0)));Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(card));
            s.HealOutsideBattle();s.HealOutsideBattle();Assert.That(s.Player.Wounds,Is.Zero);Assert.That(s.Context.Healing.Total,Is.EqualTo(1));
            Assert.That(Card(s,"basic_card_007"),Is.Not.Null);
        }
        [TestCase(false)][TestCase(true)]public void StaffRejectsWrongColorOrRepeatedBurnChoiceWithoutMutation(bool burn)
        {
            var s=Start("items_021","basic_card_000");
            Assert.Throws<InvalidOperationException>(()=>s.PlayMaterialArtifact(Card(s,"items_021"),burn,Card(s,"basic_card_000"),first:1,second:1));
            Assert.That(s.Player.Deck.Hand.Count,Is.EqualTo(2));Assert.That(s.Player.Mana.GetTotal(),Is.Zero);
        }
        [Test]public void ContextInputsRestoreAfterFailureAndSuccessfulPlay()
        {
            var s=Start("items_021","basic_card_014");var old=new DeedCard("basic_card_000",CardType.Action);s.Context.CardToDiscard=old;
            Assert.Throws<InvalidOperationException>(()=>s.PlayMaterialArtifact(Card(s,"items_021"),false,Card(s,"basic_card_014"),first:3));
            Assert.That(s.Context.CardToDiscard,Is.SameAs(old));s.PlayMaterialArtifact(Card(s,"items_021"),false,Card(s,"basic_card_014"),first:1);
            Assert.That(s.Context.CardToDiscard,Is.SameAs(old));
        }
        [Test]public void ReplaySupplySeedPreservesRealOffersAcrossTurnWithoutRebuilding()
        {
            string[] ids={"basic_card_000","basic_card_007","basic_card_014","basic_card_021","items_013","items_021","items_023"};
            var s=Start(ids);var same=Start(ids);var a=s.Context.AdvActionSupply.Offer.ToArray();var sp=s.Context.SpellSupply.Offer.ToArray();
            Assert.That(same.Context.SpellSupply.Offer.Select(c=>c.Id),Is.EqualTo(sp.Select(c=>c.Id)));
            TestContext.WriteLine("Replay0 advanced="+string.Join(",",a.Select(c=>c.Id))+" spells="+string.Join(",",sp.Select(c=>c.Id)));
            s.SidewaysMovement(Card(s,"basic_card_000"));s.EndTurn();s.BeginTurn();
            Assert.That(s.Context.AdvActionSupply.Offer[0],Is.SameAs(a[0]));Assert.That(s.Context.SpellSupply.Offer[0],Is.SameAs(sp[0]));
        }
        [Test]public void ContinuousOriginalSupplyAcquisitionStaffPaymentAndTomeBattleShareSameTurn()
        {
            var s=StartSeed(6,"basic_card_000","basic_card_007","basic_card_014","basic_card_021","items_013","items_021","items_023");
            var offered=s.Context.AdvActionSupply.Offer.Single(c=>c.Id=="advanced_card_039");
            var spell=s.Context.SpellSupply.Offer.Single(c=>c.Id=="magic_010");
            s.PlayMaterialArtifact(Card(s,"items_013"),false,Card(s,"basic_card_000"),advanced:offered);
            s.PlayMaterialArtifact(Card(s,"items_021"),false,Card(s,"basic_card_014"),first:1,crystal:ManaColor.White);
            s.PlayOutsideBattle(Card(s,"advanced_card_039"),"base");s.Move(new AxialCoord(1,0));
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var b=s.BeginBattle(new[]{e,e with{}});
            b.Play(Card(s,"basic_card_007"),"strong");b.Attack(0);Assert.That(s.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));
            b.Advance();b.Advance();b.Advance();
            s.PlayMaterialArtifact(Card(s,"items_023"),true,Card(s,"basic_card_021"),spell:spell,spellChoice:"strong_attack");
            Assert.That(b.AttackTotal(AttackType.Melee),Is.EqualTo(9));
            b.Attack(1);b.Advance();s.EndTurn();
            Assert.That(s.Player.Fame,Is.EqualTo(4));Assert.That(s.Player.Wounds,Is.EqualTo(2));
            Assert.That(s.Player.Deck.RemovedCards.Select(c=>c.Id),Is.EqualTo(new[]{"basic_card_000","items_023"}));
            Assert.That(s.Context.SpellSupply.Offer.Any(c=>ReferenceEquals(c,spell)),Is.True);
            Assert.That(s.Player.Deck.DiscardPile.Count,Is.EqualTo(6));
        }
    }
}
