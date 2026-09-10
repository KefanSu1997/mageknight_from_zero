using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Scenarios
{
    /// <summary>两个回合的固定教学任务；训练牌仅覆盖显示的能力，不替代正式卡牌数据库。</summary>
    public sealed class JourneyLessonSession : RuleScenarioSession
    {
        private MapState _map;
        private readonly HashSet<string> _played = new();
        public int Turn { get; private set; }
        public int Hand { get; private set; }
        public int Deck { get; private set; }
        public int Discard { get; private set; }
        public int Played { get; private set; }
        public int Movement { get; private set; }
        public int Influence { get; private set; }
        public int Attack { get; private set; }
        public int Block { get; private set; }
        public bool Revealed => _map.Placed.ContainsKey(new AxialCoord(1, 0));
        public bool AtVillage => Player.Position.Equals(new AxialCoord(1, 0));
        public bool BattleDone { get; private set; }
        public bool Completed { get; private set; }

        public JourneyLessonSession() => Reset();

        private void Reset()
        {
            Player = new PlayerState(); Player.Mana.AddCrystal(ManaColor.Red);
            _map = new MapState(); _map.Placed[new AxialCoord(0, 0)] = ExplorationLessonSession.Tile(0, TerrainType.Plains);
            _map.Countryside.Push(ExplorationLessonSession.Tile(1, TerrainType.Plains));
            Turn = 1; Hand = 5; Deck = 11; Discard = Played = Movement = Influence = Attack = Block = 0;
            BattleDone = Completed = false; _played.Clear(); Journal.Clear();
            Message = "第一回合：探索平原村庄，并招募一名守卫。";
            Rule = "任务使用16张训练牌、初始手牌5、红晶1。先移动，再交涉；下一回合使用部队与强化攻击清剿敌人。";
        }

        private bool PlayCard(string id)
        {
            if (Hand == 0 || _played.Contains(id)) return false;
            _played.Add(id); Hand--; Played++; return true;
        }

        protected override bool Execute(string action)
        {
            if (action == "reset") { Reset(); return true; }
            if (Completed) return Outcome(false, "旅程已完成，奖励已经结算。", "同一任务只领取一次奖励；重新开始可重放。");
            switch (action)
            {
                case "march":
                    if (Turn != 1 || _played.Contains("talk") || !PlayCard("march")) break;
                    Movement += 6;
                    return Outcome(true, "打出行军：移动力 +6，手牌 -1。", "训练行军牌产生6移动力；交涉后不能再开始移动。");
                case "explore":
                    if (Turn != 1 || _played.Contains("talk")) break;
                    var exploration = new ExplorationService(_map, new QuietFrontier()).Explore(Player, new AxialCoord(1, 0), 0, Movement);
                    if (!exploration.Success) return Outcome(false, exploration.ErrorMessage, "探索支付2点移动力；失败不扣费，不移动玩家。");
                    Movement = exploration.RemainingMovement;
                    return Outcome(true, $"翻开村庄所在平原：移动力剩余 {Movement}。", "探索费用2，玩家仍留在原位；进入平原另付2。");
                case "travel":
                    if (Turn != 1 || _played.Contains("talk")) break;
                    if (!new MovementService().TryMove(Player, new AxialCoord(1, 0), _map, Movement, DayPart.Day))
                        return Outcome(false, "还不能进入村庄。", "目标必须已揭示、相邻，且至少剩余2移动力。");
                    Movement -= TerrainCost.GetCost(TerrainType.Plains, DayPart.Day);
                    return Outcome(true, $"进入平原村庄，移动力剩余 {Movement}。", "地点不覆盖底层地形；本村庄位于平原，移动费用2。");
                case "talk":
                    if (Turn != 1 || !AtVillage || !PlayCard("talk")) break;
                    Influence += 6;
                    return Outcome(true, "打出交涉：影响力 +6，手牌 -1。", "开始交涉后不能再移动。声望修正为0，训练交涉牌提供6影响力。");
                case "recruit":
                    if (Turn != 1 || !AtVillage || !_played.Contains("talk") || Player.Units.Count != 0) break;
                    if (!new RecruitmentService().TryRecruit(Player, RecruitmentLessonSession.Candidates[0], RecruitLocation.Village, Influence)) break;
                    Influence -= 5;
                    return Outcome(true, "招募边境守卫：影响力 6 → 1，指挥槽 0/1 → 1/1。", "支付牌面费用5，单位就绪加入；同一公开单位不能重复招募。");
                case "endTurn":
                    if (Turn != 1) break;
                    Discard += Played; Played = 0; _played.Clear();
                    int draw = Math.Min(5 - Hand, Deck); Hand += draw; Deck -= draw;
                    Movement = Influence = 0; Player.Mana.ResetTokens(); Turn = 2;
                    return Outcome(true, $"回合结束：弃牌 {Discard}，补至手牌 {Hand}，牌库 {Deck}。", "已打出的牌进入弃牌堆，手牌补到5；未用移动力、影响力、临时魔力清零，水晶与部队保留。");
                case "unit":
                    if (Turn != 2 || BattleDone) break;
                    var unit = Player.Units.FirstOrDefault(u => u.CanActivate);
                    if (unit == null) break;
                    unit.Exhaust(); Block += unit.Card.BlockValue;
                    return Outcome(true, "守卫提供格挡3，状态变为已用。", "训练守卫能力：格挡3；单位横置后不能重复发动。");
                case "strike":
                case "boost":
                    if (Turn != 2 || BattleDone || _played.Contains("strike") || Hand == 0) break;
                    if (action == "boost" && !Player.Mana.Pay(new ManaCost(new() { { Element.Fire, 1 } }), Player))
                        return Outcome(false, "红色魔力不足。", "强化攻击需支付红色魔力1；失败不扣手牌或任何资源。");
                    PlayCard("strike"); Attack += action == "boost" ? 6 : 4;
                    return Outcome(true, action == "boost" ? "消耗红晶1：强化攻击6，手牌 -1。" : "基础攻击4，手牌 -1。",
                        "训练猛击：基础攻击4，支付红色魔力1后改为攻击6。同一张牌只能选择一种效果。");
                case "battle":
                    if (Turn != 2 || !AtVillage || BattleDone || !_played.Contains("strike")) break;
                    var enemy = new MK.Logic.Data.Monster("lesson_raider", 5, 3, Element.Physical, 3, Array.Empty<Ability>());
                    var result = BattleResolver.Resolve(Player, new[] { enemy },
                        new[] { new BlockAllocation(0, Block, Element.Physical) },
                        new[] { new AttackAllocation(new[] { 0 }, Attack, Element.Physical) });
                    BattleDone = true; Completed = result.AllKilled;
                    return Outcome(true, $"清剿结束：伤口 +{result.TotalWounds}，名望 +{result.FameGain}。",
                        "敌攻击3/护甲5；格挡3完全挡住，攻击6≥5击败，获得名望3。基础攻击4不足以击败。");
            }
            return Outcome(false, "当前不能执行此行动，资源保持不变。", "检查回合、位置、牌是否用过、单位是否就绪；已完成的行动不能重复结算。");
        }

        private sealed class QuietFrontier : Random { public override int Next(int maxValue) => maxValue - 1; }

        public override Dictionary<string, string> ReadState()
        {
            var s = base.ReadState(); s["turn"] = Value(Turn); s["hand"] = Value(Hand); s["deck"] = Value(Deck);
            s["discard"] = Value(Discard); s["played"] = Value(Played); s["movement"] = Value(Movement);
            s["influence"] = Value(Influence); s["attack"] = Value(Attack); s["block"] = Value(Block);
            s["redCrystal"] = Value(Player.Mana.Crystals.GetValueOrDefault(ManaColor.Red));
            s["redToken"] = Value(Player.Mana.Tokens.GetValueOrDefault(ManaColor.Red));
            s["ready"] = Value(Player.Units.Count(u => u.IsReady)); s["revealed"] = Revealed ? "1" : "0";
            s["positionQ"] = Value(Player.Position.Q); s["positionR"] = Value(Player.Position.R);
            s["completed"] = Completed ? "1" : "0"; s["battleDone"] = BattleDone ? "1" : "0";
            return s;
        }
    }
}
