using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 根據玩家挑選的戰術牌決定本輪行動順序。
    /// 編號越小的戰術牌越早行動，同時記錄其帶來的手牌上限加成。
    /// </summary>
    public sealed class RoundOrderService
    {
        private readonly LinkedList<PlayerState> _order = new();

        /// <summary>
        /// 以玩家選擇的戰術牌初始化順序。
        /// </summary>
        public void Init(IEnumerable<PlayerState> players, IReadOnlyList<TacticCard> picks)
        {
            var sorted = players.Zip(picks, (p, t) => (player: p, tactic: t))
                                .OrderBy(x => x.tactic.Number);
            _order.Clear();
            foreach (var (player, tactic) in sorted)
            {
                player.TacticHandBonus = tactic.HandBonus;
                _order.AddLast(player);
            }
        }

        /// <summary>
        /// 取得下一位行動玩家，並將其移動至隊列尾端。
        /// </summary>
        public PlayerState NextPlayer()
        {
            var p = _order.First!.Value;
            _order.RemoveFirst();
            _order.AddLast(p);
            return p;
        }
    }
}

