using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 公共魔力池系统，包含骰子数量管理、合法颜色重掷、取用与归还重掷机制。
    /// </summary>
    public sealed class ManaSource
    {
        private readonly List<ManaDie> _dice;
        private readonly Func<DayPart> _timeProvider;
        private readonly Dictionary<ManaColor, int> _rerollLimits;

        public ManaSource(int playerCount, Func<DayPart> timeProvider, Random? rnd = null)
        {
            _timeProvider = timeProvider;
            _dice = Enumerable.Range(0, playerCount + 2)
                .Select(_ => new ManaDie(rnd))
                .ToList();
            
            // 初始化重掷限制
            _rerollLimits = new Dictionary<ManaColor, int>
            {
                { ManaColor.Red, 1 },
                { ManaColor.Blue, 1 },
                { ManaColor.Green, 1 },
                { ManaColor.White, 1 },
                { ManaColor.Gold, 0 },  // 金和黑不限制重掷
                { ManaColor.Black, 0 }
            };
            
            RollAll();
        }

        /// <summary>当前可用的所有骰子。</summary>
        public IReadOnlyList<ManaDie> Dice => _dice;

        /// <summary>
        /// 获取指定颜色的可用骰子数量
        /// </summary>
        public int GetAvailableCount(ManaColor color)
        {
            return _dice.Count(d => d.Face == color);
        }

        /// <summary>
        /// 获取当前所有骰子的状态统计
        /// </summary>
        public Dictionary<ManaColor, int> GetDiceDistribution()
        {
            return _dice.GroupBy(d => d.Face)
                       .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// 取走指定颜色的骰子并标记在玩家状态上，若不存在则返回null。
        /// 允许重掷的在取用时进行重掷，不允许的保持原值。
        /// </summary>
        public ManaDie? Take(PlayerState owner, ManaColor desired)
        {
            if (owner.HeldManaDie != null)
                throw new InvalidOperationException("玩家已持有骰子");
                
            var availableDice = _dice.Where(d => d.Face == desired).ToList();
            if (!availableDice.Any())
                return null;

            var die = availableDice.First();
            _dice.Remove(die);
            
            // 检查是否需要重掷
            if (CanReroll(desired))
            {
                die.Roll(_timeProvider());
            }
            
            owner.HeldManaDie = die;
            return die;
        }

        /// <summary>
        /// 取走任意一个骰子（不传颜色参数时）
        /// </summary>
        public ManaDie? TakeAny(PlayerState owner)
        {
            if (owner.HeldManaDie != null) 
                throw new InvalidOperationException("玩家已持有骰子");

            if (_dice.Count == 0)
                return null;

            var die = _dice.First();
            _dice.Remove(die);
            owner.HeldManaDie = die;
            return die;
        }

        /// <summary>
        /// 返还所有玩家的骰子并重掷它们
        /// </summary>
        public void ReturnAllPlayersDice(IEnumerable<PlayerState> players)
        {
            foreach (var player in players)
            {
                if (player.HeldManaDie != null)
                {
                    Return(player.HeldManaDie);
                    player.HeldManaDie = null;
                }
            }
        }

        /// <summary>
        /// 返还单个骰子并根据当前日夜重掷
        /// </summary>
        public void Return(ManaDie die)
        {
            die.Roll(_timeProvider());
            _dice.Add(die);
        }

        /// <summary>
        /// 重置所有骰子，检查黑金色是否过多
        /// </summary>
        public void ResetSource()
        {
            RollAll();
            while (_dice.Count(d => d.Face is ManaColor.Black or ManaColor.Gold) * 2 > _dice.Count)
            {
                foreach (var d in _dice.Where(d => d.Face is ManaColor.Black or ManaColor.Gold))
                    d.Roll(_timeProvider());
            }
            
            // 确保非金黑骰子的总数量占多数
            var validColors = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White };
            while (_dice.Count(d => validColors.Contains(d.Face)) < (_dice.Count + 1) / 2)
            {
                RollAll();
            }
        }

        /// <summary>
        /// 重掷所有骰子（用于回合结束或手动重置）
        /// </summary>
        public void RollAll()
        {
            foreach (var d in _dice)
                d.Roll(_timeProvider());
        }

        /// <summary>
        /// 检查指定颜色是否可以重掷
        /// </summary>
        private bool CanReroll(ManaColor color)
        {
            return _rerollLimits.TryGetValue(color, out int limit) && limit > 0;
        }

        /// <summary>
        /// 获取允许的骰子颜色列表
        /// </summary>
        public IReadOnlyList<ManaColor> GetAllowedColors()
        {
            return _rerollLimits.Keys.Where(k => _rerollLimits[k] >= 0).ToList();
        }

        /// <summary>
        /// 打印当前魔力池状态
        /// </summary>
        public string PrintPoolState()
        {
            var dist = GetDiceDistribution();
            var colors = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White, ManaColor.Gold, ManaColor.Black };
            var state = string.Join(", ", colors.Select(c => $"{c}: {dist.GetValueOrDefault(c, 0)}"));
            return $"魔力池: [{state}] 总计: {_dice.Count}颗\n";
        }
    }
}
