using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using MK.Logic.Data;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

string root=Path.GetFullPath(args[0]);
var original=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.GetProperty("native");
var setup=original.GetProperty("setup").Deserialize<SoloEpisodeSetup>();
CardJsonLoader.SetBaseDirectory(Path.Combine(root,"resources/text_json"));
var catalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}.Select(f=>Path.Combine(root,"resources/original_maps",f)).ToArray());
var ruins=OriginalRuinCatalog.LoadOptional(root);
var expected=CardJsonLoader.LoadMonsters().Select(m=>m.Id).OrderBy(x=>x).ToArray();
var orders=new HashSet<string>();var firstGreen=new HashSet<string>();
string inventory=null;int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
for(int seed=0;seed<32;seed++)
{
    var configured=setup with {Seeds=setup.Seeds with {Enemies=seed}};
    var a=SoloEpisodeFactory.Create(configured,catalog,ruins);
    var b=SoloEpisodeFactory.Create(configured,catalog,ruins);
    Check(JsonSerializer.Serialize(a.Observe())==JsonSerializer.Serialize(b.Observe()),"Same-seed initial public state");
    var supply=a.Journey.Game.Globals.Enemies;
    Check(supply.DrawPile.Concat(supply.InUse).Concat(supply.DiscardPile).Select(m=>m.Id).OrderBy(x=>x).SequenceEqual(expected),"Physical token inventory conserved");
    string order=string.Join(",",supply.DrawPile.Select(m=>m.Id));orders.Add(order);
    firstGreen.Add(supply.Peek(EnemyTokenColor.Green).Id);
    string inv=JsonSerializer.Serialize(a.Observe().Inventory);
    if(inventory==null)inventory=inv;
    Check(inv==inventory,"Enemy stream must not alter initial hero deck/resources");
    for(int step=0;step<8 && !a.Terminal;step++)
    {
        var sa=SoloDecisionSupport.Capture(a);var sb=SoloDecisionSupport.Capture(b);
        Check(sa.CommandsJson.SequenceEqual(sb.CommandsJson),"Same-seed support replay");
        var ca=JsonSerializer.Deserialize<SoloPolicyCommand>(sa.CommandsJson[0]);
        var cb=JsonSerializer.Deserialize<SoloPolicyCommand>(sb.CommandsJson[0]);
        Check(JsonSerializer.Serialize(ca.Execute(a))==JsonSerializer.Serialize(cb.Execute(b)),"Same-seed transition replay");
        Check(JsonSerializer.Serialize(a.Observe())==JsonSerializer.Serialize(b.Observe()),"Same-seed public replay");
    }
}
Check(orders.Count==32,"Independent seeds must change initial enemy order");
Check(firstGreen.Count>=3,"Initial draw must use enemy seed");
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,checks,seeds=32,distinct_orders=orders.Count,distinct_green_draws=firstGreen.Count,
    scope="Actual solo reset and eight-action same-seed replay, inventory conservation; not complete scenario or learned policy acceptance"}));
