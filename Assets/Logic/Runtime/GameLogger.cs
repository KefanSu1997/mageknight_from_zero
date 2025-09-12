using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json; // 使用 Newtonsoft 以便在 Unity 中序列化
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 提供遊戲流程記錄功能的介面。
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>寫入一條日誌。</summary>
        void Log(string message);
    }

    /// <summary>
    /// 簡易的列表式日誌實作，將訊息存入字串列表。
    /// </summary>
    public sealed class ListGameLogger : IGameLogger
    {
        private readonly List<string> _entries = new();

        /// <summary>取得目前所有日誌內容。</summary>
        public List<string> Entries => _entries;

        /// <inheritdoc />
        public void Log(string message) => _entries.Add(message);
    }

    /// <summary>
    /// 擴充記錄器的輔助方法，提供複雜物件的序列化輸出。
    /// </summary>
    public static class GameLoggerExtensions
    {
        /// <summary>
        /// 以 JSON 形式記錄任意物件。
        /// </summary>
        /// <param name="logger">目標記錄器</param>
        /// <param name="tag">前置標籤</param>
        /// <param name="obj">要序列化的物件</param>
        public static void LogJson(this IGameLogger logger, string tag, object obj)
        {
            // 使用 Newtonsoft.Json 將物件轉成字串，方便在日誌中檢查
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
            logger.Log($"{tag} {json}");
        }

        /// <summary>
        /// 記錄當前玩家狀態與全局資源概況。
        /// </summary>
        /// <param name="logger">目標記錄器</param>
        /// <param name="player">玩家狀態</param>
        /// <param name="globals">全局資源</param>
        /// <param name="map">地圖資訊</param>
        public static void LogFullState(
            this IGameLogger logger,
            PlayerState player,
            GlobalResources globals,
            Map.MapState map)
        {
            var info = new
            {
                Map = map.Placed.ToDictionary(
                    p => p.Key,
                    p => new { p.Value.Set, p.Value.Id }),
                PlayerPos = player.Position,
                Globals = new
                {
                    Mana = globals.Mana.Dice.Select(d => d.Face).ToList(),
                    SpellDeck = globals.Spells.Offer.Select(c => c.Id).ToList(),
                    ArtifactDeck = globals.Artifacts.Count,
                    AdvActionDeck = globals.AdvActions.Offer.Select(c => c.Id).ToList()
                },
                Player = new
                {
                    Deck = player.Deck.DrawPile.Select(c => c.Id).ToList(),
                    Hand = player.Deck.Hand.Select(c => c.Id).ToList(),
                    Discard = player.Deck.DiscardPile.Select(c => c.Id).ToList(),
                    Units = player.Units.Select(u => u.Card.Id).ToList(),
                    Crystals = player.Mana.Crystals,
                    Tokens = player.Mana.Tokens,
                    Skills = player.Skills.Select(s => s.Id).ToList()
                }
            };
            logger.LogJson("State", info);
        }
    }
}
