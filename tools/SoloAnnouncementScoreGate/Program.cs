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
int compared=0;double oldReturn=0,newReturn=0;
foreach(var row in rows)
{
    if(JsonSerializer.Serialize(episode.Observe())!=row.ObservationBefore)throw new Exception("Public prefix mismatch "+row.BeforeStep);
    var command=JsonSerializer.Deserialize<SoloPolicyCommand>(row.CommandsJson[row.SelectedIndex]);
    var transition=command.Execute(episode);
    if(JsonSerializer.Serialize(episode.Observe())!=row.ObservationAfter)throw new Exception("Public transition mismatch "+row.BeforeStep);
    if(!transition.Terminal)
    {
        if(JsonSerializer.Serialize(transition)!=JsonSerializer.Serialize(row.Transition))throw new Exception("Nonterminal transition changed");
    }
    else
    {
        var world=JsonDocument.Parse(row.ObservationAfter).RootElement.GetProperty("World");
        if(!world.GetProperty("Dummy").GetProperty("Announced").GetBoolean())throw new Exception("Expected actual dummy announcement");
        if(world.GetProperty("PlayerAnnounced").GetBoolean())throw new Exception("Expected player did not announce");
        if(row.Transition.Score.NoAnnouncement!=5 || transition.Score.NoAnnouncement!=0
            || transition.Score.Total!=row.Transition.Score.Total-5
            || Math.Abs(transition.Reward-(row.Transition.Reward-.05))>1e-9)throw new Exception("Wrong terminal correction");
    }
    oldReturn+=row.Transition.Reward;newReturn+=transition.Reward;compared++;
}
if(!episode.Terminal || Math.Abs(oldReturn-newReturn-.05)>1e-9)throw new Exception("Not the expected complete return correction");
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,complete=true,public_transitions_unchanged=compared,old_return=oldReturn,new_return=newReturn,terminal_bonus_correction=-5,scope="Actual complete task replay; no training or all-rule acceptance"}));
