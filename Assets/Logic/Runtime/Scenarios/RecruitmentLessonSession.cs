using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Scenarios
{
    public sealed class RecruitmentLessonSession : RuleScenarioSession
    {
        public static readonly UnitCard[] Candidates =
        {
            new("lesson_guard", "边境守卫", "Frontier Guard", 1, 3, 5, RecruitLocation.Village | RecruitLocation.Keep,
                new[] { new AttackProfile(AttackType.Melee, 3, Element.Physical) }, new[] { Ability.Guard }, BlockValue: 3),
            new("lesson_ranger", "林地游侠", "Forest Ranger", 2, 3, 7, RecruitLocation.Village,
                new[] { new AttackProfile(AttackType.Ranged, 4, Element.Physical) }, Array.Empty<Ability>()),
            new("lesson_monk", "修道院学徒", "Monastery Adept", 1, 2, 6, RecruitLocation.Monastery,
                Array.Empty<AttackProfile>(), new[] { Ability.Heal })
        };
        public string Case { get; private set; }
        public int Influence { get; private set; }
        public int ReputationBonus { get; private set; }
        public RecruitLocation Location { get; private set; }
        public int Selected { get; private set; }
        public bool InfluenceCardUsed { get; private set; }
        public HashSet<string> Hired { get; } = new();
        public int Cost => RecruitmentService.GetCost(Player, Candidates[Selected], Location, applyReputationModifier: false);

        public RecruitmentLessonSession() => Reset("village");

        private void Reset(string scenario)
        {
            Case = scenario;
            Player = new PlayerState { Fame = scenario == "reputation" ? 8 : 0, Reputation = scenario == "reputation" ? 2 : 0 };
            Location = scenario == "monastery" ? RecruitLocation.Monastery : RecruitLocation.Village;
            // 教学直接给出本次互动的声望修正值，不把它当作声望轨道格序。
            ReputationBonus = -ReputationTable.CalcRecruitModifier(Player.Reputation);
            Influence = (scenario == "reputation" ? 10 : scenario == "capacity" ? 12 : scenario == "monastery" ? 6 : 4) + ReputationBonus;
            InfluenceCardUsed = false; Selected = 0; Hired.Clear(); Journal.Clear();
            Message = $"交涉开始：影响力 {Influence}，声望修正已应用一次。";
            Rule = "单位地点图标必须匹配；影响力足够且有空指挥槽才能招募。训练单位数值写在牌面上。";
        }

        protected override bool Execute(string action)
        {
            if (action.StartsWith("case:")) { Reset(action.Substring(5)); return true; }
            if (action == "reset") { Reset(Case); return true; }
            if (action.StartsWith("select:") && int.TryParse(action.Substring(7), out int index) && index >= 0 && index < Candidates.Length)
            { Selected = index; return Outcome(true, $"已选择{Candidates[index].NameCn}，费用 {Cost}。", "选中单位不扣费；单位招募地点见卡面。"); }
            if (action == "influence")
            {
                if (InfluenceCardUsed) return Outcome(false, "交涉牌已使用，本次互动不能再次使用。", "每张行动牌只能使用一次。");
                InfluenceCardUsed = true; int before = Influence; Influence += 2;
                return Outcome(true, $"打出交涉牌：影响力 {before} → {Influence}。", "训练交涉牌提供影响力2；声望修正不再重复增加。");
            }
            if (action == "recruit")
            {
                var unit = Candidates[Selected];
                if (Hired.Contains(unit.Id)) return Outcome(false, "该单位已离开招募列。", "同一张公开单位牌不能重复招募。");
                int before = Influence;
                bool success = new RecruitmentService().TryRecruit(Player, unit, Location, Influence, applyReputationModifier: false);
                if (success) { Influence -= Cost; Hired.Add(unit.Id); }
                string rejection = (unit.RecruitLocationMask & Location) == 0 ? "地点图标不匹配" :
                    before < Cost ? "影响力不足" : "没有空闲指挥槽";
                return Outcome(success, success ? $"{unit.NameCn}加入：影响力 {before} → {Influence}，部队 {Player.Units.Count}。" : $"招募失败：{rejection}；影响力仍为 {Influence}。",
                    "成功才扣牌面费用并占用1个指挥槽，单位就绪加入；声望修正仅在本次交涉开始时应用一次。");
            }
            if (action == "activate")
            {
                var ready = Player.Units.FirstOrDefault(unit => unit.IsReady);
                if (ready == null) return Outcome(false, "没有就绪的部队。", "已横置单位不能重复使用；直到新昼夜才恢复。");
                ready.Exhaust();
                return Outcome(true, $"{ready.Card.NameCn}已横置。", "单位使用一次能力后从就绪变为已用。");
            }
            return Outcome(false, "未知行动。", "只接受本场景提供的行动。");
        }

        public override Dictionary<string, string> ReadState()
        {
            var s = base.ReadState(); s["case"] = Case; s["influence"] = Value(Influence);
            s["reputationBonus"] = Value(ReputationBonus); s["location"] = Location.ToString();
            s["selected"] = Value(Selected); s["cost"] = Value(Cost); s["freeSlots"] = Value(Player.FreeSlots);
            s["offer"] = Value(Candidates.Length - Hired.Count); s["ready"] = Value(Player.Units.Count(u => u.IsReady));
            s["influenceCardUsed"] = InfluenceCardUsed ? "1" : "0";
            return s;
        }
    }
}
