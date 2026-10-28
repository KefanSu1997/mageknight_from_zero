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
        public CityColor CityColor { get; set; }
        public int CityLevel { get; set; }

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
        /// <summary>多守军地点的其余实体标记；Enemy保留首名守军以兼容单守军地点。</summary>
        public System.Collections.Generic.List<Monster> AdditionalGuards { get; } = new();
        public System.Collections.Generic.IReadOnlyList<Monster> Defenders =>
            (Enemy == null ? Enumerable.Empty<Monster>() : new[] { Enemy }).Concat(AdditionalGuards).ToArray();
        /// <summary>已征服要塞的所有者；null表示没有玩家所有权。</summary>
        public int? OwnerId { get; set; }
        /// <summary>要塞/法师塔败退后旧守军弃掉；重新抽取实际守军前仍是敌对地点。</summary>
        public bool GuardNeedsReplacement { get; set; }
        /// <summary>按获得顺序保存城市盾徽，平票时先放置者成为领袖。</summary>
        public System.Collections.Generic.List<int> CityShields { get; } = new();
        /// <summary>礦山的顏色，深層礦山可能包含兩種。</summary>
        public ManaColor[]? MineColors { get; init; }

        /// <summary>遠古遺跡翻開後的標記。</summary>
        public RuinTokenType? RuinToken { get; init; }
        public RuinToken OriginalRuinToken { get; init; }
        public bool RuinConquered { get; set; }
        /// <summary>地下城是否已被征服。</summary>
        public bool DungeonConquered { get; set; }
        public bool LairConquered { get; set; }
        public bool MonasteryBurned { get; set; }
        /// <summary>孵化领地补入的背面标记；公共观察不得读取其具体身份。</summary>
        public System.Collections.Generic.HashSet<Monster> HiddenGuards { get; } = new();

        /// <summary>是否已征服此地點。</summary>
        public bool Conquered => Type switch
        {
            PlaceType.Monastery => MonasteryBurned,
            PlaceType.AncientRuin => RuinConquered,
            PlaceType.Dungeon or PlaceType.Tomb => DungeonConquered,
            PlaceType.MonsterDen or PlaceType.SpawningGround => LairConquered,
            _ => Enemy == null && AdditionalGuards.Count == 0 && !GuardNeedsReplacement
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
            if (OriginalRuinToken != null)
                throw new System.InvalidOperationException("Original ruins require actual payment/combat resolution, not boolean claims");
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
