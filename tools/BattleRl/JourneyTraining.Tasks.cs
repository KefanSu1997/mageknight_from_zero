using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;

static partial class JourneyTraining
{
    // Declared original inventory rehearsal, not a claim of natural acquisition.
    public sealed class TaskDefinition
    {
        public string HeroId { get; set; } = "";
        public bool OriginalOpening { get; set; }
        public int RandomSeed { get; set; }
        public int InventorySeed { get; set; }
        public int ShuffleSeed { get; set; }
        public int ManaSeed { get; set; }
        public int SupplySeed { get; set; }
        public int ProgressionSeed { get; set; }
        public int EffectSeed { get; set; }
        public int SummonSeed { get; set; }
        public string[][] EnemyGroups { get; set; } = Array.Empty<string[]>();
        public BuildDefinition Build { get; set; }
        public bool OriginalWorld { get; set; }
        public string WorldSourceSha256 { get; set; }

        public void Validate()
        {
            if(OriginalWorld&&(WorldSourceSha256==null||WorldSourceSha256.Length!=64))
                throw new InvalidOperationException("Original world task must bind its exact local source SHA256");
            if(!OriginalWorld&&WorldSourceSha256!=null)throw new InvalidOperationException("World source binding requires a world task");
            if (!MK.Logic.Data.OriginalHeroes.All.Any(h => h.Id == HeroId))
                throw new InvalidOperationException("Select an original hero");
            if (new[] { RandomSeed, InventorySeed, ShuffleSeed, ManaSeed, SupplySeed,
                        ProgressionSeed, EffectSeed, SummonSeed }.Any(s => s < 0 || s > int.MaxValue - 256))
                throw new InvalidOperationException("Task random streams must be nonnegative");
            if (EnemyGroups == null || EnemyGroups.Length == 0 || EnemyGroups.Length > 16 || EnemyGroups.Any(group =>
                group == null || group.Length < 1 || group.Length > 3
                || group.Distinct().Count() != group.Length
                || group.Any(id => !Enemies.Any(e => e.Id == id))))
                throw new InvalidOperationException("Select 1-3 distinct original enemies per declared group");
            if(Build!=null)
            {
                if(OriginalOpening)throw new InvalidOperationException("Original opening cannot grant declared midgame inventory");
                Build.Validate(HeroId);
            }
        }

        public static TaskDefinition Read(JsonElement value)
        {
            var names=value.EnumerateObject().Select(p=>p.Name).ToArray();
            var allowed=typeof(TaskDefinition).GetProperties().Select(p=>p.Name).ToArray();
            var required=allowed.Where(n=>n!="Build"&&n!="OriginalWorld"&&n!="WorldSourceSha256").ToArray();
            if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length
                || required.Any(n=>!names.Contains(n,StringComparer.OrdinalIgnoreCase))
                || names.Any(n=>!allowed.Contains(n,StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Task must specify exactly the supported independent factors");
            foreach(var property in value.EnumerateObject().Where(p=>p.Name.Equals("Build",StringComparison.OrdinalIgnoreCase)))
            {
                if(property.Value.ValueKind==JsonValueKind.Null)continue;
                var buildNames=property.Value.EnumerateObject().Select(p=>p.Name).ToArray();
                var buildRequired=typeof(BuildDefinition).GetProperties().Select(p=>p.Name).ToArray();
                if(buildNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=buildNames.Length
                    || buildNames.Length!=buildRequired.Length
                    || buildNames.Any(n=>!buildRequired.Contains(n,StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Build must specify exactly the supported inventory fields");
            }
            var task = JsonSerializer.Deserialize<TaskDefinition>(value.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (task == null) throw new InvalidOperationException("Missing task definition");
            task.Validate();
            return task;
        }
    }

    public sealed class BuildDefinition
    {
        public string Family { get; set; } = "";
        public int Fame { get; set; }
        public string[] AdditionalCards { get; set; } = Array.Empty<string>();
        public string[] UnitIds { get; set; } = Array.Empty<string>();
        public string[] SkillIds { get; set; } = Array.Empty<string>();
        public Dictionary<string,int> Crystals { get; set; } = new();
        public int InitialWounds { get; set; }

        public void Validate(string heroId)
        {
            if(string.IsNullOrWhiteSpace(Family)||Family.Length>64||Fame<0||Fame>99||InitialWounds<0||InitialWounds>32)
                throw new InvalidOperationException("Invalid declared family/fame/wound bounds");
            bool DistinctOriginal(string[] ids,IEnumerable<string> originals)=>ids!=null
                &&ids.Distinct().Count()==ids.Length&&ids.All(id=>originals.Contains(id));
            if(!DistinctOriginal(AdditionalCards,Advanced.Select(c=>c.Id).Concat(Spells.Select(s=>s.Id)).Concat(Items.Select(i=>i.Id))))
                throw new InvalidOperationException("Additional inventory needs distinct original advanced/spell/artifact IDs");
            if(!DistinctOriginal(UnitIds,Units.Select(u=>u.Id))||UnitIds.Length>3)
                throw new InvalidOperationException("Choose up to three distinct original owned units");
            var hero=OriginalHeroes.All.Single(h=>h.Id==heroId);
            if(!DistinctOriginal(SkillIds,CardJsonLoader.LoadHeroSkills(hero.SkillPoolId).Select(s=>s.SourceId)))
                throw new InvalidOperationException("Declared skills must belong to the actual original hero");
            if(Crystals==null||Crystals.Any(kv=>!Enum.TryParse<ManaColor>(kv.Key,out var color)
                ||Enum.GetName(typeof(ManaColor),color)!=kv.Key
                ||!FamiliarActions.IsBasicColor(color)||kv.Value<0||kv.Value>3))
                throw new InvalidOperationException("Declared crystals require original basic colors, each zero to three");
        }
    }

    sealed partial class Env
    {
        public readonly TaskDefinition DeclaredTask;
        public int TaskEncounterCursor;
        int EffectSeed => DeclaredTask?.EffectSeed ?? (Seed ^ 0x192abc);
        int ProgressionChoiceSeed => DeclaredTask?.ProgressionSeed ?? Seed;
        void InitializeDeclaredBuild(PlayerState player,BuildDefinition build)
        {
            player.Fame=build.Fame;player.Armor=2+(player.Level+1)/4;
            var deck=player.Deck.DrawPile.ToList();
            foreach(var id in build.AdditionalCards)
            {
                var type=Spells.Any(s=>s.Id==id)?CardType.Spell:Items.Any(i=>i.Id==id)?CardType.Artifact:CardType.Action;
                deck.Add(new DeedCard(id,type));
            }
            player.Deck.SetDeck(deck);
            player.AddWoundCards(build.InitialWounds,toDiscard:true);
            foreach(var id in build.UnitIds)player.Units.Add(new UnitState(Units.Single(u=>u.Id==id)));
            foreach(var kv in build.Crystals)player.Mana.AddCrystal(Enum.Parse<ManaColor>(kv.Key),kv.Value);
        }
    }
}
