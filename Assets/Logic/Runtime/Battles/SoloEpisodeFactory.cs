using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    // Private task/recovery metadata. None of these seeds belong in policy observations.
    public sealed record SoloEpisodeSeeds(ulong Tiles,int Player,int Enemies,int Mana,int Offers,
        int RoundShuffle,int Progression,ulong Dummy,int Tactics,int DummySkills,int Ruins,int AdventureRewards=0,int Effects=0,
        int TacticEffects=0);
    public sealed record SoloEpisodeSetup(int Version,string Hero,string DummyHero,int[] DummyCrystals,
        SoloDummyDualColorRule DualColorRule,SoloEpisodeSeeds Seeds);

    public static class SoloEpisodeFactory
    {
        // Own the callback inside the rule graph; caller/test closures can retain
        // unrelated mutable objects and cannot be part of a reproducible episode.
        public static Func<string,MK.Logic.Core.ManaColor[]> BindPrintedColors(
            IReadOnlyDictionary<string,MK.Logic.Core.ManaColor[]> printed)
        {
            var colors=printed.ToDictionary(p=>p.Key,p=>(MK.Logic.Core.ManaColor[])p.Value.Clone());
            return id=>(MK.Logic.Core.ManaColor[])colors[id].Clone();
        }
        /// <summary>Rebuilds a native opening with explicit independent random streams.
        /// Callers must separately bind resource hashes and verified ruin/hero setup data.</summary>
        public static SoloConquestEpisode Create(SoloEpisodeSetup setup,PrintedTileCatalog catalog,
            IEnumerable<RuinToken> ruins=null)
        {
            if(setup==null||catalog==null||setup.Version!=1||setup.Seeds==null)
                throw new ArgumentException("Versioned setup and catalog required.");
            if(setup.Hero==setup.DummyHero)throw new InvalidOperationException("Dummy must be an unused hero.");
            var hero=OriginalHeroes.Get(setup.Hero);var dummy=OriginalHeroes.Get(setup.DummyHero);
            var s=setup.Seeds;
            var basic=CardJsonLoader.LoadBasicActions().ToDictionary(c=>c.Id);
            var advanced=CardJsonLoader.LoadAdvancedActions().ToArray();
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var player=hero.CreatePlayer(basic.Values,new Random(s.Player));
            // EnemyTokenSupply preserves caller order until recycling. A solo
            // scenario must shuffle its initial physical inventory explicitly.
            var enemyRandom=new Random(s.Enemies);
            var enemies=CardJsonLoader.LoadMonsters().ToArray();
            for(int i=enemies.Length-1;i>0;i--)
            {
                int j=enemyRandom.Next(i+1);
                (enemies[i],enemies[j])=(enemies[j],enemies[i]);
            }
            var journey=new PveJourneySession(player,basic.Values.Concat(advanced).Concat(spells.Select(c=>
                new ActionCardData(c.Id,c.Name,c.Set,c.ImagePath,c.EnImagePath,c.Top.Effect,c.Bottom.Effect,c.Top.ManaCost))),
                artifacts:CardJsonLoader.LoadItems(),spells:spells,originalSkills:CardJsonLoader.LoadHeroSkills(hero.SkillPoolId),
                enemySupply:new EnemyTokenSupply(enemies,enemyRandom),
                manaRandom:new Random(s.Mana),tacticRandom:new Random(s.TacticEffects),supplyRandom:new Random(s.Offers),roundShuffleRandom:new Random(s.RoundShuffle),
                progressionRandom:new Random(s.Progression),soloConquest:true,
                unitDeck:UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit));
            if(ruins!=null)journey.Game.Globals.Ruins=new RuinTokenSupply(ruins,new Random(s.Ruins));
            var dummyDeck=new SoloDummyDeck(dummy.StartingCards.Select((id,i)=>
                new SoloDummyCard(dummy.Id+"/"+i,id,basic[id].RequiredCrystals.Single())),
                setup.DummyCrystals,s.Dummy,setup.DualColorRule);
            var colors=advanced.Where(c=>!string.IsNullOrWhiteSpace(c.BaseEffect))
                .ToDictionary(c=>c.Id,c=>c.RequiredCrystals.ToArray());
            foreach(var spell in spells)colors.Add(spell.Id,new[]{spell.ManaColor});
            journey.EnableSoloClock(dummyDeck,BindPrintedColors(colors),new Random(s.Tactics));
            journey.EnableSoloDummySkills(dummy.Id,CardJsonLoader.LoadHeroSkills(dummy.SkillPoolId),new Random(s.DummySkills));
            var tiles=SoloConquestTileSupply.Create(catalog,s.Tiles);
            journey.InitializeSoloOriginalMap(catalog,tiles);
            return new SoloConquestEpisode(journey,tiles,advanced.Select(c=>c.Id),new Random(s.AdventureRewards),new Random(s.Effects));
        }
    }
}
