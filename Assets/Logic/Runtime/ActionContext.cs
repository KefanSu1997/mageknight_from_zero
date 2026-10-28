using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 行動牌效果運行時的臨時數據池。僅涵蓋移動與影響力等簡化數值。
    /// </summary>
    public sealed partial class ActionContext
    {
        public object CurrentSourceCard { get; internal set; }
        public DuelBattleState Duel { get; } = new();
        public EnemyDefenseState EnemyDefense { get; } = new();
        public SummoningState Summoning { get; } = new();
        public VampirismState Vampirism { get; } = new();
        public EnemyRewardMode EnemyRewardMode { get; set; } = EnemyRewardMode.PrintedFameOnly;
        public EnemyRewardState EnemyRewards { get; } = new();
        public NatureRevengeBattleState NatureRevenge { get; } = new();
        public SharedSkillArea SharedSkills { get; internal set; }
        internal ManaSkillPayment ManaSkillPayment { get; set; }
        internal bool PayingStrongSpell { get; set; }

        /// <summary>當前累計的移動力。</summary>
        public int MovementPool { get; set; }

        /// <summary>當前累計的影響力。</summary>
        public int InfluencePool { get; set; }
        public HealingPowerPool Healing { get; } = new();
        public int HealedHandWoundsThisTurn { get; set; }
        public HashSet<UnitState> HealedUnitsThisTurn { get; } = new();
        public bool ProtectionBannerBurned { get; internal set; }
        public bool RemoveNewWoundsAtTurnEnd { get; internal set; }

        /// <summary>當前累計的格擋值。</summary>
        public int BlockPool { get => CombatPower.BlockTotal; set => CombatPower.SetBlockTotal(value); }

        public CombatPowerPool CombatPower { get; } = new();
        public bool SoulGuideBlockPending { get; set; }
        public HashSet<UnitState> DamageAssignedUnits { get; } = new();
        public int SiegePool { get => CombatPower.Total(AttackType.Siege); set => CombatPower.SetTotal(AttackType.Siege, value); }
        public bool FortifiedSite { get; set; }
        public int HarvestTriggers { get; set; }
        /// <summary>鲜血仪式本次强化的三个独立选色；空列表兼容三枚同色。</summary>
        public List<ManaColor> BloodRitualColors { get; } = new();
        /// <summary>鲜血仪式本次可选转晶颜色，null 表示放弃。</summary>
        public ManaColor? BloodRitualCrystalColor { get; set; }

        /// <summary>當前累計的遠程攻擊力。</summary>
        public int RangedPool { get => CombatPower.Total(AttackType.Ranged); set => CombatPower.SetTotal(AttackType.Ranged, value); }

        /// <summary>橫置打出創傷卡時的數值，0 表示不可橫置。</summary>
        public int SidewaysWoundValue { get; set; }

        /// <summary>下一張行動牌是否免費獲得強效。</summary>
        public bool NextCardEnhanced { get; set; }

        /// <summary>強效額外獲得的數值加成。</summary>
        public int BoostValue { get; set; }

        /// <summary>Explicit per-use selections for Maximal Effect; null repeats the ordinary option.</summary>
        public List<RepeatedCardSelection> OverloadSelections { get; set; }
        public bool OverloadStepwise { get; set; }
        public RepeatedEffectResolution PendingRepeatedEffect { get; internal set; }
        public HeadbandBorrowResolution PendingHeadbandBorrow { get; internal set; }

        /// <summary>本回合可額外使用的魔力骰數量。</summary>
        public int ExtraManaDice { get; set; }
        public int ExtraBlackManaDice { get; set; }
        public List<ManaDie> ManaDiceToTransform { get; } = new();

        /// <summary>某些效果需要指定的目標部隊。</summary>
        public UnitState? TargetUnit { get; set; }
        /// <summary>Explicit readying targets; null uses the legacy single target, empty chooses none.</summary>
        public List<UnitState>? ReadyUnitTargets { get; set; }

        /// <summary>當前所在地形，用於某些牌的計算。</summary>
        public TerrainType CurrentTerrain { get; set; } = TerrainType.Plains;

        /// <summary>目前的日夜狀態。</summary>
        public DayPart DayPart { get; set; } = DayPart.Day;

        /// <summary>一次性降低敵人攻擊力的數值。</summary>
        public int AttackReduction { get; set; }
        /// <summary>已选敌人下一次实际攻击的减值，结算该攻击后消费；不修改原敌人定义。</summary>
        public Dictionary<(int Target, int Attack), int> PendingAttackReductions { get; } = new();

        public void ClearAttackReductions()
        {
            AttackReduction = 0;
            AttackReductionTimes = 0;
            PendingAttackReductions.Clear();
            AttackIfNoWound = 0;
            _skillAttackAdjustments.Clear();
            _insightAbilities.Clear();
            _insightElements.Clear();
            IgnoreFireIceDamage = IgnoreOtherDamage = IgnorePhysicalDamage = IgnoreNonPhysicalDamage = 0;
            HardenedEnemy = null;
        }

        internal void ResolveNoWoundAttack()
        {
            if (!InBattle || CombatPhase != Core.Phase.Melee || AttackIfNoWound <= 0) return;
            if (BattleWoundsReceived == 0) MeleePool += AttackIfNoWound;
            AttackIfNoWound = 0;
        }

        /// <summary>近战攻击总值；元素与方式保存在 CombatPower，供战斗结算消费。</summary>
        public int MeleePool { get => CombatPower.Total(AttackType.Melee); set => CombatPower.SetTotal(AttackType.Melee, value); }

        /// <summary>下一張移動牌獲得的額外移動力。</summary>
        public int MoveBonusNext { get; set; }

        /// <summary>本回合其餘所有移動牌的額外移動力。</summary>
        public int MoveBonusAll { get; set; }

        /// <summary>使用 Mana Pull 時黑色骰子是否可當任意顏色。</summary>
        public bool BlackManaWild { get; set; }

        /// <summary>若遠程階段擊殺敵人，額外獲得的名望。</summary>
        public int PendingFameOnRangedKill { get; set; }

        /// <summary>是否處於交涉狀態。</summary>
        public bool InNegotiation { get; set; }
        public bool IgnoreReputationTrackEffects { get; internal set; }
        public bool InfluenceRecruitmentOccurred { get; internal set; }
        public bool ForbidInfluenceRecruitment { get; internal set; }
        public int? InteractionReputationModifier { get; internal set; }
        public int InteractionAppliedModifier { get; internal set; }
        public bool InteractionReversed { get; internal set; }
        public bool InteractionDoubled { get; internal set; }
        public RecruitLocation InteractionLocation { get; internal set; }
        public bool InteractionClosed { get; internal set; }
        public PlaceState InteractionSite { get; internal set; }
        public Map.AxialCoord? InteractionPosition { get; internal set; }
        public int InteractionCityInfluence { get; internal set; }
        public bool FamiliarRecruitmentAtGlade { get; internal set; }

        public void ClearInteraction()
        {
            InNegotiation = InteractionReversed = InteractionDoubled = InteractionClosed = false;
            FamiliarRecruitmentAtGlade = false;
            InteractionReputationModifier = null;
            InteractionAppliedModifier = 0;
            InteractionLocation = RecruitLocation.None;
            InteractionSite = null;
            InteractionPosition = null;
            InteractionCityInfluence = 0;
        }

        /// <summary>隨機應變等效果需棄掉的卡牌。</summary>
        public DeedCard? CardToDiscard { get; set; }

        // Return permissions belong to the actual Joyful Mana instances played this turn.
        internal List<(DeedCard Card, bool Enhanced)> JoyfulManaPlayed { get; } = new();
        internal DeedCard? JoyfulManaReturnCard { get; set; }
        internal DeedCard? JoyfulManaReturnPayment { get; set; }

        /// <summary>招募折扣值，供無情威壓使用。</summary>
        public int RecruitDiscount { get; set; }

        /// <summary>魅惑只降低一支部队费用；成功招募时消费，失败保留，回合结束过期。</summary>
        public int CharmRecruitDiscount { get; set; }

        /// <summary>以無情威壓招募時應扣除聲望。</summary>
        public bool IntimidateUsed { get; set; }
        public bool UseIntimidateRecruitDiscount { get; set; } = true;

        /// <summary>受到創傷時每張傷牌額外獲得的攻擊力。</summary>
        public int AttackBonusPerWound { get; set; }

        /// <summary>攻擊加成上限。</summary>
        public int AttackBonusLimit { get; set; }
        private int _battleRageAwarded;

        /// <summary>本场已收到的伤牌也计入原战斗之怒，并且每份加成只发放一次。</summary>
        internal void ApplyBattleRageReward()
        {
            if (!InBattle || AttackBonusPerWound <= 0) return;
            int earned = System.Math.Min(BattleWoundsReceived * AttackBonusPerWound, AttackBonusLimit);
            if (earned <= _battleRageAwarded) return;
            MeleePool += earned - _battleRageAwarded;
            _battleRageAwarded = earned;
        }

        internal void ClearBattleRage()
        {
            AttackBonusPerWound = AttackBonusLimit = _battleRageAwarded = 0;
        }

        /// <summary>特定敵人的護甲減值表，鍵為敵人在列表中的索引。</summary>
        public Dictionary<int, int> ArmorReduction { get; } = new();
        private readonly List<(Monster Enemy, int Amount)> _unitArmorReductions = new();
        public int UnitArmorReduction(Monster enemy) => _unitArmorReductions
            .Where(r => ReferenceEquals(r.Enemy, enemy)).Sum(r => r.Amount);
        internal void AddUnitArmorReduction(Monster enemy, int amount) => _unitArmorReductions.Add((enemy, amount));
        internal void ClearUnitArmorReduction() => _unitArmorReductions.Clear();
        private readonly List<(Monster Enemy, int Amount)> _skillArmorReductions = new();
        public int SkillArmorReduction(Monster enemy) => _skillArmorReductions
            .Where(entry => ReferenceEquals(entry.Enemy, enemy)).Sum(entry => entry.Amount) + TauntArmorReduction(enemy);
        internal void AddSkillArmorReduction(Monster enemy, int amount) => _skillArmorReductions.Add((enemy, amount));
        internal void ClearSkillArmorReduction() { _skillArmorReductions.Clear(); _tauntArmorReductions.Clear(); }
        private readonly List<(Monster Enemy, int Attack)> _tauntArmorReductions = new();
        internal HashSet<int> OriginalAttackSkips { get; set; }
        internal void AddTauntArmorReduction(Monster enemy, int attack) => _tauntArmorReductions.Add((enemy, attack));
        private int TauntArmorReduction(Monster enemy)
        {
            int index = Enemies == null ? -1 : Enumerable.Range(0, Enemies.Count)
                .Where(i => ReferenceEquals(Enemies[i], enemy)).DefaultIfEmpty(-1).First();
            if (index < 0 || ColdStareSkips(enemy) || (OriginalAttackSkips ?? SkipAttackIndices).Contains(index)) return 0;
            return _tauntArmorReductions.Count(t => ReferenceEquals(t.Enemy, enemy)
                && !CancelledAttacks.Contains((index, t.Attack))) * 2;
        }

        /// <summary>本回合無法攻擊的敵人索引。</summary>
        public HashSet<int> SkipAttackIndices { get; } = new();
        public HashSet<(int Target, int Attack)> CancelledAttacks { get; } = new();
        public HashSet<(int Target, int Attack)> SuccessfullyBlockedAttacks { get; } = new();
        internal Dictionary<UnitState, HashSet<int>> MastersUsedAbilities { get; } = new();
        internal HashSet<UnitState> MastersActiveThisBattle { get; } = new();
        internal List<Monster> MastersDestroyWhenBlocked { get; } = new();
        internal List<(UnitState Source, int Amount)> OtherUnitAttackBonuses { get; } = new();
        public int OtherUnitAttackBonus(UnitState unit) => unit == null ? 0 : OtherUnitAttackBonuses
            .Where(b => !ReferenceEquals(b.Source, unit)).Sum(b => b.Amount);
        internal List<(Monster Enemy, int Attack, UnitState Unit)> ForcedDamageUnits { get; } = new();
        public UnitState ForcedDamageUnit(Monster enemy, int attack) => ForcedDamageUnits
            .Where(b => ReferenceEquals(b.Enemy, enemy) && b.Attack == attack).Select(b => b.Unit).FirstOrDefault();
        public bool IsAttackCancelled(int target, int attack) => SkipAttackIndices.Contains(target) || CancelledAttacks.Contains((target, attack));
        public void CancelEnemyAttack(int target, int attack)
        {
            CancelledAttacks.Add((target, attack));
            // A single-attack enemy (or one with every attack cancelled) has no attacks left.
            if (Enumerable.Range(0, Enemies[target].AttackCount).All(a => CancelledAttacks.Contains((target, a))))
                SkipAttackIndices.Add(target);
        }
        public HashSet<int> PossessedEnemyIndices { get; } = new();
        /// <summary>本场已实际击败的敌人；与攻击、直接消灭及奖励结算共用，防止重复击杀。</summary>
        public HashSet<int> DefeatedEnemyIndices { get; } = new();

        /// <summary>被移除火焰抗性的敵人索引。</summary>
        public HashSet<int> FireResistRemoved { get; } = new();
        /// <summary>暴露：所列敌人仅在本场战斗失去全部城防／抗性；奥术免疫不是抗性。</summary>
        public HashSet<int> FortificationsRemoved { get; } = new();
        public HashSet<int> ResistancesRemoved { get; } = new();

        /// <summary>敌人列表压缩时，仅把存活敌人的修正转到其新位置；已击败者的修正不得传给下一敌人。</summary>
        public void RemapEnemyModifiers(IReadOnlyDictionary<int, int> survivingIndices)
        {
            Remap(ArmorReduction, survivingIndices);
            Remap(EnemyAttackAdjust, survivingIndices);
            Remap(_killCrystalChoices, survivingIndices);
            var remainingAttacks = PendingAttackReductions.Where(p => survivingIndices.ContainsKey(p.Key.Target))
                .Select(p => (Key: (survivingIndices[p.Key.Target], p.Key.Attack), p.Value)).ToArray();
            PendingAttackReductions.Clear();
            foreach (var pair in remainingAttacks) PendingAttackReductions.Add(pair.Key, pair.Value);
            Remap(IgnoredAbilities, survivingIndices);
            Remap(AttackElementChange, survivingIndices);
            foreach (var attacks in new[] { CancelledAttacks, SuccessfullyBlockedAttacks })
            {
                var remaining = attacks.Where(a => survivingIndices.ContainsKey(a.Target))
                    .Select(a => (survivingIndices[a.Target], a.Attack)).ToArray();
                attacks.Clear(); attacks.UnionWith(remaining);
            }
            foreach (var indices in new[] { SkipAttackIndices, PossessedEnemyIndices, FireResistRemoved, NightWingsSkippedIndices, DefeatedEnemyIndices, FortificationsRemoved, ResistancesRemoved })
            {
                var remaining = indices.Where(survivingIndices.ContainsKey).Select(i => survivingIndices[i]).ToArray();
                indices.Clear(); indices.UnionWith(remaining);
            }
            var spellTargets = SpellTargetIndices.Where(survivingIndices.ContainsKey).Select(i => survivingIndices[i]).ToArray();
            SpellTargetIndices.Clear(); SpellTargetIndices.AddRange(spellTargets);
            if (KillEnemyIndex.HasValue)
                KillEnemyIndex = survivingIndices.TryGetValue(KillEnemyIndex.Value, out int target) ? target : null;
        }

        private static void Remap<T>(Dictionary<int, T> values, IReadOnlyDictionary<int, int> survivingIndices)
        {
            // Negative keys are existing player-wide sentinels, not enemy positions.
            var remaining = values.Where(p => p.Key < 0 || survivingIndices.ContainsKey(p.Key))
                .Select(p => new KeyValuePair<int, T>(p.Key < 0 ? p.Key : survivingIndices[p.Key], p.Value)).ToArray();
            values.Clear();
            foreach (var pair in remaining) values.Add(pair.Key, pair.Value);
        }

        /// <summary>移動力兌換近戰攻擊所需點數，0 表示不可兌換。</summary>
        public int MoveCostAttack { get; set; }

        /// <summary>移動力兌換遠程攻擊所需點數，0 表示不可兌換。</summary>
        public int MoveCostRanged { get; set; }

        /// <summary>是否處於戰鬥中，供部分效果判斷。</summary>
        private bool _inBattle;
        public bool InBattle
        {
            get => _inBattle;
            set
            {
                if (value && !_inBattle) Healing.Clear();
                _inBattle = value;
            }
        }

        /// <summary>地形移動費用的臨時覆寫表。</summary>
        public Dictionary<TerrainType, int> TerrainCostOverride { get; } = new();
        public HashSet<TerrainType> MoveForbidden { get; } = new();
        public Map.MapState MovementMap { get; set; }
        public Map.AxialCoord? TargetHex { get; set; }
        public TerrainType? TargetTerrain { get; set; }
        public Dictionary<Map.AxialCoord, int> HexMoveReduction { get; } = new();
        public Dictionary<TerrainType, int> TerrainMoveReduction { get; } = new();
        /// <summary>原部队允许降至0的地形减费；独立于最低2点的减费，均在回合末清理。</summary>
        public Dictionary<TerrainType, int> TerrainMoveReductionToZero { get; } = new();
        public Dictionary<TerrainType, int> TerrainMoveReductionToOne { get; } = new();

        /// <summary>穿越湖泊時是否需要支付一點藍色魔力。</summary>
        public bool RequireBlueForLake { get; set; }

        /// <summary>每招募一支部隊額外獲得的名望。</summary>
        public int FamePerRecruit { get; set; }

        /// <summary>每招募一支部隊額外獲得的聲望。</summary>
        public int ReputationPerRecruit { get; set; }

        /// <summary>禁止將傷害分配給部隊。</summary>
        public bool NoUnitDamage { get; set; }
        public bool UnitsResistAll { get; set; }

        /// <summary>部隊攻擊與格擋的臨時加值。</summary>
        public int UnitAttackBonus { get; set; }
        public int UnitBlockBonus { get; set; }
        /// <summary>冲锋陷阵只增强本场战斗实际发动的部队攻防；不混入旗帜/技能的其他作用域。</summary>
        public int ChargeUnitBonus { get; internal set; }
        public bool CombatActionStarted { get; internal set; }

        /// <summary>部隊護甲的臨時加值。</summary>
        public int UnitArmorBonus { get; set; }

        /// <summary>每次部隊攻擊或格擋時額外獲得的名望。</summary>
        public int FamePerUnitAction { get; set; }
        internal List<List<object>> HonorBannersThisTurn { get; } = new();
        internal List<DeedCard> CommandBannersPlayedThisTurn { get; } = new();
        internal HashSet<UnitState> RecruitedUnitsThisTurn { get; } = new();
        public DeedCard? CommandBannerForRecruit { get; internal set; }
        public int HonorBannerTurnBonus => HonorBannersThisTurn.Count;

        /// <summary>執行穩步前進時應回收的卡牌。</summary>
        public DeedCard? CardToRecycle { get; set; }
        public List<(DeedCard card, bool enhanced)> SteadyTempoPlayed { get; } = new();
        public HashSet<DeedCard> SteadyTempoReturns { get; } = new();

        /// <summary>回合結束放置到牌庫頂。</summary>
        public bool RecycleToTop { get; set; }

        /// <summary>回合結束放置到牌庫底。</summary>
        public bool RecycleToBottom { get; set; }

        /// <summary>本回合影響力可作為格擋使用。</summary>
        public bool InfluenceAsBlock { get; set; }

        /// <summary>影響力作為格擋時的元素類型。</summary>
        public Element InfluenceBlockElement { get; set; } = Element.Physical;

        /// <summary>是否在回合結束返還已消耗的晶體。</summary>
        public bool ReturnSpentCrystals { get; set; }

        /// <summary>當前回合中消耗的晶體紀錄。</summary>
        public Dictionary<ManaColor, int> SpentCrystals { get; } = new();

        /// <summary>伏擊提供的首次攻擊與格擋加值。</summary>
        public int AmbushAttackBonus { get; set; }
        public int AmbushBlockBonus { get; set; }
        public bool AmbushTriggered { get; set; }

        /// <summary>需移除的卡牌。</summary>
        public DeedCard? CardToRemove { get; set; }

        /// <summary>魔力風暴允許金色骰子通用。</summary>
        public bool GoldManaWild { get; set; }

        /// <summary>黑夜中允許使用金色法力。</summary>
        public bool AllowGoldAtNight { get; set; }

        /// <summary>白晝中允許使用黑色法力。</summary>
        public bool AllowBlackAtDay { get; set; }

        /// <summary>本回合皓日护符允许按白昼揭示已探索地图的地点标记。</summary>
        public bool RevealSitesAsDay { get; set; }

        /// <summary>執行需要公共魔力源的效果時提供引用。</summary>
        public ManaSource? ManaSource { get; set; }

        /// <summary>
        /// 本回合公共魔力源是否被封鎖，部分技能如「魔源冰封」會設置此狀態。
        /// 目前僅供測試驗證，實際阻止其他玩家取用骰子的邏輯尚未實作。
        /// </summary>
        public bool ManaSourceFrozen { get; set; }

        /// <summary>本回合擁有無限使用的法力顏色列表。</summary>
        public HashSet<ManaColor> InfiniteMana { get; } = new();

        /// <summary>施放指定顏色法術時額外獲得的名望。</summary>
        public Dictionary<ManaColor, int> FamePerSpell { get; } = new();

        /// <summary>近戰階段物理攻擊是否翻倍。</summary>
        public bool DoublePhysicalAttack { get; set; }

        /// <summary>遠程攻擊翻倍。</summary>
        public bool DoubleRangedAttack { get; set; }

        /// <summary>遠程攻擊視為攻城。</summary>
        public bool RangedAttackAsSiege { get; set; }
        public bool AllAttacksColdFire { get; set; }
        public bool AllAttacksSiege { get; set; }

        /// <summary>攻城攻擊翻倍。</summary>
        public bool DoubleSiegeAttack { get; set; }

        /// <summary>攻城攻擊視為遠程。</summary>
        public bool SiegeAttackAsRanged { get; set; }

        /// <summary>可於本回合從法術市場直接施放的法術顏色列表。</summary>
        public List<ManaColor> SpellColors { get; } = new();
        public List<ManaColor> ManaMeltdownChoices { get; } = new();
        public PlayerState? ManaMeltdownOwner { get; set; }

        /// <summary>本回合是否已執行學習獲取高級行動牌。</summary>
        public bool LearnUsed { get; set; }
        /// <summary>本回合实际打出的学习效果：1为基础、2为强化；不可凭购牌命令免费升级。</summary>
        public int LearningModes { get; internal set; }

        /// <summary>提供對高級行動牌市場的引用。</summary>
        public AdvActionSupply? AdvActionSupply { get; set; }

        /// <summary>提供對法術牌市場的引用。</summary>
        public SpellSupply? SpellSupply { get; set; }

        /// <summary>提供對技能牌市場的引用。</summary>
        public SkillSupply? SkillSupply { get; set; }
        /// <summary>启迪头环借用的原技能选项；神器option仍表示实际供给索引。</summary>
        public int HeadbandSkillOption { get; set; }
        /// <summary>宝盒揭示后可选择不用，回合末仍须归还实际供应神器。</summary>
        public CardEffects.MysticBoxEffect.PendingUse? MysticBox { get; internal set; }

        /// <summary>提供對神器牌堆的引用。</summary>
        public ArtifactSupply? ArtifactSupply { get; set; }

        /// <summary>可直接施放強效法術的顏色。</summary>
        public List<ManaColor> SpellStrongColors { get; } = new();

        /// <summary>每成功格擋一名敵人可獲得的攻擊加值。</summary>
        public int AttackBonusPerBlock { get; set; }

        /// <summary>對抗迅捷時額外計算的格擋值。</summary>
        public int SwiftBlockBonus => CombatPower.Blocks.Where(b => b.DoubleAgainstSwift).Sum(b => b.Value);

        /// <summary>傳送可移動的最大距離。</summary>
        public int TeleportRange { get; set; }

        /// <summary>时光传送的一次可选移动，结束回合时失效。</summary>
        public bool TemporalPortalPending { get; set; }
        /// <summary>传送是本回合行动；移动完成后仍禁止追加战斗或交涉。</summary>
        public bool TemporalPortalTurn { get; set; }
        public bool TurnActionCommitted { get; set; }
        public bool MovedThisTurn { get; set; }
        public bool RestedThisTurn { get; set; }
        public List<Map.AxialCoord> TravelPath { get; } = new();
        public List<int> SpellTargetIndices { get; } = new();
        public HashSet<int> NightWingsSkippedIndices { get; } = new();

        /// <summary>下一次抽牌時額外增加的手牌上限。</summary>
        public int NextDrawBonus { get; set; }

        /// <summary>每擊敗一名敵人額外獲得的聲望。</summary>
        public int PendingReputationOnKill { get; set; }

        /// <summary>每擊敗一名敵人額外獲得的名望。</summary>
        public int PendingFameOnKill { get; set; }

        public Phase CombatPhase { get; set; } = Phase.Ranged;
        public int SwordFamePerKill { get; set; }
        public int BowFamePerKill { get; set; }
        public bool StarBowActive { get; set; }
        public bool IgnorePhysicalResist { get; set; }
        public int TomeSpellOption { get; set; }
        public int BloodlineEffectOption { get; set; }
        public bool BloodlineWoundToDiscard { get; set; }
        internal HashSet<int> BloodlineBorrowing { get; } = new();

        /// <summary>本阶段每击败一名敌人获得的魔晶数；单笔攻击奖励保存在贡献中。</summary>
        public int CrystalsPerKill { get; set; }

        /// <summary>根據敵人決定獲得魔晶顏色的函式。</summary>
        public System.Func<Monster, ManaColor>? CrystalOnKill { get; set; }

        private readonly Dictionary<int, ManaColor> _killCrystalChoices = new();
        public IReadOnlyDictionary<int, ManaColor> KillCrystalChoices => _killCrystalChoices;
        public bool CanChooseKillCrystal => InBattle && (CrystalsPerKill > 0 || CombatPower.Attacks.Any(a => a.CrystalsPerDefeatedEnemy > 0));

        public void ChooseKillCrystal(int target, ManaColor color)
        {
            if (!CanChooseKillCrystal || Enemies == null || target < 0 || target >= Enemies.Count || DefeatedEnemyIndices.Contains(target))
                throw new System.InvalidOperationException("必须选择当前奖励效果对应的存活敌人");
            if (!SoulHarvesterEffect.AvailableColors(Enemies[target]).Contains(color))
                throw new System.InvalidOperationException("该敌人的抗性不允许选择此色魔晶");
            _killCrystalChoices[target] = color;
        }

        public ManaColor KillCrystalColor(int target, Monster enemy)
            => _killCrystalChoices.TryGetValue(target, out var color) ? color : CrystalOnKill?.Invoke(enemy) ?? ManaColor.White;

        public void ClearKillCrystalChoices() => _killCrystalChoices.Clear();

        internal void GrantKillCrystals(PlayerState player, Monster enemy, int attackCrystals = 0, int target = -1)
        {
            int count = CrystalsPerKill + attackCrystals;
            if (count > 0 && CrystalOnKill != null)
                player.Mana.AddCrystal(KillCrystalColor(target, enemy), count);
        }

        /// <summary>影響力轉化治療所需點數，0 表示不可轉換。</summary>
        public int HealInfluenceRate { get; set; }
        public bool PeacefulMomentAvailable { get; set; }
        public bool PeacefulMomentReady { get; set; }
        public bool PeacefulMomentAction { get; set; }

        /// <summary>重整部隊時每級需支付的影響力，0 表示不可重整。</summary>
        public int ReadyInfluencePerLevel { get; set; }
        public int ReadyUnitMaxLevel { get; set; }

        /// <summary>Clear turn-scoped movement and unit conversions, including when drawing is skipped.</summary>
        public void ClearMovementAndReadyEffects()
        {
            NatureRevengeActions.ClearTurn(this);
            HexMoveReduction.Clear();
            TerrainMoveReduction.Clear();
            TerrainMoveReductionToZero.Clear();
            TerrainMoveReductionToOne.Clear();
            TerrainCostOverride.Clear();
            MoveForbidden.Clear();
            TargetHex = null;
            TargetTerrain = null;
            ReadyInfluencePerLevel = ReadyUnitMaxLevel = 0;
            HealInfluenceRate = 0;
            PeacefulMomentAvailable = PeacefulMomentReady = PeacefulMomentAction = false;
            MovementPool = InfluencePool = 0;
            MoveCostAttack = MoveCostRanged = 0;
            MoveBonusNext = MoveBonusAll = 0;
            HarvestTriggers = 0;
            MountainSafe = LakeSafe = false;
            RequireBlueForLake = false;
            InfluenceAsBlock = false;
            InfluenceBlockElement = Element.Physical;
        }

        /// <summary>若在攻擊階段前未加入創傷可獲得的攻擊力。</summary>
        public int AttackIfNoWound { get; set; }

        /// <summary>本回合山脈視為安全格。</summary>
        public bool MountainSafe { get; set; }
        /// <summary>山川逸闻待结算模式：1基础、2强化；回合末按实际地形消费一次。</summary>
        public int MountainLoreMode { get; set; }
        /// <summary>Chosen half of an original split-color action cost, for this play only.</summary>
        public ManaColor? SelectedActionManaColor { get; set; }
        /// <summary>Actual hand wound selected for this skill invocation; never inferred by card id.</summary>
        public DeedCard SelectedSkillWound { get; set; }
        /// <summary>Power of Crystals: independently select Move/Heal/Draw for each complete set.</summary>
        public List<int> ManaPowerBonusChoices { get; } = new();

        /// <summary>探索行動的移動力花費減少值。</summary>
        public int ExploreMoveDiscount { get; set; }

        /// <summary>本回合湖泊視為安全格。</summary>
        public bool LakeSafe { get; set; }

        /// <summary>下一次火焰或寒冰攻擊可忽略的傷害。</summary>
        public int IgnoreFireIceDamage { get; set; }

        /// <summary>下一次其他類型攻擊可忽略的傷害。</summary>
        public int IgnoreOtherDamage { get; set; }

        /// <summary>下一次物理攻擊可忽略的傷害。</summary>
        public int IgnorePhysicalDamage { get; set; }

        /// <summary>下一次非物理攻擊可忽略的傷害。</summary>
        public int IgnoreNonPhysicalDamage { get; set; }

        /// <summary>本回合忽略聲望條所有變化。</summary>
        public bool IgnoreReputationChange { get; set; }

        /// <summary>下張移動牌的數值若非零可改為攻擊或格擋：0 無、1 攻擊、2 格擋。</summary>
        public int TransformMoveType { get; set; }
        internal FixedOutputKind? ShapeshiftTarget { get; set; }

        /// <summary>每擊殺一名敵人時額外降低其他敵人護甲的數值。</summary>
        public int ArmorReducePerKill => CombatPower.Attacks.Sum(a => a.ArmorReductionsPerDefeatedEnemy);
        public int ExplosiveBoltArmorUses { get; internal set; }
        private readonly List<Monster> _explosiveBoltArmorTargets = new();
        public int ExplosiveBoltArmorReduction(Monster enemy)
            => _explosiveBoltArmorTargets.Count(target => ReferenceEquals(target, enemy));
        internal void AddExplosiveBoltArmorReduction(Monster enemy) => _explosiveBoltArmorTargets.Add(enemy);
        internal void ClearExplosiveBoltArmor()
        {
            ExplosiveBoltArmorUses = 0;
            _explosiveBoltArmorTargets.Clear();
        }

        /// <summary>降低敵人攻擊的次數。</summary>
        public int AttackReductionTimes { get; set; }

        /// <summary>未來創傷進手牌時可抽牌的次數。</summary>
        public int WoundDrawRemaining { get; set; }

        /// <summary>首次新伤实际进手后移除该实例并抽牌；不防止获得伤牌。</summary>
        public bool FirstWoundIgnored { get; set; }

        private PlayerDeck _bloodSurgeDeck;

        internal void BeginBloodSurge(PlayerState player, bool enhanced)
        {
            ClearBloodSurge();
            _bloodSurgeDeck = player.Deck;
            WoundDrawRemaining = 3;
            FirstWoundIgnored = enhanced;
            _bloodSurgeDeck.NewHandWound += OnBloodSurgeWound;
        }

        private void OnBloodSurgeWound(DeedCard wound)
        {
            if (FirstWoundIgnored)
            {
                // The event fires after this exact new wound has entered the hand.
                if (!_bloodSurgeDeck.TryRemovePermanentlyExact(wound))
                    throw new System.InvalidOperationException("血脉贲张的新伤实例不在手牌中");
                FirstWoundIgnored = false;
            }
            else
            {
                if (WoundDrawRemaining == 0) return;
                WoundDrawRemaining--;
            }
            _bloodSurgeDeck.DrawExact(1);
        }

        internal void ClearBloodSurge()
        {
            if (_bloodSurgeDeck != null) _bloodSurgeDeck.NewHandWound -= OnBloodSurgeWound;
            _bloodSurgeDeck = null;
            WoundDrawRemaining = 0;
            FirstWoundIgnored = false;
        }

        /// <summary>一次敵人攻擊失去攻擊力的標記。</summary>
        public bool AttackDisabled { get; set; }

        /// <summary>本回合有敵人完全不進行攻擊。</summary>
        public bool EnemySkipAttack { get; set; }

        public Monster SelectedEnemy { get; set; }
        public int SelectedEnemyAttackIndex { get; set; }
        // Skill effects target physical enemy instances, never equal-valued copies or remapped indices.
        private readonly List<(Monster Enemy, int Attack, int Delta)> _skillAttackAdjustments = new();
        public int SkillAttackAdjustment(Monster enemy, int attack) => _skillAttackAdjustments
            .Where(s => ReferenceEquals(s.Enemy, enemy) && s.Attack == attack).Sum(s => s.Delta);
        internal void AddSkillAttackAdjustment(Monster enemy, int attack, int delta)
            => _skillAttackAdjustments.Add((enemy, attack, delta));
        private readonly List<(Monster Enemy, Ability Ability)> _insightAbilities = new();
        private readonly List<(Monster Enemy, int Attack, Element Element)> _insightElements = new();
        public bool InsightIgnores(Monster enemy, Ability ability)
            => _insightAbilities.Any(x => ReferenceEquals(x.Enemy, enemy) && x.Ability == ability);
        internal void AddInsightAbility(Monster enemy, Ability ability) => _insightAbilities.Add((enemy, ability));
        internal void AddInsightElement(Monster enemy, int attack, Element element) => _insightElements.Add((enemy, attack, element));
        public Element InsightElement(Monster enemy, int attack, Element original)
            => _insightElements.Where(x => ReferenceEquals(x.Enemy, enemy) && x.Attack == attack)
                .Select(x => x.Element).DefaultIfEmpty(original).Last();

        internal Monster HardenedEnemy { get; set; }
        internal int HardenedAttack { get; set; }
        internal bool HardenedMatches(Monster enemy, int attack)
            => ReferenceEquals(HardenedEnemy, enemy) && HardenedAttack == attack;

        private readonly List<(Monster Enemy, int Attack)> _coldStareAttacks = new();
        private readonly List<Monster> _coldStareSkipped = new();
        public bool ColdStareSuppresses(Monster enemy, int attack)
            => _coldStareAttacks.Any(s => ReferenceEquals(s.Enemy, enemy) && s.Attack == attack);
        public bool ColdStareSkips(Monster enemy)
            => _coldStareSkipped.Any(s => ReferenceEquals(s, enemy));
        internal void AddColdStare(Monster enemy, int attack, bool enhanced)
        {
            if (enhanced) _coldStareSkipped.Add(enemy);
            else _coldStareAttacks.Add((enemy, attack));
        }
        internal void ConsumeColdStareAttack(Monster enemy, int attack)
            => _coldStareAttacks.RemoveAll(s => ReferenceEquals(s.Enemy, enemy) && s.Attack == attack);
        internal void ClearColdStare()
        {
            _coldStareAttacks.Clear();
            _coldStareSkipped.Clear();
            SelectedEnemy = null;
            SelectedEnemyAttackIndex = 0;
        }

        /// <summary>需棄掉的多張卡牌列表。</summary>
        public List<DeedCard> CardsToDiscard { get; } = new();

        /// <summary>從棄牌堆取出的卡牌列表，供冥想等效果使用。</summary>
        public List<DeedCard> CardsFromDiscard { get; } = new();

        /// <summary>其他玩家的引用，用於處理部分影響全體的法術。</summary>
        public List<PlayerState> OtherPlayers { get; } = new();
        /// <summary>每位其他玩家选择的实际同色手牌；null表示没有同色牌而展示手牌。</summary>
        public Dictionary<PlayerState, DeedCard> MindReadChoices { get; } = new();
        public Dictionary<PlayerState, DeedCard[]> MindReadRevealedHands { get; } = new();
        public DeedCard MindStealTarget { get; set; }
        internal void ClearMindRead()
        {
            MindReadChoices.Clear();
            MindReadRevealedHands.Clear();
            MindStealTarget = null;
        }
        /// <summary>Optional original Energy Flow/Steal targets, at most one unit per other player.</summary>
        public List<UnitState> EnergyExhaustTargets { get; } = new();

        /// <summary>治療手牌創傷時抽牌的倍率。</summary>
        public int DrawPerHeal { get; set; }

        /// <summary>治療部隊時是否自動重整。</summary>
        public bool ReadyUnitOnHeal { get; set; }

        /// <summary>被格擋的敵人護甲是否降至1。</summary>
        public bool BlockedArmorOne { get; set; }
        // Monster is a record: identical enemy tokens must retain separate identities.
        private readonly List<Monster> _blockedEnemiesThisBattle = new();
        /// <summary>实际成功格挡的本回合累计；结束战斗不清除。</summary>
        public int BlockedEnemiesThisTurn { get; private set; }
        public void RecordBlockedEnemy(Monster enemy)
        {
            if (WasBlockedThisBattle(enemy)) return;
            _blockedEnemiesThisBattle.Add(enemy);
            BlockedEnemiesThisTurn++;
        }
        public bool WasBlockedThisBattle(Monster enemy)
            => _blockedEnemiesThisBattle.Any(blocked => ReferenceEquals(blocked, enemy));
        public void ClearDiseaseBattleState()
        {
            BlockedArmorOne = false;
            EnemyDefense.Clear();
            _blockedEnemiesThisBattle.Clear();
        }
        public void ClearCounterStrikeTurnState()
        {
            BlockedEnemiesThisTurn = 0;
            AttackBonusPerBlock = 0;
        }

        /// <summary>當前交戰的敵人列表。</summary>
        public IList<Monster>? Enemies { get; set; }

        /// <summary>傳送時不可經過的地形。</summary>
        public HashSet<TerrainType> TeleportForbidden { get; } = new();
        public bool TeleportMustEndSafe { get; set; }
        public bool TeleportMustEndFort { get; set; }
        public bool IgnoreRampaging { get; set; }
        public bool SpaceBendingActive { get; set; }
        public DeedCard TimeBendingCard { get; internal set; }
        public bool TimeBendingReturnsPlayed { get; internal set; }
        public RampagingEncounter RampagingBattle { get; internal set; }
        public bool IgnoreFortified { get; set; }
        /// <summary>本回合忽略地点提供的城防；怪物自身城防仍然有效。</summary>
        public bool IgnoreSiteFortifications { get; set; }
        public bool IgnoreResist { get; set; }
        public bool ReturnAfterRetreat { get; set; }
        public bool TunnelAssaultActive { get; set; }
        public Map.AxialCoord TunnelOrigin { get; set; }
        public bool TunnelPreviousSiteIgnore { get; set; }
        public PlayerState TunnelTargetPlayer { get; set; }
        public PlaceState TunnelTargetSite { get; set; }
        /// <summary>当前真实战斗地点，供城市守军规则使用；战斗结束即清理。</summary>
        public PlaceState CombatSite { get; set; }
        private long _battleWoundsStart;
        private bool _trackingBattleWounds;
        public int BattleWoundsReceived { get; private set; }
        public bool IsKnockedOut { get; private set; }

        internal void BeginBattleWounds(PlayerState player)
        {
            _battleWoundsStart = player.Deck.HandWoundsReceived;
            _trackingBattleWounds = true;
            BattleWoundsReceived = 0;
            IsKnockedOut = false;
        }

        internal void EnsureBattleWounds(PlayerState player)
        {
            if (InBattle && !_trackingBattleWounds) BeginBattleWounds(player);
        }

        internal void ResolveBattleWounds(PlayerState player)
        {
            if (!InBattle) return;
            EnsureBattleWounds(player);
            BattleWoundsReceived = (int)(player.Deck.HandWoundsReceived - _battleWoundsStart);
            ApplyBattleRageReward();
            if (BattleWoundsReceived < player.UnmodifiedHandLimit()) return;
            IsKnockedOut = true;
            player.DiscardNonWoundHand();
        }

        internal void EndBattleWounds()
        {
            _trackingBattleWounds = false;
            ClearBattleRage();
            ClearAttackReductions();
        }
        public EnemyTokenSupply EnemySupply { get; set; }
        public ScoutObservation LastScoutObservation { get; internal set; }
        internal List<Monster> ScoutedEnemies { get; } = new();
        public int ScoutExplorationRange { get; internal set; } = 1;
        public Map.AxialCoord TunnelDestination { get; set; }
        public Monster TunnelDefender { get; set; }
        public bool TunnelDefenderDefeated => TunnelDefender != null && IsTunnelDefenderDefeated(TunnelDefender);
        private readonly List<Monster> _tunnelDefenders = new();
        private readonly List<Monster> _defeatedTunnelDefenders = new();
        public IReadOnlyList<Monster> TunnelDefenders => _tunnelDefenders;
        public bool IsTunnelDefenderDefeated(Monster defender) => _defeatedTunnelDefenders.Any(m => ReferenceEquals(m, defender));
        public bool PendingSpellReward { get; set; }
        public SpellCard SelectedSpellReward { get; set; }

        internal void BeginTunnelDefenders(IEnumerable<Monster> defenders)
        {
            _tunnelDefenders.Clear();
            _tunnelDefenders.AddRange(defenders ?? Enumerable.Empty<Monster>());
            _defeatedTunnelDefenders.Clear();
            TunnelDefender = _tunnelDefenders.FirstOrDefault();
        }

        internal void RecordWorldDefeat(Monster enemy)
        {
            RampagingBattle?.RecordDefeat(enemy);
            if (TunnelAssaultActive && _tunnelDefenders.Any(m => ReferenceEquals(m, enemy)) && !IsTunnelDefenderDefeated(enemy))
                _defeatedTunnelDefenders.Add(enemy);
        }

        /// <summary>交戰敵人數量，用於烈焰浪湧。</summary>
        public int EngagedEnemies { get; set; }

        /// <summary>成功格擋後獲得的單次攻擊力。</summary>
        public System.Random EffectRandom { get; set; }
        public List<ManaColor> LastManaRolls { get; } = new();
        public int AttackAfterBlock => CombatPower.Blocks.Sum(b => b.AttackOnSuccess);

        /// <summary>成功格擋後直接殺死敵人。</summary>
        public bool KillBlockedEnemy => CombatPower.Blocks.Any(b => b.KillOnSuccess);
        public int BlockArmorReduction => CombatPower.Blocks.Sum(b => b.ArmorReductionOnSuccess);

        /// <summary>免費招募標記。</summary>
        public bool FreeRecruit { get; set; }
        /// <summary>真实部队供应引用；免费获取与普通交涉共用，不复制虚拟部队。</summary>
        public IList<UnitCard>? UnitOffer { get; set; }
        public UnitSupply? UnitSupply { get; set; }
        public bool UseLoyaltySlot { get; set; }
        public bool UnitsAllowed { get; set; } = true;
        public DungeonActions? Dungeon { get; set; }
        public LairActions? Lair { get; set; }
        public RuinActions? Ruin { get; set; }
        public UnitState? UnitToDisband { get; set; }
        /// <summary>战斗号召本回合尚未使用的供应部队能力次数；不是招募或指挥槽。</summary>
        public int OfferedUnitUses { get; internal set; }

        /// <summary>本回合結束後是否額外進行一次回合。</summary>
        public bool ExtraTurn { get; set; }

        /// <summary>結束回合時是否跳過抽牌。</summary>
        public bool SkipDraw { get; set; }

        /// <summary>指定立即擊殺的敵人索引。</summary>
        public int? KillEnemyIndex { get; set; }

        /// <summary>本回合是否已成功招募過部隊。</summary>
        public bool HasRecruited { get; set; }

        /// <summary>調整敵人攻擊力的表，鍵為敵人索引，正值增加負值減少。</summary>
        public Dictionary<int, int> EnemyAttackAdjust { get; } = new();

        /// <summary>忽略指定敵人的單一能力。</summary>
        public Dictionary<int, Ability> IgnoredAbilities { get; } = new();

        /// <summary>變更敵人攻擊元素的表。</summary>
        public Dictionary<int, Element> AttackElementChange { get; } = new();

        /// <summary>下一張橫置打出的非創傷牌數值。</summary>
        public int SidewaysCardValue { get; set; }

        /// <summary>下一次近戰攻擊視為遠程攻擊。</summary>
        public bool NextMeleeAsRanged { get; set; }

        /// <summary>戰鬥結束時可棄掉的傷牌上限。</summary>
        public int DiscardWoundsAfterBattle { get; set; }

        /// <summary>忽略下一個造成給英雄的傷牌。</summary>
        public bool IgnoreNextWound { get; set; }

        /// <summary>可在指定距離偵測城防守軍的範圍，0 表示無效果。</summary>
        public int CastleRevealRange { get; set; }

        /// <summary>其他玩家部隊臨時的攻擊與格擋減值。</summary>
        public int OtherUnitPenalty { get; set; }

        /// <summary>魔力霸主效果待觸發的顏色，null 表示無效果。</summary>
        public ManaColor? OverlordColor { get; set; }

        /// <summary>魔力霸主加值是否仍可觸發。</summary>
        public bool OverlordPending { get; set; }
    }
}
