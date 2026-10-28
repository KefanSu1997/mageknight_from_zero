using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Persistent players sharing the formal round engine. The world supplies actual enemy
    /// tokens; beginning another battle never rebuilds a loadout or restores spent resources.</summary>
    public sealed partial class PveJourneySession
    {
        private readonly ActionCardData[] _cards;
        private readonly ItemCardData[] _artifacts;
        private readonly SpellCardData[] _spells;
        private readonly ActionSystem _actions;
        private readonly List<Monster> _defeated = new();
        private readonly PlayerState[] _players;
        private readonly Dictionary<PlayerState, HeroProgression> _progressions = new();
        public SoloTacticRound SoloClock { get; private set; }
        public bool IsSoloConquest { get; }
        private SoloDummyDeck _soloDummy;
        private Func<string, ManaColor[]> _soloPrintedColor;
        private Queue<SkillCard> _soloDummySkills;

        public SoloConquestScore CaptureSoloScore(ISet<string> advancedActionIds)
        {
            if(SoloClock==null || _soloDummy==null)
                throw new InvalidOperationException("Official solo score requires the actual solo scheduler and dummy.");
            return SoloConquestScore.Capture(Player,Game.Map,advancedActionIds,
                SoloClock.ScenarioEnded,Math.Min(Game.Clock.RoundIndex,6),_soloDummy.Remaining,
                SoloClock.PlayerAnnouncedThisRound || _soloDummy.Announced);
        }

        /// <summary>Rulebook p15: remove interactive skills before shuffling; reveal only after acquisition.</summary>
        public void EnableSoloDummySkills(string dummyHeroId, IEnumerable<SkillCard> originals, Random random)
        {
            if (SoloClock == null || _soloDummySkills != null || Player.Tactic != null
                || Game.Clock.RoundIndex != 1 || Progression == null || CompletedTurns != 0)
                throw new InvalidOperationException("Attach dummy skills once before solo play with actual hero progression.");
            var hero = OriginalHeroes.Get(dummyHeroId);
            var skills = originals?.ToArray() ?? throw new ArgumentNullException(nameof(originals));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (dummyHeroId == Player.HeroId || skills.Length != 10
                || skills.Any(s=>s==null || !s.SourceId.StartsWith(hero.SkillPoolId+":") || s.IsInteractive==null)
                || skills.Select(s=>s.SourceId).Distinct().Count()!=10)
                throw new InvalidOperationException("Need all ten verified skills of a different unused hero.");
            var eligible=skills.Where(s=>s.IsInteractive==false).ToArray();
            if(eligible.Length<5)throw new InvalidOperationException("Insufficient dummy skills for five native skill levels.");
            for(int i=eligible.Length-1;i>0;i--){int j=random.Next(i+1);(eligible[i],eligible[j])=(eligible[j],eligible[i]);}
            _soloDummySkills=new Queue<SkillCard>(eligible);
            Progression.AfterSkillAcquired=RevealSoloDummySkill;
        }

        private void RevealSoloDummySkill()
        {
            if(_soloDummySkills.Count==0)throw new InvalidOperationException("Dummy skill supply exhausted.");
            Game.Globals.Skills.Offer.Add(_soloDummySkills.Dequeue());
        }

        public void EnableSoloClock(SoloDummyDeck dummy, Func<string, ManaColor> printedColor, Random random)
            => EnableSoloClock(dummy,printedColor==null?null:new Func<string,ManaColor[]>(id=>new[]{printedColor(id)}),random);

        public void EnableSoloClock(SoloDummyDeck dummy, Func<string, ManaColor[]> printedColor, Random random)
        {
            if (_players.Length != 1 || SoloClock != null || Game.HasActiveTurn
                || Game.Clock.RoundIndex != 1 || Player.Tactic != null || CompletedTurns != 0)
                throw new InvalidOperationException("Solo scheduling must be attached once before the first tactic.");
            if (dummy == null || printedColor == null || random == null)
                throw new ArgumentNullException("Solo clock dependencies are required.");
            if (dummy.Round != 1 || dummy.Closed || dummy.Announced || dummy.Remaining != 16)
                throw new InvalidOperationException("A fresh solo journey needs an untouched dummy deck.");
            _soloDummy = dummy;
            _soloPrintedColor = printedColor;
            SoloClock = new SoloTacticRound(Game, dummy.TakeTurn, random);
        }
        public IReadOnlyList<PlayerState> Players => Array.AsReadOnly(_players);
        public PlayerState Player { get; private set; }
        public GameEngine Game { get; }
        public ActionContext Context { get; private set; }
        public PveBattleSession Battle { get; private set; }
        public HeroProgression Progression => _progressions.TryGetValue(Player, out var progression) ? progression : null;
        public EnemyRewardMode EnemyRewardMode { get; }
        public int CompletedBattles { get; private set; }
        public int CompletedTurns { get; private set; }
        public bool IsDefeated(Monster enemy) => _defeated.Any(killed => ReferenceEquals(killed,enemy));

        public PveJourneySession(PlayerState player, IEnumerable<ActionCardData> cards,
            IEnumerable<ItemCardData> artifacts = null, Map.MapState map = null,
            int maxDays = 3, Random manaRandom = null, Random tacticRandom = null,
            IEnumerable<SpellCardData> spells = null,IEnumerable<SkillCard> originalSkills=null,Random progressionRandom=null,
            IEnumerable<UnitCard> unitDeck=null,Random supplyRandom=null,EnemyTokenSupply enemySupply=null,
            IEnumerable<PlayerState> companions=null,Func<PlayerState,IEnumerable<SkillCard>> companionSkills=null,
            EnemyRewardMode enemyRewardMode=EnemyRewardMode.PrintedFameOnly,Random roundShuffleRandom=null,
            bool soloConquest=false)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (!Enum.IsDefined(typeof(EnemyRewardMode), enemyRewardMode)) throw new ArgumentOutOfRangeException(nameof(enemyRewardMode));
            EnemyRewardMode = enemyRewardMode;
            _players = new[] { player }.Concat(companions ?? Array.Empty<PlayerState>()).ToArray();
            if (_players.Any(p => p == null) || _players.Select(p => p.Id).Distinct().Count() != _players.Length
                || _players.Length > 4)
                throw new ArgumentException("合作旅程需要至多四位具有不同玩家身份的实际玩家");
            if(soloConquest && (_players.Length!=1 || maxDays!=3 || originalSkills==null))
                throw new ArgumentException("Solo conquest requires one actual hero, original skills and six rounds.");
            IsSoloConquest=soloConquest;
            _cards = cards?.ToArray() ?? throw new ArgumentNullException(nameof(cards));
            _artifacts = artifacts?.ToArray() ?? Array.Empty<ItemCardData>();
            // Printed p19 excludes cards109--112 (verified on original card scans).
            _spells = spells?.Where(s=>!soloConquest || s.Id is not ("magic_006" or "magic_012" or "magic_018" or "magic_000")).ToArray();
            _actions = new ActionSystem(false, _spells);
            Game = new GameEngine(_players, maxDays, map: map,
                manaRandom: manaRandom, tacticRandom: tacticRandom,roundShuffleRandom:roundShuffleRandom);
            EnemySupply = enemySupply ?? Game.Globals.Enemies;
            Game.Globals.Enemies = EnemySupply;
            Game.Globals.Units.SetDeck(unitDeck);
            InitializeDeedSupplies(supplyRandom??new Random());
            var rng=progressionRandom??new Random();
            foreach (var participant in _players)
            {
                var skills = ReferenceEquals(participant, player) ? originalSkills : companionSkills?.Invoke(participant);
                if (skills != null) _progressions.Add(participant, new HeroProgression(participant,skills,Game.Globals.AdvActions,Game.Globals.Skills,()=>Game.IsEndingTurn && ReferenceEquals(Game.CurrentPlayer,participant),rng,Game.Globals.Units,soloConquest));
            }
        }

        // Selecting a view never grants a turn: commands still require the formal engine/context.
        public void PreparePlayerView()
        {
            if (Game.HasActiveTurn) return;
            if (Game.IsRoundPrepared)
                Player=_players.FirstOrDefault(p=>p.Units.Any(u=>u.NeedsAttunementUpkeep && !u.IsDestroyed)) ?? _players[0];
            else if (Game.HasPendingTacticSelection)
                Player=_players.First(p=>p.Tactic!=null && !p.Tactic.SelectionResolved);
            else if (!Game.ClockTracker.RoundEnded && _players.All(p=>p.Tactic!=null))
                Player=Game.NextPlayerBeforeTurn();
            else Player=_players[0];
        }
        public void PrepareRound() => Game.PrepareNewRound();
        public void SelectTactic(string id) => SelectTactics(id);
        public void SelectTactics(params string[] ids)
        {
            if (SoloClock == null) Game.StartNewRound(ids.Select(id => OriginalTactics.Get(id).Card).ToArray());
            else
            {
                if (ids == null || ids.Length != 1) throw new InvalidOperationException("Solo needs exactly one player tactic.");
                SoloClock.Start(OriginalTactics.Get(ids[0]).Card);
            }
        }

        public void BeginTurn()
        {
            var context = new ActionContext { EnemyRewardMode = EnemyRewardMode };
            Player = SoloClock == null ? Game.BeginNextTurn(context) : SoloClock.BeginPlayerTurn(context);
            Context = context;
            Context.AdvActionSupply=Game.Globals.AdvActions;Context.SpellSupply=Game.Globals.Spells;
            Context.ArtifactSupply=Game.Globals.Artifacts;Context.SkillSupply=Game.Globals.Skills;
            Context.UnitOffer=Game.Globals.Units.Offer;
            Context.UnitSupply=Game.Globals.Units;
            Context.EnemySupply=EnemySupply;
            if (Game.Map.Placed.TryGetValue(Player.Position,out var tile) && tile.Edges.Length>0)
                Context.CurrentTerrain=tile.Edges[0];
            Map.SiteVisibility.RevealVisible(Player,Game.Map,Context.DayPart,Context);
            Battle = null;
        }

        public PveBattleSession BeginBattle(IEnumerable<Monster> enemies)
        {
            RepeatedEffectResolution.RequireCompleted(Context);
            if (!Game.HasActiveTurn || Game.IsEndingTurn || Context == null || Context.InBattle || Context.TurnActionCommitted)
                throw new InvalidOperationException("需要本人的进行中回合，且尚未使用本回合行动");
            RequireSoloCombatCardObligation();
            var actual = enemies?.ToArray();
            if (actual == null || actual.Length == 0 || actual.Any(e => e == null || IsDefeated(e))
                || actual.Select((e,i) => actual.Take(i).Any(other => ReferenceEquals(other,e))).Any(x => x))
                throw new InvalidOperationException("请选择互不重复、尚未击败的实际敌人标记");
            var context = Context;
            var battle = new PveBattleSession(Player, actual, _cards, context, _artifacts,
                canAct: () => Game.HasActiveTurn && !Game.IsEndingTurn && ReferenceEquals(Context,context), spells: _spells);
            Context.MovementPool = Context.InfluencePool = 0;
            Context.TurnActionCommitted = true;
            Battle = battle;
            return battle;
        }

        private void RequireSoloCombatCardObligation()
        {
            // Solo convention v2: discharge the mandatory card obligation before entering
            // combat with only wounds. Outside-combat draw/heal/wound-use actions remain
            // available. This does not grant a free end turn after an unpayable combat.
            if (SoloClock != null && Player.Deck.CardsPlayedOrDiscardedThisTurn==0
                && !Player.Deck.EmptyHandExceptionAtTurnStart
                && !Player.Deck.Hand.Any(c=>c.Type!=CardType.Wound))
                throw new InvalidOperationException("全伤牌开战前须先通过合法效果满足本回合出牌/弃牌义务，或先取得非伤牌；也可进行缓慢恢复");
        }

        public void EndTurn(IEnumerable<DeedCard> discard = null, SiteTurnSelection siteSelection = null)
        {
            RequireOutsideBattle();
            Context.Dungeon?.ValidateReward();
            Context.Lair?.ValidateReward();
            Context.Ruin?.ValidateReward();
            var selected=discard?.ToArray()??Array.Empty<DeedCard>();
            if(selected.Any(c=>c==null || c.Type==CardType.Wound || !Player.Deck.Hand.Any(h=>ReferenceEquals(h,c)))
                || selected.Where((c,i)=>selected.Take(i).Any(other=>ReferenceEquals(c,other))).Any())
                throw new InvalidOperationException("回合末只能弃掉所选实际非创伤手牌，不能重复选择同一张");
            Game.BeginEndCurrentTurn(Context,selected,siteSelection);
            RevealPurchasedCityArtifacts();
            if(_purchasedCityArtifacts>0)return;
            Progression?.PrepareNext();
            if(Progression?.Pending!=true)CompleteTurn();
        }

        public void ChooseLevelReward(SkillCard skill,AdvActionCard action)
        {
            if(!Game.IsEndingTurn || Progression==null)throw new InvalidOperationException("当前没有升级奖励");
            Progression.Choose(skill,action);
            if(!Progression.Pending)CompleteTurn();
        }

        private void CompleteTurn()
        {
            if(_purchasedCityArtifacts>0)throw new InvalidOperationException("Resolve purchased city artifacts before completing the turn.");
            Game.CompleteEndCurrentTurn(Context);
            if (Battle != null)
            {
                if (Battle.Context.Dungeon == null && Battle.Context.Lair == null && Battle.Context.Ruin == null)
                    foreach (int index in Battle.Context.DefeatedEnemyIndices) _defeated.Add(Battle.Enemies[index]);
                CompletedBattles++;
            }
            CompletedTurns++;
            SoloClock?.ObserveCompletedPlayerTurn();
            // Keep the completed battle available for a read-only result view. Its commands now reject.
        }

        private void RequireOutsideBattle()
        {
            if(CommittedRewardEnd!=null)throw new InvalidOperationException("The turn is committed; resolve its revealed rewards.");
            if (!Game.HasActiveTurn || Game.IsEndingTurn || Context == null || Context.InBattle)
                throw new InvalidOperationException("请在本人的进行中回合、战斗外操作手牌或治疗");
        }

        public BattleCardChoice[] OutsideChoices(DeedCard card) => BattleCardChoice.For(card.Id)
            .Where(c => c.Allows(Phase.End)).ToArray();

        public void PlayOutsideBattle(DeedCard card, string choiceId, params UnitState[] targets)
            => PlayOutsideCore(card,choiceId,null,targets);
        public void PlayOutsideWithInputs(DeedCard card,string choiceId,PveDeedSelection inputs)
            => PlayOutsideCore(card,choiceId,inputs,Array.Empty<UnitState>());
        public void PlayOutsideWithBorrowedPayment(DeedCard physical, ManaSkillPaymentSelection payment,
            DeedCard card, string choiceId, PveDeedSelection inputs = null, params UnitState[] targets)
        {
            if (physical == null || payment == null) throw new InvalidOperationException("请选择实际头环及公共付款技能");
            PlayOutsideCore(card, choiceId, inputs, targets, physical, payment);
        }
        private void PlayOutsideCore(DeedCard card,string choiceId,PveDeedSelection inputs,UnitState[] targets,
            DeedCard physical = null, ManaSkillPaymentSelection payment = null)
        {
            RequireOutsideBattle();
            var choice = OutsideChoices(card).SingleOrDefault(c=>c.Id==choiceId)
                ?? throw new InvalidOperationException("请选择这张原卡的战斗外效果");
            var original = _cards.SingleOrDefault(c=>c.Id==card.Id)
                ?? throw new InvalidOperationException("缺少原卡定义");
            ValidateWorldSpell(card.Id,choice,inputs);
            var selected = targets ?? Array.Empty<UnitState>();
            if (selected.Any(u=>u==null || !Player.Units.Contains(u) || u.IsDestroyed)
                || selected.Distinct().Count()!=selected.Length || card.Id!="magic_005" && selected.Length>1)
                throw new InvalidOperationException("请选择合法且不重复的自有部队目标");
            var previous = Context.TargetUnit; var previousMany = Context.ReadyUnitTargets;
            var previousMana = Context.SelectedActionManaColor;
            try
            {
                Context.SelectedActionManaColor = choice.ActionMana;
                Context.TargetUnit = selected.FirstOrDefault();
                Context.ReadyUnitTargets = selected.ToList();
                PveDeedInputs.Execute(Player,Context,card,choice,inputs,option=>
                {
                    if (physical == null) CardPlayActions.Play(Player,Context,card,original,choice.Enhanced,option,_actions);
                    else ManaSkillPaymentActions.ExecuteBorrowedCard(Player,Context,physical,payment,card,original,choice.Enhanced,option,_actions);
                });
                AttachTunnelBattle();
            }
            finally { Context.TargetUnit=previous; Context.ReadyUnitTargets=previousMany; Context.SelectedActionManaColor=previousMana; }
        }

        public void HealOutsideBattle(UnitState unit = null, bool grailFirst = true)
        {
            RequireOutsideBattle();
            CardEffects.CardHealing.Spend(Player,Context,unit:unit,grailFirst:grailFirst);
        }

        public void ContinueCopiedEffect(string choiceId,PveDeedSelection inputs=null)
        {RequireOutsideBattle();PveCopyInputs.Continue(Player,Context,choiceId,inputs);}

        public void Rest(DeedCard ordinary,params DeedCard[] wounds)
        {
            RequireOutsideBattle();RestActions.Rest(Player,Context,ordinary,wounds);
        }

        public BattleSkillChoice[] OutsideSkillChoices(SkillCard skill) =>
            HeroSocialSkillRules.Handles(skill)?HeroSocialSkillRules.Options(Player,skill).Select(o=>new BattleSkillChoice(o.Label,o.Option,Phase.End)).ToArray():
            skill?.SourceId=="hero_skill_pool_003:07"?ChaosSkillRules.Options(Player,Context).Select(o=>new BattleSkillChoice(o.Label,o.Option,Phase.End)).ToArray():
            BattleSkillChoice.For(skill?.SourceId).Where(c=>c.Phases.Contains(Phase.End))
            .Where(c=>skill?.SourceId!="hero_skill_pool_003:03" || (c.Option==100)==Player.FlippedSkills.Contains(skill.SourceId)).ToArray();

        public void UseOutsideSkill(SkillCard skill,int option=0,UnitState target=null,DeedCard wound=null)
        {
            RequireOutsideBattle();
            if(!OutsideSkillChoices(skill).Any(c=>c.Option==option))throw new InvalidOperationException("请选择本技能的战斗外效果");
            if(target!=null && (!Player.Units.Contains(target) || target.IsDestroyed))
                throw new InvalidOperationException("请选择未销毁的自有部队");
            var previous=Context.TargetUnit;var previousWound=Context.SelectedSkillWound;
            try { Context.TargetUnit=target;Context.SelectedSkillWound=wound;SkillActions.Use(Player,Context,skill,option); }
            finally { Context.TargetUnit=previous;Context.SelectedSkillWound=previousWound; }
        }

        public void Fly(SkillCard skill, Map.AxialCoord destination)
        {
            RequireOutsideBattle();
            FlightSkillActions.Move(Player,Context,skill,destination,Context.OtherPlayers);
        }

        public int MovementCost(Map.AxialCoord destination) => MovementService.GetCost(destination,Game.Map,Game.Clock.DayPart,Context);

        public void Move(Map.AxialCoord destination,DeedCard harvestCard=null,ManaColor? harvestColor=null)
        {
            RequireOutsideBattle();
            if (!new MovementService().TryMoveUsingPool(Player,destination,Game.Map,Context,harvestCard:harvestCard,harvestColor:harvestColor))
                throw new InvalidOperationException("无法移动：请核对相邻位置、原地形费用、通行许可和本回合行动");
            AttachRampagingBattle();
        }

        public void TemporalPortal(Map.AxialCoord? destination)
        {
            RequireOutsideBattle();
            if(!Context.TemporalPortalPending)throw new InvalidOperationException("当前没有待选择的时光传送");
            if(destination.HasValue)
            {
                if(!CardTravelActions.TryTemporalPortal(Player,Context,destination.Value))
                    throw new InvalidOperationException("请选择原范围内已揭示的安全格");
            }
            else CardTravelActions.EndTemporalPortal(Context);
        }

        public void PlayTravelCard(DeedCard card,string choiceId,Map.AxialCoord? hex=null,TerrainType? terrain=null)
        {
            RequireOutsideBattle();
            var oldHex=Context.TargetHex;var oldTerrain=Context.TargetTerrain;
            try { Context.TargetHex=hex;Context.TargetTerrain=terrain;PlayOutsideBattle(card,choiceId); }
            finally { Context.TargetHex=oldHex;Context.TargetTerrain=oldTerrain; }
        }

        public void PlayTravelWithBorrowedPayment(DeedCard physical, ManaSkillPaymentSelection payment,
            DeedCard card, string choiceId, Map.AxialCoord? hex = null, TerrainType? terrain = null)
        {
            RequireOutsideBattle();
            var oldHex = Context.TargetHex; var oldTerrain = Context.TargetTerrain;
            try
            {
                Context.TargetHex = hex; Context.TargetTerrain = terrain;
                PlayOutsideWithBorrowedPayment(physical, payment, card, choiceId);
            }
            finally { Context.TargetHex = oldHex; Context.TargetTerrain = oldTerrain; }
        }

        public void SidewaysMovement(DeedCard card)
        {
            RequireOutsideBattle();
            if (Context.TurnActionCommitted || !_actions.PlaySideways(card,Player,Context,SidewaysType.Move))
                throw new InvalidOperationException("只能在移动阶段横置一张实际非创伤手牌");
        }

        public void PlayShapeshift(SkillCard skill, DeedCard card, bool enhanced, int option, FixedOutputKind target, DeedCard material=null, Map.AxialCoord? hex=null, TerrainType? terrain=null)
        {
            RequireOutsideBattle();
            ShapeshiftActions.Play(Player,Context,skill,card,enhanced,option,target,material,hex,terrain);
        }

        public void PlaySidewaysWithSkill(SkillCard skill, DeedCard card, SidewaysType type, ManaColor? color = null, bool forgoSourceDice = false)
        {
            RequireOutsideBattle();
            if (type != SidewaysType.Move && type != SidewaysType.Influence)
                throw new InvalidOperationException("战斗外请选择移动或影响力");
            if (type == SidewaysType.Move && Context.TurnActionCommitted)
                throw new InvalidOperationException("本回合行动已经开始，不能再产生移动力");
            SkillActions.PlaySideways(Player, Context, skill, card, type, color, forgoSourceDice);
        }

        public void SelectSteadyReturn(DeedCard card,bool selected)
        {
            RequireOutsideBattle();
            CardEffects.SteadyTempoEffect.SelectReturn(Player,Context,card,selected);
        }

        public void AnnounceRoundEnd()
        {
            if (SoloClock == null) Game.AnnounceRoundEnd();
            else SoloClock.AnnounceRoundEnd();
        }
        public void EndRound()
        {
            if (SoloClock == null) Game.EndRound();
            else SoloClock.EndRound(_soloDummy, _soloPrintedColor);
        }
    }
}
