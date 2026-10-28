using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

// Synthetic parser fixtures only: deliberately NOT an original token transcription.
string root=Path.Combine(Path.GetTempPath(),"solo_ruin_catalog_gate_"+Guid.NewGuid().ToString("N"));
string folder=Path.Combine(root,"resources","original_ruins");
Directory.CreateDirectory(folder);
if(OriginalRuinCatalog.LoadOptional(root)!=null)throw new Exception("Missing catalog must remain explicit.");
string evidence=Path.Combine(folder,"synthetic.txt");
File.WriteAllText(evidence,"SYNTHETIC TEST DATA, NOT PRINTED RULES");
string sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(evidence))).ToLowerInvariant();
var tokens=Enumerable.Range(0,12).Select(i=>new {
    id="fixture_"+i,source_id="fixture",source_location="synthetic face "+i,
    mana_cost=i<4?Enumerable.Repeat(new[]{"Red","Blue","Green","White"}[i],3).ToArray():Array.Empty<string>(),
    fame=i<4?7:0,enemies=i<4?Array.Empty<string>():new[]{"Green","Brown"},
    rewards=i<4?Array.Empty<string>():new[]{"Artifact"}}).ToArray();
string json=JsonSerializer.Serialize(new {version=1,edition="base",sources=new[]{new{id="fixture",file="synthetic.txt",sha256=sha}},tokens});
string manifest=Path.Combine(folder,"base_tokens.json");
File.WriteAllText(manifest,json);
if(OriginalRuinCatalog.LoadOptional(root).Length!=12)throw new Exception("Fixture load failed.");
int rejected=0;
void Reject(Action<JsonNode> change)
{
    var node=JsonNode.Parse(json);change(node);File.WriteAllText(manifest,node.ToJsonString());
    try{OriginalRuinCatalog.LoadOptional(root);}catch(InvalidDataException){rejected++;return;}
    throw new Exception("Invalid catalog accepted.");
}
Reject(n=>n["sources"][0]["sha256"]="bad");
Reject(n=>n["sources"][0]["file"]="../escape.txt");
Reject(n=>n["tokens"][1]["id"]="fixture_0");
Reject(n=>n["tokens"][4]["enemies"][0]="999");
Reject(n=>n["tokens"][4]["source_id"]="missing");
Reject(n=>n["tokens"][4]["source_location"]="");
Reject(n=>n["tokens"].AsArray().RemoveAt(11));
File.WriteAllText(manifest,json);
File.AppendAllText(evidence,"tampered");
try{OriginalRuinCatalog.LoadOptional(root);throw new Exception("Changed source accepted.");}
catch(InvalidDataException){rejected++;}
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,scope="synthetic_loader_integrity_only",original_inventory_verified=false,rejected_cases=rejected,fixture_root=root}));
