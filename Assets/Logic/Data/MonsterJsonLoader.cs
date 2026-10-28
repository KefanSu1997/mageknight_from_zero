using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq; // 以 JObject 解析怪物資料
using MK.Logic.Core;

namespace MK.Logic.Data
{
    /// <summary>
    /// 從 resources/text_json/monster.json 讀取怪物資料。
    /// 僅轉換攻擊、護甲、抗性與能力等欄位。
    /// </summary>
    public static class MonsterJsonLoader
    {
        // 測試環境執行時工作目錄位於 bin/Debug/net8.0，需回推至專案根目錄。
        private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "../../../../resources/text_json");

        /// <summary>
        /// 讀取並轉換所有怪物資料。若資料不完整則跳過。
        /// </summary>
        public static List<Monster> LoadMonsters(string baseDirectory = null)
        {
            // 以 JArray 輕量解析，避免 System.Text.Json 在 IL2CPP 下的限制
            var json = File.ReadAllText(Path.Combine(baseDirectory ?? Folder, "monster.json"));
            return ParseMonsters(json);
        }

        /// <summary>同一正式解析器可读取打包的 TextAsset，运行时不依赖工程目录。</summary>
        public static List<Monster> ParseMonsters(string json)
        {
            var doc = JArray.Parse(json);
            var list = new List<Monster>();
            foreach (var element in doc)
            {
                var atkArr = element["attack"] as JArray;
                if (atkArr == null || atkArr.Count == 0)
                    continue;
                var armorObj = element["armor"] as JObject;
                if (armorObj == null)
                    continue;
                string id = (string)element["id"]!;
                int atkVal = (int)atkArr[0]["value"]!;
                string elemStr = (string)atkArr[0]["element"]!;
                Element elem = ParseElement(elemStr);
                var attacks = new List<EnemyAttack>();
                foreach (var attack in atkArr)
                    attacks.Add(new EnemyAttack((int)attack["value"]!, ParseElement((string)attack["element"]!)));
                int armor = (int)armorObj["value"]!;
                var abilities = new List<Ability>();
                if ((bool?)armorObj["fortified"] == true)
                    abilities.Add(Ability.Fortified);

                var resistArr = armorObj["resist"] as JArray;
                if (resistArr != null)
                {
                    foreach (var r in resistArr)
                    {
                        var ab = ParseResist((string)r!);
                        if (ab != null) abilities.Add(ab.Value);
                    }
                }

                var abilArr = element["abilities"] as JArray;
                if (abilArr != null)
                {
                    foreach (var a in abilArr)
                    {
                        if (TryParseAbility((string)a!, out var ab))
                            abilities.Add(ab);
                    }
                }

                int elusiveArmor = (int?)armorObj["elusive"] ?? 0;
                int defendValue = (int?)element["defend"] ?? 0;
                if (elusiveArmor > 0) abilities.Add(Ability.Elusive);
                if (defendValue > 0) abilities.Add(Ability.Defend);
                Enum.TryParse<EnemyFaction>((string?)element["faction"], out var faction);
                int fame = (int)element["fame"]!;
                string type = (string?)element["monster_type"] ?? "Unknown";
                Enum.TryParse<EnemyTokenColor>((string?)element["token_color"], out var tokenColor);
                list.Add(new Monster(id, armor, atkVal, elem, fame, abilities, type, tokenColor, attacks, elusiveArmor, defendValue, faction, (bool?)element["summon_attack"] ?? false, (int?)element["summon_count"] ?? 1));
            }
            return list;
        }

        private static Element ParseElement(string s)
        {
            s = s.Replace(" ", string.Empty);
            return Enum.TryParse<Element>(s, true, out var e) ? e : Element.Physical;
        }

        private static Ability? ParseResist(string s) => s switch
        {
            "Physical" => Ability.PhysicalResist,
            "Fire" => Ability.FireResist,
            "Ice" => Ability.IceResist,
            "Cold Fire" => Ability.ColdFireResist,
            "ColdFire" => Ability.ColdFireResist,
            _ => null
        };

        private static bool TryParseAbility(string s, out Ability ability)
        {
            ability = s switch
            {
                "swift" => Ability.Swift,
                "brutal" => Ability.Brutal,
                "poison" => Ability.Poison,
                "paralyze" => Ability.Paralyze,
                "regenerate" => Ability.Regenerate,
                "magic resist" => Ability.MagicResist,
                "assassination" => Ability.Assassination,
                "cumbersome" => Ability.Cumbersome,
                "vampirism" => Ability.Vampirism,
                "ignore site fortification" => Ability.IgnoreSiteFortification,
                _ => Ability.Fortified // placeholder
            };
            return s switch
            {
                "swift" or
                "brutal" or
                "poison" or
                "paralyze" or
                "regenerate" or
                "magic resist" or
                "assassination" or "cumbersome" or "vampirism" or "ignore site fortification" => true,
                _ => false
            };
        }
    }
}
