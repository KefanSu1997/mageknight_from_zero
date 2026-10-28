using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

// Preparation adapter. All currently implemented choices are retained, but full action
// coverage must pass before formal training. Ruin inventory can be explicitly provisional.
string root=Path.GetFullPath(args.Length==1?args[0]:throw new ArgumentException("Pass repository root."));
bool baselineAProjection=Environment.GetEnvironmentVariable("SOLO_BASELINE_A_OBSERVATION")=="1";
if(baselineAProjection)
    foreach(string name in new[]{"NATIVE_PAYMENTS","NATIVE_CONVERSIONS","NATIVE_ENERGY_TARGETS",
        "SHARED_SKILLS","SHAPESHIFT","ORIGINAL_WORLD","ORIGINAL_OPENINGS"})
        Environment.SetEnvironmentVariable("BATTLE_RL_"+name,"1");
var binding=ResourceBinding.Capture(root);
string bindingDigest=ResourceBinding.Digest(binding);
var ruins=OriginalRuinCatalog.LoadOptional(root);
CardJsonLoader.SetBaseDirectory(Path.Combine(root,"resources","text_json"));
var catalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}
    .Select(f=>Path.Combine(root,"resources","original_maps",f)).ToArray());
SoloConquestEpisode episode=null;
SoloEpisodeSetup setup=null;
SoloDecisionSupport support=null;
string initialStateDigest=null;
var journal=new List<SoloTrajectoryDecision>();
object State()
{
    var observation=episode.Observe();
    if(!baselineAProjection)return new {observation,support_sha256=support?.Sha256,
        support_payload_json=support?.PublicSigningPayload,
        commands=support?.CommandsJson??Array.Empty<string>(),support_seconds=support?.CaptureSeconds??0};
    // Complete commands have no reversible planning streak. The legacy A
    // RoundWindow was a special banner-return menu, not solo EndingTurn.
    // Solo pending obligations remain in the full public DTO alongside A.
    var counters=new BaselineAObservation.Counters(episode.Steps,
        episode.Journey.CompletedTurns+1,observation.World.Round,0,false);
    return new {observation,baseline_a_features=BaselineAObservation.Capture(episode.Journey,counters),
        baseline_a_counter_schema="solo_decisions_completedTurnsPlus1_round_noLegacyPlanningWindow_v1",
        support_sha256=support?.Sha256,support_payload_json=support?.PublicSigningPayload,
        commands=support?.CommandsJson??Array.Empty<string>(),support_seconds=support?.CaptureSeconds??0};
}
string line;
while((line=Console.ReadLine())!=null)
{
    try
    {
        using var request=JsonDocument.Parse(line);
        var r=request.RootElement;string op=r.GetProperty("op").GetString();
        if(ResourceBinding.Digest(ResourceBinding.Capture(root))!=bindingDigest)
            throw new InvalidOperationException("Rules/data changed since process startup; preserve archive and restart explicitly.");
        object result;
        if(op=="capabilities")result=new {protocol=1,formal_training_ready=false,binding_sha256=bindingDigest,binding,
            ruin_inventory_count=ruins?.Length??0,
            ruin_inventory_verification=ruins==null?"missing":"see_hash_bound_catalog_provenance",
            limitations=new[]{"incomplete_action_coverage",ruins==null?"ruin_inventory_not_bound":"ruin_inventory_semantic_audit_required","semantic_cycle_audit_incomplete"}};
        else if(op=="public_schema")result=PublicSchema.Capture();
        else if(op=="reset")
        {
            var proposed=r.GetProperty("setup").Deserialize<SoloEpisodeSetup>();
            var next=SoloEpisodeFactory.Create(proposed,catalog,ruins);
            var nextSupport=SoloDecisionSupport.Capture(next);
            string nextInitialDigest=SoloStateDigest.Compute(next);
            episode=next;setup=proposed;support=nextSupport;initialStateDigest=nextInitialDigest;journal.Clear();result=State();
        }
        else if(op=="restore")
        {
            var archive=r.GetProperty("archive");
            if(archive.GetProperty("version").GetInt32()!=3)throw new InvalidOperationException("Unsupported archive version; explicit migration required.");
            var savedBinding=archive.GetProperty("binding").Deserialize<SortedDictionary<string,string>>();
            if(archive.GetProperty("binding_sha256").GetString()!=bindingDigest
                ||savedBinding==null||binding.Count!=savedBinding.Count
                ||binding.Any(pair=>!savedBinding.TryGetValue(pair.Key,out var value)||value!=pair.Value))
                throw new InvalidOperationException("Archive rules/data binding differs; explicit migration required.");
            var proposed=archive.GetProperty("setup").Deserialize<SoloEpisodeSetup>();
            var records=archive.GetProperty("records").Deserialize<SoloTrajectoryDecision[]>();
            var next=SoloEpisodeFactory.Create(proposed,catalog,ruins);
            string nextInitialDigest=SoloStateDigest.Compute(next);
            if(nextInitialDigest!=archive.GetProperty("initial_state_sha256").GetString())
                throw new InvalidOperationException("Archive initial private state differs from reconstructed setup.");
            SoloTrajectoryReplay.Apply(next,records);
            if(next.Terminal!=archive.GetProperty("terminal").GetBoolean()
                ||next.InitialScore!=archive.GetProperty("initial_score").GetInt32())
                throw new InvalidOperationException("Archive completion/accounting mismatch.");
            var nextSupport=next.Terminal?null:SoloDecisionSupport.Capture(next);
            if(SoloStateDigest.Compute(next)!=archive.GetProperty("current_state_sha256").GetString())
                throw new InvalidOperationException("Archive final private state differs from replay.");
            episode=next;setup=proposed;support=nextSupport;initialStateDigest=nextInitialDigest;journal.Clear();journal.AddRange(records);result=State();
        }
        else
        {
            if(episode==null)throw new InvalidOperationException("Reset required.");
            if(op=="observe")result=State();
            else if(op=="private_initial_world")result=PrivateWorldExport.Capture(episode,catalog);
            else if(op=="export")result=new {version=3,binding_sha256=bindingDigest,binding,setup,records=journal.ToArray(),
                initial_state_sha256=initialStateDigest,current_state_sha256=SoloStateDigest.Compute(episode),
                terminal=episode.Terminal,initial_score=episode.InitialScore};
            else if(op=="step")
            {
                if(support==null||r.GetProperty("support_sha256").GetString()!=support.Sha256)
                    throw new InvalidOperationException("Missing or stale legal support digest.");
                var boundary=DemoStateCheckpoint.Capture(episode);
                SoloTrajectoryDecision row;
                SoloDecisionSupport nextSupport;
                try
                {
                    double? Optional(string name)=>r.TryGetProperty(name,out var v)&&v.ValueKind!=JsonValueKind.Null?v.GetDouble():null;
                    row=support.ExecuteRecorded(episode,r.GetProperty("index").GetInt32(),Optional("log_probability"),Optional("value"));
                    nextSupport=episode.Terminal?null:SoloDecisionSupport.Capture(episode);
                }
                catch {boundary.Restore();throw;}
                support=nextSupport;journal.Add(row);result=new {state=State(),trajectory=row};
            }
            else throw new InvalidOperationException("Unknown operation.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new {ok=true,result}));
    }
    catch(Exception ex)
    {
        Console.WriteLine(JsonSerializer.Serialize(new {ok=false,error=ex.GetType().Name,message=ex.Message}));
    }
    Console.Out.Flush();
}
