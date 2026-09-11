using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using MK.Logic.Core;

namespace MK.Logic.Data
{
    /// <summary>Loads audited original definitions; unsupported abilities are rejected, never approximated.</summary>
    public static class UnitJsonLoader
    {
        public static readonly string[] SupportedIds = { "army_003", "army_2_001", "army_2_005", "army_2_006",
            "army_2_007", "army_2_008", "army_2_012", "army_2_014" };

        public static UnitCard Load(string folder, string id)
        {
            if (!SupportedIds.Contains(id)) throw new InvalidOperationException("该原版部队的完整能力尚未接入");
            string file = id.StartsWith("army_2_") ? "army_2.json" : "army.json";
            var row = JArray.Parse(File.ReadAllText(Path.Combine(folder, file))).Single(r => (string)r["id"] == id);
            var actions = ((JArray)row["abilities"]).Select(ParseAbility).ToArray();
            int level = ((string)row["level"]) switch { "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => throw new InvalidDataException("部队等级缺失") };
            string location = (string)row["recruit_location"];
            var places = RecruitLocation.None;
            if (location.Contains("村庄")) places |= RecruitLocation.Village;
            if (location.Contains("城堡") || location.Contains("要塞")) places |= RecruitLocation.Keep;
            if (location.Contains("修道院")) places |= RecruitLocation.Monastery;
            if (location.Contains("魔法塔")) places |= RecruitLocation.MageTower;
            if (location.Contains("城市")) places |= RecruitLocation.City;
            if (places == RecruitLocation.None) throw new InvalidDataException("部队招募地点未解析");
            string resist = (string)row["resistances"] ?? "";
            var resistances = string.IsNullOrWhiteSpace(resist) ? Array.Empty<Ability>()
                : resist == "物理" ? new[] { Ability.PhysicalResist } : throw new InvalidDataException("部队抗性未解析");
            return new UnitCard(id, (string)row["card_name_cn"], (string)row["en_card_name"] ?? "", level,
                int.Parse((string)row["armor"]), int.Parse((string)row["influence_cost"]), places,
                Array.Empty<AttackProfile>(), resistances, !id.StartsWith("army_2_"), Actions: actions);
        }

        private static UnitAbility ParseAbility(JToken raw)
        {
            string text = raw.Type == JTokenType.String ? (string)raw : (string)raw["effect"];
            string cost = raw.Type == JTokenType.String ? "None" : (string)raw["mana_cost"];
            var colors = cost switch
            {
                "None" => Array.Empty<ManaColor>(), "红" => new[] { ManaColor.Red },
                "蓝" => new[] { ManaColor.Blue }, "绿" => new[] { ManaColor.Green },
                "白" => new[] { ManaColor.White }, _ => throw new InvalidDataException("部队魔力费用未解析")
            };
            string patternText = text.Replace("寒冰攻击或寒冰格挡", "寒冰攻击或格挡")
                .Replace("火焰攻击或火焰格挡", "火焰攻击或格挡");
            var match = Regex.Match(patternText, @"^(攻城|远程)?(寒冰|火焰)?(攻击或格挡|攻击|格挡|移动力|影响力)(\d+)[。]?(.*)$");
            if (!match.Success) throw new InvalidDataException("部队能力不能近似执行：" + text);
            string suffix = match.Groups[5].Value;
            int wounds = suffix == "这支队伍受到一点创伤。" || suffix == "这支部队受到一点创伤。" ? 1 : 0;
            if (suffix.Length > 0 && wounds == 0) throw new InvalidDataException("部队后续结算未解析：" + text);
            string action = match.Groups[3].Value;
            var kind = action switch { "格挡" => UnitAbilityKind.Block, "攻击或格挡" => UnitAbilityKind.AttackOrBlock,
                "移动力" => UnitAbilityKind.Movement, "影响力" => UnitAbilityKind.Influence, _ => UnitAbilityKind.Attack };
            var element = match.Groups[2].Value switch { "寒冰" => Element.Ice, "火焰" => Element.Fire, _ => Element.Physical };
            var type = match.Groups[1].Value == "远程" ? AttackType.Ranged : match.Groups[1].Value == "攻城" ? AttackType.Siege : AttackType.Melee;
            return new UnitAbility(text, kind, int.Parse(match.Groups[4].Value), element, type, colors, wounds);
        }
    }
}
