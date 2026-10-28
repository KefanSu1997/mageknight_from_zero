using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveKrangCurseArmorJourneyTests
    {
        [Test] public void CurseReducesActualWolfArmorThenThreeAttackKillsAndOldWoundSurvivesNextTurn()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
            var p=new PlayerState{HeroId="Krang",Fame=15,Armor=3};
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[8];
            HeroSkillAcquisition.Acquire(p,skill);
            foreach(var id in new[]{"000","001","007","008","014","021","022"})
                p.Deck.Hand.Add(new DeedCard("basic_card_"+id,CardType.Action));
            var originals=p.Deck.Hand.ToArray();
            var target=new AxialCoord(0,1);var map=new MapState();
            foreach(var pos in new[]{new AxialCoord(0,0),target})
                map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);
            j.SelectTactic("day_1");j.BeginTurn();
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_007");
            var b=j.BeginBattle(new[]{enemy});b.Advance();b.Advance();b.Advance();
            var wound=p.Deck.Hand.Single(c=>c.Type==CardType.Wound);
            Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(CombatMath.EffectiveArmor(enemy,0,b.Context),Is.EqualTo(4));
            b.UseSkill(skill,1,target:0);
            Assert.That(CombatMath.EffectiveArmor(enemy,0,b.Context),Is.EqualTo(3));
            Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            b.Play(originals.Single(c=>c.Id=="basic_card_021"),"attack");
            b.PlaySideways(originals.Single(c=>c.Id=="basic_card_014"));
            Assert.That(b.Context.MeleePool,Is.EqualTo(3));
            Assert.That(b.Attack(0).Kills,Is.EqualTo(1));b.Advance();j.EndTurn();
            j.BeginTurn();
            j.PlayOutsideBattle(originals.Single(c=>c.Id=="basic_card_000"),"base");j.Move(target);j.EndTurn();
            Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Position,Is.EqualTo(target));
            Assert.That(j.CompletedTurns,Is.EqualTo(2));Assert.That(j.CompletedBattles,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(18));Assert.That(p.Wounds,Is.EqualTo(1));
            var final=p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile).ToArray();
            Assert.That(final.Length,Is.EqualTo(8));
            Assert.That(final.Any(c=>ReferenceEquals(c,wound)),Is.True);
            Assert.That(originals.All(c=>final.Any(f=>ReferenceEquals(c,f))),Is.True);
        }
    }
}
