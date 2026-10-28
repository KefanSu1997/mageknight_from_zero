using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime
{
    /// <summary>Explicit optional choices for the actual end position, not an action or a healing effect.</summary>
    public sealed class SiteTurnSelection
    {
        public DeedCard GladeWound { get; init; }
        public ManaColor? MineColor { get; init; }
    }

    public static class SiteTurnRules
    {
        public static PlaceState At(MapState map, PlayerState player)
            => map != null && player != null && map.Places.TryGetValue(player.Position, out var place) ? place : null;

        internal static void BeginTurn(PlayerState player, DayPart part, MapState map)
        {
            if (At(map, player)?.Type == PlaceType.MagicalGlade)
                player.Mana.AddToken(part == DayPart.Day ? ManaColor.Gold : ManaColor.Black);
        }

        public static void ValidateEndTurn(PlayerState player, MapState map, SiteTurnSelection selection = null)
        {
            var place = At(map, player);
            if (selection?.GladeWound != null)
            {
                var wound = selection.GladeWound;
                if (place?.Type != PlaceType.MagicalGlade || wound.Type != CardType.Wound
                    || !player.Deck.Hand.Any(c => ReferenceEquals(c, wound)) && !player.Deck.DiscardPile.Any(c => ReferenceEquals(c, wound)))
                    throw new InvalidOperationException("请选择当前魔法林地上本人手牌或弃牌中的实际一张创伤，或不移除");
            }
            bool mine = place?.Type == PlaceType.ManaMine || place?.Type == PlaceType.DeepMine;
            if (selection?.MineColor != null && !mine)
                throw new InvalidOperationException("只有回合结束实际停留的矿山可以选择魔晶颜色");
            if (!mine) return;
            var colors = place.MineColors?.Distinct().ToArray();
            if (colors == null || colors.Length == 0 || colors.Any(c => c < ManaColor.Red || c > ManaColor.White))
                throw new InvalidOperationException("实际矿山缺少合法原色");
            if (selection?.MineColor is ManaColor chosen && !colors.Contains(chosen))
                throw new InvalidOperationException("所选颜色不属于实际矿山");
            if (colors.Length > 1 && selection?.MineColor == null)
                throw new InvalidOperationException("请选择实际多色矿山中的一种颜色");
        }

        // Called only after preflight and played-card cleanup; never invokes CardHealing or its bonuses.
        internal static void EndTurn(PlayerState player, MapState map, SiteTurnSelection selection)
        {
            var place = At(map, player);
            if (place?.Type == PlaceType.MagicalGlade && selection?.GladeWound != null)
            {
                var wound = selection.GladeWound;
                int index = player.Deck.Hand.FindIndex(c => ReferenceEquals(c, wound));
                if (index >= 0) player.Deck.Hand.RemoveAt(index);
                else player.Deck.TryRemoveFromDiscardExact(wound);
                // Like other wound removal, it leaves the personal deck for the common wound supply,
                // not the permanently removed action-card zone. Earlier end effects may already remove it.
            }
            if (place?.Type == PlaceType.ManaMine || place?.Type == PlaceType.DeepMine)
            {
                var color = selection?.MineColor ?? place.MineColors[0];
                int stored = player.Mana.Crystals.TryGetValue(color, out int count) ? count : 0;
                if (stored < 3) player.Mana.AddCrystal(color);
            }
        }
    }
}
