using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class RuleScenarioView
{
    private void BuildCombat(RectTransform actions)
    {
        Picture(_root, "CombatArt", "UI/RulesScenarios/combat_v1", 356, 226, 1052, 464);
        var band = Panel(_root, "CombatBand", 356, 594, 1052, 96, new Color32(8, 21, 21, 227), false);
        Text(band, "Hero", "行旅骑士", 24, 9, 340, 42, 25, Cream);
        Text(band, "HeroArmor", "英雄护甲  2", 26, 55, 340, 27, 18, Muted);
        Text(band, "Enemy", "边境敌人", 676, 9, 350, 42, 25, Cream, TextAlignmentOptions.Right);
        Text(band, "EnemyStats", "攻击 4    护甲 4    名望 3", 526, 55, 500, 27, 18, Muted, TextAlignmentOptions.Right);
        string[] keys = { "effectiveBlock", "requiredBlock", "effectiveAttack", "wounds", "fame" };
        string[] names = { "有效格挡", "格挡需求", "有效攻击", "伤口", "名望" };
        for (int i = 0; i < keys.Length; i++) Metric(_root, keys[i], names[i], 356 + i * 214, 712, 196, 114);
        Stepper(actions, "block", "格挡分配", "block-", "block+", 28);
        Stepper(actions, "attack", "攻击分配", "attack-", "attack+", 446);
        Command(actions, "blockPhysical", "物理格挡", "block:physical", 26, 113, 180, 48);
        Command(actions, "blockIce", "冰格挡", "block:ice", 219, 113, 180, 48);
        Command(actions, "attackPhysical", "物理攻击", "attack:physical", 444, 113, 180, 48);
        Command(actions, "attackFire", "火焰攻击", "attack:fire", 637, 113, 180, 48);
        Text(actions, "Hint", "格挡和攻击各可分配 0～8 点训练资源。\n结算后可查看伤口与名望，再重新布阵对比。", 858, 22, 904, 60, 19, Muted);
        Command(actions, "resolve", "结算交战", "resolve", 858, 101, 938, 63, true);
    }

    private void Stepper(Transform parent, string key, string caption, string minus, string plus, float x)
    {
        Text(parent, key + "Caption", caption, x, 19, 150, 31, 21, Gold);
        Command(parent, key + "Minus", "−", minus, x, 56, 63, 47);
        ValueLabel(parent, key, x + 92, 51, 166, 54, 39).alignment = TextAlignmentOptions.Center;
        Command(parent, key + "Plus", "+", plus, x + 307, 56, 63, 47);
    }

    private void BuildExploration(RectTransform actions)
    {
        var map = Rect(_root, "Map", 356, 226, 1052, 464);
        Picture(map, "Illustration", "UI/RulesScenarios/exploration_v1", 0, 0, 1052, 464);
        Panel(map, "Mist", 0, 0, 1052, 464, new Color(0.025f, .065f, .06f, .28f), false);
        foreach (var pair in ExplorationLessonSession.Coordinates)
        {
            Vector2 pos = HexPosition(pair.Key);
            var hex = Rect(map, "Btn_select_" + pair.Key, pos.x - 70, pos.y - 79, 140, 158);
            var border = hex.gameObject.AddComponent<RuleHexGraphic>(); border.color = Gold;
            var inner = Rect(hex, "Fill", 2, 2, 136, 154);
            var graphic = inner.gameObject.AddComponent<RuleHexGraphic>(); graphic.color = new Color32(18, 43, 39, 228); graphic.raycastTarget = false;
            var button = hex.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            string id = pair.Key; button.onClick.AddListener(() => _act("select:" + id));
            _hexes[id] = graphic;
            _hexLabels[id] = Text(hex, "Label", "", 10, 34, 120, 100, 18, Cream, TextAlignmentOptions.Center);
        }
        _pawn = Rect(map, "PlayerPawn", 0, 0, 22, 22);
        var image = _pawn.gameObject.AddComponent<Image>(); image.color = Gold; image.raycastTarget = false;
        _pawn.localRotation = Quaternion.Euler(0, 0, 45);
        Text(map, "Caption", "教学地图 · 每个六边格代表一个可移动位置", 22, 14, 680, 25, 15, Cream);
        Metric(_root, "movement", "剩余移动力", 356, 712, 248, 114);
        Metric(_root, "cost", "所选费用", 624, 712, 248, 114);
        Metric(_root, "tiles", "已揭示地块", 892, 712, 248, 114);
        Metric(_root, "tileDeck", "剩余地块", 1160, 712, 248, 114);
        Text(actions, "Hint", "选择地块后，进入已揭示地形或探索未知空位。费用不足、非邻接或地形不可通行时，行动会被拒绝。", 28, 24, 1770, 40, 21, Muted);
        Command(actions, "move", "确认移动 · 支付地形费用", "move", 28, 91, 870, 65, true);
        Command(actions, "explore", "揭示所选空位 · 移动力 2", "explore", 921, 91, 875, 65);
    }

    private static Vector2 HexPosition(string key)
    {
        var c = ExplorationLessonSession.Coordinates[key];
        return new Vector2(340 + c.Q * 164 + c.R * 82, 250 + c.R * 130);
    }

    private void BuildRecruitment(RectTransform actions)
    {
        var sidebar = _root.Find("Sidebar");
        Text(sidebar, "CostCaption", "所选费用", 24, 157, 110, 32, 18, Gold);
        ValueLabel(sidebar, "cost", 144, 147, 110, 46, 30);
        Picture(_root, "RecruitmentArt", "UI/RulesScenarios/recruitment_v1", 356, 226, 1052, 348);
        var band = Panel(_root, "RecruitmentBand", 356, 513, 1052, 61, new Color32(8, 21, 21, 218), false);
        Text(band, "Caption", "可招募的旅伴", 22, 13, 900, 34, 24, Cream);
        string[] details = { "村庄 / 要塞\n护甲3 · 攻击3 · 格挡" + RecruitmentLessonSession.Candidates[0].BlockValue, "村庄\n护甲3 · 远程攻击4", "修道院\n护甲2 · 治疗能力" };
        for (int i = 0; i < 3; i++)
        {
            var card = Panel(_root, "Btn_select_" + i, 356 + i * 357, 591, 338, 235, Ink);
            card.GetComponent<Image>().raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card.GetComponent<Image>();
            int index = i; button.onClick.AddListener(() => _act("select:" + index));
            _choices["unit" + i] = card.GetComponent<Image>();
            var data = RecruitmentLessonSession.Candidates[i];
            Text(card, "Name", data.NameCn, 20, 19, 296, 39, 25, Cream);
            Text(card, "Cost", "影响力  " + data.InfluenceCost, 20, 70, 296, 41, 29, Gold);
            Text(card, "Stats", details[i], 20, 125, 296, 61, 18, Muted);
            _unitStatuses[i] = Text(card, "Hint", "可招募 · 点击选择", 20, 196, 296, 31, 16, Muted);
        }
        Metric(actions, "influence", "可用影响力", 26, 17, 208, 149);
        Metric(actions, "units", "已招募部队", 252, 17, 208, 149);
        Metric(actions, "freeSlots", "空闲指挥槽", 478, 17, 208, 149);
        Metric(actions, "ready", "就绪部队", 704, 17, 208, 149);
        Command(actions, "influence", "打出交涉牌 · +2", "influence", 938, 20, 858, 54);
        Command(actions, "recruit", "招募所选单位", "recruit", 938, 96, 418, 66, true);
        Command(actions, "activate", "使用一名就绪部队", "activate", 1378, 96, 418, 66);
    }

    private void BuildJourney(RectTransform actions)
    {
        Picture(_root, "JourneyArt", "UI/RulesScenarios/exploration_v1", 356, 226, 1052, 361);
        var band = Panel(_root, "JourneyBand", 356, 496, 1052, 91, new Color32(8, 21, 21, 224), false);
        _journeyPhase = Text(band, "Phase", "", 25, 18, 1000, 57, 28, Cream);
        string[] keys = { "hand", "deck", "discard", "movement", "influence", "redCrystal" };
        string[] names = { "手牌", "牌库", "弃牌", "移动力", "影响力", "红色水晶" };
        for (int i = 0; i < 6; i++) Metric(_root, keys[i], names[i], 356 + i * 178, 606, 162, 100);
        string[] combatKeys = { "units", "block", "attack", "wounds", "fame" };
        string[] combatNames = { "部队", "格挡", "攻击", "伤口", "名望" };
        for (int i = 0; i < 5; i++) Metric(_root, combatKeys[i], combatNames[i], 356 + i * 214, 724, 196, 102);
        string[] ids = { "march", "explore", "travel", "talk", "recruit", "endTurn" };
        string[] labels = { "行军 · 移动6", "揭示村庄 · 付2", "进入村庄 · 付2", "交涉 · 影响6", "招募守卫 · 付5", "结束回合并补牌" };
        for (int i = 0; i < ids.Length; i++) Command(actions, ids[i], labels[i], ids[i], 22 + i * 299, 20, 284, 62);
        Command(actions, "unit", "守卫 · 格挡3", "unit", 22, 102, 322, 60);
        Command(actions, "strike", "基础攻击4", "strike", 361, 102, 322, 60);
        Command(actions, "boost", "强化攻击6 · 红1", "boost", 700, 102, 401, 60);
        Command(actions, "battle", "结算战斗 · 敌护甲5 / 攻击3", "battle", 1118, 102, 680, 60, true);
    }

    private void RefreshContent(RuleScenarioSession session, Dictionary<string, string> state)
    {
        if (session is CombatLessonSession combat)
        {
            string[] elements = { "物理", "火焰", "冰", "冷火" };
            _caseLabel.text = $"护甲4 / 攻击4 / 名望3\n\n当前格挡：{elements[(int)combat.BlockElement]}\n当前攻击：{elements[(int)combat.AttackElement]}";
        }
        else if (session is ExplorationLessonSession map)
        {
            _caseLabel.text = $"{(map.Time == DayPart.Day ? "白昼" : "夜晚")} · 初始移动力6\n\n位置 ({map.Player.Position.Q}, {map.Player.Position.R})\n探索揭示不移动棋子";
            var names = new Dictionary<string, string> { ["camp"] = "营地", ["forest"] = "森林", ["plains"] = "平原", ["desert"] = "沙漠", ["lake"] = "湖泊", ["mountain"] = "山脉", ["frontier"] = "未知空位" };
            foreach (var pair in _hexes)
            {
                bool placed = map.Map.Placed.TryGetValue(ExplorationLessonSession.Coordinates[pair.Key], out var tile);
                int cost = placed ? MK.Logic.Runtime.Map.TerrainCost.GetCost(tile.Edges[0], map.Time) : 2;
                pair.Value.color = pair.Key == map.Selected ? new Color32(112, 90, 51, 255) : new Color32(14, 39, 35, 255);
                _hexLabels[pair.Key].text = (pair.Key == "frontier" && placed ? "新平原" : names[pair.Key]) + "\n" + (cost == int.MaxValue ? "不可通行" : (placed ? "进入 " : "探索 ") + cost);
                if (map.Player.Position.Equals(ExplorationLessonSession.Coordinates[pair.Key]))
                { var position = HexPosition(pair.Key); _pawn.anchoredPosition = new Vector2(position.x - 11, -position.y + 54); }
            }
        }
        else if (session is RecruitmentLessonSession recruit)
        {
            _caseLabel.text = $"{(recruit.Location == RecruitLocation.Monastery ? "修道院" : "村庄")} · 等级{recruit.Player.Level}\n声望修正 +{recruit.ReputationBonus}\n指挥槽 {recruit.Player.Units.Count}/{recruit.Player.CommandSlots}";
            for (int i = 0; i < 3; i++)
            {
                _choices["unit" + i].color = recruit.Hired.Contains(RecruitmentLessonSession.Candidates[i].Id) ? new Color32(33, 40, 38, 245) :
                    i == recruit.Selected ? new Color32(56, 72, 56, 255) : Ink;
                var hired = recruit.Player.Units.FirstOrDefault(u => u.Card.Id == RecruitmentLessonSession.Candidates[i].Id);
                _unitStatuses[i].text = hired == null ? "可招募 · 点击选择" : hired.IsReady ? "已招募 · 就绪" : "已招募 · 已用";
            }
        }
        else if (session is JourneyLessonSession journey)
        {
            _caseLabel.text = $"回合 {journey.Turn} / 2\n\n{(journey.AtVillage ? "位置：平原村庄" : "位置：起始营地")}\n\n卡牌守恒：{journey.Hand + journey.Deck + journey.Discard + journey.Played} / 16";
            _journeyPhase.text = journey.Completed ? "远征完成 · 村庄已安全，名望 +3" : journey.Turn == 1 ?
                "01   营地  →  揭示村庄  →  招募盟友" : "02   守卫协同  →  强化攻击  →  清剿敌人";
        }
    }
}
