using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;
using System.Collections.Generic;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 行動牌效果運行時的臨時數據池。僅涵蓋移動與影響力等簡化數值。
    /// </summary>
    public sealed class ActionContext
    {
        /// <summary>當前累計的移動力。</summary>
        public int MovementPool { get; set; }

        /// <summary>當前累計的影響力。</summary>
        public int InfluencePool { get; set; }

        /// <summary>當前累計的格擋值。</summary>
        public int BlockPool { get => CombatPower.BlockTotal; set => CombatPower.SetBlockTotal(value); }

        public CombatPowerPool CombatPower { get; } = new();
        public int SiegePool { get => CombatPower.Total(AttackType.Siege); set => CombatPower.SetTotal(AttackType.Siege, value); }
        public bool FortifiedSite { get; set; }
        public int HarvestTriggers { get; set; }

        /// <summary>當前累計的遠程攻擊力。</summary>
        public int RangedPool { get => CombatPower.Total(AttackType.Ranged); set => CombatPower.SetTotal(AttackType.Ranged, value); }

        /// <summary>橫置打出創傷卡時的數值，0 表示不可橫置。</summary>
        public int SidewaysWoundValue { get; set; }

        /// <summary>下一張行動牌是否免費獲得強效。</summary>
        public bool NextCardEnhanced { get; set; }

        /// <summary>強效額外獲得的數值加成。</summary>
        public int BoostValue { get; set; }

        /// <summary>本回合可額外使用的魔力骰數量。</summary>
        public int ExtraManaDice { get; set; }

        /// <summary>某些效果需要指定的目標部隊。</summary>
        public UnitState? TargetUnit { get; set; }

        /// <summary>當前所在地形，用於某些牌的計算。</summary>
        public TerrainType CurrentTerrain { get; set; } = TerrainType.Plains;

        /// <summary>目前的日夜狀態。</summary>
        public DayPart DayPart { get; set; } = DayPart.Day;

        /// <summary>一次性降低敵人攻擊力的數值。</summary>
        public int AttackReduction { get; set; }

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

        /// <summary>隨機應變等效果需棄掉的卡牌。</summary>
        public DeedCard? CardToDiscard { get; set; }

        /// <summary>招募折扣值，供無情威壓使用。</summary>
        public int RecruitDiscount { get; set; }

        /// <summary>以無情威壓招募時應扣除聲望。</summary>
        public bool IntimidateUsed { get; set; }

        /// <summary>受到創傷時每張傷牌額外獲得的攻擊力。</summary>
        public int AttackBonusPerWound { get; set; }

        /// <summary>攻擊加成上限。</summary>
        public int AttackBonusLimit { get; set; }

        /// <summary>特定敵人的護甲減值表，鍵為敵人在列表中的索引。</summary>
        public Dictionary<int, int> ArmorReduction { get; } = new();

        /// <summary>本回合無法攻擊的敵人索引。</summary>
        public HashSet<int> SkipAttackIndices { get; } = new();

        /// <summary>被移除火焰抗性的敵人索引。</summary>
        public HashSet<int> FireResistRemoved { get; } = new();

        /// <summary>移動力兌換近戰攻擊所需點數，0 表示不可兌換。</summary>
        public int MoveCostAttack { get; set; }

        /// <summary>移動力兌換遠程攻擊所需點數，0 表示不可兌換。</summary>
        public int MoveCostRanged { get; set; }

        /// <summary>是否處於戰鬥中，供部分效果判斷。</summary>
        public bool InBattle { get; set; }

        /// <summary>地形移動費用的臨時覆寫表。</summary>
        public Dictionary<TerrainType, int> TerrainCostOverride { get; } = new();
        public HashSet<TerrainType> MoveForbidden { get; } = new();
        public Map.MapState MovementMap { get; set; }
        public Map.AxialCoord? TargetHex { get; set; }
        public TerrainType? TargetTerrain { get; set; }
        public Dictionary<Map.AxialCoord, int> HexMoveReduction { get; } = new();
        public Dictionary<TerrainType, int> TerrainMoveReduction { get; } = new();

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

        /// <summary>部隊護甲的臨時加值。</summary>
        public int UnitArmorBonus { get; set; }

        /// <summary>每次部隊攻擊或格擋時額外獲得的名望。</summary>
        public int FamePerUnitAction { get; set; }

        /// <summary>執行穩步前進時應回收的卡牌。</summary>
        public DeedCard? CardToRecycle { get; set; }

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

        /// <summary>攻城攻擊翻倍。</summary>
        public bool DoubleSiegeAttack { get; set; }

        /// <summary>攻城攻擊視為遠程。</summary>
        public bool SiegeAttackAsRanged { get; set; }

        /// <summary>可於本回合從法術市場直接施放的法術顏色列表。</summary>
        public List<ManaColor> SpellColors { get; } = new();

        /// <summary>本回合是否已執行學習獲取高級行動牌。</summary>
        public bool LearnUsed { get; set; }

        /// <summary>提供對高級行動牌市場的引用。</summary>
        public AdvActionSupply? AdvActionSupply { get; set; }

        /// <summary>提供對法術牌市場的引用。</summary>
        public SpellSupply? SpellSupply { get; set; }

        /// <summary>提供對技能牌市場的引用。</summary>
        public SkillSupply? SkillSupply { get; set; }

        /// <summary>提供對神器牌堆的引用。</summary>
        public ArtifactSupply? ArtifactSupply { get; set; }

        /// <summary>可直接施放強效法術的顏色。</summary>
        public List<ManaColor> SpellStrongColors { get; } = new();

        /// <summary>每成功格擋一名敵人可獲得的攻擊加值。</summary>
        public int AttackBonusPerBlock { get; set; }

        /// <summary>對抗迅捷時額外計算的格擋值。</summary>
        public int SwiftBlockBonus { get; set; }

        /// <summary>傳送可移動的最大距離。</summary>
        public int TeleportRange { get; set; }

        /// <summary>下一次抽牌時額外增加的手牌上限。</summary>
        public int NextDrawBonus { get; set; }

        /// <summary>每擊敗一名敵人額外獲得的聲望。</summary>
        public int PendingReputationOnKill { get; set; }

        /// <summary>每擊敗一名敵人額外獲得的名望。</summary>
        public int PendingFameOnKill { get; set; }

        /// <summary>每擊殺一名敵人獲得的魔晶數量。</summary>
        public int CrystalsPerKill { get; set; }

        /// <summary>根據敵人決定獲得魔晶顏色的函式。</summary>
        public System.Func<Monster, ManaColor>? CrystalOnKill { get; set; }

        /// <summary>影響力轉化治療所需點數，0 表示不可轉換。</summary>
        public int HealInfluenceRate { get; set; }

        /// <summary>重整部隊時每級需支付的影響力，0 表示不可重整。</summary>
        public int ReadyInfluencePerLevel { get; set; }
        public int ReadyUnitMaxLevel { get; set; }

        /// <summary>Clear turn-scoped movement and unit conversions, including when drawing is skipped.</summary>
        public void ClearMovementAndReadyEffects()
        {
            HexMoveReduction.Clear();
            TerrainMoveReduction.Clear();
            TargetHex = null;
            TargetTerrain = null;
            ReadyInfluencePerLevel = ReadyUnitMaxLevel = 0;
            MovementPool = InfluencePool = 0;
        }

        /// <summary>若在攻擊階段前未加入創傷可獲得的攻擊力。</summary>
        public int AttackIfNoWound { get; set; }

        /// <summary>本回合山脈視為安全格。</summary>
        public bool MountainSafe { get; set; }

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

        /// <summary>每擊殺一名敵人時額外降低其他敵人護甲的數值。</summary>
        public int ArmorReducePerKill { get; set; }

        /// <summary>降低敵人攻擊的次數。</summary>
        public int AttackReductionTimes { get; set; }

        /// <summary>未來創傷進手牌時可抽牌的次數。</summary>
        public int WoundDrawRemaining { get; set; }

        /// <summary>首次進手的創傷會被忽略並抽牌。</summary>
        public bool FirstWoundIgnored { get; set; }

        /// <summary>一次敵人攻擊失去攻擊力的標記。</summary>
        public bool AttackDisabled { get; set; }

        /// <summary>本回合有敵人完全不進行攻擊。</summary>
        public bool EnemySkipAttack { get; set; }

        /// <summary>需棄掉的多張卡牌列表。</summary>
        public List<DeedCard> CardsToDiscard { get; } = new();

        /// <summary>從棄牌堆取出的卡牌列表，供冥想等效果使用。</summary>
        public List<DeedCard> CardsFromDiscard { get; } = new();

        /// <summary>其他玩家的引用，用於處理部分影響全體的法術。</summary>
        public List<PlayerState> OtherPlayers { get; } = new();

        /// <summary>治療手牌創傷時抽牌的倍率。</summary>
        public int DrawPerHeal { get; set; }

        /// <summary>治療部隊時是否自動重整。</summary>
        public bool ReadyUnitOnHeal { get; set; }

        /// <summary>被格擋的敵人護甲是否降至1。</summary>
        public bool BlockedArmorOne { get; set; }

        /// <summary>當前交戰的敵人列表。</summary>
        public IList<Monster>? Enemies { get; set; }

        /// <summary>傳送時不可經過的地形。</summary>
        public HashSet<TerrainType> TeleportForbidden { get; } = new();
        public bool TeleportMustEndSafe { get; set; }
        public bool TeleportMustEndFort { get; set; }
        public bool IgnoreRampaging { get; set; }
        public bool IgnoreFortified { get; set; }
        public bool IgnoreResist { get; set; }
        public bool ReturnAfterRetreat { get; set; }

        /// <summary>交戰敵人數量，用於烈焰浪湧。</summary>
        public int EngagedEnemies { get; set; }

        /// <summary>成功格擋後獲得的單次攻擊力。</summary>
        public int AttackAfterBlock { get; set; }

        /// <summary>成功格擋後直接殺死敵人。</summary>
        public bool KillBlockedEnemy { get; set; }

        /// <summary>免費招募標記。</summary>
        public bool FreeRecruit { get; set; }

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
