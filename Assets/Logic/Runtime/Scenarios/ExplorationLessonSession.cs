using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Scenarios
{
    public sealed class ExplorationLessonSession : RuleScenarioSession
    {
        public MapState Map { get; private set; }
        public DayPart Time { get; private set; }
        public int Movement { get; private set; }
        public string Selected { get; private set; }
        public static readonly Dictionary<string, AxialCoord> Coordinates = new()
        {
            ["camp"] = new(0, 0), ["forest"] = new(1, 0), ["plains"] = new(0, 1),
            ["desert"] = new(1, -1), ["lake"] = new(-1, 0), ["mountain"] = new(0, -1), ["frontier"] = new(2, 0)
        };
        public int SelectedCost => Map.Placed.TryGetValue(Coordinates[Selected], out var tile)
            ? TerrainCost.GetCost(tile.Edges[0], Time) : 2;

        public ExplorationLessonSession() => Reset(DayPart.Day);

        public static MapTile Tile(int id, TerrainType terrain) => new(TileSet.Countryside, id,
            new[] { terrain, terrain, terrain, terrain, terrain, terrain });

        private void Reset(DayPart time)
        {
            Player = new PlayerState(); Map = new MapState(); Time = time; Movement = 6; Selected = "forest";
            Map.Placed[Coordinates["camp"]] = Tile(0, TerrainType.Plains);
            Map.Placed[Coordinates["forest"]] = Tile(1, TerrainType.Forest);
            Map.Placed[Coordinates["plains"]] = Tile(2, TerrainType.Plains);
            Map.Placed[Coordinates["desert"]] = Tile(3, TerrainType.Desert);
            Map.Placed[Coordinates["lake"]] = Tile(4, TerrainType.Lake);
            Map.Placed[Coordinates["mountain"]] = Tile(5, TerrainType.Mountain);
            Map.Countryside.Push(Tile(6, TerrainType.Plains));
            Journal.Clear(); Message = "已备好6点移动力，选择相邻地形。";
            Rule = "森林白天3/夜间5；沙漠白天5/夜间3。探索空位付2点，翻开后进入仍需另付地形费用。";
        }

        protected override bool Execute(string action)
        {
            if (action == "day" || action == "night" || action == "reset")
            {
                Reset(action == "night" ? DayPart.Night : action == "day" ? DayPart.Day : Time);
                return true;
            }
            if (action.StartsWith("select:") && Coordinates.ContainsKey(action.Substring(7)))
            {
                Selected = action.Substring(7);
                return Outcome(true, "已选择目的地；确认后才消耗移动力。", "选中地块只显示费用，不移动玩家。");
            }
            int before = Movement;
            if (action == "move")
            {
                bool success = new MovementService().TryMove(Player, Coordinates[Selected], Map, Movement, Time);
                if (success) Movement -= SelectedCost;
                return Outcome(success, success ? $"移动力 {before} → {Movement}；已进入目的地。" : "无法移动：检查邻接、地形或剩余移动力。",
                    "只能进入相邻且可通行地块；成功按目标地形扣费，失败保持位置与资源。");
            }
            if (action == "explore")
            {
                // 固定无敌事件的教学地图；不把旧引擎随机事件当作正式地标规则。
                var result = new ExplorationService(Map, new QuietFrontier()).Explore(Player, Coordinates[Selected], 0, Movement);
                if (result.Success) Movement = result.RemainingMovement;
                return Outcome(result.Success, result.Success ? $"探索成功：移动力 {before} → {Movement}；玩家留在原地。" : result.ErrorMessage,
                    "相邻空位、牌堆非空且移动力≥2时才能探索；揭示地块不等于进入地块。");
            }
            return Outcome(false, "未知行动。", "只接受本场景提供的行动。");
        }

        private sealed class QuietFrontier : Random { public override int Next(int maxValue) => maxValue - 1; }

        public override Dictionary<string, string> ReadState()
        {
            var s = base.ReadState();
            s["movement"] = Value(Movement); s["day"] = Time == DayPart.Day ? "1" : "0";
            s["positionQ"] = Value(Player.Position.Q); s["positionR"] = Value(Player.Position.R);
            s["tiles"] = Value(Map.Placed.Count); s["tileDeck"] = Value(Map.Countryside.Count + Map.Core.Count);
            s["selected"] = Selected; s["cost"] = SelectedCost == int.MaxValue ? "不可通行" : Value(SelectedCost);
            s["revealed"] = Map.Placed.ContainsKey(Coordinates["frontier"]) ? "1" : "0";
            return s;
        }
    }
}
