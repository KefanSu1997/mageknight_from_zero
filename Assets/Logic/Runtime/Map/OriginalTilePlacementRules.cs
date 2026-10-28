using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// Rulebook p7 §5: seven-hex printed-upright tile topology.
    /// Coordinates are hex centres, not the legacy one-cell MapTile coordinates.
    /// Scenario coastline restrictions must be supplied explicitly by the caller.
    /// This validator does not draw a tile, pay movement, or initialize its sites.
    /// </summary>
    public static class OriginalTilePlacementRules
    {
        // Joining marks of verified A/1/2: (0,0), (1,-3), (3,-2).
        public static IReadOnlyList<AxialCoord> AnchorOffsets { get; } = Array.AsReadOnly(new[]
        {
            new AxialCoord(1, -3), new AxialCoord(3, -2), new AxialCoord(2, 1),
            new AxialCoord(-1, 3), new AxialCoord(-3, 2), new AxialCoord(-2, -1)
        });

        public static bool OnLattice(AxialCoord anchor, AxialCoord origin)
        {
            int q = anchor.Q - origin.Q, r = anchor.R - origin.R;
            return (3 * q + r) % 7 == 0;
        }

        /// <summary>Rulebook p14 cone map, starting A. Brown tiles cannot touch either coastline.</summary>
        public static bool SoloConquestShapeAllows(AxialCoord origin, AxialCoord target, string kind)
        {
            if (!OnLattice(target, origin)) return false;
            // target-origin = left*(1,-3) + right*(3,-2).
            int q = target.Q - origin.Q, r = target.R - origin.R;
            int left = (-2 * q - 3 * r) / 7, right = (3 * q + r) / 7;
            if (left < 0 || right < 0 || left + right == 0) return false;
            return kind == "Countryside" ||
                ((kind == "Core" || kind == "City") && left > 0 && right > 0);
        }

        /// <summary>
        /// Enumerates public positions before drawing. Only the visible deck-back category
        /// is needed; the hidden next tile ID, cells and enemy supply are not inspected.
        /// </summary>
        public static IReadOnlyList<AxialCoord> SoloExplorationPositions(IEnumerable<AxialCoord> placedAnchors,
            AxialCoord origin, AxialCoord playerHex, string visibleDeckKind, bool reserveTile, int range = 1)
        {
            var anchors = placedAnchors.ToArray();
            return anchors.SelectMany(a => AnchorOffsets.Select(d => a + d)).Distinct()
                .Where(p => PlacementError(anchors, origin, p, visibleDeckKind, 0, reserveTile,
                    (target, kind) => SoloConquestShapeAllows(origin, target, kind)) == null)
                .Where(p => CanReachUnrevealedTile(playerHex, p, range))
                .OrderBy(p => p.Q).ThenBy(p => p.R).ToArray();
        }

        public static string PlacementError(IEnumerable<AxialCoord> placedAnchors,
            AxialCoord origin, AxialCoord target, string kind, int rotationDegrees,
            bool reserveTile, Func<AxialCoord, string, bool> scenarioAllows)
        {
            if (scenarioAllows == null) throw new ArgumentNullException(nameof(scenarioAllows));
            if (rotationDegrees != 0) return "printed_orientation_required";
            if (kind != "Countryside" && kind != "Core" && kind != "City") return "invalid_exploration_tile_kind";
            var anchors = new HashSet<AxialCoord>(placedAnchors);
            if (!anchors.Contains(origin) || anchors.Any(a => !OnLattice(a, origin)))
                throw new ArgumentException("Existing anchors do not match the original joining marks.");
            if (!OnLattice(target, origin)) return "off_joining_marks";
            if (anchors.Contains(target)) return "occupied";
            if (!scenarioAllows(target, kind)) return "scenario_coastline_or_shape";
            var adjacent = AnchorOffsets.Select(d => target + d).Where(anchors.Contains).ToArray();
            if (reserveTile) return adjacent.Length >= 3 ? null : "reserve_requires_three_neighbors";
            if (adjacent.Length >= 2) return null;
            if (kind == "Countryside" && adjacent.Length == 1 &&
                AnchorOffsets.Count(d => anchors.Contains(adjacent[0] + d)) >= 2) return null;
            return "insufficient_tile_neighbors";
        }

        public static bool CanReachUnrevealedTile(AxialCoord playerHex, AxialCoord targetAnchor, int range = 1)
        {
            if (range < 1) throw new ArgumentOutOfRangeException(nameof(range));
            var cells = AxialCoord.NeighborDirs.Select(d => targetAnchor + d)
                .Concat(new[] { targetAnchor });
            int distance = cells.Min(c => (Math.Abs(c.Q - playerHex.Q) + Math.Abs(c.R - playerHex.R)
                + Math.Abs(c.Q - playerHex.Q + c.R - playerHex.R)) / 2);
            return distance >= 1 && distance <= range;
        }
    }
}
