using System;
using System.Collections.Generic;

namespace MK.Logic.Core
{
    /// <summary>
    /// 將中文字的法力顏色轉換為枚舉值的輔助類。
    /// </summary>
    public static class ManaColorParser
    {
        private static readonly Dictionary<string, ManaColor> _map = new()
        {
            ["红"] = ManaColor.Red,
            ["藍"] = ManaColor.Blue,
            ["蓝"] = ManaColor.Blue,
            ["綠"] = ManaColor.Green,
            ["绿"] = ManaColor.Green,
            ["白"] = ManaColor.White,
            ["金"] = ManaColor.Gold,
            ["黑"] = ManaColor.Black,
            ["紫"] = ManaColor.Black,
            ["紫色"] = ManaColor.Black,
            ["无色"] = ManaColor.Gold,
            ["无"] = ManaColor.Gold
        };

        /// <summary>
        /// 解析由逗號分隔的顏色字串。
        /// </summary>
        public static ManaColor[] ParseMany(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<ManaColor>();

            raw = raw.Replace('，', ',').Replace('、', ',');
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var result = new ManaColor[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var key = parts[i].Trim();
                if (key.EndsWith("色")) key = key[..^1];
                if (!_map.TryGetValue(key, out var color))
                    throw new FormatException($"未知顏色:{key}");
                result[i] = color;
            }
            return result;
        }
    }
}
