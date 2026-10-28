using System;
using System.Linq;
using MK.Logic.Runtime.Map;

var origin = new AxialCoord(0, 0);
var opening = new[] { origin, new AxialCoord(1,-3), new AxialCoord(3,-2) };
int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
string Error(AxialCoord p, string kind = "Countryside", int rotation = 0, bool reserve = false)
    => OriginalTilePlacementRules.PlacementError(opening, origin, p, kind, rotation, reserve, (_, _) => true);
var two = new AxialCoord(4,-5);
var one = new AxialCoord(2,-6);
Check(Error(two) == null, "Two-neighbor countryside");
Check(Error(two, "City") == null, "Two-neighbor city");
Check(Error(one) == null, "One neighbor which itself has two neighbors");
Check(Error(one, "Core") == "insufficient_tile_neighbors", "Core cannot use countryside exception");
Check(Error(two, rotation:60) == "printed_orientation_required", "No rotation");
Check(Error(new AxialCoord(1,0)) == "off_joining_marks", "No arbitrary hex anchors");
Check(Error(origin) == "occupied", "No overlap");
Check(Error(two, reserve:true) == "reserve_requires_three_neighbors", "Reserve is stricter");
Check(OriginalTilePlacementRules.PlacementError(opening, origin, two, "Core", 0, false, (_, _) => false)
    == "scenario_coastline_or_shape", "Scenario shape cannot be bypassed");
Check(OriginalTilePlacementRules.CanReachUnrevealedTile(new AxialCoord(1,-1), new AxialCoord(1,-3)), "Player adjacent to tile hex, not anchor");
Check(!OriginalTilePlacementRules.CanReachUnrevealedTile(origin, new AxialCoord(1,-3)), "Cannot explore from too far away");
Check(!OriginalTilePlacementRules.CanReachUnrevealedTile(origin, origin), "Cannot explore occupied own tile");
var hexes = new[] { origin }.Concat(AxialCoord.NeighborDirs).ToArray();
foreach (var offset in OriginalTilePlacementRules.AnchorOffsets)
{
    var shifted = hexes.Select(h => h + offset).ToArray();
    Check(!hexes.Intersect(shifted).Any(), "Adjacent tiles have no overlapping hexes");
    Check(hexes.Sum(h => AxialCoord.NeighborDirs.Count(d => shifted.Contains(h+d))) == 3,
        "Adjacent original tiles share exactly three hex edges");
}
Console.WriteLine($"PASS {checks} original tile topology checks; scenario geometry and site initialization remain separate gates.");
for (int left = -3; left <= 5; left++)
for (int right = -3; right <= 5; right++)
{
    var p = new AxialCoord(left + 3*right, -3*left - 2*right);
    bool land = left >= 0 && right >= 0 && left + right > 0;
    Check(OriginalTilePlacementRules.SoloConquestShapeAllows(origin, p, "Countryside") == land, "A coastline bounds");
    Check(OriginalTilePlacementRules.SoloConquestShapeAllows(origin, p, "Core") == (left > 0 && right > 0), "Brown coast exclusion");
    Check(OriginalTilePlacementRules.SoloConquestShapeAllows(origin, p, "City") == (left > 0 && right > 0), "City is brown");
}
var edgePlayer = new AxialCoord(2,-3);
var countryPositions = OriginalTilePlacementRules.SoloExplorationPositions(opening, origin, edgePlayer, "Countryside", false);
var corePositions = OriginalTilePlacementRules.SoloExplorationPositions(opening, origin, edgePlayer, "Core", false);
Check(countryPositions.Contains(two), "Interior discoverable from actual edge hex");
Check(corePositions.Contains(two), "Core interior discoverable");
Check(corePositions.All(p => OriginalTilePlacementRules.SoloConquestShapeAllows(origin,p,"Core")), "No coastal core support");
Check(!OriginalTilePlacementRules.SoloExplorationPositions(opening, origin, origin, "Countryside", false).Any(), "Portal cannot reveal distant tiles");
Console.WriteLine($"PASS {checks} total checks including solo cone coastline and public exploration support; site initialization remains unintegrated.");
