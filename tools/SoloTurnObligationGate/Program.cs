using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

string root=Path.GetFullPath(args[0]);
var data=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.GetProperty("native");
CardJsonLoader.SetBaseDirectory(Path.Combine(root,"resources/text_json"));
var catalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}.Select(f=>Path.Combine(root,"resources/original_maps",f)).ToArray());
var episode=SoloEpisodeFactory.Create(data.GetProperty("setup").Deserialize<SoloEpisodeSetup>(),catalog,OriginalRuinCatalog.LoadOptional(root));
var rows=data.GetProperty("records").Deserialize<SoloTrajectoryDecision[]>();
int compared=0;
int rejectedStep=args.Length>2?int.Parse(args[2]):112;
// Explicit cross-version command replay: old private digests contain a different schema.
// Public observations and rewards must still match up to the first changed admission.
foreach(var row in rows.Take(rejectedStep))
{
    var support=SoloDecisionSupport.Capture(episode);
    if(support.ObservationJson!=row.ObservationBefore)throw new Exception("Public prefix mismatch "+row.BeforeStep);
    var command=row.CommandsJson[row.SelectedIndex].DeserializeCommand();
    var transition=command.Execute(episode);
    if(JsonSerializer.Serialize(transition)!=JsonSerializer.Serialize(row.Transition))throw new Exception("Transition mismatch "+row.BeforeStep);
    compared++;
}
var boundary=DemoStateCheckpoint.Capture(episode);
string before=SoloStateDigest.Compute(episode);
bool rejected=false;
try{rows[rejectedStep].CommandsJson[rows[rejectedStep].SelectedIndex].DeserializeCommand().Execute(episode);}
catch(InvalidOperationException){rejected=true;}
if(!rejected||SoloStateDigest.Compute(episode)!=before)throw new Exception("Invalid combat must reject transactionally");
var rests=SoloRestInputs.Legal(episode).ToArray();
if(rests.Length==0)throw new Exception("Missing slow recovery");
SoloRestInputs.Apply(episode,rests[0]);
SoloEndTurnInputs.Apply(episode,new SoloEndTurnInput(Array.Empty<int>()));
boundary.Restore();
if(SoloStateDigest.Compute(episode)!=before)throw new Exception("Restore mismatch");
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,public_prefix_transitions=compared,invalid_combat_rejected=rejected,slow_recovery_choices=rests.Length,rest_then_end_passed=true,scope="Cross-version original failure prefix; not full scenario acceptance"}));

static class Commands
{
    public static SoloPolicyCommand DeserializeCommand(this string json)=>JsonSerializer.Deserialize<SoloPolicyCommand>(json);
}
