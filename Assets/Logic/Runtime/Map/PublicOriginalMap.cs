using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    public sealed record PublicMapGuard(int Slot, string BackColor, bool Revealed, string DefinitionId);
    public sealed record PublicRuinDetails(string Id, string[] ManaCost, int Fame, string[] Enemies, string[] Rewards);
    public sealed record PublicMapSite(string Type, bool Conquered, int? OwnerId,
        string CityColor, int CityLevel, string[] MineColors, bool TokenRevealed, string RuinToken, PublicMapGuard[] Guards)
    {
        public PublicRuinDetails RuinDetails { get; init; }
        public bool MonasteryBurned { get; init; }
    }
    public sealed record PublicMapCell(int Q, int R, string Terrain, string PrintedSite, PublicMapSite Site);

    /// <summary>
    /// Public map projection from revealed cells only. No supply, hidden tile identity,
    /// private seed, or hidden guard definition is returned. Consumers may resolve only
    /// non-null guard DefinitionId values to printed enemy rules.
    /// </summary>
    public static class PublicOriginalMap
    {
        public static PublicMapCell[] Observe(MapState map)
        {
            if(map==null)throw new ArgumentNullException(nameof(map));
            return map.OriginalCells.Values.OrderBy(c=>c.Position.Q).ThenBy(c=>c.Position.R)
                .Select(cell=>new PublicMapCell(cell.Position.Q,cell.Position.R,cell.TerrainName,cell.Site,
                    map.Places.TryGetValue(cell.Position,out var site)?ObserveSite(map,site):null)).ToArray();
        }
        public static PublicMapSite ObserveSite(MapState map, PlaceState site)
        {
            if(map==null||site==null||!map.Places.Values.Contains(site))throw new ArgumentException("Actual mapped site required");
            bool visible=map.RevealedSites.Contains(site);
            var guards=site.Defenders.Select((enemy,index)=>
            {
                bool known=visible&&!site.HiddenGuards.Contains(enemy);
                return new PublicMapGuard(index,enemy.TokenColor.ToString(),known,known?enemy.Id:null);
            }).ToArray();
            return new PublicMapSite(site.Type.ToString(),site.Conquered,site.OwnerId,
                site.Type==PlaceType.City?site.CityColor.ToString():null,site.Type==PlaceType.City?site.CityLevel:0,
                site.MineColors?.Select(c=>c.ToString()).ToArray()??Array.Empty<string>(),visible,
                visible?site.RuinToken?.ToString():null,guards)
            {
                MonasteryBurned=site.Type==PlaceType.Monastery&&site.MonasteryBurned,
                RuinDetails=visible&&site.OriginalRuinToken!=null&&!site.RuinConquered
                    ?new PublicRuinDetails(site.OriginalRuinToken.Id,
                        site.OriginalRuinToken.ManaCost.Select(c=>c.ToString()).ToArray(),site.OriginalRuinToken.Fame,
                        site.OriginalRuinToken.Enemies.Select(c=>c.ToString()).ToArray(),
                        site.OriginalRuinToken.Rewards.Select(r=>r.ToString()).ToArray()):null
            };
        }
    }
}
