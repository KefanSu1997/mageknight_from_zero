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
            "army_2_007", "army_2_008", "army_2_012", "army_2_014", "army_2_011", "army_2_013",
            "army_004", "army_006", "army_012", "army_002", "army_007", "army_013", "army_2_002", "army_000", "army_001", "army_2_000", "army_2_003", "army_014", "army_008", "army_009", "army_010", "army_011", "army_005", "army_2_010", "army_2_004", "army_2_009" };

        public static UnitCard Load(string folder, string id)
        {
            string file = id.StartsWith("army_2_") ? "army_2.json" : "army.json";
            return Parse(File.ReadAllText(Path.Combine(folder, file)), id);
        }

        /// <summary>Same original definition parser for packaged TextAssets and file-based rules tests.</summary>
        public static UnitCard Parse(string json, string id)
        {
            if (!SupportedIds.Contains(id)) throw new InvalidOperationException("该原版部队的完整能力尚未接入");
            var row = JArray.Parse(json).Single(r => (string)r["id"] == id);
            const string assaultFee = "在任何突袭或攻击其他玩家时，你必须支付2点影响力才能激活这支部队。";
            var printedAbilities = ((JArray)row["abilities"]).ToArray();
            bool isAssaultFee(JToken a) => a.Type == JTokenType.String && (string)a == assaultFee;
            bool isMultiple(JToken a) => id == "army_005" && a.Type == JTokenType.String && (string)a == "激活这支部队时，你可以对同一个或不同的目标使用多个不同的能力。";
            bool isFamiliarUpkeep(JToken a) => id == "army_2_004" && a.Type == JTokenType.Object
                && (string)a["effect"] == "招募时支付一点基本色魔力，并将一个魔力标记放在部队该颜色能力上。对应的能力获得提升。每轮开始时，使用你物品栏中一颗任意颜色的魔晶替换该标记，或去除这支部队。";
            var actions = printedAbilities.Where(a => !isAssaultFee(a) && !isMultiple(a) && !isFamiliarUpkeep(a)).Select(ParseAbility).ToArray();
            int level = ((string)row["level"]) switch { "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => throw new InvalidDataException("部队等级缺失") };
            string location = (string)row["recruit_location"];
            var places = RecruitLocation.None;
            if (location.Contains("村庄")) places |= RecruitLocation.Village;
            if (location.Contains("城堡") || location.Contains("要塞")) places |= RecruitLocation.Keep;
            if (location.Contains("修道院")) places |= RecruitLocation.Monastery;
            if (location.Contains("魔法塔")) places |= RecruitLocation.MageTower;
            if (location.Contains("城市")) places |= RecruitLocation.City;
            if (location.Contains("魔法空地")) places |= RecruitLocation.MagicalGlade;
            if (places == RecruitLocation.None) throw new InvalidDataException("部队招募地点未解析");
            string resist = (string)row["resistances"] ?? "";
            var resistances = string.IsNullOrWhiteSpace(resist) ? Array.Empty<Ability>()
                : resist.Split(',').Select(part => part.Trim() switch
                {
                    "物理" => Ability.PhysicalResist, "火焰" => Ability.FireResist,
                    "寒冰" => Ability.IceResist, _ => throw new InvalidDataException("部队抗性未解析：" + part)
                }).ToArray();
            return new UnitCard(id, (string)row["card_name_cn"], (string)row["en_card_name"] ?? "", level,
                int.Parse((string)row["armor"]), int.Parse((string)row["influence_cost"]), places,
                Array.Empty<AttackProfile>(), resistances, !id.StartsWith("army_2_"), Actions: actions,
                DamageInfluenceCost: (int?)row["damage_influence_cost"] ?? 0,
                ReverseRecruitReputation: (bool?)row["reverse_recruit_reputation"] ?? false,
                AssaultActivationInfluenceCost: printedAbilities.Any(isAssaultFee) ? 2 : 0,
                DoubleRecruitReputation: (bool?)row["double_recruit_reputation"] ?? false,
                InteractionRecruitOnly: (bool?)row["interaction_recruit_only"] ?? false,
                RequiresAttunement: printedAbilities.Any(isFamiliarUpkeep));
        }

        private static UnitAbility ParseAbility(JToken raw)
        {
            string text = raw.Type == JTokenType.String ? (string)raw : (string)raw["effect"];
            string cost = raw.Type == JTokenType.String ? "None" : (string)raw["mana_cost"];
            var colors = cost switch
            {
                "None" => Array.Empty<ManaColor>(), "红" => new[] { ManaColor.Red },
                "蓝" => new[] { ManaColor.Blue }, "绿" => new[] { ManaColor.Green },
                "白" => new[] { ManaColor.White }, "黑" => new[] { ManaColor.Black }, _ => throw new InvalidDataException("部队魔力费用未解析")
            };
            if (text == "获得两个任意颜色的魔力标记。")
                return new UnitAbility(text, UnitAbilityKind.GainChosenTokens, 2, Element.Physical, AttackType.Melee, colors);
            if (text == "查看地图上3格之内一个正面朝下的标记，或者任何敌人标记堆顶部的一个标记。如果本回合中击败该标记，获得名望+1。")
                return new UnitAbility(text, UnitAbilityKind.ScoutToken, 3, Element.Physical, AttackType.Melee, colors, MovementPhaseOnly: true);
            if (text == "移动力2。你可以在2格距离翻开一块新的板块。")
                return new UnitAbility(text, UnitAbilityKind.Movement, 2, Element.Physical, AttackType.Melee, colors, ExplorationRange: 2, MovementPhaseOnly: true);
            if (text == "攻击3 (红 +2)")
                return new UnitAbility(text, UnitAbilityKind.Attack, 3, Element.Physical, AttackType.Melee, colors, AttunementColor: ManaColor.Red, AttunementBonus: 2);
            if (text == "格挡4 (蓝 +3)")
                return new UnitAbility(text, UnitAbilityKind.Block, 4, Element.Physical, AttackType.Melee, colors, AttunementColor: ManaColor.Blue, AttunementBonus: 3);
            if (text == "移动力或影响力3 (白 +2)")
                return new UnitAbility(text, UnitAbilityKind.MovementOrInfluence, 3, Element.Physical, AttackType.Melee, colors, AttunementColor: ManaColor.White, AttunementBonus: 2);
            if (text == "治疗2 (绿 +1)")
                return new UnitAbility(text, UnitAbilityKind.Healing, 2, Element.Physical, AttackType.Melee, colors, AttunementColor: ManaColor.Green, AttunementBonus: 1);
            if (text == "远程攻击1。本回合中你所有其他部队的所有攻击+1。")
                return new UnitAbility(text, UnitAbilityKind.Attack, 1, Element.Physical, AttackType.Ranged, colors, OtherUnitAttackBonus: 1);
            if (text == "使一个敌人的护甲和一次攻击降低1点。")
                return new UnitAbility(text, UnitAbilityKind.WeakenEnemyAttack, 1, Element.Physical, AttackType.Melee, colors);
            if (text == "使一次敌人的攻击降低3点。即使敌人具有刺杀，此次攻击造成的伤害必须先分配给这支部队。")
                return new UnitAbility(text, UnitAbilityKind.InterceptEnemyAttack, 3, Element.Physical, AttackType.Melee, colors);
            if (text == "目标敌人不攻击。")
                return new UnitAbility(text, UnitAbilityKind.StopEnemy, 0, Element.Ice, AttackType.Melee, colors);
            if (text == "如果目标敌人被格挡，消灭目标敌人。")
                return new UnitAbility(text, UnitAbilityKind.DestroyBlockedEnemy, 0, Element.Fire, AttackType.Melee, colors);
            if (text == "目标敌人护甲降低5点（最低至1）。")
                return new UnitAbility(text, UnitAbilityKind.ReduceEnemyArmor, 5, Element.Physical, AttackType.Ranged, colors);
            if (text == "目标敌人失去所有城防和抗性。")
                return new UnitAbility(text, UnitAbilityKind.RemoveEnemyDefenses, 0, Element.Physical, AttackType.Ranged, colors);
            if (text == "获得一颗白色魔晶，放入物品栏。")
                return new UnitAbility(text, UnitAbilityKind.GainCrystal, 1, Element.Physical, AttackType.Melee, colors, GrantedMana: ManaColor.White);
            if (text == "本次战斗中无城防的目标敌人不攻击。")
                return new UnitAbility(text, UnitAbilityKind.StopUnfortifiedEnemy, 0, Element.Physical, AttackType.Melee, colors);
            if (text == "目标敌人失去所有城防。远程攻击3。" || text == "目标敌人失去所有抗性。远程攻击3。")
                return new UnitAbility(text, UnitAbilityKind.Attack, 3, Element.Physical, AttackType.Ranged, colors,
                    RemoveTargetFortifications: text.Contains("城防"), RemoveTargetResistances: text.Contains("抗性"));
            if (text == "移动力2。本回合中森林、丘陵和沼泽的移动费用降低1点，最低至0。")
                return new UnitAbility(text, UnitAbilityKind.Movement, 2, Element.Physical, AttackType.Melee, colors,
                    MovementReductionTerrains: new[] { TerrainType.Forest, TerrainType.Hills, TerrainType.Swamp }, MovementReduction: 1);
            if (text == "冰火攻击或冰火格挡5。你可以支付一点蓝色或红色魔力将此数值提升至7，或支付蓝红各一点提升至9。")
                return new UnitAbility(text, UnitAbilityKind.AttackOrBlock, 5, Element.ColdFire, AttackType.Melee, colors, RedBlueBoost: true);
            if (text == "选择一项：本次战斗中你所有的攻击变为冰火攻击，或所有攻击附加攻城属性。")
                return new UnitAbility(text, UnitAbilityKind.TransformAttacks, 0, Element.Physical, AttackType.Melee, colors);
            if (text == "本次战斗中目标敌人不攻击，且受到护甲-3（最低至1）。")
                return new UnitAbility(text, UnitAbilityKind.FreezeArmor, 3, Element.Ice, AttackType.Melee, colors);
            if (text == "获得一个红色魔力标记和一颗红色魔晶。将魔晶放入物品栏。" ||
                text == "获得一个蓝色魔力标记和一颗蓝色魔晶。将魔晶放入物品栏。")
                return new UnitAbility(text, UnitAbilityKind.GainMana, 1, Element.Physical, AttackType.Melee, colors,
                    GrantedMana: text.Contains("红色") ? ManaColor.Red : ManaColor.Blue);
            if (text == "获得一个绿色魔力标记。")
                return new UnitAbility(text, UnitAbilityKind.GainToken, 1, Element.Physical, AttackType.Melee, colors, GrantedMana: ManaColor.Green);
            if (text == "治疗2。" || text == "治疗4。")
                return new UnitAbility(text, UnitAbilityKind.Healing, text == "治疗4。" ? 4 : 2, Element.Physical, AttackType.Melee, colors);
            if (text == "影响力5。如果用于交涉，声誉+1。")
                return new UnitAbility(text, UnitAbilityKind.Influence, 5, Element.Physical, AttackType.Melee, colors, InteractionReputationChange: 1);
            if (text == "本回合中你所有的部队获得所有抗性。")
                return new UnitAbility(text, UnitAbilityKind.AllUnitResistance, 0, Element.Physical, AttackType.Melee, colors);
            if (text == "重整一支1或2级部队。")
                return new UnitAbility(text, UnitAbilityKind.ReadyUnit, 2, Element.Physical, AttackType.Melee, colors);
            string patternText = text.Replace("寒冰攻击或寒冰格挡", "寒冰攻击或格挡")
                .Replace("火焰攻击或火焰格挡", "火焰攻击或格挡");
            var match = Regex.Match(patternText, @"^(攻城|远程)?(寒冰|火焰|冰火)?(攻击或格挡|攻击|格挡|移动力|影响力)(\d+)[。]?(.*)$");
            if (!match.Success) throw new InvalidDataException("部队能力不能近似执行：" + text);
            string suffix = match.Groups[5].Value;
            int wounds = suffix == "这支队伍受到一点创伤。" || suffix == "这支部队受到一点创伤。" ? 1 : 0;
            int reputation = suffix == "，声誉-1" || suffix == "，声誉-1。" ? -1 : 0;
            bool doubleAgainstSwift = suffix == "对抗迅捷攻击时计算两次。";
            if (suffix.Length > 0 && wounds == 0 && reputation == 0 && !doubleAgainstSwift) throw new InvalidDataException("部队后续结算未解析：" + text);
            string action = match.Groups[3].Value;
            var kind = action switch { "格挡" => UnitAbilityKind.Block, "攻击或格挡" => UnitAbilityKind.AttackOrBlock,
                "移动力" => UnitAbilityKind.Movement, "影响力" => UnitAbilityKind.Influence, _ => UnitAbilityKind.Attack };
            var element = match.Groups[2].Value switch { "寒冰" => Element.Ice, "火焰" => Element.Fire, "冰火" => Element.ColdFire, _ => Element.Physical };
            var type = match.Groups[1].Value == "远程" ? AttackType.Ranged : match.Groups[1].Value == "攻城" ? AttackType.Siege : AttackType.Melee;
            return new UnitAbility(text, kind, int.Parse(match.Groups[4].Value), element, type, colors, wounds, reputation,
                DoubleAgainstSwift: doubleAgainstSwift);
        }
    }
}
