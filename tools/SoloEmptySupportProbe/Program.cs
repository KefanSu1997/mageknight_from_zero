using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
SoloTrajectoryReplay.Apply(episode,data.GetProperty("records").Deserialize<SoloTrajectoryDecision[]>());
var j=episode.Journey;var checkpoint=DemoStateCheckpoint.Capture(episode);
var outcomes=new Dictionary<string,string>();
void Probe(string name,Action action){try{action();outcomes[name]="accepted";}catch(Exception error){outcomes[name]=error.ToString();}finally{checkpoint.Restore();}}
Probe("end_empty",()=>SoloEndTurnInputs.Apply(episode,new SoloEndTurnInput(Array.Empty<int>())));
Probe("end_discard_all_nonwounds",()=>SoloEndTurnInputs.Apply(episode,new SoloEndTurnInput(Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>j.Player.Deck.Hand[i].Type!=MK.Logic.Core.CardType.Wound).ToArray())));
Probe("native_end_turn",()=>j.EndTurn());
Console.WriteLine(JsonSerializer.Serialize(new {step=episode.Steps,support=SoloDecisionSupport.Capture(episode).CommandsJson.Count,
    inBattle=j.Context.InBattle,phase=j.Battle?.Phase,active=j.Game.HasActiveTurn,ending=j.Game.IsEndingTurn,
    outcomes},new JsonSerializerOptions{WriteIndented=true}));
