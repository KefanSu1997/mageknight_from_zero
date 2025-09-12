using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq; // 改以 Newtonsoft 讀取 JSON

namespace MK.Logic.Data
{
    /// <summary>
    /// 從 resources/text_json/place.json 讀取特殊地點資料。
    /// </summary>
    public static class PlaceJsonLoader
    {
        private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "../../../../resources/text_json");

        public static List<PlaceData> LoadPlaces()
        {
            // 解析地點資訊，使用 JArray 配合 IL2CPP
            var json = File.ReadAllText(Path.Combine(Folder, "place.json"));
            var doc = JArray.Parse(json);
            var list = new List<PlaceData>();
            foreach (var el in doc)
            {
                if (el["flip_effect"] == null)
                    continue; // 缺少必要欄位則忽略

                list.Add(new PlaceData(
                    (string)el["id"]!,
                    (string)el["site_name"]!,
                    (string)el["flip_effect"]!,
                    (string)el["ongoing_effect"]!,
                    (string)el["action"]!,
                    (string)el["reward"]!
                ));
            }
            return list;
        }
    }
}
