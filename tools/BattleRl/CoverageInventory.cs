using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using MK.Logic.Data;
using Newtonsoft.Json.Linq;

// Inventory is a training requirement, never evidence that an item was trained or passed.
static class CoverageInventory
{
    public static void Write(string path)
    {
        var folder=Path.GetFullPath("resources/text_json");
        var entries=new List<object>();var counts=new Dictionary<string,int>();
        foreach(var file in new[]{"basic_card","advanced_card","magic","items","army","army_2"})
        {
            var raw=JArray.Parse(File.ReadAllText(Path.Combine(folder,file+".json")));
            int count=0;
            foreach(var card in raw)
            {
                string id=card.Value<string>("id");
                string name=card.Value<string>("card_name")??card.Value<string>("card_name_cn")??card["spell_top"]?.Value<string>("name");
                if(string.IsNullOrWhiteSpace(name)||name.Contains("牌背"))continue;
                string[] modes=file=="items"?new[]{"normal","burn"}:file.StartsWith("army")?new[]{"actual_unit_abilities"}:new[]{"base","strong"};
                string[] owners=OriginalHeroes.All.Where(h=>h.UniqueCards.Contains(id)).Select(h=>h.Id).ToArray();
                entries.Add(new{id,name,category=file,modes,startingHeroOwners=owners,
                    status="uncovered",requiredEvidence=new[]{"legal_training_transition","real_cost_and_material","downstream_consumer","ui_replay"}});
                count++;
            }
            counts[file]=count;
        }
        var heroes=OriginalHeroes.All.Select(h=>new{h.Id,h.Name,h.StartingCards,
            skills=CardJsonLoader.LoadHeroSkills(h.SkillPoolId).Select(s=>new{s.SourceId,s.Id,status="uncovered"}).ToArray()}).ToArray();
        var enemies=CardJsonLoader.LoadMonsters().Select(m=>new{m.Id,m.Armor,m.Attack,m.AttackCount,m.AttackElement,
            m.Abilities,m.TokenColor,m.Faction,status="uncovered"}).ToArray();
        var contexts=new[]{"rampaging","fortified_site","city","dungeon","multi_enemy","day","night"};
        var result=new{schemaVersion=2,algorithm="PPO",status="inventory_only_not_training_complete",counts,
            heroes,enemies,cards=entries,requiredCombatContexts=contexts,
            coverageFloors=new[]{"all seven heroes","all original enemies and relevant abilities","all card modes with real downstream use",
                "all owned hero skills with legal acquisition","all unit abilities and artifact lifecycle",
                "legal hero-card and hero-enemy pair strata","multiple turns and battles","real day/night round transition"},
            persistence=new[]{"same player and original card identities","hand/draw/discard/wounds","mana/crystals/source quota",
                "unit exhaustion/wounds","skill turn/round frequency","artifact burn/banner ownership","random state"},
            excludedEvidence=new[]{"loaded metadata","put in deck but never drawn","drawn but never used","blocked or illegal operation",
                "new snapshot passed off as continuation","pilot seed permutation passed off as held-out scenario"},
            pilotEvidenceScope=CombatEnv.Scope};
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{path,counts,heroes=heroes.Length,skills=heroes.Sum(h=>h.skills.Length),enemies=enemies.Length,status=result.status}));
    }
}
