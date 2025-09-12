using System.Linq;
using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 遊戲中一個特殊地點的狀態，記錄其類型與駐守敵人。
    /// </summary>
    public sealed class PlaceState
    {
        public PlaceType Type { get; init; }

        /// <summary>
        /// 建構函式用來指定地點類型，避免使用 C#11 的 <c>required</c> 關鍵字。
        /// </summary>
        public PlaceState(PlaceType type)
        {
            Type = type;
        }

        // 提供無參建構以利序列化或默認狀態建立。
        public PlaceState() { }
        public Monster? Enemy { get; set; }
        /// <summary>礦山的顏色，深層礦山可能包含兩種。</summary>
        public ManaColor[]? MineColors { get; init; }

        /// <summary>遠古遺跡翻開後的標記。</summary>
        public RuinTokenType? RuinToken { get; init; }
        public bool RuinConquered { get; set; }
        /// <summary>地下城是否已被征服。</summary>
        public bool DungeonConquered { get; set; }

        /// <summary>是否已征服此地點。</summary>
        public bool Conquered => Type switch
        {
            PlaceType.AncientRuin => RuinConquered,
            PlaceType.Dungeon => DungeonConquered,
            _ => Enemy == null
        };

        /// <summary>
        /// 挑戰此地點的敵人。若勝利則根據類型給予聲望並清除敵人。
        /// </summary>
        public bool Challenge(PlayerState player, bool win)
        {
            if (Enemy == null) return false;
            if (win)
            {
                Enemy = null;
                player.Reputation += Type switch
                {
                    PlaceType.OrcRampaging => 1,
                    PlaceType.Dragon => 2,
                    _ => 0
                };
            }
            return win;
        }

        /// <summary>
        /// 在村莊治療。需要支付 3 點影響力。
        /// </summary>
        public bool HealVillage(PlayerState player, int influence)
        {
            if (Type != PlaceType.Village || influence < 3) return false;
            if (player.Wounds > 0) player.Wounds -= 1;
            return true;
        }

        /// <summary>
        /// 洗劫村莊：抽兩張牌並降低聲望。
        /// </summary>
        public void PlunderVillage(PlayerState player)
        {
            if (Type != PlaceType.Village) return;
            player.Deck.DrawExact(2);
            player.Reputation -= 1;
        }

        /// <summary>
        /// 終止回合時在礦山挖掘，獲得對應顏色的魔晶。
        /// </summary>
        public bool MineCrystal(PlayerState player, ManaColor color)
        {
            if (MineColors == null || !MineColors.Contains(color)) return false;
            player.Mana.AddCrystal(color, 1);
            return true;
        }

        /// <summary>
        /// 於遠古遺跡進行祭壇獻祭或挑戰敵人。
        /// </summary>
        public bool EnterRuin(PlayerState player, bool payMana, bool win)
        {
            if (Type != PlaceType.AncientRuin || RuinConquered || RuinToken == null)
                return false;

            if (RuinToken == RuinTokenType.Altar)
            {
                if (!payMana) return false;
                player.Fame += 7;
                RuinConquered = true;
                return true;
            }
            else
            {
                if (!Challenge(player, win)) return false;
                if (win) RuinConquered = true;
                return win;
            }
        }
    }
}
