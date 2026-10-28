using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Playable encounter orchestration over the formal card, phase and combat services.
    /// The caller supplies the actual player and original enemies; this class invents no stats or effects.</summary>
    public sealed class PveBattleSession
    {
        private readonly GameState _state = new();
        private readonly TurnMachine _turn;
        private readonly Dictionary<string, ActionCardData> _cards;
        private readonly Dictionary<string, ItemCardData> _artifacts;
        private readonly ActionSystem _actions;
        private readonly Func<bool> _canAct;
        private void RequireActiveTurn()
        {
            if (_canAct != null && !_canAct())
                throw new InvalidOperationException("这场战斗所属回合已结束，只能查看战果");
        }
        private readonly List<BlockAllocation> _blocks = new();
        private readonly HashSet<(int, int)> _blocked = new();
        private readonly List<UnitDamageAllocation> _damagePlan = new();
        public PlayerState Player { get; }
        public ActionContext Context { get; }
        public IList<Monster> Enemies { get; }
        public Phase Phase => _state.CurrentPhase;
        public bool Won => Phase == Phase.End && Context.DefeatedEnemyIndices.Count == Enemies.Count;
        public string LastEvent { get; private set; } = "战斗开始：先进行远程／攻城攻击。";
        public IReadOnlyList<BlockAllocation> ReservedBlocks => _blocks;
        public IReadOnlyList<UnitDamageAllocation> DamagePlan => _damagePlan;

        public void ConvertMovement(bool ranged, int amount = 1)
        {
            RequireActiveTurn();
            RepeatedEffectResolution.RequireCompleted(Context);
            if (ranged ? Phase != Phase.Ranged && Phase != Phase.Melee : Phase != Phase.Melee)
                throw new InvalidOperationException("请在对应的攻击阶段转换移动力");
            if (!(ranged ? _actions.ConvertMoveToRanged(Context, amount) : _actions.ConvertMoveToAttack(Context, amount)))
                throw new InvalidOperationException("需要灵动的原转换许可及足够实际移动力");
            LastEvent = $"灵动：实际移动力已支付，获得{amount}点" + (ranged ? "远程攻击。" : "普通攻击。");
        }

        public void ConvertInfluence(int amount = 1)
        {
            RequireActiveTurn();
            RepeatedEffectResolution.RequireCompleted(Context);
            if (Phase != Phase.Block || !_actions.ConvertInfluenceToBlock(Context, amount))
                throw new InvalidOperationException("请在格挡阶段，按外交许可支付实际影响力");
            LastEvent = $"外交：支付影响力{amount}，获得{Context.InfluenceBlockElement}格挡{amount}。";
        }

        public void ResolveExplosiveArmor(int target = -1)
        {
            RequireActiveTurn();
            if(target>=0)RequireTarget(target);
            CardCombatActions.ResolveExplosiveBoltArmor(Context,target<0?null:Enemies[target]);
            LastEvent=target<0?"已放弃剩余爆裂箭矢减甲奖励。":$"爆裂箭矢：所选实际敌人护甲-1，本场战斗有效；剩余{Context.ExplosiveBoltArmorUses}次。";
        }

        public PveBattleSession(PlayerState player, IEnumerable<Monster> enemies,
            IEnumerable<ActionCardData> originalCards, ActionContext context = null, IEnumerable<ItemCardData> artifacts = null,
            Func<bool> canAct = null, IEnumerable<SpellCardData> spells = null, RampagingEncounter rampaging = null,
            EnemyRewardMode? enemyRewardMode = null)
        {
            _canAct = canAct;
            RequireActiveTurn();
            _actions = new ActionSystem(loadSpellMetadata: false, originalSpells: spells);
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Enemies = enemies?.ToList() ?? throw new ArgumentNullException(nameof(enemies));
            if (Enemies.Count == 0) throw new ArgumentException("遭遇至少需要一名原版敌人");
            _cards = originalCards.ToDictionary(c => c.Id);
            _artifacts = (artifacts ?? Array.Empty<ItemCardData>()).ToDictionary(c => c.Id);
            Context = context ?? new ActionContext();
            if (enemyRewardMode.HasValue)
            {
                if (!Enum.IsDefined(typeof(EnemyRewardMode), enemyRewardMode.Value)) throw new ArgumentOutOfRangeException(nameof(enemyRewardMode));
                Context.EnemyRewardMode = enemyRewardMode.Value;
            }
            if (rampaging != null && (Context.Enemies == null || Context.Enemies.Count != Enemies.Count
                || Context.Enemies.Where((enemy, index) => !ReferenceEquals(enemy, Enemies[index])).Any()))
                throw new InvalidOperationException("接管肆虐战斗不能替换实际敌人标记");
            Context.Enemies = Enemies;
            Context.EngagedEnemies = Enemies.Count;
            if (rampaging != null)
            {
                if (!ReferenceEquals(Context.RampagingBattle, rampaging) || !Context.InBattle
                    || rampaging.State.Players.Count != 1 || !ReferenceEquals(rampaging.State.Players[0], Player))
                    throw new InvalidOperationException("只能接管此玩家当前实际肆虐战斗");
                _state = rampaging.State;
                _turn = rampaging.Machine;
            }
            else
            {
                _state.Players.Add(Player);
                _turn = new TurnMachine(_state);
                _turn.BeginBattle(Context);
            }
        }

        public BattleCardChoice[] Choices(DeedCard card) => BattleCardChoice.For(card.Id)
            .Where(c => c.Allows(Phase) || Context.AllAttacksSiege && Phase == Phase.Ranged && c.Allows(Phase.Melee)
                || c.ProducesMovement && CanUseCombatMovement).ToArray();

        private bool CanUseCombatMovement => (Phase == Phase.Ranged || Phase == Phase.Block || Phase == Phase.Melee)
            && (Context.MoveCostAttack > 0 || Context.MoveCostRanged > 0
                || Phase == Phase.Block && Enemies.Any(e => NatureRevengeActions.HasCumbersome(Context, e)));

        public void ContinueCopiedEffect(string choiceId,PveDeedSelection inputs=null)
        {RequireActiveTurn();PveCopyInputs.Continue(Player,Context,choiceId,inputs);}

        public int AttackTotal(AttackType type) => Context.CombatPower.Attacks
            .Where(a => (CombatMath.IsSiege(a, Context) ? AttackType.Siege : a.Type == AttackType.Melee ? AttackType.Melee : AttackType.Ranged) == type).Sum(a => CombatMath.AttackValue(a, Phase, Context));

        public BattleSkillChoice[] SkillChoices(SkillCard skill,int target=-1,int attackIndex=0)
        {
            if(HeroSocialSkillRules.Handles(skill))return HeroSocialSkillRules.Options(Player,skill).Select(o=>new BattleSkillChoice(o.Label,o.Option,Phase)).ToArray();
            if(skill?.SourceId=="hero_skill_pool_003:07")return ChaosSkillRules.Options(Player,Context).Select(o=>new BattleSkillChoice(o.Label,o.Option,Phase)).ToArray();
            if (Phase == Phase.End && skill?.SourceId is "hero_skill_pool_005:07" or "hero_skill_pool_007:06" or "hero_skill_pool_007:07")
                return Array.Empty<BattleSkillChoice>();
            if(skill?.SourceId!="hero_skill_pool_004:05")return BattleSkillChoice.For(skill?.SourceId)
                .Where(c=>c.Phases.Contains(Phase) && !(skill?.SourceId=="hero_skill_pool_003:03" && c.Option==100)).ToArray();
            var old=Context.SelectedEnemy;int previousAttack=Context.SelectedEnemyAttackIndex;
            try
            {
                Context.SelectedEnemy=target>=0 && target<Enemies.Count?Enemies[target]:null;Context.SelectedEnemyAttackIndex=attackIndex;
                return CardEffects.InsightPreyEffect.Options(Context).Select(o=>new BattleSkillChoice(o.Label,o.Option,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee)).ToArray();
            }
            finally{Context.SelectedEnemy=old;Context.SelectedEnemyAttackIndex=previousAttack;}
        }

        public void UseSkill(SkillCard skill, int option, int target = -1,DeedCard wound=null,int attackIndex=0)
        {
            RequireActiveTurn();
            var selectedChoice=SkillChoices(skill,target,attackIndex).SingleOrDefault(c=>c.Option==option);
            if (selectedChoice==null)
                throw new InvalidOperationException("此技能选择不能在当前战斗阶段使用");
            bool targeted = BattleSkillChoice.NeedsEnemy(skill.SourceId) && (skill.SourceId!="hero_skill_pool_001:01" || option==1);
            if (targeted) RequireTarget(target);
            if(targeted && (attackIndex<0 || attackIndex>=Enemies[target].AttackCount))
                throw new InvalidOperationException("请选择该敌人的实际攻击");
            var previousWound=Context.SelectedSkillWound;
            var previousEnemy=Context.SelectedEnemy;
            var previousAttack=Context.SelectedEnemyAttackIndex;
            try
            {
                Context.SelectedSkillWound=wound;
                if(targeted){Context.SelectedEnemy=Enemies[target];Context.SelectedEnemyAttackIndex=attackIndex;}
                SkillActions.Use(Player, Context, skill, skill.SourceId=="hero_skill_pool_007:04" ? target : option);
            }
            finally { Context.SelectedSkillWound=previousWound;Context.SelectedEnemy=previousEnemy;Context.SelectedEnemyAttackIndex=previousAttack; }
            LastEvent = skill.Id + "：" + selectedChoice.Label +
                (skill.Frequency == SkillFrequency.OncePerRound ? "。本轮已翻面使用。" : "。本回合已使用。");
        }

        public void UseForkedLightning(SkillCard skill, params int[] targets)
        {
            RequireActiveTurn();
            foreach(var target in targets)RequireTarget(target);
            ForkedLightningActions.Use(Player,Context,skill,targets.Select(i=>Enemies[i]).ToArray());
            LastEvent="叉形闪电：每名所选敌人各有冰火远程1，不能转移给另一敌人。";
        }
        public void UsePuppetToken(SkillCard skill, Monster enemy, int mode)
        {
            RequireActiveTurn();
            if(mode==0)PuppetMasterActions.Keep(Player,Context,skill,enemy);
            else if(mode==1 || mode==2)PuppetMasterActions.Consume(Player,Context,skill,enemy,mode==2);
            else throw new InvalidOperationException("请选择保存、攻击或格挡");
            LastEvent=mode==0?"已保存本回合实际击败的敌人标记。":"已消费保存标记，按原元素产生实际贡献。";
        }

        private string BattleArtifactId(DeedCard card)
        {
            if (card == null || card.Type != CardType.Artifact) return null;
            if (card.Id != "items_024") return card.Id;
            var pending = Context.MysticBox;
            return pending != null && !pending.Used && ReferenceEquals(pending.Owner, Player)
                && ReferenceEquals(pending.Box, card) && Player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card))
                ? pending.RevealedArtifact.Id : null;
        }

        private void ExecuteBattleArtifact(DeedCard card, string effectiveId, bool burn, int option = 0)
        {
            if (card.Id == "items_024") CardPlayActions.UseMysticBox(Player, Context, card, burn, option);
            else CardPlayActions.PlayArtifact(Player, Context, card, _artifacts[effectiveId], burn, option);
        }

        public bool CanUseArtifact(DeedCard card)
        {
            var id = BattleArtifactId(card);
            return id != null && _artifacts.ContainsKey(id) && (PveArtifactInputs.IsResource(id) || id == "items_002" || id == "items_012" && Phase == Phase.End || id == "items_018" || id == "items_003" || (id == "items_000" || id == "items_001") && Phase != Phase.End || (id == "items_008" || id == "items_019") && Phase == Phase.Melee || id == "items_020" && Phase == Phase.Block || (id == "items_009" || id == "items_016") && (Phase == Phase.Ranged || Phase == Phase.Melee));
        }

        public void PlayResourceArtifact(DeedCard card, bool burn, params ManaColor[] colors)
        {
            RequireActiveTurn();
            if (!CanUseArtifact(card)) throw new InvalidOperationException("缺少当前实际神器定义");
            var id = BattleArtifactId(card);
            int option=PveArtifactInputs.ColorOption(id,burn,colors);
            ExecuteBattleArtifact(card,id,burn,option);
            LastEvent=_artifacts[id].Name + (burn ? "：焚毁，原卡永久移除。" : "：普通效果，原卡进入已打出区。")
                + (id=="items_011" && !burn ? "\n实际骰面："+string.Join("、",Context.LastManaRolls) : "");
        }

        public void ChooseBowAttack(int contribution, int choice)
        {
            RequireActiveTurn();
            if (Phase != Phase.Ranged && Phase != Phase.Melee)
                throw new InvalidOperationException("请在远程或近战阶段选择攻击贡献");
            CardCombatActions.ChooseStarBow(Context, contribution, choice);
            LastEvent = "星辰之弓：仅变更所选一笔攻击；原元素与击杀奖励保持不变。";
        }

        public void PlayHorn(DeedCard card, bool burn, int bonus)
        {
            RequireActiveTurn();
            if (BattleArtifactId(card) != "items_009" || !CanUseArtifact(card))
                throw new InvalidOperationException("请在远程或近战阶段选择原愤怒号角");
            int before = Player.Wounds;
            ExecuteBattleArtifact(card, "items_009", burn, bonus);
            string colors = string.Join("、", Context.LastManaRolls.Select(c => c switch
                { ManaColor.Red => "红", ManaColor.Blue => "蓝", ManaColor.Green => "绿", ManaColor.White => "白", ManaColor.Gold => "金", _ => "黑" }));
            LastEvent = $"愤怒号角：攻城攻击{5 + (burn ? bonus : 0)}；骰面：{(colors.Length == 0 ? "无需投骰" : colors)}；创伤 +{Player.Wounds - before}。\n" +
                (burn ? "原神器已永久移除。" : "原神器进入已打出区。");
        }

        public void UseCourage(UnitState unit)
        {
            RequireActiveTurn();
            BannerActions.UseCourage(Player, Context, unit);
            LastEvent = "勇气旌旗：本轮已使用，所选部队已重整；创伤保持不变。";
        }

        public void SelectFortitude(UnitState unit, bool use)
        {
            RequireActiveTurn();
            if (Phase == Phase.End) throw new InvalidOperationException("战斗已结束，无需预选伤害保护");
            BannerActions.SelectFortitudeProtection(Player, unit, use);
            LastEvent = use ? "已选择强韧旌旗：忽略下一点创伤及附加效果，本轮一次。" : "已取消强韧旌旗保护选择。";
        }

        public void ChooseKillCrystal(int target, ManaColor color)
        {
            RequireActiveTurn();
            RequireTarget(target);
            if (Phase != Phase.Melee) throw new InvalidOperationException("请在当前近战阶段选择击杀奖励");
            Context.ChooseKillCrystal(target, color);
            LastEvent = "已选择该敌人的击杀奖励魔晶；只有实际击杀才会获得。";
        }

        public void PlayShield(DeedCard card, bool burn, bool split)
        {
            RequireActiveTurn();
            if (BattleArtifactId(card) != "items_020" || !CanUseArtifact(card))
                throw new InvalidOperationException("请在格挡阶段选择原亡君之盾");
            ExecuteBattleArtifact(card, "items_020", burn, split ? 1 : 0);
            LastEvent = "亡君之盾：" + (split ? "分次格挡，每次选择不同攻击。" : "单次格挡。") +
                (burn ? "原卡已永久移除。" : "原卡进入已打出区。");
        }

        public void BurnFearBanner(DeedCard card, params int[] targets)
        {
            RequireActiveTurn();
            if (BattleArtifactId(card) != "items_001") throw new InvalidOperationException("请选择原恐惧旌旗");
            var previous = Context.SpellTargetIndices.ToArray();
            try
            {
                Context.SpellTargetIndices.Clear(); Context.SpellTargetIndices.AddRange(targets);
                PlayArtifact(card, true);
            }
            finally { Context.SpellTargetIndices.Clear(); Context.SpellTargetIndices.AddRange(previous); }
        }

        public void UseFearBanner(UnitState unit, int target, int attack = 0)
        {
            RequireActiveTurn();
            RequireTarget(target);
            if (IsBlocked(target, attack)) throw new InvalidOperationException("该次攻击已经格挡");
            BannerActions.UseFear(Player, Context, unit, target, attack);
            LastEvent = $"恐惧旌旗：取消敌人 #{target + 1} 的攻击 #{attack + 1}，名望 +1。部队已耗竭。";
        }

        public void PlayArtifact(DeedCard card, bool burn, UnitState unit = null, params DeedCard[] materials)
        {
            RequireActiveTurn();
            if (!CanUseArtifact(card)) throw new InvalidOperationException("此神器效果尚未接入当前战斗阶段");
            var id = BattleArtifactId(card);
            if (materials == null) throw new InvalidOperationException("必须提供实际手牌材料列表");
            if (id != "items_008" && id != "items_016" && materials.Length > 0 || burn && materials.Length > 0)
                throw new InvalidOperationException("此效果不需要弃牌材料");
            if (id == "items_009") { PlayHorn(card, burn, 0); return; }
            if (PveArtifactInputs.IsResource(id)) { PlayResourceArtifact(card,burn); return; }
            var previousUnit = Context.TargetUnit;
            var previousMaterials = Context.CardsToDiscard.ToArray();
            try
            {
                Context.TargetUnit = unit;
                Context.CardsToDiscard.Clear(); Context.CardsToDiscard.AddRange(materials);
                ExecuteBattleArtifact(card, id, burn);
                LastEvent = id == "items_012" ? (burn ? "黄金圣杯已焚毁：治疗6，本回合治疗手牌创伤时抽牌。" : "黄金圣杯已打出：治疗2；仅消费本牌治疗点时获得名望。") : _artifacts[id].Name + (burn ? "：已焚毁，实际卡永久移除。" :
                    PveArtifactInputs.IsAttachedBanner(id) ? "：已附着所选部队。" : id == "items_019" ? "：攻击3；选目标后可切换奖励魔晶。本次攻击击杀后获得。" : "：所选材料实际弃置，神器进入已打出区。");
            }
            finally
            {
                Context.TargetUnit = previousUnit;
                Context.CardsToDiscard.Clear(); Context.CardsToDiscard.AddRange(previousMaterials);
            }
        }

        public void Play(DeedCard card, string choiceId, int target = -1, int attackIndex = 0, PveDeedSelection inputs = null)
            => PlayCore(card, choiceId, target, attackIndex, inputs);

        public void PlayWithBorrowedPayment(DeedCard physical, ManaSkillPaymentSelection payment,
            DeedCard card, string choiceId, int target = -1, int attackIndex = 0, PveDeedSelection inputs = null)
        {
            if (physical == null || payment == null) throw new InvalidOperationException("请选择实际头环及公共付款技能");
            PlayCore(card, choiceId, target, attackIndex, inputs, physical, payment);
        }

        private void PlayCore(DeedCard card, string choiceId, int target, int attackIndex, PveDeedSelection inputs,
            DeedCard physical = null, ManaSkillPaymentSelection payment = null)
        {
            RequireActiveTurn();
            var choice = Choices(card).SingleOrDefault(c => c.Id == choiceId)
                ?? throw new InvalidOperationException("此卡效果不能在当前阶段使用");
            if (!_cards.TryGetValue(card.Id, out var original))
                throw new InvalidOperationException("缺少原卡定义");
            if (choice.Target != BattleCardTarget.None) RequireTarget(target);
            var previousEnemy = Context.SelectedEnemy;
            int previousAttack = Context.SelectedEnemyAttackIndex;
            var previousMana = Context.SelectedActionManaColor;
            try
            {
                Context.SelectedActionManaColor = choice.ActionMana;
                if (choice.Target == BattleCardTarget.EnemyAttack)
                {
                    if (attackIndex < 0 || attackIndex >= Enemies[target].AttackCount || IsBlocked(target, attackIndex))
                        throw new InvalidOperationException("请选择尚未格挡的实际敌人攻击");
                    Context.SelectedEnemy = Enemies[target];
                    Context.SelectedEnemyAttackIndex = attackIndex;
                }
                PveDeedInputs.Execute(Player,Context,card,choice,inputs,option=>
                {
                    int actualOption = choice.Target == BattleCardTarget.EnemyIndex ? target : option;
                    if (physical == null) CardPlayActions.Play(Player, Context, card, original, choice.Enhanced, actualOption, _actions);
                    else ManaSkillPaymentActions.ExecuteBorrowedCard(Player,Context,physical,payment,card,original,choice.Enhanced,actualOption,_actions);
                });
            }
            finally
            {
                Context.SelectedEnemy = previousEnemy;
                Context.SelectedEnemyAttackIndex = previousAttack;
                Context.SelectedActionManaColor = previousMana;
            }
            LastEvent = original.Name + " · " + choice.Label + "\n" +
                (choice.Enhanced ? original.EnhancedEffect : original.BaseEffect);
        }

        public void ReduceAttack(int target, int attackIndex = 0)
        {
            RequireActiveTurn();
            RequireTarget(target);
            if (Phase != Phase.Block || IsBlocked(target, attackIndex) || Context.IsAttackCancelled(target, attackIndex) || Context.ColdStareSkips(Enemies[target]))
                throw new InvalidOperationException("请在格挡阶段选择尚未解决的攻击");
            CardCombatActions.ReduceEnemyAttack(Context, target, attackIndex);
            LastEvent = $"减攻已用于敌人 #{target + 1} 的攻击 #{attackIndex + 1}；按降低后的攻击计算迅捷格挡与实际伤害。";
        }

        public void PlaySideways(DeedCard card)
        {
            RequireActiveTurn();
            if (Phase != Phase.Block && Phase != Phase.Melee && !(Phase == Phase.Ranged && Context.AllAttacksSiege))
                throw new InvalidOperationException("横置只能提供本阶段的普通格挡或普通攻击，不能远程攻击");
            int before=Phase==Phase.Block?Context.BlockPool:Context.MeleePool;
            if (!_actions.PlaySideways(card, Player, Context,
                    Phase == Phase.Block ? SidewaysType.Block : SidewaysType.Attack))
                throw new InvalidOperationException("请选择实际手牌；创伤牌需要原技能许可");
            int gained=(Phase==Phase.Block?Context.BlockPool:Context.MeleePool)-before;
            LastEvent = "横置原卡：" + (Phase == Phase.Block ? "普通格挡 +" : "普通攻击 +")+gained;
        }

        public void PlayShapeshift(SkillCard skill, DeedCard card, bool enhanced, int option, FixedOutputKind target, DeedCard material=null, Map.AxialCoord? hex=null, TerrainType? terrain=null)
        {
            RequireActiveTurn();
            ShapeshiftActions.Play(Player,Context,skill,card,enhanced,option,target,material,hex,terrain);
        }

        public void PlaySidewaysWithSkill(SkillCard skill, DeedCard card, ManaColor? color = null, bool forgoSourceDice = false)
        {
            RequireActiveTurn();
            if (Phase != Phase.Block && Phase != Phase.Melee)
                throw new InvalidOperationException("请在格挡或近战阶段选择横置技能");
            int before = Phase == Phase.Block ? Context.BlockPool : Context.MeleePool;
            SkillActions.PlaySideways(Player, Context, skill, card,
                Phase == Phase.Block ? SidewaysType.Block : SidewaysType.Attack, color, forgoSourceDice);
            LastEvent = skill.Id + "：横置实际原牌，" + (Phase == Phase.Block ? "格挡 +" : "攻击 +")
                + ((Phase == Phase.Block ? Context.BlockPool : Context.MeleePool) - before);
        }

        public void BoostCardAttack(SkillCard skill, AttackProfile contribution)
        {
            RequireActiveTurn();
            ContributionSkillActions.Bullseye(Player, Context, skill, contribution);
            LastEvent = skill.Id + "：所选原牌攻击加值，保留原元素与攻击方式。";
        }

        public void ApplySoulGuideBlock(BlockAllocation contribution)
        {
            RequireActiveTurn();
            ContributionSkillActions.ApplySoulGuideBlock(Player, Context, contribution);
            LastEvent = "灵魂向导：所选实际格挡 +1，保留原元素与附效。";
        }

        public bool IsBlocked(int target, int attack = 0) => _blocked.Contains((target, attack))
            || Context.SuccessfullyBlockedAttacks.Contains((target, attack));

        public void UseUnit(UnitState unit, int abilityIndex, int option = 0, int target = -1, ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0, SkillCard leadership = null)
            => UseUnitCore(unit, abilityIndex, option, target, manaColors, boost, attackIndex, leadership);

        public void UseUnitWithBorrowedPayment(DeedCard physical, ManaSkillPaymentSelection payment,
            UnitState unit, int abilityIndex, int option = 0, int target = -1,
            ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0)
        {
            if (physical == null) throw new ArgumentNullException(nameof(physical));
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            UseUnitCore(unit, abilityIndex, option, target, manaColors, boost, attackIndex, null, physical, payment);
        }

        private void UseUnitCore(UnitState unit, int abilityIndex, int option, int target,
            ManaColor[] manaColors, int boost, int attackIndex, SkillCard leadership,
            DeedCard physical = null, ManaSkillPaymentSelection payment = null)
        {
            RequireActiveTurn();
            if (physical != null && (leadership != null || DelphanaMastersActions.IsMasters(unit)))
                throw new InvalidOperationException("头环付款不能与领导合并；大师独立付款尚未接入");
            void Execute(UnitState unitTarget = null)
            {
                if (physical == null)
                    CardUnitActions.Use(Player, Context, unit, abilityIndex, option, target, unitTarget, manaColors, boost, attackIndex);
                else
                    ManaSkillPaymentActions.ExecuteBorrowedUnit(Player, Context, physical, payment,
                        unit, abilityIndex, option, target, unitTarget, manaColors, boost, attackIndex);
            }
            if (leadership != null)
            {
                LeadershipActions.Use(Player,Context,leadership,unit,abilityIndex,option,target,manaColors,boost,attackIndex);
                LastEvent = "领导：" + unit.Card.NameCn + "本次原能力已加值；保留元素，已支付费用并耗竭。";
                return;
            }
            if (unit == null || !Player.Units.Contains(unit))
                throw new InvalidOperationException("只能发动自有部队");
            var abilities = unit.Card.Actions;
            if (abilities == null || abilityIndex < 0 || abilityIndex >= abilities.Length)
                throw new InvalidOperationException("部队能力选择无效");
            var ability = abilities[abilityIndex];
            if (ShockTroopsActions.IsReaction(ability))
            {
                RequireTarget(target);
                if (IsBlocked(target, attackIndex)) throw new InvalidOperationException("该次攻击已经格挡");
                Execute();
                LastEvent = unit.Card.NameCn + "：第" + (attackIndex + 1) + "次攻击-" + ability.Value + "。\n" +
                    (ability.Kind == UnitAbilityKind.InterceptEnemyAttack ? "本部队必须先承伤（含刺杀）。" : "目标护甲-1（奥术免疫除外）。") + "部队已耗竭。";
                return;
            }
            if (DelphanaMastersActions.IsMasters(unit))
            {
                RequireTarget(target);
                Execute();
                LastEvent = unit.Card.NameCn + "：" + ability.PrintedRule + "\n已付本次费用；未受伤时可继续使用本回合尚未用过的原能力。";
                return;
            }
            bool block = ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock && option == 1;
            bool attack = ability.Kind == UnitAbilityKind.Attack || ability.Kind == UnitAbilityKind.AttackOrBlock && option == 0;
            bool freeze = ability.Kind == UnitAbilityKind.FreezeArmor;
            bool stop = ability.Kind == UnitAbilityKind.StopUnfortifiedEnemy;
            if (freeze || stop || ability.RemoveTargetFortifications || ability.RemoveTargetResistances) RequireTarget(target);
            if (stop && Phase != Phase.Block) throw new InvalidOperationException("幻术师停攻能力只能在格挡阶段使用");
            bool support = ability.Kind == UnitAbilityKind.Healing || ability.Kind == UnitAbilityKind.ReadyUnit;
            bool influence = ability.Kind == UnitAbilityKind.Influence || ability.Kind == UnitAbilityKind.MovementOrInfluence && option == 1;
            if (influence && Phase == Phase.End) throw new InvalidOperationException("战斗已结束，请在交涉场景使用影响力能力");
            if (support && Phase != Phase.End) throw new InvalidOperationException("治疗与重整部队能力只能在战斗外使用");
            if (!support && !influence && !stop && ability.Kind != UnitAbilityKind.GainCrystal && ability.Kind != UnitAbilityKind.GainChosenTokens && ability.Kind != UnitAbilityKind.TransformAttacks && ability.Kind != UnitAbilityKind.AllUnitResistance && ability.Kind != UnitAbilityKind.GainToken && ability.Kind != UnitAbilityKind.GainMana && (freeze ? Phase != Phase.Ranged && Phase != Phase.Block && Phase != Phase.Melee : block ? Phase != Phase.Block : !attack || (Phase != Phase.Melee &&
                (Phase != Phase.Ranged || ability.AttackType == AttackType.Melee && !Context.AllAttacksSiege))))
                throw new InvalidOperationException("此部队能力不能在当前战斗阶段发动");
            UnitState unitTarget = ability.Kind == UnitAbilityKind.ReadyUnit && target >= 0 && target < Player.Units.Count ? Player.Units[target] : null;
            Execute(unitTarget);
            string outcome = ability.Kind == UnitAbilityKind.TransformAttacks ? (option == 0 ? "本次战斗全部攻击变为冰火。" : "本次战斗全部攻击获得攻城。") :
                ability.Kind == UnitAbilityKind.GainChosenTokens ? "已获得两枚所选魔力标记。" : ability.RedBlueBoost ? "冰火" + (block ? "格挡" : "攻击") + (ability.Value + 2 * ((boost & 1) + ((boost >> 1) & 1))) + "。" : ability.PrintedRule;
            LastEvent = unit.Card.NameCn + "：" + outcome + "\n部队已耗竭；未受伤时仍可承伤。";
        }

        public void HealWound(int target = -1, bool grailFirst = true)
        {
            RequireActiveTurn();
            if (target < -1 || target >= Player.Units.Count) throw new InvalidOperationException("治疗目标无效");
            var unit = target < 0 ? null : Player.Units[target];
            CardEffects.CardHealing.Spend(Player, Context, unit: unit, grailFirst: grailFirst);
            LastEvent = (unit == null ? "英雄手牌" : unit.Card.NameCn) + "：治疗一张真实创伤；剩余治疗点 " + Context.Healing.Total + "。";
        }

        /// <summary>Reserve actual owned units in order; no wounds or payments before confirmation.</summary>
        public void PlanDamage(int target, int attack, params UnitState[] units)
        {
            RequireActiveTurn();
            if (Phase != Phase.AssignDamage) throw new InvalidOperationException("当前不是分配伤害阶段");
            RequireTarget(target);
            if (attack < 0 || attack >= Enemies[target].AttackCount)
                throw new InvalidOperationException("敌人攻击序号无效");
            if (IsBlocked(target, attack) || Context.IsAttackCancelled(target, attack) || Context.ColdStareSkips(Enemies[target]))
                throw new InvalidOperationException("该次攻击不会造成伤害");
            if (units == null) throw new InvalidOperationException("承伤部队列表无效");
            var forced = Context.ForcedDamageUnit(Enemies[target], attack);
            if (forced != null && CardCombatActions.DefenseProfile(Enemies[target], target, Context, attack).Attack > 0)
                units = new[] { forced }.Concat(units.Where(u => !ReferenceEquals(u, forced))).ToArray();
            var others = _damagePlan.Where(a => a.TargetIndex != target || a.AttackIndex != attack).SelectMany(a => a.Units).ToHashSet();
            foreach (var unit in units)
            {
                UnitDamageResolver.Validate(Player, Context, unit);
                ShockTroopsActions.ValidateDamageUnit(Context, Enemies[target], target, attack, unit);
                if (!others.Add(unit)) throw new InvalidOperationException("同场战斗不能重复向该部队分配伤害");
            }
            _damagePlan.RemoveAll(a => a.TargetIndex == target && a.AttackIndex == attack);
            if (units.Length > 0) _damagePlan.Add(new UnitDamageAllocation(target, units.ToArray(), attack));
            LastEvent = units.Length == 0 ? "已取消此攻击的部队承伤，由英雄承担。" :
                "承伤顺序：" + string.Join(" → ", units.Select(u => u.Card.NameCn)) + " → 英雄；确认后结算。";
        }

        public (int Effective, int Required) BlockPreview(int target, int attack = 0)
            => PreviewBlock(target, attack, null);

        public (int Effective, int Required) BlockSelectedPreview(int target, IReadOnlyList<BlockAllocation> selected, int attack = 0)
            => PreviewBlock(target, attack, selected ?? throw new ArgumentNullException(nameof(selected)));

        private BlockAllocation[] SelectBlockSources(int target, int attack, IReadOnlyList<BlockAllocation> selected)
        {
            var available = Context.CombatPower.AvailableBlocks(target, attack, Enemies[target]);
            if (selected == null) return available;
            if (selected.Count == 0 || selected.Any(b => b == null || !available.Any(a => ReferenceEquals(a, b)))
                || selected.Where((b, i) => selected.Take(i).Any(a => ReferenceEquals(a, b))).Any())
                throw new InvalidOperationException("请选择当前攻击可用的实际格挡来源，不能重复、复制或使用已消耗来源");
            return selected.ToArray();
        }

        private (int Effective, int Required) PreviewBlock(int target, int attack, IReadOnlyList<BlockAllocation> selected)
        {
            RequireTarget(target);
            var enemy = CardCombatActions.DefenseProfile(Enemies[target], target, Context, attack);
            var ignored = Context.IgnoredAbilities.TryGetValue(target, out var ability)
                ? new HashSet<Ability> { ability } : null;
            var parts = CombatMath.ResolveBlocks(enemy, SelectBlockSources(target, attack, selected));
            int value = CombatMath.EffectiveBlock(Context.AttackElementChange.GetValueOrDefault(target, enemy.AttackElement),
                parts, enemy.Abilities.Contains(Ability.Swift) && ignored?.Contains(Ability.Swift) != true);
            return (value, AbilityRules.RequiredBlock(enemy, ignored));
        }

        public void Block(int target, int attack = 0)
            => ResolveBlock(target, attack, null);

        public void BlockSelected(int target, IReadOnlyList<BlockAllocation> selected, int attack = 0)
            => ResolveBlock(target, attack, selected ?? throw new ArgumentNullException(nameof(selected)));

        private void ResolveBlock(int target, int attack, IReadOnlyList<BlockAllocation> selected)
        {
            RequireActiveTurn();
            if (Phase != Phase.Block) throw new InvalidOperationException("当前不是格挡阶段");
            if (IsBlocked(target, attack)) throw new InvalidOperationException("该次攻击已经格挡");
            if (Context.IsAttackCancelled(target, attack)) throw new InvalidOperationException("该次攻击已经取消");
            var preview = PreviewBlock(target, attack, selected);
            if (preview.Effective < preview.Required)
                throw new InvalidOperationException($"格挡不足：有效 {preview.Effective} / 需要 {preview.Required}；部分格挡不能减伤");
            var sources = SelectBlockSources(target, attack, selected);
            // Keep source allocations unresolved: target-dependent bonuses are calculated exactly once by BattleResolver.
            _blocks.AddRange(sources.Select(b => b with { TargetIndex = target, AttackIndex = attack }));
            Context.CombatPower.ConsumeBlocks(sources, target, attack);
            _blocked.Add((target, attack));
            Context.CombatActionStarted = true;
            Context.TurnActionCommitted = true;
            LastEvent = $"格挡成立：有效 {preview.Effective} ≥ {preview.Required}。效果随承伤阶段结算。";
        }

        public IReadOnlyList<EnemyDefendChoice> DefendChoices(params int[] targets)
            => Context.EnemyDefense.Choices(Enemies, targets, Context);
        public void ChooseDefend(int sourceIndex, int targetIndex, params int[] targets)
        {
            RequireActiveTurn();
            if (Phase != Phase.Ranged && Phase != Phase.Melee) throw new InvalidOperationException("当前不是攻击阶段");
            Context.EnemyDefense.Choose(Enemies, targets, Context, sourceIndex, targetIndex);
        }
        public void ClearDefendChoices() => Context.EnemyDefense.ClearChoices();
        public int AttackArmorPreview(params int[] targets)
            {
            var pending = Context.EnemyDefense.PreviewAssignments(Enemies, targets, Context);
            return targets.Sum(i => CombatMath.EffectiveArmor(Enemies[i], i, Context, Phase,
                pending.Where(a => ReferenceEquals(a.Target, Enemies[i])).Sum(a => a.Value)));
        }

        public CardCombatActions.Result Attack(params int[] targets)
            => ResolveAttack(targets, null);

        public CardCombatActions.Result AttackSelected(IReadOnlyList<int> targets, IReadOnlyList<AttackProfile> selected)
            => ResolveAttack(targets, selected ?? throw new ArgumentNullException(nameof(selected)));

        private CardCombatActions.Result ResolveAttack(IReadOnlyList<int> targets, IReadOnlyList<AttackProfile> selected)
        {
            RequireActiveTurn();
            if (Phase != Phase.Ranged && Phase != Phase.Melee)
                throw new InvalidOperationException("当前不是攻击阶段");
            var result = Phase == Phase.Melee ? _turn.Attack(targets, Context, selected)
                : CardCombatActions.Attack(Player, Context, Enemies, targets, Phase, selected);
            LastEvent = $"攻击结算：有效 {result.EffectivePower} / 护甲 {result.RequiredPower}\n击败 {result.Kills}，名望 +{result.Fame}。";
            return result;
        }

        public void Advance()
        {
            RequireActiveTurn();
            RepeatedEffectResolution.RequireCompleted(Context);
            switch (Phase)
            {
                case Phase.Ranged:
                    // Individual attacks already recorded stable defeated indices; close without re-awarding or reindexing.
                    _turn.ApplyRanged(Enemies, Array.Empty<AttackAllocation>(), Context);
                    LastEvent = "格挡阶段：选择一名敌人的一次攻击；未完全格挡的攻击将造成完整伤害。";
                    break;
                case Phase.Block:
                    _turn.SetBlock(0, Element.Physical);
                    LastEvent = "分配伤害：可点选敌人与部队安排承伤；剩余伤害由英雄承担，确认后结算。";
                    break;
                case Phase.AssignDamage:
                    int before = Player.Wounds;
                    _turn.ResolveDefense(Enemies, _blocks, Context, _damagePlan);
                    LastEvent = $"承伤完成：手牌创伤 +{Player.Wounds - before}。进入近战阶段。";
                    break;
                case Phase.Melee:
                    _turn.FinishBattle(Context);
                    LastEvent = Won ? "战斗胜利：所有敌人已击败，名望已按每次击杀结算。" : "战斗结束：仍有敌人存活，本次未获胜。";
                    break;
                default: throw new InvalidOperationException("战斗已结束，不能重复结算或领取奖励");
            }
        }

        private void RequireTarget(int target)
        {
            if (target < 0 || target >= Enemies.Count || Context.DefeatedEnemyIndices.Contains(target))
                throw new InvalidOperationException("请选择存活的敌人");
        }
    }

    /// <summary>Only input choices/timing live here; all numbers, costs and effects stay in original card services.
    /// Unbound cards remain visible with original text and can be played sideways, never with a guessed effect.</summary>
    public sealed record BattleSkillChoice(string Label, int Option, params Phase[] Phases)
    {
        public static bool NeedsEnemy(string id) => id is "hero_skill_pool_001:01" or "hero_skill_pool_007:04" or "hero_skill_pool_003:01" or "hero_skill_pool_003:08" or "hero_skill_pool_004:06" or "hero_skill_pool_004:05" or "hero_skill_pool_004:07";
        public static bool NeedsAttack(string id,int option) => id=="hero_skill_pool_004:07" || id=="hero_skill_pool_001:01" && option==1 || id is "hero_skill_pool_003:01" or "hero_skill_pool_004:06" || id=="hero_skill_pool_003:08" && option==0 || id=="hero_skill_pool_004:05" && option>=200;
        public static BattleSkillChoice[] For(string id) => id switch
        {
            "hero_skill_pool_004:07" => new[] { new BattleSkillChoice("决斗：选敌格挡1，近战同敌攻击1",0,Phase.Block) },
            "hero_skill_pool_001:05" => new[] { new BattleSkillChoice("选择至多三名实际敌人",0,Phase.Ranged,Phase.Melee) },
            "hero_skill_pool_003:06" => new[] { new BattleSkillChoice("选择实际敌人标记与用途",0,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End) },
            "hero_skill_pool_003:00" => new[]{new BattleSkillChoice("移动1 · 一次实际格挡加值",0,Phase.Block,Phase.End)},
            "hero_skill_pool_004:04" => new[]{new BattleSkillChoice("选择一张原牌的攻击",0,Phase.Ranged,Phase.Melee)},
            "hero_skill_pool_002:06" => new[]{new BattleSkillChoice("选择部队及原能力",0,Phase.Ranged,Phase.Block,Phase.Melee)},
            "hero_skill_pool_001:01" => new[]{new BattleSkillChoice("攻击1",0,Phase.Melee),new BattleSkillChoice("所选实际攻击减1",1,Phase.Ranged,Phase.Block,Phase.AssignDamage)},
            "hero_skill_pool_001:07" => new[]{new BattleSkillChoice("移动1与山脉费用5",0,Phase.End),new BattleSkillChoice("支付蓝魔力并开放湖泊2",1,Phase.End)},
            "hero_skill_pool_004:02" => new[]{new BattleSkillChoice("移动1（探索与揭示为常驻）",0,Phase.End)},
            "hero_skill_pool_005:06" => new[]{new BattleSkillChoice("选择实际安全飞行落点",0,Phase.End)},
            "hero_skill_pool_001:06" => new[]{new BattleSkillChoice("选择原基本牌固定项转换",0,Phase.Block,Phase.Melee,Phase.End)},
            "hero_skill_pool_005:07" or "hero_skill_pool_007:06" or "hero_skill_pool_007:07" => new[]{new BattleSkillChoice("选择实际手牌横置",0,Phase.Block,Phase.Melee,Phase.End)},
            "hero_skill_pool_003:02" => new[]{new BattleSkillChoice("攻击2 · 保持正面",0,Phase.Melee),new BattleSkillChoice("攻击4 · 翻面至正式休息",1,Phase.Melee)},
            "hero_skill_pool_003:03" => new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black}.Select(c=>new BattleSkillChoice("取得"+new[]{"红","蓝","绿","白","金","黑"}[(int)c]+"标记并翻面",(int)c,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End)).Concat(new[]{new BattleSkillChoice("使用回合行动翻回正面",100,Phase.End)}).ToArray(),
            "hero_skill_pool_001:00" => new[]{new BattleSkillChoice("下次火焰/寒冰承伤忽略2",0,Phase.AssignDamage),new BattleSkillChoice("下次其他类型承伤忽略1",1,Phase.AssignDamage)},
            "hero_skill_pool_003:01" => new[]{new BattleSkillChoice("所选物理承伤忽略2",0,Phase.AssignDamage),new BattleSkillChoice("所选非物理承伤忽略1",1,Phase.AssignDamage)},
            "hero_skill_pool_003:08" => new[]{new BattleSkillChoice("所选一次攻击降低1",0,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee),new BattleSkillChoice("所选敌人护甲降低1",1,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee)},
            "hero_skill_pool_004:06" => new[]{new BattleSkillChoice("所选一次攻击降低1",0,Phase.Block),new BattleSkillChoice("所选攻击增加2 · 敌甲降低2",1,Phase.Block)},
            "hero_skill_pool_001:08" or "hero_skill_pool_003:04" => new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black}.Select(c=>new BattleSkillChoice("支付"+new[]{"红","蓝","绿","白","金","黑"}[(int)c]+"色魔力",(int)c,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End)).ToArray(),
            "hero_skill_pool_000:05" => new[]{new BattleSkillChoice("本回合一张伤牌可横置为2",0,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End)},
            "hero_skill_pool_002:02" => new[]{new BattleSkillChoice("重整所选部队",0,Phase.End),new BattleSkillChoice("治疗所选部队",1,Phase.End)},
            "hero_skill_pool_000:08" or "hero_skill_pool_002:08" or "hero_skill_pool_004:08" or "hero_skill_pool_005:08" or "hero_skill_pool_007:08" => new[]{new BattleSkillChoice("抽两张原牌 · 最低名望奖励",0,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End)},
            "hero_skill_pool_000:03" or "hero_skill_pool_002:03" => new[]{new BattleSkillChoice("依昼夜获得原影响力",0,Phase.End)},
            "hero_skill_pool_001:04" => new[]{new BattleSkillChoice("依实际地形及城防获得影响力",0,Phase.End)},
            "hero_skill_pool_005:05" => new[]{new BattleSkillChoice("交涉：按库存魔晶种类获得影响力",0,Phase.End)},
            "hero_skill_pool_001:02" => StormChoices("绿","蓝","绿","白"),
            "hero_skill_pool_001:03" => StormChoices("蓝","绿","蓝","红"),
            "hero_skill_pool_000:00" or "hero_skill_pool_007:00" => new[] {new BattleSkillChoice("依昼夜获得移动力",0,Phase.End)},
            "hero_skill_pool_002:00" => new[] {new BattleSkillChoice("按就绪且未受伤部队获得移动力",0,Phase.End)},
            "hero_skill_pool_007:05" => new[] {new BattleSkillChoice("弃一张手牌创伤，再抽一张牌",0,Phase.End)},
            "hero_skill_pool_000:04" => new[] { new BattleSkillChoice("红色魔晶与红色魔力", 0, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee, Phase.End), new BattleSkillChoice("红色魔晶与黑色魔力", 1, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee, Phase.End) },
            "hero_skill_pool_002:04" => new[] { new BattleSkillChoice("绿色魔晶与白色魔力", 0, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee, Phase.End) },
            "hero_skill_pool_002:05" => new[] { new BattleSkillChoice("白色魔晶与绿色魔力", 0, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee, Phase.End) },
            "hero_skill_pool_004:00" => new[] { new BattleSkillChoice("获得治疗1与蓝色魔晶", 0, Phase.End) },
            "hero_skill_pool_004:01" => new[] { new BattleSkillChoice("获得治疗1与白色魔晶", 0, Phase.End) },
            "hero_skill_pool_005:01" => new[] { new BattleSkillChoice("获得治疗2", 0, Phase.End) },
            "hero_skill_pool_005:02" or "hero_skill_pool_005:03" or "hero_skill_pool_005:04" => new[] { new BattleSkillChoice("获得蓝晶体和印刷颜色魔力", 0, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee, Phase.End) },
            "hero_skill_pool_000:01" => new[] { new BattleSkillChoice("普通攻城攻击", 0, Phase.Ranged, Phase.Melee), new BattleSkillChoice("火焰攻城攻击", 1, Phase.Ranged, Phase.Melee) },
            "hero_skill_pool_005:00" => new[] { new BattleSkillChoice("普通攻城攻击", 0, Phase.Ranged, Phase.Melee), new BattleSkillChoice("寒冰攻城攻击", 1, Phase.Ranged, Phase.Melee) },
            "hero_skill_pool_002:01" => new[] { new BattleSkillChoice("依昼夜获得远程攻击", 0, Phase.Ranged, Phase.Melee) },
            "hero_skill_pool_000:02" => new[] { new BattleSkillChoice("普通攻击", 0, Phase.Melee), new BattleSkillChoice("火焰攻击", 1, Phase.Melee) },
            "hero_skill_pool_007:02" => new[] { new BattleSkillChoice("普通攻击", 0, Phase.Melee), new BattleSkillChoice("寒冰攻击", 1, Phase.Melee) },
            "hero_skill_pool_007:01" => new[] { new BattleSkillChoice("依昼夜获得远程攻击", 0, Phase.Ranged, Phase.Melee) },
            "hero_skill_pool_007:03" => new[] { new BattleSkillChoice("普通格挡", 0, Phase.Block), new BattleSkillChoice("寒冰格挡", 1, Phase.Block), new BattleSkillChoice("火焰格挡", 2, Phase.Block) },
            "hero_skill_pool_007:04" => new[] { new BattleSkillChoice("削弱所选敌人的抗性", 0, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee) },
            _ => Array.Empty<BattleSkillChoice>()
        };

        private static BattleSkillChoice[] StormChoices(string first,string firstAlternative,string second,string secondAlternative) =>
            new[]{0,1,256,257}.Select(option=>new BattleSkillChoice(
                ((option&1)==0?first:firstAlternative)+"标记 + "+((option&256)==0?second:secondAlternative)+"标记",option,
                Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End)).ToArray();
    }

    public enum BattleCardTarget { None, EnemyIndex, EnemyAttack }

    public sealed record BattleCardChoice(string Id, string Label, bool Enhanced, int Option, params Phase[] Phases)
    {
        public BattleCardTarget Target { get; init; }
        public ManaColor? ActionMana { get; init; }
        public bool Allows(Phase phase) => Phases.Contains(phase);
        public bool ProducesMovement { get; init; }
        public static BattleCardChoice[] For(string id) => id switch
        {
            "magic_004" => new[]{Outside("base","地道穿行 · 已揭示安全路径",false),Outside("strong","地道攻击 · 实际城防地点",true)},
            "magic_023" => new[]{Outside("base","清风之翼 · 逐格支付移动",false),new BattleCardChoice("strong","黑夜之翼 · 选敌停攻",true,0,Phase.Ranged,Phase.Block)},
            "magic_006" => new[]{Any("base","熔毁 · 实际库存与随机丢晶",false),Any("strong","辐射 · 选色旧库存伤害与两晶",true)},
            "magic_012" => new[]{Any("base","宣夺 · 立即三枚魔力",false),Any("base_deferred","宣夺 · 下回合起每回合一枚",false,256),
                Any("strong","诅咒 · 立即三枚魔力",true),Any("strong_deferred","诅咒 · 下回合起每回合一枚",true,256)},
            "magic_018" => new[]{Any("base","读取 · 选基本色魔晶",false),Any("strong","窃取 · 选色与实际他人行动",true)},
            "advanced_card_024" => new[]{Any("base","原行动基础效果 · 动态三次",false),Any("strong","原行动强效 · 动态两次",true)},
            "advanced_card_025" => new[]{Any("base","弃同色原牌 · 供应施法许可",false),Any("strong","蓝费与同色魔力 · 取得原法术",true)},
            "advanced_card_030" => new[]{Any("base","同色魔力取得原行动 · 伤入手",false),Any("strong","借供应原强效 · 选择伤牌去向",true)},
            "magic_008" => new[]{new BattleCardChoice("base","火焰格挡4 · 成功后火攻4",false,0,Phase.Block),new BattleCardChoice("strong","火焰格挡4 · 成功后消灭",true,0,Phase.Block)},
            "magic_011" => new[]{Any("base","献祭 · 可弃至多三张得晶",false),new BattleCardChoice("strong","牺牲 · 选择实际两色配对",true,0,Phase.Ranged,Phase.Melee)},
            "magic_020" => new[]{Any("base","战斗号召 · 供应一次原能力",false),Any("strong","荣誉号召 · 取得实际供应部队",true)},
            "magic_021" => new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>Any("base_"+c,"影响4 · 交涉得"+Color(c)+"晶",false,(int)c))
                .Concat(new[]{Any("base_discount","影响4 · 交涉一次招募-3",false,256),Targeted("strong","附身 · 借原攻击打其他敌人",true,Phase.Ranged,Phase.Block,Phase.AssignDamage)}).ToArray(),
            "basic_card_003" => TravelChoices(),
            "advanced_card_032" => new[]{Outside("base","安全传送1格 · 下次手牌+1",false),Outside("strong_range","安全传送2格 · 下次手牌+1",true),Outside("strong_draw","安全传送1格 · 下次手牌+2",true,1)},
            "magic_014" => new[]{Outside("base","迷雾移动4 · 地形费用2",false),Any("strong","部队全抗性 · 忽略首张伤",true)},
            "magic_016" => new[]{Outside("base","空间扭曲 · 两格仍付地形费",false),Outside("strong","时间扭曲 · 不补牌再行动",true)},
            "advanced_card_037" => new[]{false,true}.SelectMany(strong=>new[]{
                Outside((strong?"strong_":"base_")+"move","可弃牌 · 移动",strong),Any((strong?"strong_":"base_")+"influence","可弃牌 · 影响力",strong,1),
                new BattleCardChoice((strong?"strong_":"base_")+"attack","可弃牌 · 攻击",strong,2,Phase.Melee),
                new BattleCardChoice((strong?"strong_":"base_")+"block","可弃牌 · 格挡",strong,3,Phase.Block)}).ToArray(),
            "advanced_card_038" => new[]{Combat("base","自有部队物理抗性",false),
                new BattleCardChoice("strong_siege","强化攻城3",true,0,Phase.Ranged,Phase.Melee),new BattleCardChoice("strong_block","强化格挡6",true,1,Phase.Block)},
            "advanced_card_040" => new[]{Any("base","取得缺少颜色魔晶",false)}.Concat(new[]{ManaColor.Blue,ManaColor.Green}.SelectMany(color=>new[]{
                Any("strong_move_"+color,"移动4 · "+Color(color)+"费",true,0) with{ActionMana=color},
                Outside("strong_heal_"+color,"治疗2 · "+Color(color)+"费",true,1) with{ActionMana=color},
                Any("strong_draw_"+color,"抽牌2 · "+Color(color)+"费",true,2) with{ActionMana=color}})).ToArray(),
            "advanced_card_041" => new[]{Any("base","受伤 · 红白各一晶",false),
                new BattleCardChoice("strong_white","白费 · 远程3与击杀减甲",true,0,Phase.Ranged,Phase.Melee){ActionMana=ManaColor.White},
                new BattleCardChoice("strong_red","红费 · 远程3与击杀减甲",true,0,Phase.Ranged,Phase.Melee){ActionMana=ManaColor.Red}},
            "advanced_card_006" => new[]{Any("base","移动2 · 可转换攻击",false),Any("strong","移动4 · 可转换远攻",true)},
            "advanced_card_018" => new[]{Any("base","影响力2 · 可转格挡",false),Any("strong_ice","影响力4 · 寒冰格挡",true),Any("strong_fire","影响力4 · 火焰格挡",true,1)},
            "advanced_card_035" => new[]{Any("base","影响力3 · 可治疗",false),Any("strong","影响力6 · 可治疗重整",true)},
            "advanced_card_026" => new[]{Any("base","影响力2 · 可另付6购牌",false),Any("strong","影响力4 · 可另付9购牌",true)},
            "advanced_card_027" => new[]{Any("base","去牌换同色牌入弃牌",false),Any("strong","去牌换同色牌入手",true)},
            "advanced_card_033" => new[]{Any("base","选供应法术得同色晶",false),Any("strong","选两张法术各得一晶",true)},
            "advanced_card_004" => new[]{false,true}.SelectMany(strong=>new[]{
                new BattleCardChoice(strong?"strong":"base",strong?"强化攻击4":"基础攻击2",strong,0,Phase.Melee),
                new BattleCardChoice(strong?"strong_wound":"wound",strong?"受伤1 · 攻击9":"受伤1 · 攻击5",strong,1,Phase.Melee)}).ToArray(),
            "advanced_card_005" => new[]{new BattleCardChoice("base","寒冰格挡3",false,0,Phase.Block),
                new BattleCardChoice("strong","寒冰格挡3 · 减甲3",true,0,Phase.Block)},
            "advanced_card_031" => new[]{new BattleCardChoice("base","格挡3 · 对迅捷翻倍",false,0,Phase.Block),
                new BattleCardChoice("strong","格挡5 · 多余值减甲",true,0,Phase.Block)},
            "advanced_card_016" => new[]{new BattleCardChoice("base","部队攻防+2",false,0,Phase.Ranged),
                new BattleCardChoice("strong","部队攻防+3",true,0,Phase.Ranged)},
            "advanced_card_023" => new[]{Outside("base","移动与本回合首次攻防加成",false),Outside("strong","强化移动与首次攻防加成",true)},
            "advanced_card_028" => new[]{new BattleCardChoice("base","攻击2 · 每格挡+2",false,0,Phase.Melee),
                new BattleCardChoice("strong","攻击4 · 每格挡+3",true,0,Phase.Melee)},
            "advanced_card_034" => new[]{false,true}.SelectMany(strong=>new[]{
                new BattleCardChoice(strong?"strong":"base",strong?"强化攻击6":"基础攻击3",strong,0,Phase.Melee),
                new BattleCardChoice(strong?"strong_reward":"reward",strong?"攻击4 · 击杀奖励":"攻击2 · 击杀声誉+1",strong,1,Phase.Melee)}).ToArray(),
            "advanced_card_042" => new[]{Any("base","基础 · 新伤抽牌",false),
                Any("strong_red","红费 · 除首伤抽牌",true) with {ActionMana=ManaColor.Red},
                Any("strong_green","绿费 · 除首伤抽牌",true) with {ActionMana=ManaColor.Green}},
            "advanced_card_020" => new[]{Any("base","基础 · 去牌得同色晶",false),Any("strong","强化 · 去牌得异色晶",true)},
            "advanced_card_029" => new[]{new BattleCardChoice("base","基础 · 去牌换攻击",false,0,Phase.Ranged,Phase.Melee),
                new BattleCardChoice("strong","强化 · 去牌换攻击",true,0,Phase.Ranged,Phase.Melee)},
            "basic_card_009" or "basic_card_013" => new[]{Any("base","额外取用实际源骰",false),Any("strong","选择实际源骰并转换",true)},
            "advanced_card_012" => new[]{Any("base","受伤、红晶与自选魔力",false),Any("strong","受伤、独立三色与可选转晶",true)},
            "advanced_card_021" => new[]{Any("base","实际持有颜色再获一晶",false),Any("strong","本回合已付魔晶回合末返还",true)},
            "advanced_card_022" => new[]{Any("base","实际基本色源骰获晶并重掷",false),Any("strong","重掷源骰及额外取骰许可",true)},
            "basic_card_023" => new[]{false,true}.SelectMany(strong=>new[]{
                Outside(strong?"strong_move":"move","弃牌换移动",strong),Any(strong?"strong_influence":"influence","弃牌换影响力",strong,1),
                new BattleCardChoice(strong?"strong_attack":"attack","弃牌换攻击",strong,2,Phase.Melee),
                new BattleCardChoice(strong?"strong_block":"block","弃牌换格挡",strong,3,Phase.Block)}).ToArray(),
            "advanced_card_013" => new[]{false,true}.SelectMany(strong=>new[]{
                Outside(strong?"strong_move":"move","另付绿魔力换移动",strong,(int)ManaColor.Green),
                Any(strong?"strong_influence":"influence","另付白魔力换影响力",strong,(int)ManaColor.White),
                new BattleCardChoice(strong?"strong_attack":"attack","另付红魔力换攻击",strong,(int)ManaColor.Red,Phase.Melee),
                new BattleCardChoice(strong?"strong_block":"block","另付蓝魔力换格挡",strong,(int)ManaColor.Blue,Phase.Block)}).ToArray(),
            "magic_002" => new[]{Outside("top","随机两张弃牌置顶",false),Outside("bottom","随机两张弃牌置底",false,1),
                Outside("strong_top","选择两张实际弃牌置顶",true),Outside("strong_bottom","选择两张实际弃牌置底",true,1)},
            "magic_003" => new[] {
                Targeted("base", "震颤：单体减甲", false), Combat("base_all", "震颤：全体减甲", false, -1),
                Targeted("strong", "地动山摇：单体", true), Combat("strong_all", "地动山摇：全体", true, -1) },
            "magic_007" => new[] { Combat("base", "拆毁：忽略地点城防", false),
                Targeted("strong", "解离：消灭所选敌人", true, Phase.Melee) },
            "magic_013" => new[] { Targeted("base", "冰冻：停攻及去火抗", false), Targeted("strong", "致命冰冻：停攻及减甲", true) },
            "magic_019" => new[] { Targeted("base", "暴露：所选敌人", false, Phase.Ranged, Phase.Melee),
                new BattleCardChoice("strong_fortification", "群体：去全部城防", true, 0, Phase.Ranged, Phase.Melee),
                new BattleCardChoice("strong_resistance", "群体：去全部抗性", true, 1, Phase.Ranged, Phase.Melee) },
            "magic_022" => new[] { Targeted("base", "旋风：所选敌人停攻", false), Targeted("strong", "飓风：消灭所选敌人", true, Phase.Melee) },
            "advanced_card_043" => new[] { Any("base", "基础影响力", false),
                Any("strong_blue", "蓝费强化影响力", true) with { ActionMana = ManaColor.Blue },
                Any("strong_white", "白费强化影响力", true) with { ActionMana = ManaColor.White },
                new BattleCardChoice("enemy", "去所选攻击的进攻能力", false, 1, Phase.Ranged, Phase.Block) { Target = BattleCardTarget.EnemyAttack },
                new BattleCardChoice("strong_enemy_blue", "蓝费：所选敌人停攻", true, 1, Phase.Ranged, Phase.Block) { Target = BattleCardTarget.EnemyAttack, ActionMana = ManaColor.Blue },
                new BattleCardChoice("strong_enemy_white", "白费：所选敌人停攻", true, 1, Phase.Ranged, Phase.Block) { Target = BattleCardTarget.EnemyAttack, ActionMana = ManaColor.White } },
            "basic_card_010" => new[] { Outside("move", "基础移动", false), Outside("strong_move", "强化移动", true),
                new BattleCardChoice("ranged", "基础远程攻击", false, 1, Phase.Ranged, Phase.Melee),
                new BattleCardChoice("strong_ranged", "强化远程攻击", true, 1, Phase.Ranged, Phase.Melee),
                new BattleCardChoice("reduce", "降低一次攻击", false, 2, Phase.Block),
                new BattleCardChoice("strong_reduce", "强化降低一次攻击", true, 2, Phase.Block) },
            "advanced_card_036" => new[] { new BattleCardChoice("base", "一次减攻与无伤奖励", false, 0, Phase.Block),
                new BattleCardChoice("strong", "强化：一次减攻", true, 0, Phase.Block),
                new BattleCardChoice("strong_split", "强化：两次减攻", true, 1, Phase.Block) },
            "basic_card_000" or "basic_card_014" or "basic_card_017" => TravelChoices(),
            "basic_card_018" => new[]{Outside("base","移动并降低所选格费用",false),Outside("strong","移动并降低所选地形费用",true)},
            "advanced_card_009" or "advanced_card_010" or "advanced_card_011" or "advanced_card_017" or "advanced_card_039" => TravelChoices(),
            "basic_card_001" => new[] { Outside("heal","基础治疗",false,0),Outside("draw","基础抽牌",false,1),Outside("strong_heal","强化治疗",true,0),Outside("strong_draw","强化抽牌",true,1) },
            "basic_card_004" => new[] { Outside("heal","基础治疗",false,0),Outside("strong_heal","强化治疗",true,0),
                Outside("move","基础移动",false,2),Outside("strong_move","强化移动",true,2),
                new BattleCardChoice("block","基础格挡",false,1,Phase.Block),new BattleCardChoice("strong_block","强化地形格挡",true,1,Phase.Block) },
            "basic_card_005" => new[]{false,true}.SelectMany(strong=>new[]{"治疗","抽牌","绿色资源","重整部队"}.Select((label,option)=>Outside((strong?"strong_":"base_")+option,(strong?"强化":"基础")+label,strong,option))).ToArray(),
            "advanced_card_007" => new[] { Outside("base","移动与治疗",false),Outside("strong","强化移动与治疗",true) },
            "advanced_card_015" => new[] { Outside("base","治疗并可重整部队",false),Outside("strong","强化治疗并可重整部队",true) },
            "magic_000" => new[] { Outside("base","能量流转：重整",false),Outside("strong","能量窃取：治疗与重整",true) },
            "magic_001" => new[] { Outside("base","痊愈：治疗及本回合奖励",false),new BattleCardChoice("strong","感染：格挡目标减甲",true,0,Phase.Ranged,Phase.Block) },
            "magic_005" => new[] { Outside("base","复原：按地形治疗",false),Outside("strong","重生：治疗与部队重整",true) },
            "basic_card_016" => new[] { BasicAttack(), BasicBlock(), new BattleCardChoice("strong", "强化格挡", true, 0, Phase.Block) },
            "basic_card_021" => new[] { BasicAttack(), BasicBlock(), new BattleCardChoice("strong", "强化攻击", true, 0, Phase.Melee) },
            "basic_card_007" => new[] { Outside("base","基础移动",false) with {ProducesMovement=true},new BattleCardChoice("strong", "强化远程攻击", true, 0, Phase.Ranged, Phase.Melee) },
            "basic_card_011" => new[] { new BattleCardChoice("base", "基础远程攻击", false, 1, Phase.Ranged, Phase.Melee),
                Outside("move","基础移动",false),
                new BattleCardChoice("strong", "强化远程攻击", true, 0, Phase.Ranged, Phase.Melee) },
            "basic_card_002" => ManaChoices(false,false).Concat(new[]{Any("strong","另一行动牌免费强化 · 数值+2",true)}).ToArray(),
            "basic_card_006" => ManaChoices(false,true).Concat(new[]{Any("strong","另一行动牌免费强化 · 数值+3",true)}).ToArray(),
            "basic_card_008" or "basic_card_022" => new[] { Any("base","基础影响力",false),Any("strong","强化影响力",true) },
            "basic_card_012" or "advanced_card_014" => new[] { Any("base","影响力与本回合招募奖励",false),Any("strong","强化影响力与招募奖励",true) },
            "basic_card_026" => new[] { Any("base","影响力与一次招募折扣",false),Any("strong","强化影响力与声誉代价",true) },
            "advanced_card_019" => new[] { Any("base","按实际伤牌求援",false),Any("strong","强化求援",true) },
            "advanced_card_008" => new[] { Any("base","胁迫影响力",false),Any("strong","强化胁迫影响力",true),
                new BattleCardChoice("attack","胁迫攻击",false,1,Phase.Melee),new BattleCardChoice("strong_attack","强化胁迫攻击",true,1,Phase.Melee) },
            "basic_card_015" or "basic_card_019" => CrystalChoices(),
            "basic_card_020" => new[] { BasicAttack(),new BattleCardChoice("block","基础寒冰格挡",false,1,Phase.Block),new BattleCardChoice("strong","强化寒冰格挡",true,0,Phase.Block) },
            "basic_card_024" => new[] { BasicAttack(),BasicBlock(),new BattleCardChoice("base_ranged","基础远程攻击",false,2,Phase.Ranged,Phase.Melee),
                new BattleCardChoice("strong_attack","强化普通攻击",true,0,Phase.Melee),new BattleCardChoice("strong_block","强化普通格挡",true,1,Phase.Block),
                new BattleCardChoice("strong_fire","强化火焰攻击",true,2,Phase.Melee),new BattleCardChoice("strong_fire_block","强化火焰格挡",true,3,Phase.Block),
                new BattleCardChoice("strong_ranged","强化远程攻击",true,4,Phase.Ranged,Phase.Melee),new BattleCardChoice("strong_siege","强化攻城攻击",true,5,Phase.Ranged,Phase.Melee) },
            "basic_card_025" => new[] { new BattleCardChoice("base","基础攻击与本场受伤奖励",false,0,Phase.Melee),new BattleCardChoice("strong","强化攻击与本场受伤奖励",true,0,Phase.Melee) },
            "basic_card_027" => new[] { new BattleCardChoice("attack","基础攻击",false,2,Phase.Melee),new BattleCardChoice("block","基础格挡",false,3,Phase.Block),
                Outside("move","基础移动",false),Outside("strong_move","强化移动",true),
                new BattleCardChoice("strong_attack","强化攻击",true,2,Phase.Melee),new BattleCardChoice("strong_block","强化格挡",true,3,Phase.Block),
                Any("base_influence","基础影响力",false,1),Any("strong_influence","强化影响力",true,1) },
            "advanced_card_000" or "advanced_card_001" or "advanced_card_002" or "advanced_card_003" => new[] {
                Any("base","取得印刷颜色魔晶",false),new BattleCardChoice("strong",id=="advanced_card_003" ? "强化攻城攻击" : "强化原元素远程攻击",true,0,Phase.Ranged,Phase.Melee) },
            "magic_009" or "magic_015" => new[] {new BattleCardChoice("base","上半：原元素远程攻击",false,0,Phase.Ranged,Phase.Melee),
                new BattleCardChoice("strong","下半：原元素攻城并受伤",true,0,Phase.Ranged,Phase.Melee)},
            "magic_010" => new[] {new BattleCardChoice("base_attack","上半：火焰攻击",false,0,Phase.Melee),new BattleCardChoice("base_block","上半：火焰格挡",false,1,Phase.Block),
                new BattleCardChoice("strong_attack","下半：火焰攻击与敌群加成",true,0,Phase.Melee),new BattleCardChoice("strong_block","下半：火焰格挡与敌群加成",true,1,Phase.Block)},
            "magic_017" => BoltChoices(),
            _ => Array.Empty<BattleCardChoice>()
        };
        private static BattleCardChoice Combat(string id, string label, bool enhanced, int option = 0)
            => new(id, label, enhanced, option, Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee);
        private static BattleCardChoice Targeted(string id, string label, bool enhanced, params Phase[] phases)
            => new(id, label, enhanced, 0, phases.Length == 0
                ? new[] { Phase.Ranged, Phase.Block, Phase.AssignDamage, Phase.Melee } : phases) { Target = BattleCardTarget.EnemyIndex };
        private static BattleCardChoice Any(string id,string label,bool enhanced,int option=0)
            => new(id,label,enhanced,option,Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End);
        private static BattleCardChoice Outside(string id,string label,bool enhanced,int option=0)
            => new(id,label,enhanced,option,Phase.End);
        private static BattleCardChoice[] TravelChoices()=>new[]{Outside("base","基础移动效果",false) with {ProducesMovement=true},Outside("strong","强化移动效果",true) with {ProducesMovement=true}};
        private static BattleCardChoice[] ManaChoices(bool enhanced,bool green)
            => new[] {Any("blue","蓝色魔力",enhanced,0),Any("white","白色魔力",enhanced,1),Any("red","红色魔力",enhanced,2)}
                .Concat(green ? new[]{Any("green_crystal","绿色魔晶",enhanced,3)} : Array.Empty<BattleCardChoice>()).ToArray();
        private static BattleCardChoice[] CrystalChoices()
            => new[] {ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.SelectMany(color=>new[] {
                Any("base_"+color,"基础："+Color(color)+"转晶",false,(int)color),Any("strong_"+color,"强化："+Color(color)+"魔晶",true,(int)color)}).ToArray();
        private static BattleCardChoice[] BoltChoices()
            => new[] {ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.SelectMany(color=>new[]{false,true}.Select(enhanced=>
                new BattleCardChoice((enhanced?"strong_":"base_")+color,(enhanced?"下半 · ":"上半 · ")+Color(color)+"额外费用",enhanced,(int)color,
                    color==ManaColor.Green || color==ManaColor.White ? new[]{Phase.Ranged,Phase.Melee} : new[]{Phase.Melee}))).ToArray();
        private static string Color(ManaColor color) => color switch {ManaColor.Red=>"红色",ManaColor.Blue=>"蓝色",ManaColor.Green=>"绿色",_=>"白色"};
        private static BattleCardChoice BasicAttack() => new("attack", "基础攻击", false, 0, Phase.Melee);
        private static BattleCardChoice BasicBlock() => new("block", "基础格挡", false, 1, Phase.Block);
    }
}
