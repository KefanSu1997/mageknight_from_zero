using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>按真实地点和玩家位置翻开标记，揭示结果持续保存在地图中。</summary>
    public static class SiteVisibility
    {
        public static void RevealVisible(PlayerState player, MapState map, DayPart dayPart, ActionContext context = null)
        {
            if (map == null) return;
            bool daylight = dayPart == DayPart.Day || context?.RevealSitesAsDay == true;
            int fortifiedRange = dayPart == DayPart.Day && player.Skills.Any(s => s.SourceId == "hero_skill_pool_004:02") ? 2 : 1;
            foreach (var pair in map.Places)
            {
                if (!map.Placed.ContainsKey(pair.Key)) continue;
                int q = pair.Key.Q - player.Position.Q, r = pair.Key.R - player.Position.R;
                int distance = (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2;
                bool ruin = pair.Value.Type == PlaceType.AncientRuin;
                bool fortified = pair.Value.Type == PlaceType.Keep || pair.Value.Type == PlaceType.MageTower;
                bool city = pair.Value.Type == PlaceType.City;
                if (ruin && (daylight || distance == 0) || fortified && daylight && distance <= fortifiedRange
                    || city && distance <= (dayPart == DayPart.Day ? fortifiedRange : 1))
                    map.RevealedSites.Add(pair.Value);
            }
        }
    }
}
