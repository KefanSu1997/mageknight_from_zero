using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Only the player's original-artifact input choices; effects remain in CardPlayActions.</summary>
    public static class PveArtifactInputs
    {
        public static bool IsResource(string id) => id is "items_004" or "items_005" or "items_006" or "items_007"
            or "items_010" or "items_011" or "items_014" or "items_015";
        public static bool IsAttachedBanner(string id) => id is "items_000" or "items_001" or "items_002" or "items_003" or "items_018";
        public static bool IsMaterial(string id) => id is "items_013" or "items_021" or "items_023";
        public static bool IsSpecial(string id) => id is "items_017" or "items_022" or "items_024";
        public static bool Supports(string id) => IsResource(id) || IsAttachedBanner(id) || IsMaterial(id) || IsSpecial(id)
            || id is "items_008" or "items_009" or "items_012" or "items_016" or "items_019" or "items_020";
        public static bool OutsideAllowed(string id, bool burn) => IsResource(id) || id == "items_012"
            || IsAttachedBanner(id) && (id != "items_001" || !burn);
        public static int ColorCount(string id, bool burn) => id == "items_015" ? (burn ? 3 : 1) : id == "items_011" && !burn ? 2 : 0;
        public static int ColorOption(string id, bool burn, ManaColor[] colors)
        {
            if (!IsResource(id)) throw new InvalidOperationException("请选择已接入的原资源神器");
            if (colors == null || colors.Length != ColorCount(id,burn))
                throw new InvalidOperationException("请为原效果的每颗魔力或每次金色骰选择颜色");
            int result=0;
            for(int i=0;i<colors.Length;i++)
            {
                int color=(int)colors[i];
                if(color<0 || color>(id=="items_011" ? 3 : 5))
                    throw new InvalidOperationException("所选颜色不符合原神器效果");
                result |= color << (8*i);
            }
            return result;
        }
    }
}
