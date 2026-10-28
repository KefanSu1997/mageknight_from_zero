using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>Reveal-time original site effects. Must run inside a rollback transaction.</summary>
    public static class SoloPrintedSiteFactory
    {
        public static IReadOnlyDictionary<AxialCoord, PlaceState> Prepare(
            PrintedTileDefinition tile, MapState map, GlobalResources globals)
        {
            if (tile == null || map == null || globals == null) throw new ArgumentNullException();
            // Missing original ruin inventory is an explicit integration gap, never an empty site.
            if (tile.Cells.Any(c => c.Site == "AncientRuin") && globals.Ruins == null)
                throw new NotSupportedException("Original ruin-token supply must be bound before revealing this tile");
            var sites = new Dictionary<AxialCoord, PlaceState>();
            int cities = map.Places.Values.Count(s => s.Type == PlaceType.City);
            foreach (var cell in tile.Cells.Where(c => c.Site != null && c.Site != "Portal"))
            {
                PlaceState site;
                if (cell.Site == "AncientRuin")
                {
                    var token=globals.Ruins.Draw();
                    site=new PlaceState(PlaceType.AncientRuin){RuinToken=token.Kind,OriginalRuinToken=token};
                }
                else if (cell.Site == "City")
                {
                    if (cities >= 2) throw new NotSupportedException("Reserve city level requires an explicit scenario rule");
                    site = SoloCityGarrison.Reveal(Enum.Parse<CityColor>(cell.CityColor),
                        cities++ == 0 ? 5 : 8, globals.Enemies);
                }
                else
                {
                    site = new PlaceState(OriginalTileMapPlacement.SiteType(cell.Site))
                    {
                        MineColors = cell.ManaColor == null ? null : new[] { Enum.Parse<ManaColor>(cell.ManaColor) }
                    };
                    EnemyTokenColor color = cell.Site switch
                    {
                        "RampagingOrcs" => EnemyTokenColor.Green,
                        "RampagingDragon" => EnemyTokenColor.Red,
                        "Keep" => EnemyTokenColor.Gray,
                        "MageTower" => EnemyTokenColor.Purple,
                        _ => EnemyTokenColor.Unknown
                    };
                    if (color != EnemyTokenColor.Unknown) site.Enemy = globals.Enemies.Draw(color);
                    if (cell.Site == "Monastery"&&globals.AdvActions.TryDraw(out var monasteryCard)) globals.MonasteryOffer.Add(monasteryCard);
                    // Underground and lair enemies are drawn on entry, not on tile revelation.
                }
                sites.Add(cell.Local, site);
            }
            return sites;
        }
    }
}
