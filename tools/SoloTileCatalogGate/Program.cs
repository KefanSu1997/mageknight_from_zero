using System;
using System.IO;
using System.Linq;
using MK.Logic.Runtime.Map;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;

string[] files = { "tutorial_opening.json", "countryside_3_11_transcription.json", "core_1_8_transcription.json" };
var documents = files.Select(f => File.ReadAllText(Path.Combine("resources/original_maps", f))).ToArray();
var catalog = PrintedTileCatalog.Parse(documents);
int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
Check(catalog.Tiles.Count == 20, "Base catalog A + 11 countryside + 8 core");
Check(catalog.Tiles.Values.Sum(t => t.Cells.Count) == 140, "140 printed cells");
Check(catalog.Tiles.Values.Count(t => t.HasCity) == 4, "Four city tiles");
Check(catalog.Tiles.Values.Where(t => t.Kind == "Core").All(t => t.DeckBack == "Core"), "Brown back does not leak city presence");
Check(catalog.Tiles["A"].Cells.Any(c => c.Terrain == "Sea"), "Sea remains distinct from lake");
Check(catalog.Tiles["7"].Cells.Single(c => c.Local.Equals(new AxialCoord(0,1))).Site == "Dungeon", "Printed countryside7 dungeon");
foreach (var kind in new[] { "missing_cell", "duplicate_cell", "unknown_site", "city_color", "walls", "rotation" })
{
    var root = JObject.Parse(documents[2]); var tile = root["tiles"][4];
    var cells = (JArray)tile["cells"];
    switch (kind)
    {
        case "missing_cell": cells.RemoveAt(0); break;
        case "duplicate_cell": cells[1] = cells[0].DeepClone(); break;
        case "unknown_site": cells[0]["site"] = "InventedSite"; break;
        case "city_color": cells[0]["cityColor"] = "Black"; break;
        case "walls": tile["walls"] = new JArray(); break;
        case "rotation": tile["rotation"] = 60; break;
    }
    bool rejected = false;
    try { PrintedTileCatalog.Parse(root.ToString()); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Reject " + kind);
}
bool duplicateRejected = false;
try { PrintedTileCatalog.Parse(documents[0], documents[0]); } catch (InvalidDataException) { duplicateRejected = true; }
Check(duplicateRejected, "Duplicate IDs rejected across sources");
Console.WriteLine($"PASS {checks} native catalog checks; definitions only, not exploration/site acceptance.");
for (ulong seed=1; seed<=16; seed++)
{
    var supply=SoloConquestTileSupply.Create(catalog,seed);
    var initial=supply.Capture();
    Check(initial.Main.Length==11 && initial.ReserveCountry.Length==4 && initial.ReserveCore.Length==4,"Solo 7+2+2 and reserves");
    Check(initial.Main.Take(7).All(id=>catalog.Tiles[id].Kind=="Countryside"),"Country on top");
    Check(initial.Main.Skip(7).Count(id=>catalog.Tiles[id].HasCity)==2,"Two shuffled cities");
    var restored=SoloConquestTileSupply.Restore(catalog,initial);
    for(int draw=0;draw<19;draw++)
    {
        Check(supply.Observe()==restored.Observe(),"Public backs restored");
        Check(supply.Observe().IsReserve==(draw>=11),"Reserve placement rule flag");
        Check(supply.Draw().Id==restored.Draw().Id,"Exact hidden order restored");
        restored=SoloConquestTileSupply.Restore(catalog,restored.Capture());
    }
    Check(supply.Observe().VisibleBack==null,"Empty supply");
    bool emptyRejected=false;try{supply.Draw();}catch(InvalidOperationException){emptyRejected=true;}
    Check(emptyRejected,"No extra synthetic tile");
    var broken=initial with { Main=initial.Main.Select((id,i)=>i==1?initial.Main[0]:id).ToArray() };
    bool badRejected=false;try{SoloConquestTileSupply.Restore(catalog,broken);}catch(ArgumentException){badRejected=true;}
    Check(badRejected,"Corrupt inventory rejected");
}
Console.WriteLine($"PASS {checks} catalog/supply checks including every draw restoration across 16 seeds; movement/site transaction still pending.");
// Prepared fixtures exercise placement contracts, not site combat or reward acceptance.
Dictionary<AxialCoord,PlaceState> Prepare(PrintedTileDefinition tile) => tile.Cells
    .Where(c=>c.Site!=null && c.Site!="Portal").ToDictionary(c=>c.Local,c=>new PlaceState(OriginalTileMapPlacement.SiteType(c.Site)) {
        CityColor=c.CityColor==null ? CityColor.None : Enum.Parse<CityColor>(c.CityColor),
        MineColors=c.ManaColor==null ? null : new[]{Enum.Parse<ManaColor>(c.ManaColor)} });
foreach(var tile in catalog.Tiles.Values)
{
    var map=new MapState(); var sites=Prepare(tile); var anchor=new AxialCoord(4,-5);
    OriginalTileMapPlacement.Place(map,tile,anchor,sites);
    Check(map.OriginalCells.Count==7 && map.OriginalTiles.Count==1,"Seven cell native placement");
    Check(map.Placed.Count==tile.Cells.Count(c=>c.Terrain!="Sea"),"Sea not traversable terrain projection");
    Check(map.Places.Count==sites.Count && sites.All(p=>ReferenceEquals(map.Places[anchor+p.Key],p.Value)),"Physical sites preserved");
    bool duplicate=false;try{OriginalTileMapPlacement.Place(map,tile,anchor,sites);}catch(InvalidOperationException){duplicate=true;}
    Check(duplicate && map.OriginalCells.Count==7,"Duplicate placement rejects without mutation");
    if(sites.Count>0)
    {
        var empty=new MapState(); var missing=new Dictionary<AxialCoord,PlaceState>(sites);missing.Remove(missing.Keys.First());
        bool rejected=false;try{OriginalTileMapPlacement.Place(empty,tile,anchor,missing);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && empty.OriginalCells.Count==0 && empty.Placed.Count==0,"Missing site rejects whole tile");
    }
}
Check(OriginalTileMapPlacement.SiteType("Dungeon")!=OriginalTileMapPlacement.SiteType("Tomb"),"Dungeon and tomb distinct");
Check(OriginalTileMapPlacement.SiteType("MonsterDen")!=OriginalTileMapPlacement.SiteType("SpawningGround"),"Den and spawning ground distinct");
Console.WriteLine($"PASS {checks} including native terrain/site placement fixtures; site consumers and complete exploration remain pending.");
// Illegal positions must not become an oracle for hidden map order.
for (ulong seed = 1; seed <= 16; seed++)
{
    var supply = SoloConquestTileSupply.Create(catalog, seed);
    supply.Draw(); supply.Draw(); // Scenario opening consumes two of the seven countryside tiles.
    var origin = new AxialCoord(0, 0);
    var anchors = new[] { origin, new AxialCoord(1,-3), new AxialCoord(3,-2) };
    var player = new AxialCoord(2,-3);
    string before = Newtonsoft.Json.JsonConvert.SerializeObject(supply.Capture());
    foreach (var target in new[] { origin, new AxialCoord(1,0), new AxialCoord(-1,3), new AxialCoord(40,-50) })
    {
        bool rejected = false;
        try { supply.DrawForExploration(anchors, origin, player, target); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid exploration rejected before draw");
        Check(Newtonsoft.Json.JsonConvert.SerializeObject(supply.Capture()) == before,
            "Rejected exploration preserves all piles, revealed IDs and RNG");
    }
    var expected = SoloConquestTileSupply.Restore(catalog, supply.Capture()).Draw().Id;
    Check(supply.DrawForExploration(anchors, origin, player, new AxialCoord(4,-5)).Id == expected,
        "Committed legal position draws exact next tile without reroll");
    Check(supply.Capture().Revealed.Length == 3, "Opening plus one exploration consumes exactly three");
}
Console.WriteLine($"PASS {checks} including public-position-before-draw checks; payment/site transaction still pending.");
{
    var supply = SoloConquestTileSupply.Create(catalog, 101);
    var map = new MapState(); var origin = new AxialCoord(0,0);
    OriginalTileMapPlacement.Place(map, catalog.Tiles["A"], origin, Prepare(catalog.Tiles["A"]));
    foreach (var anchor in new[] { new AxialCoord(1,-3), new AxialCoord(3,-2) })
    {
        var tile = supply.Draw(); OriginalTileMapPlacement.Place(map,tile,anchor,Prepare(tile));
    }
    var player = new PlayerState { Position = new AxialCoord(2,-3) };
    var context = new ActionContext { MovementMap = map, MovementPool = 4 };
    var rng = new Random(37); var referenceRng = new Random(37);
    var sideEffects = new List<int>();
    string before = Newtonsoft.Json.JsonConvert.SerializeObject(supply.Capture());
    bool failed = false;
    try
    {
        OriginalExplorationTransaction.Explore(map,supply,player,context,origin,new AxialCoord(4,-5),tile =>
        {
            sideEffects.Add(rng.Next());
            // Inject a failure after actual placement too, to verify map restoration.
            OriginalTileMapPlacement.Place(map,tile,new AxialCoord(4,-5),Prepare(tile));
            throw new InvalidOperationException("Injected site initialization failure");
        },rng,sideEffects);
    }
    catch (InvalidOperationException ex) { failed = ex.Message.Contains("Injected"); }
    Check(failed,"Initializer failure propagated, not treated as successful exploration");
    Check(context.MovementPool==4 && sideEffects.Count==0,"Payment and supply-side effects restored");
    Check(map.OriginalTiles.Count==3 && map.OriginalCells.Count==21,"Partial map placement restored");
    Check(Newtonsoft.Json.JsonConvert.SerializeObject(supply.Capture())==before,"Hidden tile order restored on failure");
    Check(rng.Next()==referenceRng.Next(),"Initializer RNG restored");
    var placed = OriginalExplorationTransaction.Explore(map,supply,player,context,origin,
        new AxialCoord(4,-5),Prepare);
    Check(context.MovementPool==2 && map.OriginalTiles.ContainsKey(placed.Id) && map.OriginalCells.Count==28,
        "Successful exploration commits exactly one tile and two movement");
    Check(player.Position.Equals(new AxialCoord(2,-3)),"Exploration does not move player");
    context.RestedThisTurn=true;
    bool restRejected=false;
    try { OriginalExplorationTransaction.Explore(map,supply,player,context,origin,new AxialCoord(2,-6),Prepare); }
    catch(InvalidOperationException){restRejected=true;}
    Check(restRejected && context.MovementPool==2,"Resting cannot explore or consume movement");
}
Console.WriteLine($"PASS {checks} including exploration transaction rollback; prepared sites are fixtures, not full scenario acceptance.");
