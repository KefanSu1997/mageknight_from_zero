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
        private readonly Dictionary<ManaDie, PlayerState> _holders = new();
        private readonly HashSet<ManaDie> _returnWithoutReroll = new();
        private readonly Dictionary<ManaDie, bool?> _sourceOpeningReturns = new();
        public IReadOnlyList<ManaDie> PendingSourceOpeningReturns(PlayerState player)
            => _sourceOpeningReturns.Where(p=>!p.Value.HasValue && _holders.TryGetValue(p.Key,out var owner)
                && ReferenceEquals(owner,player)).Select(p=>p.Key).ToArray();
        public void RequireSourceOpeningReturnChoices(PlayerState player)
        {
            if(PendingSourceOpeningReturns(player).Count>0)
                throw new InvalidOperationException("请先选择魔力开源原骰回合末是否重掷");
        }
        internal void TakeSourceOpening(PlayerState player,ManaDie die)
        {
            if(die==null || !FamiliarActions.IsBasicColor(die.Face))
                throw new InvalidOperationException("魔力开源只能使用实际基本色源骰");
            TakeExtra(player,die);_sourceOpeningReturns.Add(die,null);
        }
        internal void ChooseSourceOpeningReturn(PlayerState player,ManaDie die,bool reroll)
        {
            if(die==null || !_sourceOpeningReturns.ContainsKey(die)
                || !_holders.TryGetValue(die,out var owner) || !ReferenceEquals(owner,player))
                throw new InvalidOperationException("只能决定本人魔力开源所持原骰的返还方式");
            _sourceOpeningReturns[die]=reroll;
        }
        private readonly Dictionary<ManaDie, PlayerState> _roundClaims = new();
        private readonly Dictionary<ManaDie, PlayerState> _tacticClaims = new();
        private readonly HashSet<PlayerState> _usedThisTurn = new();
        public IReadOnlyDictionary<ManaDie, PlayerState> TacticClaims => _tacticClaims;
        public bool WasUsedThisTurn(PlayerState player) => _usedThisTurn.Contains(player);
        public static void ValidateUse(PlayerState player)
        {
            if (player == null) throw new InvalidOperationException("源骰使用需要实际玩家");
            if (player.ForgoSourceDiceThisTurn) throw new InvalidOperationException("魔法抗拒已选择3，本回合不能再使用魔力源骰子");
        }
        internal void BeginPlayerTurn(PlayerState player) => _usedThisTurn.Remove(player);

        internal void ReserveForTactic(PlayerState owner, ManaDie die)
        {
            if (die == null || !_dice.Contains(die) || !FamiliarActions.IsBasicColor(die.Face))
                throw new InvalidOperationException("魔力窃取必须选择源中实际可用的基本色骰");
            _dice.Remove(die); _tacticClaims.Add(die, owner);
        }
        internal void UseTacticReservation(PlayerState owner, ManaDie die)
        {
            if (die == null || !_tacticClaims.TryGetValue(die, out var actual) || !ReferenceEquals(owner, actual))
                throw new InvalidOperationException("本战术没有保留这颗原骰");
            _tacticClaims.Remove(die); _holders.Add(die, owner);
        }

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

        /// <summary>魔力宣夺保留到轮末的原骰；不占普通每回合取骰位置。</summary>
        public IReadOnlyDictionary<ManaDie, PlayerState> RoundClaims => _roundClaims;

        /// <summary>本回合取用的普通、额外及卡牌转换骰，均保留原实例。</summary>
        public IReadOnlyDictionary<ManaDie, PlayerState> TurnClaims => _holders;

        public void TakeOrdinary(PlayerState owner, ManaDie die)
        {
            ValidateUse(owner);
            if (owner == null || owner.HeldManaDie != null || die == null || !_dice.Contains(die))
                throw new InvalidOperationException("普通取骰需要空闲额度及真实可用骰");
            _dice.Remove(die);
            owner.HeldManaDie = die;
            _holders.Add(die, owner);
            _usedThisTurn.Add(owner);
        }

        public void TakeExtra(PlayerState owner, ManaDie die)
        {
            ValidateUse(owner);
            if (owner == null || die == null || !_dice.Contains(die))
                throw new InvalidOperationException("额外取骰必须选择真实可用骰");
            _dice.Remove(die);
            _holders.Add(die, owner);
            _usedThisTurn.Add(owner);
        }

        public void TakeTransformed(PlayerState owner, IReadOnlyList<ManaDie> dice,
            IReadOnlyList<ManaColor> colors)
        {
            ValidateUse(owner);
            if (owner == null || dice == null || colors == null || dice.Count != colors.Count
                || dice.Distinct().Count() != dice.Count || dice.Any(d => d == null || !_dice.Contains(d))
                || colors.Any(c => !Enum.IsDefined(typeof(ManaColor), c) || c == ManaColor.Gold))
                throw new InvalidOperationException("转换必须选择不同的可用原骰及非金色骰面");
            for (int i = 0; i < dice.Count; i++)
            {
                TakeExtra(owner, dice[i]);
                dice[i].TurnTo(colors[i]);
                _returnWithoutReroll.Add(dice[i]);
            }
        }

        public void ReturnPlayerDice(PlayerState owner)
        {
            RequireSourceOpeningReturnChoices(owner);
            foreach (var die in _holders.Where(p => ReferenceEquals(p.Value, owner)).Select(p => p.Key).ToArray())
                Return(die);
        }

        public ManaDie ClaimForRound(PlayerState owner, ManaColor color)
        {
            ValidateUse(owner);
            if (owner == null || color < ManaColor.Red || color > ManaColor.White)
                throw new InvalidOperationException("魔力宣夺必须选择基本色骰和实际玩家");
            var die = _dice.FirstOrDefault(d => d.Face == color)
                ?? throw new InvalidOperationException("魔力源中没有所选基本色骰");
            _dice.Remove(die);
            _roundClaims.Add(die, owner);
            _usedThisTurn.Add(owner);
            return die;
        }

        /// <summary>轮末把同一批原骰放回，随后的 ResetSource 统一重掷。</summary>
        public void ReturnRoundClaims()
        {
            foreach (var die in _roundClaims.Keys)
                if (!_dice.Contains(die)) _dice.Add(die);
            _roundClaims.Clear();
            foreach (var die in _tacticClaims.Keys)
                if (!_dice.Contains(die)) _dice.Add(die);
            _tacticClaims.Clear();
        }

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
            ValidateUse(owner);
            if (owner.HeldManaDie != null)
                throw new InvalidOperationException("玩家已持有骰子");
                
            var availableDice = _dice.Where(d => d.Face == desired).ToList();
            if (!availableDice.Any())
                return null;

            var die = availableDice.First();
            _dice.Remove(die);
            
            // 取用时保持选择的颜色，仅在回合结束归还时重掷。
            owner.HeldManaDie = die;
            _holders[die] = owner;
            _usedThisTurn.Add(owner);
            return die;
        }

        /// <summary>
        /// 取走任意一个骰子（不传颜色参数时）
        /// </summary>
        public ManaDie? TakeAny(PlayerState owner)
        {
            ValidateUse(owner);
            if (owner.HeldManaDie != null) 
                throw new InvalidOperationException("玩家已持有骰子");

            if (_dice.Count == 0)
                return null;

            var die = _dice.First();
            _dice.Remove(die);
            owner.HeldManaDie = die;
            _holders[die] = owner;
            _usedThisTurn.Add(owner);
            return die;
        }

        /// <summary>
        /// 返还所有玩家的骰子并重掷它们
        /// </summary>
        public void ReturnAllPlayersDice(IEnumerable<PlayerState> players)
        {
            var actualPlayers=players.ToArray();
            foreach (var player in actualPlayers)RequireSourceOpeningReturnChoices(player);
            foreach (var player in actualPlayers)
                ReturnPlayerDice(player);
        }

        /// <summary>
        /// 返还单个骰子并根据当前日夜重掷
        /// </summary>
        public void Return(ManaDie die)
        {
            if(_sourceOpeningReturns.TryGetValue(die,out var reroll) && !reroll.HasValue)
                throw new InvalidOperationException("魔力开源原骰尚未选择回合末重掷方式");
            if (_roundClaims.ContainsKey(die) || _tacticClaims.ContainsKey(die))
                throw new InvalidOperationException("魔力宣夺保留骰只能在轮末归还");
            if (_dice.Contains(die)) return;
            if (_holders.TryGetValue(die, out var owner))
            {
                if (owner.HeldManaDie == die) owner.HeldManaDie = null;
                _holders.Remove(die);
            }
            bool preserveOpening=_sourceOpeningReturns.TryGetValue(die,out var chosen)&&chosen==false;
            _sourceOpeningReturns.Remove(die);
            bool preserveTransformed=_returnWithoutReroll.Remove(die);
            if (!preserveOpening && !preserveTransformed)
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
