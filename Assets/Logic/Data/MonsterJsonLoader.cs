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
        public static List<Monster> LoadMonsters()
        {
            // 以 JArray 輕量解析，避免 System.Text.Json 在 IL2CPP 下的限制
            var json = File.ReadAllText(Path.Combine(Folder, "monster.json"));
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

                int fame = (int)element["fame"]!;
                string type = (string?)element["monster_type"] ?? "Unknown";
                list.Add(new Monster(id, armor, atkVal, elem, fame, abilities, type));
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
                _ => Ability.Fortified // placeholder
            };
            return s switch
            {
                "swift" or
                "brutal" or
                "poison" or
                "paralyze" or
                "regenerate" or
                "magic resist" => true,
                _ => false
            };
        }
    }
}
