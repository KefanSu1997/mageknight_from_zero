using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Map
{
    // Private save data; never put this object in a policy observation.
    public sealed record SoloTileSupplySnapshot(int Version, string[] Main, string[] ReserveCountry,
        string[] ReserveCore, string[] Revealed, ulong RandomState);
    public sealed record SoloTileSupplyPublic(int MainRemaining, string VisibleBack, bool IsReserve);

    /// <summary>
    /// Rulebook setup and solo conquest: seven countryside, two non-city core and two city core.
    /// The two initial countryside tiles are drawn from those seven, not added to them.
    /// This owns hidden order only; the exploration transaction must validate/pay/place sites.
    /// </summary>
    public sealed class SoloConquestTileSupply
    {
        private readonly PrintedTileCatalog _catalog;
        private List<string> _main, _country, _core, _revealed;
        private ulong _rng;
        private SoloConquestTileSupply(PrintedTileCatalog catalog) { _catalog = catalog; }
        public static SoloConquestTileSupply Create(PrintedTileCatalog catalog, ulong seed)
        {
            if (catalog == null || seed == 0) throw new ArgumentException("Catalog and nonzero seed required");
            var supply = new SoloConquestTileSupply(catalog) { _rng = seed, _revealed = new List<string>() };
            var country = catalog.Tiles.Values.Where(t => t.Kind == "Countryside").Select(t => t.Id).OrderBy(x=>x,StringComparer.Ordinal).ToList();
            var cities = catalog.Tiles.Values.Where(t => t.Kind == "Core" && t.HasCity).Select(t=>t.Id).OrderBy(x=>x,StringComparer.Ordinal).ToList();
            var noncities = catalog.Tiles.Values.Where(t => t.Kind == "Core" && !t.HasCity).Select(t=>t.Id).OrderBy(x=>x,StringComparer.Ordinal).ToList();
            if (country.Count < 7 || cities.Count < 2 || noncities.Count < 2)
                throw new ArgumentException("Insufficient original tiles for solo conquest");
            supply.Shuffle(country); supply.Shuffle(cities); supply.Shuffle(noncities);
            var brown = cities.Take(2).Concat(noncities.Take(2)).ToList(); supply.Shuffle(brown);
            supply._main = country.Take(7).Concat(brown).ToList();
            supply._country = country.Skip(7).ToList();
            supply._core = cities.Skip(2).Concat(noncities.Skip(2)).ToList(); supply.Shuffle(supply._core);
            return supply;
        }
        private ulong Next()
        {
            _rng ^= _rng >> 12; _rng ^= _rng << 25; _rng ^= _rng >> 27;
            return unchecked(_rng * 2685821657736338717UL);
        }
        private void Shuffle(List<string> list)
        {
            for (int i=list.Count-1; i>0; i--)
            {
                ulong n=(ulong)(i+1), threshold=unchecked(0UL-n)%n, value;
                do { value=Next(); } while(value<threshold);
                int j=(int)(value%n); (list[i],list[j])=(list[j],list[i]);
            }
        }
        private List<string> Current => _main.Count > 0 ? _main : _country.Count > 0 ? _country : _core;
        public SoloTileSupplyPublic Observe() => new(_main.Count,
            Current.Count == 0 ? null : _catalog.Tiles[Current[0]].DeckBack, _main.Count == 0 && Current.Count > 0);
        // Position is committed using public information before exposing the hidden tile.
        // The caller still owns movement payment and transactional site initialization.
        public PrintedTileDefinition DrawForExploration(IEnumerable<AxialCoord> placedAnchors,
            AxialCoord origin, AxialCoord playerHex, AxialCoord target, int range = 1)
        {
            var visible = Observe();
            if (visible.VisibleBack == null) throw new InvalidOperationException("No tiles remain");
            var legal = OriginalTilePlacementRules.SoloExplorationPositions(placedAnchors,
                origin, playerHex, visible.VisibleBack, visible.IsReserve, range);
            if (!legal.Contains(target)) throw new InvalidOperationException("Illegal exploration position");
            return Draw();
        }
        // Engine-only draw. Legal positions must be chosen using Observe() BEFORE calling this.
        public PrintedTileDefinition Draw()
        {
            var pile=Current;
            if(pile.Count==0) throw new InvalidOperationException("No tiles remain");
            string id=pile[0]; pile.RemoveAt(0); _revealed.Add(id); return _catalog.Tiles[id];
        }
        public SoloTileSupplySnapshot Capture() => new(1,_main.ToArray(),_country.ToArray(),_core.ToArray(),_revealed.ToArray(),_rng);
        public static SoloConquestTileSupply Restore(PrintedTileCatalog catalog, SoloTileSupplySnapshot snapshot)
        {
            if(catalog==null || snapshot==null || snapshot.Version!=1 || snapshot.RandomState==0
                || snapshot.Main==null || snapshot.ReserveCountry==null || snapshot.ReserveCore==null || snapshot.Revealed==null)
                throw new ArgumentException("Invalid tile supply snapshot");
            var ids=snapshot.Main.Concat(snapshot.ReserveCountry).Concat(snapshot.ReserveCore).Concat(snapshot.Revealed).ToArray();
            var expected=catalog.Tiles.Values.Where(t=>t.Kind!="Starting").Select(t=>t.Id).ToHashSet();
            if(ids.Length!=expected.Count || ids.Distinct().Count()!=ids.Length || !expected.SetEquals(ids))
                throw new ArgumentException("Tile supply does not partition catalog");
            var initial=snapshot.Revealed.Take(11).Concat(snapshot.Main).ToArray();
            if(initial.Length!=11 || initial.Take(7).Any(id=>catalog.Tiles[id].Kind!="Countryside")
                || initial.Skip(7).Any(id=>catalog.Tiles[id].Kind!="Core")
                || initial.Count(id=>catalog.Tiles[id].HasCity)!=2
                || snapshot.ReserveCountry.Any(id=>catalog.Tiles[id].Kind!="Countryside")
                || snapshot.ReserveCore.Any(id=>catalog.Tiles[id].Kind!="Core"))
                throw new ArgumentException("Invalid solo conquest pile composition/order");
            if(snapshot.Revealed.Length>11 && snapshot.Main.Length!=0)
                throw new ArgumentException("Reserve drawn before main pile exhausted");
            var reserveRevealed=snapshot.Revealed.Skip(11).ToArray();
            bool coreSeen=false;
            foreach(var id in reserveRevealed)
            {
                if(catalog.Tiles[id].Kind=="Core")coreSeen=true;
                else if(coreSeen)throw new ArgumentException("Reserve countryside must precede core");
            }
            if(coreSeen && snapshot.ReserveCountry.Length!=0)throw new ArgumentException("Core reserve drawn too soon");
            return new SoloConquestTileSupply(catalog) { _rng=snapshot.RandomState,
                _main=snapshot.Main.ToList(),_country=snapshot.ReserveCountry.ToList(),
                _core=snapshot.ReserveCore.ToList(),_revealed=snapshot.Revealed.ToList() };
        }
    }
}
