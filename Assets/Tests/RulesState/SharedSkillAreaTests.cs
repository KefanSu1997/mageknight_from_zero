using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SharedSkillAreaTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        [TestCase("000")] [TestCase("001")] [TestCase("002")] [TestCase("003")]
        [TestCase("004")] [TestCase("005")] [TestCase("007")]
        public void OriginalCooperativeSkillLoadsItsPrintedRoundFrequency(string hero)
        {
            var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_"+hero)[9];
            Assert.That(skill.SourceId,Is.EqualTo("hero_skill_pool_"+hero+":09"));
            Assert.That(skill.Frequency,Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.That(skill.PrintedRule,Does.StartWith("每轮一次"));
        }
        private static (GameEngine Game,PlayerState Owner,PlayerState Other,ActionContext Context,SkillCard Skill) Start(string pool)
        {
            var a=new PlayerState(1,"技能持有者") { Fame=3, HeroId=pool switch
                { "hero_skill_pool_000"=>"Arythea", "hero_skill_pool_001"=>"Braevalar", "hero_skill_pool_002"=>"Norowas", _=>throw new ArgumentException(nameof(pool)) } };
            var b=new PlayerState(2,"另一玩家") { Fame=3, HeroId="Tovak" };
            foreach(var player in new[]{a,b})player.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var skill=CardJsonLoader.LoadHeroSkills(pool)[9];a.Skills.Add(skill);
            var game=new GameEngine(new[]{a,b},3);
            game.StartNewRound(new[]{OriginalTactics.Get("day_1").Card,OriginalTactics.Get("day_4").Card});
            var context=new ActionContext();Assert.That(game.BeginNextTurn(context),Is.SameAs(a));
            return(game,a,b,context,skill);
        }
        [Test] public void PainAllowsZeroRemovalAndOwnerClaimsAnActualWoundOnlyOnce()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_000");
            var token=g.SharedSkills.UsePain(a,c,s,Array.Empty<DeedCard>());a.Deck.GainWound();var wound=a.Deck.Hand.Last();
            g.SharedSkills.ClaimPain(a,c,token,wound,SidewaysType.Move);
            Assert.That(a.Deck.PlayedCards.Single(),Is.SameAs(wound));Assert.That(c.MovementPool,Is.EqualTo(3));
            Assert.That(c.SidewaysWoundValue,Is.Zero);Assert.That(g.SharedSkills.Tokens,Is.Empty);
            Assert.That(SkillActions.IsUsed(a,s),Is.True);
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimPain(a,c,token,wound,SidewaysType.Move));
            g.EndCurrentTurn(c);Assert.That(a.Deck.DiscardPile.Any(card=>ReferenceEquals(card,wound)),Is.True);
        }
        [Test] public void PainPreservesExactUnselectedWoundsAndPassesToAnotherActualPlayer()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_000");
            a.Deck.GainWound();a.Deck.GainWound();var wounds=a.Deck.Hand.Where(h=>h.Type==CardType.Wound).ToArray();
            c.DrawPerHeal=1;int before=a.Deck.Hand.Count;
            var token=g.SharedSkills.UsePain(a,c,s,new[]{wounds[1]});
            Assert.That(a.Deck.Hand.Count,Is.EqualTo(before-1));Assert.That(a.Deck.Hand.Any(h=>ReferenceEquals(h,wounds[0])),Is.True);
            Assert.That(a.Deck.RemovedCards,Is.Empty);Assert.That(c.HealedHandWoundsThisTurn,Is.Zero);
            g.EndCurrentTurn(c,new[]{a.Deck.Hand.First(h=>h.Type!=CardType.Wound)});
            var next=new ActionContext();Assert.That(g.BeginNextTurn(next),Is.SameAs(b));b.Deck.GainWound();var wound=b.Deck.Hand.Last();
            g.SharedSkills.ClaimPain(b,next,token,wound,SidewaysType.Influence);
            Assert.That(next.InfluencePool,Is.EqualTo(3));Assert.That(b.Deck.PlayedCards.Single(),Is.SameAs(wound));
        }
        [Test] public void PainInvalidChoicesLeaveSharedTokenAndHandUntouched()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_000");a.Deck.GainWound();var wound=a.Deck.Hand.Last();
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UsePain(a,c,s,new[]{wound,wound}));
            c.InBattle=true;Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UsePain(a,c,s,Array.Empty<DeedCard>()));c.InBattle=false;
            Assert.That(SkillActions.IsUsed(a,s),Is.False);Assert.That(a.Deck.Hand.Last(),Is.SameAs(wound));
            var token=g.SharedSkills.UsePain(a,c,s,Array.Empty<DeedCard>());
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimPain(a,new ActionContext(),token,wound,SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimPain(b,c,token,wound,SidewaysType.Move));
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimPain(a,c,token,new DeedCard(wound.Id,CardType.Wound),SidewaysType.Move));
            Assert.That(g.SharedSkills.Tokens.Single(),Is.SameAs(token));Assert.That(a.Deck.Hand.Last(),Is.SameAs(wound));
        }
        [Test] public void CalmSurvivesTurnBoundaryAndAppliesOnlyToClaimantCurrentTurn()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_002");var token=g.SharedSkills.UseCalm(a,c,s);
            Assert.That(c.TerrainMoveReductionToOne.Values.All(v=>v==2),Is.True);
            g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});Assert.That(c.TerrainMoveReductionToOne,Is.Empty);
            Assert.That(g.SharedSkills.Tokens.Single(),Is.SameAs(token));var next=new ActionContext();g.BeginNextTurn(next);
            g.SharedSkills.ClaimCalm(b,next,token);Assert.That(next.TerrainMoveReductionToOne.Values.All(v=>v==1),Is.True);
            Assert.That(SkillActions.IsUsed(a,s),Is.True);Assert.That(g.SharedSkills.Tokens,Is.Empty);
            g.EndCurrentTurn(next,new[]{b.Deck.Hand[0]});Assert.That(next.TerrainMoveReductionToOne,Is.Empty);
        }
        [Test] public void CalmOwnerMayClaimBothEffectsWithoutReopeningImpassableTerrain()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_002");var token=g.SharedSkills.UseCalm(a,c,s);g.SharedSkills.ClaimCalm(a,c,token);
            Assert.That(c.TerrainMoveReductionToOne.Values.All(v=>v==3),Is.True);Assert.That(c.TerrainCostOverride,Is.Empty);
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.UseCalm(a,c,s));
        }
        [Test] public void NaturePassesToActualNextPlayerAndUsesTheirActualAttack()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_001");var enemy=CardJsonLoader.LoadMonsters().First(m=>m.Attack>2);
            c.InBattle=true;c.CombatPhase=Phase.Block;c.Enemies=new[]{enemy};
            var token=g.SharedSkills.UseNature(a,c,s,enemy);
            Assert.That(NatureRevengeActions.Reduction(c,enemy,0),Is.EqualTo(1));
            c.InBattle=false;g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});var next=new ActionContext();g.BeginNextTurn(next);
            next.InBattle=true;next.CombatPhase=Phase.Block;next.Enemies=new[]{enemy};
            Assert.Throws<InvalidOperationException>(()=>g.SharedSkills.ClaimNature(b,next,token,enemy with{}));
            Assert.That(g.SharedSkills.Tokens.Single(),Is.SameAs(token));
            g.SharedSkills.ClaimNature(b,next,token,enemy);Assert.That(NatureRevengeActions.HasCumbersome(next,enemy),Is.True);
            Assert.That(g.SharedSkills.Tokens,Is.Empty);Assert.That(SkillActions.IsUsed(a,s),Is.True);
        }
        [Test] public void NatureExpiresAtOwnerNextActualTurnWithoutRefreshingRoundUse()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_001");var enemy=CardJsonLoader.LoadMonsters().First(m=>m.Attack>2);
            c.InBattle=true;c.CombatPhase=Phase.Block;c.Enemies=new[]{enemy};g.SharedSkills.UseNature(a,c,s,enemy);
            c.InBattle=false;g.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});var next=new ActionContext();g.BeginNextTurn(next);
            Assert.That(g.SharedSkills.Tokens.Count,Is.EqualTo(1));g.EndCurrentTurn(next,new[]{b.Deck.Hand[0]});
            var again=new ActionContext();Assert.That(g.BeginNextTurn(again),Is.SameAs(a));
            Assert.That(g.SharedSkills.Tokens,Is.Empty);Assert.That(SkillActions.IsUsed(a,s),Is.True);
        }
        [Test] public void CalmDiscountPaysActualOriginalMovementAndNeverOpensLake()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_002");
            var destination=new MK.Logic.Runtime.Map.AxialCoord(1,0);
            var lake=new MK.Logic.Runtime.Map.AxialCoord(0,1);
            g.Map.Placed[a.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});
            g.Map.Placed[destination]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Forest});
            g.Map.Placed[lake]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Lake});
            var token=g.SharedSkills.UseCalm(a,c,s);
            void March(PlayerState p,ActionContext ctx)
            {
                var card=p.Deck.Hand.First(h=>h.Id=="basic_card_000");
                CardPlayActions.Play(p,ctx,card,CardJsonLoader.LoadBasicActions().Single(d=>d.Id==card.Id));
            }
            March(a,c);
            Assert.That(MovementService.GetCost(lake,g.Map,c.DayPart,c),Is.EqualTo(int.MaxValue));
            Assert.That(new MovementService().TryMoveUsingPool(a,destination,g.Map,c),Is.True);
            Assert.That(c.MovementPool,Is.EqualTo(1));Assert.That(a.Position,Is.EqualTo(destination));
            g.EndCurrentTurn(c);var next=new ActionContext();g.BeginNextTurn(next);
            g.SharedSkills.ClaimCalm(b,next,token);March(b,next);
            Assert.That(new MovementService().TryMoveUsingPool(b,destination,g.Map,next),Is.True);
            Assert.That(next.MovementPool,Is.Zero);Assert.That(b.Position,Is.EqualTo(destination));
        }
        [Test] public void LegacyEffectEntryCannotBypassActualSharedArea()
        {
            var(g,a,b,c,s)=Start("hero_skill_pool_002");
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(a,c,s));
            Assert.That(SkillActions.IsUsed(a,s),Is.False);Assert.That(g.SharedSkills.Tokens,Is.Empty);
            Assert.That(c.TerrainMoveReductionToOne,Is.Empty);Assert.That(c.TerrainCostOverride,Is.Empty);
        }
    }
}
