using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 圍繞日夜與輪次進行的流程控制核心。
    /// 僅涵蓋順序、回合刷新與輪次結束等邏輯。
    /// </summary>
    public sealed class GameEngine
    {
        public RoundClock Clock { get; }
        public GlobalResources Globals { get; }
        public SharedSkillArea SharedSkills { get; }
        public RoundEndTracker ClockTracker { get; } = new();
        public Map.MapState Map { get; }
        private readonly IList<PlayerState> _players;
        private readonly RoundOrderService _order = new();
        private readonly PlayerTurnEngine _turn;
        private readonly int _maxDays;
        private readonly IGameLogger? _logger;
        private PlayerState _currentPlayer;
        private PlayerState _immediateNextPlayer;
        private ActionContext _currentContext;
        private bool _roundStarted;
        private bool _roundPrepared;
        private bool _beforeTurnTacticResolved;
        private readonly System.Random _tacticRandom;
        private readonly System.Random _roundShuffleRandom;
        public bool HasPendingTacticSelection => _players.Any(p => p.Tactic != null && !p.Tactic.SelectionResolved);
        public bool IsRoundPrepared => _roundPrepared;
        public bool HasActiveTurn => _currentPlayer != null;
        public bool HasImmediateExtraTurn => _immediateNextPlayer != null;
        public bool IsEndingTurn { get; private set; }
        public PlayerState CurrentPlayer => _currentPlayer;
        // Empty uninitialized sites are not conquered cities: require actual ownership and shields.
        public bool SoloCitiesConquered => _players.Count == 1 && _maxDays == 3
            && Map.Places.Values.Distinct().Count(s => s.Type == MK.Logic.Core.PlaceType.City
                && s.Conquered && s.OwnerId == _players[0].Id && s.CityShields.Contains(_players[0].Id)) == 2;
        public bool NextPlayerHasNoCards => _roundStarted && !HasActiveTurn && _order.HasPlayers
            && !HasPendingTacticSelection
            && (_immediateNextPlayer ?? _order.PeekNextPlayer()).Deck.Hand.Count == 0
            && (_immediateNextPlayer ?? _order.PeekNextPlayer()).Deck.DrawPileCount == 0;
        public bool CanChooseRoundBannerReturns => _roundStarted && !_roundPrepared && !HasActiveTurn && ClockTracker.RoundEnded;
        private DeedCard[] _endDiscards=System.Array.Empty<DeedCard>();
        public bool CanSkipEmptyTurn => _roundStarted && !HasActiveTurn && _order.HasPlayers
            && ClockTracker.CountdownStarted && !ClockTracker.RoundEnded && !HasPendingTacticSelection
            && (_immediateNextPlayer??_order.PeekNextPlayer()).Deck.Hand.Count==0
            && (_immediateNextPlayer??_order.PeekNextPlayer()).Deck.DrawPileCount==0;

        /// <summary>轮末声明替代下一名玩家的整个回合，不在已经开始操作后补做。</summary>
        public bool CanAnnounceRoundEnd => _roundStarted && !HasActiveTurn
            && _order.HasPlayers && !ClockTracker.CountdownStarted && !HasPendingTacticSelection
            && (_immediateNextPlayer ?? _order.PeekNextPlayer()).Deck.DrawPileCount == 0;

        public GameEngine(
            IList<PlayerState> players,
            int maxDays,
            IGameLogger? logger = null,
            Map.MapState? map = null,
            System.Random? manaRandom = null,
            System.Random? tacticRandom = null,
            System.Random? roundShuffleRandom = null)
        {
            _players = players;
            SharedSkills = new SharedSkillArea((player, context) => !IsEndingTurn
                && ReferenceEquals(player, _currentPlayer) && ReferenceEquals(context, _currentContext));
            Clock = new RoundClock();
            Globals = new GlobalResources(players.Count, () => Clock.DayPart, manaRandom);
            Map = map ?? new Map.MapState();
            _maxDays = maxDays;
            _logger = logger;
            _turn = new PlayerTurnEngine(logger);
            _tacticRandom = tacticRandom ?? new System.Random();
            _roundShuffleRandom = roundShuffleRandom ?? new System.Random();
        }

        /// <summary>新轮先补牌、重整及处理魔宠维持；战术须在准备完毕后选择。</summary>
        public void PrepareNewRound()
        {
            if (CheckEndOfScenario())
                throw new System.InvalidOperationException("剧本时限已到，不能再准备新一轮");
            if (_roundStarted || _roundPrepared || HasActiveTurn)
                throw new System.InvalidOperationException("必须先合法结束当前轮次，不能重复补牌或重整部队");
            _logger?.Log($"PrepareRound {Clock.RoundIndex} {Clock.DayPart} Day{Clock.DayIndex}");
            Globals.Mana.ResetSource();
            ClockTracker.Reset();
            Globals.Units.StartRound(_players.Count,Map.Placed.Values.Any(t=>t.Set==MK.Logic.Core.TileSet.Core));
            if(Clock.RoundIndex>1)Globals.RefreshMonasteryOffer(Map.Places.Values.Count(s=>s.Type==MK.Logic.Core.PlaceType.Monastery&&!s.MonasteryBurned));
            // 開局抓牌：根據手牌上限抽到滿，不會在此階段洗牌
            foreach (var p in _players)
            {
                SkillActions.StartRound(p);
                BannerActions.StartRound(p);
                foreach (var unit in p.Units)
                {
                    unit.NewRound();
                    if (unit.Card.RequiresAttunement && !unit.IsDestroyed) unit.NeedsAttunementUpkeep = true;
                }
                int limit = p.HandLimit();
                p.Deck.DrawToLimit(limit);
            }
            _roundPrepared = true;
        }

        public void ResolveFamiliarUpkeep(PlayerState player, UnitState unit, MK.Logic.Core.ManaColor? crystal)
        {
            if (!_roundPrepared || _roundStarted || HasActiveTurn || !_players.Contains(player))
                throw new System.InvalidOperationException("只能在新轮准备后、选择战术前维持自有魔宠");
            FamiliarActions.ResolveUpkeep(player, unit, crystal);
        }

        /// <summary>所有魔宠已维持或移除后，提交战术并确定真实回合顺序。</summary>
        public void StartNewRound(IReadOnlyList<TacticCard> picks)
        {
            if (_roundStarted || HasActiveTurn)
                throw new System.InvalidOperationException("必须先合法结束当前轮次，不能重复补牌或重整部队");
            if (picks == null || picks.Count == 0 && _order.HasPlayers || picks.Count != 0 && (picks.Count != _players.Count
                || picks.Any(t => t == null || t.Part != Clock.DayPart || t.Number < 1 || t.Number > 6)
                || picks.Select(t => t.Number).Distinct().Count() != picks.Count))
                throw new System.InvalidOperationException("每名玩家必须选择本轮昼夜对应且互不重复的战术");
            foreach (var pick in picks) OriginalTactics.ValidateIdentity(pick);
            // Preserve the legacy no-unit convenience entry; new UI prepares before displaying tactics.
            if (!_roundPrepared) PrepareNewRound();
            if (_players.Any(p => p.Units.Any(u => u.NeedsAttunementUpkeep && !u.IsDestroyed)))
                throw new System.InvalidOperationException("选择战术前必须为每支魔宠支付库存魔晶或移除它");
            _order.Init(_players, picks);
            for (int i = 0; i < picks.Count; i++) TacticActions.Select(_players[i],picks[i]);
            _roundStarted = true;
            _roundPrepared = false;
            _logger?.Log($"StartRound {Clock.RoundIndex} {Clock.DayPart} Day{Clock.DayIndex}");
        }

        public void ResolveTacticSelection(PlayerState player, IReadOnlyList<DeedCard> cards = null, ManaDie die = null)
        {
            if (!_roundStarted || HasActiveTurn || !_players.Contains(player))
                throw new System.InvalidOperationException("只能在选定战术后、开始回合前处理取得效果");
            TacticActions.ResolveSelection(player,Globals.Mana,cards,die,_tacticRandom);
        }

        public PlayerState NextPlayerBeforeTurn()
        {
            if (!_roundStarted || HasActiveTurn || !_order.HasPlayers || ClockTracker.RoundEnded || HasPendingTacticSelection)
                throw new System.InvalidOperationException("当前没有可开始回合的玩家");
            return _immediateNextPlayer ?? _order.PeekNextPlayer();
        }

        public void UseMidnightMeditation(IReadOnlyList<DeedCard> cards)
            => TacticActions.MidnightMeditation(NextPlayerBeforeTurn(),cards,_tacticRandom);

        public void ResolveSparingPower(bool collect)
        {
            var player = NextPlayerBeforeTurn();
            if (_beforeTurnTacticResolved) throw new System.InvalidOperationException("本次回合开始前已处理积蓄力量");
            TacticActions.SparingPower(player,collect);
            _beforeTurnTacticResolved = true;
        }

        public void UseLongNight(PlayerState player)
        {
            if (!_roundStarted || ClockTracker.RoundEnded || HasPendingTacticSelection || !_players.Contains(player))
                throw new System.InvalidOperationException("只能在正在进行的轮次使用本人的漫漫长夜");
            TacticActions.LongNight(player,_tacticRandom);
        }
        private PlayerState RequireTacticTurn()
        {
            if (!HasActiveTurn || HasPendingTacticSelection) throw new System.InvalidOperationException("战术能力需要本人的实际进行中回合");
            return _currentPlayer;
        }
        public void UseRightMoment()
        {
            if (ClockTracker.CountdownStarted) throw new System.InvalidOperationException("已声明轮次结束，不能再取得额外回合");
            TacticActions.RightMoment(RequireTacticTurn(),_currentContext);
        }
        public void UseReservedTacticMana() => TacticActions.UseReservedMana(RequireTacticTurn(),Globals.Mana);
        public void UseManaSearch(IReadOnlyList<ManaDie> dice) => TacticActions.ManaSearch(RequireTacticTurn(),Globals.Mana,dice);

        /// <summary>執行一名玩家的回合。</summary>
        public void RunOneTurn()
        {
            if(CanSkipEmptyTurn){SkipEmptyTurn();return;}
            if (CanAnnounceRoundEnd && (_immediateNextPlayer ?? _order.PeekNextPlayer()).Deck.Hand.Count == 0)
            {
                AnnounceRoundEnd();
                return;
            }
            var context = new ActionContext();
            BeginNextTurn(context);
            // This convenience loop performs no player action. Spend a legal rest instead
            // of silently advancing a turn with a full hand and refreshing skills.
            if(_currentPlayer.Deck.Hand.Count>0)
            {
                var ordinary=_currentPlayer.Deck.Hand.FirstOrDefault(c=>c.Type!=MK.Logic.Core.CardType.Wound);
                var wounds=_currentPlayer.Deck.Hand.Where(c=>c.Type==MK.Logic.Core.CardType.Wound).ToArray();
                RestActions.Rest(_currentPlayer,context,ordinary,ordinary==null?wounds.Take(1):wounds);
            }
            EndCurrentTurn(context);
        }

        public void SkipEmptyTurn()
        {
            if(!CanSkipEmptyTurn)throw new System.InvalidOperationException("只有本轮已被声明结束且手牌、牌库都为空，才能跳过最后回合");
            if(_immediateNextPlayer==null)_order.NextPlayer();
            _immediateNextPlayer=null;_beforeTurnTacticResolved=false;
            ClockTracker.NotifyTurnFinished();
        }

        /// <summary>Start the real next turn, including a single earned immediate turn.</summary>
        public PlayerState BeginNextTurn(ActionContext context)
        {
            if (context == null || _currentPlayer != null)
                throw new System.InvalidOperationException("上一回合尚未结束或缺少回合状态");
            if (!_roundStarted || !_order.HasPlayers || ClockTracker.RoundEnded || HasPendingTacticSelection)
                throw new System.InvalidOperationException("必须先准备合法轮次和战术顺序，且轮次尚未结束");
            var next = _immediateNextPlayer ?? _order.PeekNextPlayer();
            ChaosSkillRules.RequireResolvedBeforeStartTurn(next);
            if (next.Tactic?.Card.Id == "night_6" && !next.Tactic.Spent && !_beforeTurnTacticResolved)
                throw new System.InvalidOperationException("开始回合前请选择积蓄力量的蓄牌或收回");
            if (next.Deck.Hand.Count == 0 && next.Deck.DrawPileCount == 0)
                throw new System.InvalidOperationException(ClockTracker.CountdownStarted?"手牌和牌库均为空，必须跳过本轮最后回合":"手牌和牌库均为空，必须以宣布轮次结束代替本回合");
            var player = _immediateNextPlayer ?? _order.NextPlayer();
            _immediateNextPlayer = null;
            _currentPlayer = player;
            _currentContext = context;
            _beforeTurnTacticResolved = false;
            context.SharedSkills = SharedSkills;
            context.OtherPlayers.Clear();
            context.OtherPlayers.AddRange(_players.Where(p => !ReferenceEquals(p, player)));
            context.DayPart = Clock.DayPart;
            context.ManaSource = Globals.Mana;
            context.MovementMap = Map;
            _logger?.Log($"TurnStart P{player.Id}");

            SharedSkills.BeginTurn(player);
            _turn.StartTurn(player, Clock.DayPart, Globals, Map);
            return player;
        }

        public void EndCurrentTurn(ActionContext context,IEnumerable<DeedCard> discard=null,SiteTurnSelection siteSelection=null)
        {
            BeginEndCurrentTurn(context,discard,siteSelection);
            CompleteEndCurrentTurn(context);
        }

        /// <summary>Validate the same formal turn-end requirements without changing state.</summary>
        public void ValidateEndCurrentTurn(ActionContext context,IEnumerable<DeedCard> discard=null,SiteTurnSelection siteSelection=null)
        {
            if (IsEndingTurn || _currentPlayer == null || !ReferenceEquals(context, _currentContext))
                throw new System.InvalidOperationException("必须结束当前玩家的同一回合状态");
            if (context.InBattle) throw new System.InvalidOperationException("必须先结束实际战斗，才能结束回合");
            // The actual end-turn lifecycle already rejects pending copied uses.
            // Preview must advertise the same permission without mutating state.
            RepeatedEffectResolution.RequireCompleted(context);
            SiteTurnRules.ValidateEndTurn(_currentPlayer, Map, siteSelection);
            var selected=discard?.ToArray()??System.Array.Empty<DeedCard>();
            if(selected.Any(c=>c==null || c.Type==MK.Logic.Core.CardType.Wound || !_currentPlayer.Deck.Hand.Any(h=>ReferenceEquals(h,c)))
                || selected.Where((c,i)=>selected.Take(i).Any(other=>ReferenceEquals(c,other))).Any())
                throw new System.InvalidOperationException("回合末只能弃掉所选实际非创伤手牌，不能重复选择同一张");
            if(_currentPlayer.Deck.CardsPlayedOrDiscardedThisTurn==0 && selected.Length==0
                && !_currentPlayer.Deck.EmptyHandExceptionAtTurnStart)
                throw new System.InvalidOperationException("本回合必须至少打出或弃掉一张牌；请在手牌中选择回合末弃牌，或进行休息");
        }

        private bool _pendingEndRewards;
        public void BeginEndCurrentTurn(ActionContext context,IEnumerable<DeedCard> discard=null,SiteTurnSelection siteSelection=null,bool deferRewards=false)
        {
            var selected=discard?.ToArray()??System.Array.Empty<DeedCard>();
            ValidateEndCurrentTurn(context,selected,siteSelection);
            _turn.EndTurn(_currentPlayer, Clock.DayPart, Globals.Mana, context, deferDraw:true, siteSelection:siteSelection,deferRewards:deferRewards);
            _endDiscards=selected;
            _pendingEndRewards=deferRewards;
            IsEndingTurn=true;
        }
        public void GrantPendingEndRewards(ActionContext context)
        {
            if(!IsEndingTurn||!_pendingEndRewards||!ReferenceEquals(context,_currentContext))
                throw new System.InvalidOperationException("No pending rewards at this native end-turn boundary.");
            _turn.GrantEndRewards(_currentPlayer,context);_pendingEndRewards=false;
        }

        public void CompleteEndCurrentTurn(ActionContext context)
        {
            if(!IsEndingTurn || _pendingEndRewards || _currentPlayer==null || !ReferenceEquals(context,_currentContext))
                throw new System.InvalidOperationException("请先清理当前回合并完成奖励选择");
            var player=_currentPlayer;
            foreach(var card in _endDiscards)player.Deck.TryDiscardExact(card);
            _endDiscards=System.Array.Empty<DeedCard>();
            _turn.DrawAfterEndTurn(player,context);
            if (context.ExtraTurn && !ClockTracker.CountdownStarted)
            {
                _immediateNextPlayer = player;
            }
            context.ExtraTurn = false;
            SharedSkills.ClearManaSelections(context);
            _currentPlayer = null;
            _currentContext = null;
            IsEndingTurn=false;
            _logger?.Log($"TurnEnd P{player.Id}");
            ClockTracker.NotifyTurnFinished();
        }

        /// <summary>有人宣告結束輪次時啟動倒數。</summary>
        public void AnnounceRoundEnd()
        {
            if (!CanAnnounceRoundEnd)
                throw new System.InvalidOperationException("只能在自己回合开始前、牌库为空且尚无人声明时结束轮次");
            var player = _immediateNextPlayer ?? _order.NextPlayer();
            _immediateNextPlayer = null;
            _logger?.Log($"AnnounceRoundEnd P{player.Id}");
            // The declarer takes no turn; each other player has exactly one final turn.
            ClockTracker.StartCountdown(_players.Count - 1);
        }

        /// <summary>The dummy is the other player and receives the final turn after a player declaration.</summary>
        public void AnnounceSoloPlayerRoundEnd()
        {
            if (_players.Count != 1 || _maxDays != 3)
                throw new System.InvalidOperationException("Requires the solo six-round scenario.");
            AnnounceRoundEnd();
            ClockTracker.StartCountdown(1);
        }

        /// <summary>
        /// Solo scenario bridge: call at the dummy's scheduled turn only after its
        /// clock reports an empty deck at turn START. Flipping the last card alone
        /// does not call this method. Dummy scheduling/deck ownership stay in the
        /// scenario controller; it is not a policy action.
        /// </summary>
        public void AnnounceSoloDummyRoundEnd()
        {
            if (_players.Count != 1 || !_roundStarted || HasActiveTurn || IsEndingTurn
                || HasPendingTacticSelection || ClockTracker.CountdownStarted
                || _immediateNextPlayer != null)
                throw new System.InvalidOperationException("虚拟玩家只能在实际排定的回合边界宣布轮次结束");
            ClockTracker.StartCountdown(1);
            _logger?.Log("SoloDummyAnnounceRoundEnd");
        }

        /// <summary>結束當前輪次並推進到下一輪。</summary>
        public void EndRound() => EndRoundCore(false);

        private void ValidateRoundEnd()
        {
            if (_roundPrepared || HasActiveTurn || _order.HasPlayers && (!_roundStarted || !ClockTracker.RoundEnded))
                throw new System.InvalidOperationException("必须结束当前回合并完成其余玩家的最后回合，才能结束本轮");
        }

        /// <summary>Transfer exactly the old bottom advanced card, then perform the normal spell rotation once.</summary>
        public void EndSoloRound(SoloDummyDeck dummy, System.Func<string, MK.Logic.Core.ManaColor> printedColor)
            => EndSoloRound(dummy,printedColor==null?null:new System.Func<string,MK.Logic.Core.ManaColor[]>(id=>new[]{printedColor(id)}));

        public void EndSoloRound(SoloDummyDeck dummy, System.Func<string, MK.Logic.Core.ManaColor[]> printedColors)
        {
            ValidateRoundEnd();
            if (_players.Count != 1 || _maxDays != 3 || dummy == null || dummy.Closed || dummy.Round != Clock.RoundIndex)
                throw new System.InvalidOperationException("Solo dummy and native six-round clock must agree.");
            var next = SoloDummyDeck.Restore(dummy.Capture());
            next.CloseRound();
            bool transfer = Clock.RoundIndex < 6;
            if (transfer)
            {
                if (printedColors == null || Globals.AdvActions.Offer.Count == 0 || Globals.Spells.Offer.Count == 0)
                    throw new System.InvalidOperationException("Next-round dummy growth requires actual bottom offers and printed colors.");
                var advanced = Globals.AdvActions.Offer[0];
                var spell = Globals.Spells.Offer[0];
                // Validate all card identities, colors and next RNG state before moving any supply card.
                var colors=printedColors(advanced.Id);
                var spellColors=printedColors(spell.Id);
                if(colors==null || colors.Length<1 || colors.Length>2 || spellColors==null || spellColors.Length!=1)
                    throw new System.InvalidOperationException("Need one/two original action colors and one spell color.");
                next.BeginNextRound(new SoloDummyCard("dummy/round/" + (Clock.RoundIndex + 1), advanced.Id,
                    colors[0],colors.Length==2?colors[1]:null), spellColors[0]);
                Globals.AdvActions.Offer.RemoveAt(0);
            }
            EndRoundCore(transfer);
            dummy.ApplyValidatedSnapshot(next.Capture());
        }

        private void EndRoundCore(bool advancedBottomTransferredToDummy)
        {
            ValidateRoundEnd();
            _logger?.Log("EndRound");
            Globals.EndRound(coreRevealed: false, advancedBottomTransferredToDummy: advancedBottomTransferredToDummy);
            SharedSkills.EndRound();
            foreach (var p in _players)
            {
                p.TacticHandBonus = 0;
                p.Tactic = null;
                p.Deck.ReturnTacticAside();
                BannerActions.EndRound(p);
                p.Deck.ReturnRoundAside();
                p.Deck.ShuffleForNewRound(_roundShuffleRandom);
                p.TokensPerTurn.Clear();
                p.ManaCurseColor = null;
                p.ManaCurseTriggered = false;
            }
            Clock.NextRound();
            _immediateNextPlayer = null;
            _roundStarted = false;
            _beforeTurnTacticResolved = false;
            _logger?.Log($"NextRound {Clock.RoundIndex} {Clock.DayPart} Day{Clock.DayIndex}");
        }

        /// <summary>檢查整個劇本是否應該結束。</summary>
        public bool CheckEndOfScenario() => Clock.ScenarioEnded(_maxDays);
    }
}
